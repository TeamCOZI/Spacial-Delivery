using System;
using System.Collections.Generic;
using UnityEngine;

public static class OutputPortTransferUtility
{
    private const string LauncherPartName = "Launcher";
    private const float CellSize = 0.1f;
    private const int GridSize = 99;
    private const float PipeTokenLocalZ = -0.06f;

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    private struct InputPortCandidate
    {
        public AssemblyPartFocus partFocus;
        public StructureResourceInventory inputInventory;
        public Transform portTransform;
        public Vector2Int mappedCell;
        public Vector2Int receiverTerminalDirection;
    }

    private readonly struct TraversalNode : IEquatable<TraversalNode>
    {
        public TraversalNode(Vector2Int cell, Vector2Int incomingSideDirection)
        {
            this.cell = cell;
            this.incomingSideDirection = incomingSideDirection;
        }

        public readonly Vector2Int cell;
        public readonly Vector2Int incomingSideDirection;

        public bool Equals(TraversalNode other)
        {
            return cell == other.cell && incomingSideDirection == other.incomingSideDirection;
        }

        public override bool Equals(object obj)
        {
            return obj is TraversalNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (cell, incomingSideDirection).GetHashCode();
        }
    }

    public struct TransferRouteSelection
    {
        private struct SplitCommit
        {
            public SplitPipeState state;
            public AssemblyPartPortLayout.PortSide outputSide;
        }

        private List<SplitCommit> splitCommits;

        public bool HasSplitSelection => splitCommits != null && splitCommits.Count > 0;

        public void Add(SplitPipeState splitPipeState, AssemblyPartPortLayout.PortSide splitOutputSide)
        {
            if (splitPipeState == null)
            {
                return;
            }

            if (splitCommits == null)
            {
                splitCommits = new List<SplitCommit>();
            }

            splitCommits.Add(new SplitCommit
            {
                state = splitPipeState,
                outputSide = splitOutputSide
            });
        }

        public void Commit()
        {
            if (splitCommits == null)
            {
                return;
            }

            for (int i = 0; i < splitCommits.Count; i++)
            {
                SplitCommit splitCommit = splitCommits[i];
                if (splitCommit.state != null)
                {
                    splitCommit.state.CommitSelectedOutputSide(splitCommit.outputSide);
                }
            }
        }
    }

    public static bool SupportsTransfer(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return false;
        }

        if (IsCoreOutputPort(outputPort))
        {
            return true;
        }

        AssemblyPartFocus ownerPartFocus = ResolveOwnerPartFocus(outputPort);
        if (ownerPartFocus == null || ownerPartFocus.SourcePart == null)
        {
            return false;
        }

        Part sourcePart = ownerPartFocus.SourcePart;
        if (sourcePart.partType == PartType.Pipe)
        {
            return false;
        }

        if (string.Equals(sourcePart.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (PowerGeneratorRecipeCatalog.IsGeneratorPart(sourcePart) || SolarPanelUtility.IsSolarPanelPart(sourcePart) || SolarTurbineUtility.IsSolarTurbinePart(sourcePart))
        {
            return false;
        }

        return WarehouseUtility.IsWarehousePart(sourcePart) || ModulePartInventoryUtility.UsesSplitInventories(sourcePart);
    }

    public static string ResolveModuleLabel(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return "-";
        }

        if (IsCoreOutputPort(outputPort))
        {
            return "Core";
        }

        AssemblyPartFocus ownerPartFocus = ResolveOwnerPartFocus(outputPort);
        if (ownerPartFocus != null && ownerPartFocus.SourcePart != null && !string.IsNullOrWhiteSpace(ownerPartFocus.SourcePart.partName))
        {
            return ownerPartFocus.SourcePart.partName;
        }

        return outputPort.OwnerLabel;
    }

    public static bool TryResolveSenderOutputInventory(AssemblyOutputPortFocus outputPort, out StructureResourceInventory outputInventory)
    {
        outputInventory = null;
        if (!SupportsTransfer(outputPort))
        {
            return false;
        }

        if (IsCoreOutputPort(outputPort))
        {
            return TryResolveCoreLogisticsInventory(outputPort.OwnerSatellite, out outputInventory);
        }

        AssemblyPartFocus ownerPartFocus = ResolveOwnerPartFocus(outputPort);
        return ModulePartInventoryUtility.TryResolveOutputInventory(ownerPartFocus, out outputInventory);
    }

    public static bool TryResolveReceiverInputInventory(AssemblyPartFocus partFocus, out StructureResourceInventory inputInventory)
    {
        return ModulePartInventoryUtility.TryResolveInputInventory(partFocus, out inputInventory);
    }

    public static bool IsReceiverInputAttachedToPipeCell(
        ArtificialSatellite ownerSatellite,
        AssemblyPartFocus receiverPartFocus,
        Transform receiverPortTransform,
        Vector2Int pipeCell,
        Vector2Int receiverTerminalDirection)
    {
        if (ownerSatellite == null || receiverPartFocus == null || receiverPortTransform == null || receiverTerminalDirection == Vector2Int.zero)
        {
            return false;
        }

        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        HashSet<Vector2Int> pipeCells = new HashSet<Vector2Int>();
        PipeConnectivityUtility.BuildPipePortMaps(ownerSatellite, null, outputDirectionsByCell, null, pipeCells);
        if (!pipeCells.Contains(pipeCell) || !HasDirection(outputDirectionsByCell, pipeCell, receiverTerminalDirection))
        {
            return false;
        }

        AssemblyPartPortLayout[] layouts = ownerSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null)
            {
                continue;
            }

            AssemblyPartFocus partFocus = layout.GetComponent<AssemblyPartFocus>();
            if (partFocus == null)
            {
                partFocus = layout.GetComponentInParent<AssemblyPartFocus>();
            }

            if (partFocus != receiverPartFocus)
            {
                continue;
            }

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null)
            {
                continue;
            }

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[entryIndex];
                if (entry == null || entry.portTransform != receiverPortTransform || !AssemblyPortTypeUtility.IsInputCompatible(entry.portType))
                {
                    continue;
                }

                if (!TryGetMappedCellForInputEntry(receiverPartFocus, layout, entry, out Vector2Int mappedCell, out Vector2Int resolvedTerminalDirection))
                {
                    continue;
                }

                return mappedCell == pipeCell && resolvedTerminalDirection == receiverTerminalDirection;
            }
        }

        return false;
    }

    public static void BuildOutputResourceAmounts(
        AssemblyOutputPortFocus outputPort,
        List<StructureResourceInventory.ResourceAmount> results,
        out int totalCapacity)
    {
        totalCapacity = 0;
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (IsCoreOutputPort(outputPort))
        {
            BuildCoreLogisticsResourceAmounts(outputPort != null ? outputPort.OwnerSatellite : null, results, out totalCapacity);
            return;
        }

        if (!TryResolveSenderOutputInventory(outputPort, out StructureResourceInventory outputInventory) || outputInventory == null)
        {
            return;
        }

        totalCapacity = outputInventory.Capacity;
        IReadOnlyList<StructureResourceInventory.ResourceAmount> resources = outputInventory.Resources;
        for (int i = 0; i < resources.Count; i++)
        {
            StructureResourceInventory.ResourceAmount resourceAmount = resources[i];
            if (resourceAmount.amount <= 0)
            {
                continue;
            }

            results.Add(resourceAmount);
        }
    }

    public static int GetOutputResourceAmount(AssemblyOutputPortFocus outputPort, InventoryResourceType resourceType)
    {
        if (IsCoreOutputPort(outputPort))
        {
            return GetCoreLogisticsResourceAmount(outputPort != null ? outputPort.OwnerSatellite : null, resourceType);
        }

        if (!TryResolveSenderOutputInventory(outputPort, out StructureResourceInventory outputInventory) || outputInventory == null)
        {
            return 0;
        }

        return outputInventory.GetAmount(resourceType);
    }

    public static bool TryConsumeOutputResource(AssemblyOutputPortFocus outputPort, InventoryResourceType resourceType, int amount)
    {
        if (IsCoreOutputPort(outputPort))
        {
            return TryConsumeCoreLogisticsResource(outputPort != null ? outputPort.OwnerSatellite : null, resourceType, amount);
        }

        if (!TryResolveSenderOutputInventory(outputPort, out StructureResourceInventory outputInventory) || outputInventory == null)
        {
            return false;
        }

        return outputInventory.TryRemove(resourceType, amount);
    }

    public static bool TryResolveTransferRoute(
        AssemblyOutputPortFocus outputPort,
        List<Vector3> localPoints,
        out int pipeCellCount,
        out StructureResourceInventory receiverInputInventory,
        out AssemblyPartFocus receiverPartFocus)
    {
        return TryResolveTransferRouteDetailed(
            outputPort,
            localPoints,
            out pipeCellCount,
            out receiverInputInventory,
            out receiverPartFocus,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);
    }

    public static bool TryResolveTransferRouteDetailed(
        AssemblyOutputPortFocus outputPort,
        List<Vector3> localPoints,
        out int pipeCellCount,
        out StructureResourceInventory receiverInputInventory,
        out AssemblyPartFocus receiverPartFocus,
        out TransferRouteSelection routeSelection,
        out int firstPipePointIndex,
        out int lastPipePointIndex,
        out Transform receiverPortTransform,
        out Vector2Int receiverTerminalDirection,
        out List<Vector2Int> pipeCellPath)
    {
        pipeCellCount = 0;
        receiverInputInventory = null;
        receiverPartFocus = null;
        routeSelection = default;
        firstPipePointIndex = -1;
        lastPipePointIndex = -1;
        receiverPortTransform = null;
        receiverTerminalDirection = Vector2Int.zero;
        pipeCellPath = null;

        if (localPoints == null)
        {
            return false;
        }

        localPoints.Clear();
        if (!SupportsTransfer(outputPort) || outputPort.OwnerSatellite == null || !outputPort.HasCellMapping)
        {
            return false;
        }

        ArtificialSatellite ownerSatellite = outputPort.OwnerSatellite;
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell = new Dictionary<Vector2Int, SplitPipeState>();
        Dictionary<Vector2Int, CrossPipeState> crossStatesByCell = new Dictionary<Vector2Int, CrossPipeState>();
        HashSet<Vector2Int> pipeCells = new HashSet<Vector2Int>();
        PipeConnectivityUtility.BuildPipePortMaps(ownerSatellite, inputDirectionsByCell, outputDirectionsByCell, splitStatesByCell, pipeCells);
        CrossPipeUtility.BuildStateMap(ownerSatellite, crossStatesByCell);

        Vector2Int senderCell = outputPort.MappedCell;
        if (!pipeCells.Contains(senderCell))
        {
            return false;
        }

        Dictionary<Vector2Int, HashSet<Vector2Int>> reachableIncomingDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<TraversalNode, TraversalNode> cameFrom = new Dictionary<TraversalNode, TraversalNode>();
        if (!TryCollectTraversalReachability(
            outputPort,
            inputDirectionsByCell,
            outputDirectionsByCell,
            splitStatesByCell,
            crossStatesByCell,
            pipeCells,
            reachableIncomingDirectionsByCell,
            cameFrom,
            out TraversalNode startNode))
        {
            return false;
        }

        if (!TrySelectReceiverCandidate(
            outputPort,
            ownerSatellite,
            reachableIncomingDirectionsByCell,
            inputDirectionsByCell,
            outputDirectionsByCell,
            splitStatesByCell,
            crossStatesByCell,
            pipeCells,
            out InputPortCandidate candidate,
            out TraversalNode candidateNode))
        {
            return false;
        }

        List<Vector2Int> cellPath = ReconstructTraversalPath(cameFrom, startNode, candidateNode);
        if (cellPath.Count <= 0 || cellPath[0] != senderCell)
        {
            return false;
        }

        pipeCellCount = cellPath.Count;
        pipeCellPath = new List<Vector2Int>(cellPath);
        routeSelection = BuildRouteSelectionForPath(cellPath, candidate, outputDirectionsByCell, splitStatesByCell);

        Vector3 senderPortLocalPoint = ownerSatellite.transform.InverseTransformPoint(outputPort.transform.position);
        senderPortLocalPoint.z = PipeTokenLocalZ;
        AppendPointIfDistinct(localPoints, senderPortLocalPoint);

        for (int i = 0; i < cellPath.Count; i++)
        {
            AppendPointIfDistinct(localPoints, GridToLocalPosition(cellPath[i], PipeTokenLocalZ));
            int appendedPointIndex = Mathf.Max(0, localPoints.Count - 1);
            if (firstPipePointIndex < 0)
            {
                firstPipePointIndex = appendedPointIndex;
            }

            lastPipePointIndex = appendedPointIndex;
        }

        Vector3 receiverPortLocalPoint = ownerSatellite.transform.InverseTransformPoint(candidate.portTransform.position);
        receiverPortLocalPoint.z = PipeTokenLocalZ;
        AppendPointIfDistinct(localPoints, receiverPortLocalPoint);

        receiverInputInventory = candidate.inputInventory;
        receiverPartFocus = candidate.partFocus;
        receiverPortTransform = candidate.portTransform;
        receiverTerminalDirection = candidate.receiverTerminalDirection;
        return firstPipePointIndex >= 0 && lastPipePointIndex >= firstPipePointIndex && localPoints.Count >= 1;
    }

    private static bool TrySelectReceiverCandidate(
        AssemblyOutputPortFocus outputPort,
        ArtificialSatellite ownerSatellite,
        Dictionary<Vector2Int, HashSet<Vector2Int>> reachableIncomingDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        Dictionary<Vector2Int, CrossPipeState> crossStatesByCell,
        HashSet<Vector2Int> pipeCells,
        out InputPortCandidate candidate,
        out TraversalNode candidateNode)
    {
        candidate = default;
        candidateNode = default;
        if (outputPort == null || ownerSatellite == null)
        {
            return false;
        }

        AssemblyPartFocus senderPartFocus = ResolveOwnerPartFocus(outputPort);
        AssemblyPartPortLayout[] layouts = ownerSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null)
            {
                continue;
            }

            AssemblyPartFocus partFocus = layout.GetComponent<AssemblyPartFocus>();
            if (partFocus == null)
            {
                partFocus = layout.GetComponentInParent<AssemblyPartFocus>();
            }

            if (partFocus == null || partFocus == senderPartFocus || partFocus.SourcePart == null)
            {
                continue;
            }

            if (!TryResolveReceiverInputInventory(partFocus, out StructureResourceInventory inputInventory) ||
                inputInventory == null)
            {
                continue;
            }

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null)
            {
                continue;
            }

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[entryIndex];
                if (entry == null || !AssemblyPortTypeUtility.IsInputCompatible(entry.portType) || entry.portTransform == null)
                {
                    continue;
                }

                if (!TryResolveReceiverAttachmentCell(partFocus, layout, entry, pipeCells, outputDirectionsByCell, out Vector2Int attachmentCell, out Vector2Int receiverTerminalDirection) ||
                    reachableIncomingDirectionsByCell == null ||
                    !reachableIncomingDirectionsByCell.TryGetValue(attachmentCell, out HashSet<Vector2Int> reachableIncomingDirections) ||
                    reachableIncomingDirections == null ||
                    reachableIncomingDirections.Count <= 0)
                {
                    continue;
                }

                if (!IsReceiverInputAttachedToPipeCell(ownerSatellite, partFocus, entry.portTransform, attachmentCell, receiverTerminalDirection))
                {
                    continue;
                }

                foreach (Vector2Int reachableIncomingDirection in reachableIncomingDirections)
                {
                    if (!CanCellDispatchToDirection(
                        attachmentCell,
                        reachableIncomingDirection,
                        receiverTerminalDirection,
                        outputDirectionsByCell,
                        splitStatesByCell,
                        crossStatesByCell))
                    {
                        continue;
                    }

                    candidate = new InputPortCandidate
                    {
                        partFocus = partFocus,
                        inputInventory = inputInventory,
                        portTransform = entry.portTransform,
                        mappedCell = attachmentCell,
                        receiverTerminalDirection = receiverTerminalDirection
                    };
                    candidateNode = new TraversalNode(attachmentCell, reachableIncomingDirection);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryCollectTraversalReachability(
        AssemblyOutputPortFocus outputPort,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        Dictionary<Vector2Int, CrossPipeState> crossStatesByCell,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, HashSet<Vector2Int>> reachableIncomingDirectionsByCell,
        Dictionary<TraversalNode, TraversalNode> cameFrom,
        out TraversalNode startNode)
    {
        startNode = default;
        if (outputPort == null || !outputPort.HasCellMapping || pipeCells == null || !pipeCells.Contains(outputPort.MappedCell))
        {
            return false;
        }

        Vector2Int startIncomingDirection = ResolveSenderTerminalDirection(outputPort);
        if (startIncomingDirection == Vector2Int.zero)
        {
            return false;
        }

        reachableIncomingDirectionsByCell?.Clear();
        cameFrom?.Clear();

        startNode = new TraversalNode(outputPort.MappedCell, startIncomingDirection);
        Queue<TraversalNode> open = new Queue<TraversalNode>();
        HashSet<TraversalNode> visited = new HashSet<TraversalNode>();
        open.Enqueue(startNode);
        visited.Add(startNode);
        AddReachableIncomingDirection(reachableIncomingDirectionsByCell, startNode.cell, startNode.incomingSideDirection);

        List<Vector2Int> availableDirections = new List<Vector2Int>(4);
        while (open.Count > 0)
        {
            TraversalNode current = open.Dequeue();
            GetAvailableTraversalOutputDirections(
                current,
                inputDirectionsByCell,
                outputDirectionsByCell,
                splitStatesByCell,
                crossStatesByCell,
                pipeCells,
                availableDirections);

            for (int i = 0; i < availableDirections.Count; i++)
            {
                Vector2Int outputDirection = availableDirections[i];
                Vector2Int neighborCell = current.cell + outputDirection;
                if (!CanTraversePipeDirection(current.cell, outputDirection, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
                {
                    continue;
                }

                TraversalNode nextNode = new TraversalNode(neighborCell, -outputDirection);
                if (!visited.Add(nextNode))
                {
                    continue;
                }

                cameFrom[nextNode] = current;
                AddReachableIncomingDirection(reachableIncomingDirectionsByCell, nextNode.cell, nextNode.incomingSideDirection);
                open.Enqueue(nextNode);
            }
        }

        return true;
    }

    private static void GetAvailableTraversalOutputDirections(
        TraversalNode current,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        Dictionary<Vector2Int, CrossPipeState> crossStatesByCell,
        HashSet<Vector2Int> pipeCells,
        List<Vector2Int> availableDirections)
    {
        if (availableDirections == null)
        {
            return;
        }

        availableDirections.Clear();
        if (outputDirectionsByCell == null || !outputDirectionsByCell.TryGetValue(current.cell, out HashSet<Vector2Int> outputDirections) || outputDirections == null)
        {
            return;
        }

        if (crossStatesByCell != null && crossStatesByCell.TryGetValue(current.cell, out CrossPipeState crossPipeState) && crossPipeState != null)
        {
            if (crossPipeState.TryResolvePairedOutputDirection(current.incomingSideDirection, out Vector2Int pairedOutputDirection))
            {
                Vector2Int neighborCell = current.cell + pairedOutputDirection;
                if (HasDirection(outputDirectionsByCell, current.cell, pairedOutputDirection) &&
                    CanTraversePipeDirection(current.cell, pairedOutputDirection, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
                {
                    availableDirections.Add(pairedOutputDirection);
                }
            }

            return;
        }

        foreach (Vector2Int outputDirection in outputDirections)
        {
            Vector2Int neighborCell = current.cell + outputDirection;
            if (!CanTraversePipeDirection(current.cell, outputDirection, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
            {
                continue;
            }

            availableDirections.Add(outputDirection);
        }

        if (splitStatesByCell != null && splitStatesByCell.TryGetValue(current.cell, out SplitPipeState splitPipeState) && splitPipeState != null)
        {
            if (!splitPipeState.TrySelectPreviewOutputDirection(availableDirections, out Vector2Int selectedDirection))
            {
                availableDirections.Clear();
                return;
            }

            availableDirections.Clear();
            availableDirections.Add(selectedDirection);
        }
    }

    private static bool CanCellDispatchToDirection(
        Vector2Int cell,
        Vector2Int incomingSideDirection,
        Vector2Int desiredOutputDirection,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        Dictionary<Vector2Int, CrossPipeState> crossStatesByCell)
    {
        if (desiredOutputDirection == Vector2Int.zero || !HasDirection(outputDirectionsByCell, cell, desiredOutputDirection))
        {
            return false;
        }

        if (crossStatesByCell != null && crossStatesByCell.TryGetValue(cell, out CrossPipeState crossPipeState) && crossPipeState != null)
        {
            return crossPipeState.TryResolvePairedOutputDirection(incomingSideDirection, out Vector2Int pairedOutputDirection)
                && pairedOutputDirection == desiredOutputDirection;
        }

        if (splitStatesByCell != null && splitStatesByCell.TryGetValue(cell, out SplitPipeState splitPipeState) && splitPipeState != null)
        {
            List<Vector2Int> availableDirections = new List<Vector2Int>();
            if (outputDirectionsByCell != null && outputDirectionsByCell.TryGetValue(cell, out HashSet<Vector2Int> outputDirections) && outputDirections != null)
            {
                foreach (Vector2Int outputDirection in outputDirections)
                {
                    if (!availableDirections.Contains(outputDirection))
                    {
                        availableDirections.Add(outputDirection);
                    }
                }
            }

            return splitPipeState.TrySelectPreviewOutputDirection(availableDirections, out Vector2Int selectedDirection)
                && selectedDirection == desiredOutputDirection;
        }

        return true;
    }

    private static void AddReachableIncomingDirection(
        Dictionary<Vector2Int, HashSet<Vector2Int>> reachableIncomingDirectionsByCell,
        Vector2Int cell,
        Vector2Int incomingSideDirection)
    {
        if (reachableIncomingDirectionsByCell == null || incomingSideDirection == Vector2Int.zero)
        {
            return;
        }

        if (!reachableIncomingDirectionsByCell.TryGetValue(cell, out HashSet<Vector2Int> directions))
        {
            directions = new HashSet<Vector2Int>();
            reachableIncomingDirectionsByCell[cell] = directions;
        }

        directions.Add(incomingSideDirection);
    }

    private static Vector2Int ResolveSenderTerminalDirection(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null || !outputPort.HasCellMapping)
        {
            return Vector2Int.zero;
        }

        Vector2Int direction = outputPort.SourceCell - outputPort.MappedCell;
        return Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1 ? direction : Vector2Int.zero;
    }

    private static List<Vector2Int> ReconstructTraversalPath(
        Dictionary<TraversalNode, TraversalNode> cameFrom,
        TraversalNode startNode,
        TraversalNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        path.Add(endNode.cell);
        TraversalNode current = endNode;
        while (!current.Equals(startNode) && cameFrom != null && cameFrom.TryGetValue(current, out TraversalNode previous))
        {
            current = previous;
            path.Add(current.cell);
        }

        path.Reverse();
        return path;
    }

    private static bool TryResolveReceiverAttachmentCell(
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        out Vector2Int attachmentCell,
        out Vector2Int receiverTerminalDirection)
    {
        attachmentCell = Vector2Int.zero;
        receiverTerminalDirection = Vector2Int.zero;
        if (!TryGetMappedCellForInputEntry(partFocus, layout, entry, out Vector2Int mappedCell, out receiverTerminalDirection) ||
            receiverTerminalDirection == Vector2Int.zero ||
            pipeCells == null)
        {
            return false;
        }
        if (pipeCells.Contains(mappedCell) && HasDirection(outputDirectionsByCell, mappedCell, receiverTerminalDirection))
        {
            attachmentCell = mappedCell;
            return true;
        }
        Vector2Int fallbackCell = mappedCell - receiverTerminalDirection;
        if (pipeCells.Contains(fallbackCell) && HasDirection(outputDirectionsByCell, fallbackCell, receiverTerminalDirection))
        {
            attachmentCell = fallbackCell;
            return true;
        }
        return false;
    }
    private static bool IsReceiverCandidateReachable(
        Vector2Int mappedCell,
        Vector2Int receiverTerminalDirection,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> receiverTerminalDirectionsByCell)
    {
        if (splitStatesByCell == null || !splitStatesByCell.TryGetValue(mappedCell, out SplitPipeState splitPipeState) || splitPipeState == null)
        {
            return true;
        }
        List<Vector2Int> availableDirections = new List<Vector2Int>(4);
        if (outputDirectionsByCell != null && outputDirectionsByCell.TryGetValue(mappedCell, out HashSet<Vector2Int> outputDirections) && outputDirections != null)
        {
            foreach (Vector2Int outputDirection in outputDirections)
            {
                Vector2Int neighborCell = mappedCell + outputDirection;
                if (!CanTraversePipeDirection(mappedCell, outputDirection, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
                {
                    continue;
                }
                if (!availableDirections.Contains(outputDirection))
                {
                    availableDirections.Add(outputDirection);
                }
            }
        }
        if (receiverTerminalDirectionsByCell != null && receiverTerminalDirectionsByCell.TryGetValue(mappedCell, out HashSet<Vector2Int> receiverDirections) && receiverDirections != null)
        {
            foreach (Vector2Int receiverDirection in receiverDirections)
            {
                if (receiverDirection == Vector2Int.zero || availableDirections.Contains(receiverDirection))
                {
                    continue;
                }
                availableDirections.Add(receiverDirection);
            }
        }
        if (!splitPipeState.TrySelectPreviewOutputDirection(availableDirections, out Vector2Int selectedDirection))
        {
            return false;
        }
        return receiverTerminalDirection != Vector2Int.zero && selectedDirection == receiverTerminalDirection;
    }
    private static void CollectReachableReceiverTerminalDirections(
        AssemblyOutputPortFocus outputPort,
        ArtificialSatellite ownerSatellite,
        HashSet<Vector2Int> reachableCells,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> receiverTerminalDirectionsByCell)
    {
        receiverTerminalDirectionsByCell?.Clear();
        if (ownerSatellite == null || reachableCells == null || receiverTerminalDirectionsByCell == null)
        {
            return;
        }
        AssemblyPartFocus senderPartFocus = ResolveOwnerPartFocus(outputPort);
        AssemblyPartPortLayout[] layouts = ownerSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null)
            {
                continue;
            }
            AssemblyPartFocus partFocus = layout.GetComponent<AssemblyPartFocus>();
            if (partFocus == null)
            {
                partFocus = layout.GetComponentInParent<AssemblyPartFocus>();
            }
            if (partFocus == null || partFocus == senderPartFocus || partFocus.SourcePart == null)
            {
                continue;
            }
            if (!TryResolveReceiverInputInventory(partFocus, out StructureResourceInventory inputInventory) ||
                inputInventory == null)
            {
                continue;
            }
            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null)
            {
                continue;
            }
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[entryIndex];
                if (entry == null || !AssemblyPortTypeUtility.IsInputCompatible(entry.portType))
                {
                    continue;
                }
                if (!TryResolveReceiverAttachmentCell(partFocus, layout, entry, pipeCells, outputDirectionsByCell, out Vector2Int attachmentCell, out Vector2Int receiverTerminalDirection) ||
                    !reachableCells.Contains(attachmentCell))
                {
                    continue;
                }
                if (receiverTerminalDirection == Vector2Int.zero)
                {
                    continue;
                }
                AddReceiverTerminalDirection(receiverTerminalDirectionsByCell, attachmentCell, receiverTerminalDirection);
            }
        }
    }
    private static void AddReceiverTerminalDirection(
        Dictionary<Vector2Int, HashSet<Vector2Int>> receiverTerminalDirectionsByCell,
        Vector2Int mappedCell,
        Vector2Int receiverDirection)
    {
        if (receiverTerminalDirectionsByCell == null || receiverDirection == Vector2Int.zero)
        {
            return;
        }

        if (!receiverTerminalDirectionsByCell.TryGetValue(mappedCell, out HashSet<Vector2Int> directions))
        {
            directions = new HashSet<Vector2Int>();
            receiverTerminalDirectionsByCell[mappedCell] = directions;
        }

        directions.Add(receiverDirection);
    }

    private static bool CanTraversePipeDirection(
        Vector2Int currentCell,
        Vector2Int outputDirection,
        Vector2Int neighborCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        HashSet<Vector2Int> pipeCells)
    {
        if (outputDirection == Vector2Int.zero || pipeCells == null || !pipeCells.Contains(neighborCell))
        {
            return false;
        }

        return HasDirection(outputDirectionsByCell, currentCell, outputDirection)
            && HasDirection(inputDirectionsByCell, neighborCell, -outputDirection);
    }

    private static void CollectReachability(
        Vector2Int senderCell,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<Vector2Int> reachableCells,
        Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        reachableCells?.Clear();
        cameFrom.Clear();

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(senderCell);
        reachableCells?.Add(senderCell);

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (adjacency == null || !adjacency.TryGetValue(current, out List<Vector2Int> nextCells) || nextCells == null)
            {
                continue;
            }

            for (int i = 0; i < nextCells.Count; i++)
            {
                Vector2Int nextCell = nextCells[i];
                if (reachableCells != null && reachableCells.Contains(nextCell))
                {
                    continue;
                }

                reachableCells?.Add(nextCell);
                cameFrom[nextCell] = current;
                open.Enqueue(nextCell);
            }
        }
    }

    private static TransferRouteSelection BuildRouteSelectionForPath(
        List<Vector2Int> cellPath,
        InputPortCandidate candidate,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell)
    {
        TransferRouteSelection selection = default;
        if (cellPath == null || cellPath.Count <= 0 || splitStatesByCell == null || splitStatesByCell.Count == 0)
        {
            return selection;
        }

        HashSet<SplitPipeState> committedStates = new HashSet<SplitPipeState>();
        for (int i = 0; i < cellPath.Count - 1; i++)
        {
            Vector2Int currentCell = cellPath[i];
            Vector2Int nextCell = cellPath[i + 1];
            if (!splitStatesByCell.TryGetValue(currentCell, out SplitPipeState splitPipeState) || splitPipeState == null || !committedStates.Add(splitPipeState))
            {
                continue;
            }

            Vector2Int selectedDirection = nextCell - currentCell;
            if (selectedDirection == Vector2Int.zero || !HasDirection(outputDirectionsByCell, currentCell, selectedDirection))
            {
                continue;
            }

            selection.Add(splitPipeState, ConvertDirectionToSide(selectedDirection));
        }

        Vector2Int terminalCell = cellPath[cellPath.Count - 1];
        if (splitStatesByCell.TryGetValue(terminalCell, out SplitPipeState terminalSplitState) && terminalSplitState != null && committedStates.Add(terminalSplitState))
        {
            Vector2Int terminalDirection = candidate.receiverTerminalDirection;
            if (terminalDirection != Vector2Int.zero && HasDirection(outputDirectionsByCell, terminalCell, terminalDirection))
            {
                selection.Add(terminalSplitState, ConvertDirectionToSide(terminalDirection));
            }
        }

        return selection;
    }

    private static bool TryResolvePartCenterCell(AssemblyPartFocus partFocus, AssemblyPartPortLayout layout, out Vector2Int centerCell)
    {
        centerCell = Vector2Int.zero;
        if (partFocus == null || partFocus.SourcePart == null || layout == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffset(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        centerCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        return true;
    }

    private static bool TryGetPipePortMapping(
        AssemblyPartPortLayout layout,
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int sourceCell,
        out Vector2Int sideDirection)
    {
        sourceCell = Vector2Int.zero;
        sideDirection = Vector2Int.zero;
        if (layout == null || partFocus == null || partFocus.SourcePart == null || entry == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffset(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = AssemblyMathUtility.RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        Vector2Int rotatedSideDirection = RotateSideToCellOffset(entry.side, quarterTurns);
        if (rotatedSideDirection == Vector2Int.zero)
        {
            return false;
        }

        sourceCell = partCenterCell + rotatedRelativeCell;
        sideDirection = rotatedSideDirection;
        return true;
    }

    private static bool HasDirection(Dictionary<Vector2Int, HashSet<Vector2Int>> map, Vector2Int cell, Vector2Int direction)
    {
        return map != null && map.TryGetValue(cell, out HashSet<Vector2Int> directions) && directions != null && directions.Contains(direction);
    }

    private static void AddDirection(Dictionary<Vector2Int, HashSet<Vector2Int>> map, Vector2Int cell, Vector2Int direction)
    {
        if (map == null || direction == Vector2Int.zero)
        {
            return;
        }

        if (!map.TryGetValue(cell, out HashSet<Vector2Int> directions))
        {
            directions = new HashSet<Vector2Int>();
            map[cell] = directions;
        }

        directions.Add(direction);
    }

    private static AssemblyPartPortLayout.PortSide ConvertDirectionToSide(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            return AssemblyPartPortLayout.PortSide.Top;
        }

        if (direction == Vector2Int.down)
        {
            return AssemblyPartPortLayout.PortSide.Bottom;
        }

        if (direction == Vector2Int.left)
        {
            return AssemblyPartPortLayout.PortSide.Left;
        }

        return AssemblyPartPortLayout.PortSide.Right;
    }

    private static Vector2Int ConvertSideToDirection(AssemblyPartPortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top:
                return Vector2Int.up;
            case AssemblyPartPortLayout.PortSide.Bottom:
                return Vector2Int.down;
            case AssemblyPartPortLayout.PortSide.Left:
                return Vector2Int.left;
            case AssemblyPartPortLayout.PortSide.Right:
                return Vector2Int.right;
            default:
                return Vector2Int.zero;
        }
    }

    private static bool TryGetMappedCellForInputEntry(
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int mappedCell,
        out Vector2Int receiverTerminalDirection)
    {
        mappedCell = Vector2Int.zero;
        receiverTerminalDirection = Vector2Int.zero;
        if (partFocus == null || layout == null || entry == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffset(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = AssemblyMathUtility.RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        Vector2Int sideOffset = RotateSideToCellOffset(entry.side, quarterTurns);
        if (sideOffset == Vector2Int.zero)
        {
            return false;
        }

        Vector2Int sourceCell = partCenterCell + rotatedRelativeCell;
        mappedCell = sourceCell + sideOffset;
        receiverTerminalDirection = -sideOffset;
        return true;
    }
    private static Vector2 GetSnapOffset(AssemblyPartFocus partFocus, AssemblyPartPortLayout layout)
    {
        if (partFocus == null || partFocus.SourcePart == null)
        {
            return Vector2.zero;
        }

        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            return Vector2.zero;
        }

        int width = Mathf.Max(1, partFocus.SourcePart.gridWidth);
        int height = Mathf.Max(1, partFocus.SourcePart.gridHeight);
        float offsetX = (width % 2 == 0) ? (CellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (CellSize * 0.5f) : 0f;
        return new Vector2(offsetX, offsetY);
    }

    private static Vector2 GetPartPivotOffset(AssemblyPartPortLayout layout, Quaternion rotation)
    {
        if (!TryBuildLayoutCellCenterOffset(layout, AssemblyMathUtility.GetQuarterTurns(rotation), out Vector2 offsetInCells))
        {
            return Vector2.zero;
        }

        return offsetInCells * CellSize;
    }

    private static bool TryBuildLayoutCellCenterOffset(
        AssemblyPartPortLayout layout,
        int quarterTurns,
        out Vector2 offsetInCells)
    {
        offsetInCells = Vector2.zero;
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0)
        {
            return false;
        }

        bool hasCell = false;
        int minX = 0;
        int maxX = 0;
        int minY = 0;
        int maxY = 0;
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null)
            {
                continue;
            }

            Vector2Int rotatedCell = AssemblyMathUtility.RotateCellOffset(entry.relativeSourceCell, quarterTurns);
            if (!uniqueCells.Add(rotatedCell))
            {
                continue;
            }

            if (!hasCell)
            {
                hasCell = true;
                minX = maxX = rotatedCell.x;
                minY = maxY = rotatedCell.y;
                continue;
            }

            if (rotatedCell.x < minX) minX = rotatedCell.x;
            if (rotatedCell.x > maxX) maxX = rotatedCell.x;
            if (rotatedCell.y < minY) minY = rotatedCell.y;
            if (rotatedCell.y > maxY) maxY = rotatedCell.y;
        }

        if (!hasCell)
        {
            return false;
        }

        offsetInCells = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        return true;
    }

    private static Vector2Int RotateSideToCellOffset(AssemblyPartPortLayout.PortSide side, int quarterTurns)
    {
        Vector2Int baseOffset;
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top:
                baseOffset = Vector2Int.up;
                break;
            case AssemblyPartPortLayout.PortSide.Bottom:
                baseOffset = Vector2Int.down;
                break;
            case AssemblyPartPortLayout.PortSide.Left:
                baseOffset = Vector2Int.left;
                break;
            case AssemblyPartPortLayout.PortSide.Right:
                baseOffset = Vector2Int.right;
                break;
            default:
                return Vector2Int.zero;
        }

        return AssemblyMathUtility.RotateCellOffset(baseOffset, quarterTurns);
    }

    private static bool TryResolveCoreLogisticsInventory(ArtificialSatellite ownerSatellite, out StructureResourceInventory inventory)
    {
        inventory = null;
        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(ownerSatellite, inventories);
        if (inventories.Count <= 0)
        {
            return false;
        }

        inventory = inventories[0];
        return inventory != null;
    }

    private static void CollectCoreLogisticsInventories(ArtificialSatellite ownerSatellite, List<StructureResourceInventory> inventories)
    {
        if (inventories == null)
        {
            return;
        }

        inventories.Clear();
        if (ownerSatellite == null)
        {
            return;
        }

        StructureInstance[] instances = ownerSatellite.GetComponentsInChildren<StructureInstance>(true);
        for (int i = 0; i < instances.Length; i++)
        {
            StructureInstance instance = instances[i];
            if (instance == null || instance.SourceStructure == null || !instance.SourceStructure.UsesLogisticsHubUi)
            {
                continue;
            }

            StructureResourceInventory inventory = instance.GetComponent<StructureResourceInventory>();
            if (inventory == null)
            {
                inventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(instance.gameObject);
                inventory.InitializeForStructure(instance.SourceStructure);
            }

            if (inventory != null)
            {
                inventories.Add(inventory);
            }
        }
    }

    private static void BuildCoreLogisticsResourceAmounts(
        ArtificialSatellite ownerSatellite,
        List<StructureResourceInventory.ResourceAmount> results,
        out int totalCapacity)
    {
        totalCapacity = 0;
        if (results == null)
        {
            return;
        }

        results.Clear();
        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(ownerSatellite, inventories);
        if (inventories.Count <= 0)
        {
            return;
        }

        Dictionary<InventoryResourceType, int> mergedAmounts = new Dictionary<InventoryResourceType, int>();
        for (int i = 0; i < inventories.Count; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            totalCapacity += inventory.Capacity;
            IReadOnlyList<StructureResourceInventory.ResourceAmount> resources = inventory.Resources;
            for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                StructureResourceInventory.ResourceAmount resourceAmount = resources[resourceIndex];
                int sanitizedAmount = Mathf.Max(0, resourceAmount.amount);
                if (sanitizedAmount <= 0)
                {
                    continue;
                }

                if (mergedAmounts.TryGetValue(resourceAmount.resourceType, out int currentAmount))
                {
                    mergedAmounts[resourceAmount.resourceType] = currentAmount + sanitizedAmount;
                }
                else
                {
                    mergedAmounts.Add(resourceAmount.resourceType, sanitizedAmount);
                }
            }
        }

        InventoryResourceType[] resourceTypes = InventoryResourceCatalog.All;
        for (int i = 0; i < resourceTypes.Length; i++)
        {
            InventoryResourceType resourceType = resourceTypes[i];
            if (!mergedAmounts.TryGetValue(resourceType, out int amount) || amount <= 0)
            {
                continue;
            }

            results.Add(new StructureResourceInventory.ResourceAmount(resourceType, amount));
        }
    }

    private static int GetCoreLogisticsResourceAmount(ArtificialSatellite ownerSatellite, InventoryResourceType resourceType)
    {
        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(ownerSatellite, inventories);

        int totalAmount = 0;
        for (int i = 0; i < inventories.Count; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            totalAmount += inventory.GetAmount(resourceType);
        }

        return totalAmount;
    }

    private static bool TryConsumeCoreLogisticsResource(ArtificialSatellite ownerSatellite, InventoryResourceType resourceType, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return true;
        }

        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(ownerSatellite, inventories);
        if (inventories.Count <= 0)
        {
            return false;
        }

        int totalAvailable = 0;
        for (int i = 0; i < inventories.Count; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            totalAvailable += inventory.GetAmount(resourceType);
        }

        if (totalAvailable < remaining)
        {
            return false;
        }

        for (int i = 0; i < inventories.Count && remaining > 0; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            int available = inventory.GetAmount(resourceType);
            int removeAmount = Mathf.Min(remaining, available);
            if (removeAmount <= 0)
            {
                continue;
            }

            if (inventory.TryRemove(resourceType, removeAmount))
            {
                remaining -= removeAmount;
            }
        }

        return remaining <= 0;
    }

    private static AssemblyPartFocus ResolveOwnerPartFocus(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return null;
        }

        AssemblyPartFocus ownerPartFocus = outputPort.OwnerPartFocus;
        if (ownerPartFocus != null)
        {
            return ownerPartFocus;
        }

        return outputPort.ResolveOwnerModuleFocus();
    }

    private static bool IsCoreOutputPort(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return false;
        }

        AssemblyPartFocus ownerPartFocus = ResolveOwnerPartFocus(outputPort);
        if (ownerPartFocus != null && ownerPartFocus.SourcePart != null && ownerPartFocus.SourcePart.partType == PartType.Core)
        {
            return true;
        }

        return string.Equals(outputPort.OwnerLabel, "Core", StringComparison.OrdinalIgnoreCase);
    }

    private static void CollectPipeCells(ArtificialSatellite ownerSatellite, HashSet<Vector2Int> pipeCells)
    {
        pipeCells.Clear();
        if (ownerSatellite == null)
        {
            return;
        }

        AssemblyPartFocus[] partFocuses = ownerSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || partFocus.SourcePart == null || partFocus.SourcePart.partType != PartType.Pipe)
            {
                continue;
            }

            pipeCells.Add(LocalPositionToGrid(partFocus.transform.localPosition, Vector2.zero));
        }
    }

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition, Vector2 snapOffset)
    {
        int gridCenter = GridSize / 2;
        return new Vector2Int(
            AssemblyMathUtility.QuantizeToCellIndex((localPosition.x - snapOffset.x) / CellSize) + gridCenter,
            AssemblyMathUtility.QuantizeToCellIndex((localPosition.y - snapOffset.y) / CellSize) + gridCenter);
    }

    private static Vector3 GridToLocalPosition(Vector2Int gridPosition, float z)
    {
        int gridCenter = GridSize / 2;
        float x = (gridPosition.x - gridCenter) * CellSize;
        float y = (gridPosition.y - gridCenter) * CellSize;
        return new Vector3(x, y, z);
    }

    private static void AppendPointIfDistinct(List<Vector3> localPoints, Vector3 point)
    {
        if (localPoints == null)
        {
            return;
        }

        if (localPoints.Count > 0 && (localPoints[localPoints.Count - 1] - point).sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        localPoints.Add(point);
    }
}

