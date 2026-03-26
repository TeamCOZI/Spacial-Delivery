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
        public int distance;
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

        return ModulePartInventoryUtility.UsesSplitInventories(sourcePart);
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
        pipeCellCount = 0;
        receiverInputInventory = null;
        receiverPartFocus = null;

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
        HashSet<Vector2Int> pipeCells = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, List<Vector2Int>> pipeAdjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        PipeConnectivityUtility.BuildConnectedPipeAdjacency(ownerSatellite, pipeAdjacency, pipeCells);

        Vector2Int senderCell = outputPort.MappedCell;
        if (!pipeCells.Contains(senderCell))
        {
            return false;
        }

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(senderCell);
        distances[senderCell] = 0;

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (!pipeAdjacency.TryGetValue(current, out List<Vector2Int> neighbors))
            {
                continue;
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int neighbor = neighbors[i];
                if (distances.ContainsKey(neighbor))
                {
                    continue;
                }

                distances[neighbor] = distances[current] + 1;
                cameFrom[neighbor] = current;
                open.Enqueue(neighbor);
            }
        }

        if (!TrySelectReceiverCandidate(outputPort, ownerSatellite, distances, out InputPortCandidate candidate))
        {
            return false;
        }

        List<Vector2Int> cellPath = new List<Vector2Int>();
        AssemblyMathUtility.ReconstructPath(cameFrom, candidate.mappedCell, cellPath);
        if (cellPath.Count <= 0 || cellPath[0] != senderCell)
        {
            return false;
        }

        pipeCellCount = cellPath.Count;

        Vector3 senderPortLocalPoint = ownerSatellite.transform.InverseTransformPoint(outputPort.transform.position);
        senderPortLocalPoint.z = PipeTokenLocalZ;
        AppendPointIfDistinct(localPoints, senderPortLocalPoint);

        for (int i = 0; i < cellPath.Count; i++)
        {
            AppendPointIfDistinct(localPoints, GridToLocalPosition(cellPath[i], PipeTokenLocalZ));
        }

        Vector3 receiverPortLocalPoint = ownerSatellite.transform.InverseTransformPoint(candidate.portTransform.position);
        receiverPortLocalPoint.z = PipeTokenLocalZ;
        AppendPointIfDistinct(localPoints, receiverPortLocalPoint);

        receiverInputInventory = candidate.inputInventory;
        receiverPartFocus = candidate.partFocus;
        return localPoints.Count >= 2;
    }

    private static bool TrySelectReceiverCandidate(
        AssemblyOutputPortFocus outputPort,
        ArtificialSatellite ownerSatellite,
        Dictionary<Vector2Int, int> distances,
        out InputPortCandidate candidate)
    {
        candidate = default;
        bool found = false;
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

            if (!ModulePartInventoryUtility.UsesSplitInventories(partFocus.SourcePart) ||
                !TryResolveReceiverInputInventory(partFocus, out StructureResourceInventory inputInventory) ||
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
                if (entry == null || entry.portType != AssemblyPortType.Input || entry.portTransform == null)
                {
                    continue;
                }

                if (!TryGetMappedCellForInputEntry(partFocus, layout, entry, out Vector2Int mappedCell) ||
                    !distances.TryGetValue(mappedCell, out int distance))
                {
                    continue;
                }

                if (!found || distance < candidate.distance)
                {
                    candidate = new InputPortCandidate
                    {
                        partFocus = partFocus,
                        inputInventory = inputInventory,
                        portTransform = entry.portTransform,
                        mappedCell = mappedCell,
                        distance = distance
                    };
                    found = true;
                }
            }
        }

        return found;
    }

    private static bool TryGetMappedCellForInputEntry(
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int mappedCell)
    {
        mappedCell = Vector2Int.zero;
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
