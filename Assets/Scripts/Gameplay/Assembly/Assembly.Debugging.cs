using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Assembly
{
    private static bool IsPipePart(Part targetPart)
    {
        return PipePartUtility.IsPipePart(targetPart);
    }

    private static bool UsesPipePathPlacement(Part targetPart)
    {
        return PipePartUtility.UsesPathPlacement(targetPart);
    }

    private static bool CanReplaceInstalledPipe(Part targetPart)
    {
        return PipePartUtility.CanReplaceInstalledPipe(targetPart);
    }

    private static bool TryGetAssemblyManager(out AssemblyManager manager)
    {
        return GameplayRuntimeAccess.TryGetAssemblyManager(out manager);
    }

    private void DebugLogPipeStartMatch(
        Vector2Int centerCell,
        CellSideMask inputSide,
        CellSideMask requiredOutputSide,
        AssemblyPort matchedPort)
    {
        if (!debugPipePlacement || partGhost == null) return;

        float rotZ = Mathf.Repeat(partGhost.transform.localEulerAngles.z, 360f);
        bool isMatch = matchedPort != null;
        bool shouldLog = !hasPipeDebugState
            || centerCell != lastPipeDebugCell
            || Mathf.Abs(rotZ - lastPipeDebugRotZ) > 0.1f
            || isMatch != lastPipeDebugMatch
            || inputSide != lastPipeDebugInputSide
            || requiredOutputSide != lastPipeDebugRequiredSide;

        if (!shouldLog) return;

        CellSideMask mask = outputMaskByCell.TryGetValue(centerCell, out CellSideMask cellMask)
            ? cellMask
            : CellSideMask.None;

        string matchedName = matchedPort != null ? matchedPort.name : "none";
        string sides = GetCellOutputSideDetails(centerCell);
        Debug.Log(
            $"[PipeStartDebug] cell={centerCell} rotZ={rotZ:F1} input={inputSide} needs={requiredOutputSide} " +
            $"cellOutputMask={mask} sides={sides} matched={isMatch} matchedPort={matchedName}");

        hasPipeDebugState = true;
        lastPipeDebugCell = centerCell;
        lastPipeDebugRotZ = rotZ;
        lastPipeDebugMatch = isMatch;
        lastPipeDebugInputSide = inputSide;
        lastPipeDebugRequiredSide = requiredOutputSide;
    }

    private string GetCellOutputSideDetails(Vector2Int cell)
    {
        if (!outputPortsByCellAndSide.TryGetValue(cell, out Dictionary<CellSideMask, AssemblyPort> perSide))
        {
            return "T:none,B:none,L:none,R:none";
        }

        string top = perSide.TryGetValue(CellSideMask.Top, out AssemblyPort t) && t != null ? t.name : "none";
        string bottom = perSide.TryGetValue(CellSideMask.Bottom, out AssemblyPort b) && b != null ? b.name : "none";
        string left = perSide.TryGetValue(CellSideMask.Left, out AssemblyPort l) && l != null ? l.name : "none";
        string right = perSide.TryGetValue(CellSideMask.Right, out AssemblyPort r) && r != null ? r.name : "none";
        return $"T:{top},B:{bottom},L:{left},R:{right}";
    }

    private void DebugLogHoveredCellPortMapping()
    {
        if (!debugHoverCellPortMapping || !TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)) return;
        RefreshOutputPortsNow();

        Vector2Int hoveredCell = LocalPositionToGrid(localPoint, ZeroSnapOffset);
        if (!IsInsideGrid(hoveredCell)) return;

        if (hasHoverDebugCell && hoveredCell == lastHoverDebugCell) return;
        hasHoverDebugCell = true;
        lastHoverDebugCell = hoveredCell;

        string portTypes = GetCellPortTypeDetails(hoveredCell);
        string owner = GetOwnerLabelForCell(hoveredCell);
        string neighbors = GetNeighborConnectionDetails(hoveredCell);
        string pipeEndDetails = GetPipeEndDetailsForCell(hoveredCell);
        int cellIndex = hoveredCell.y * gridSize + hoveredCell.x;
        string pipeSuffix = string.IsNullOrEmpty(pipeEndDetails) ? string.Empty : $" {pipeEndDetails}";
        Debug.Log($"[CellInfo] gridCell={hoveredCell} index={cellIndex} owner={owner} ports({portTypes}) neighbors({neighbors}){pipeSuffix}");
    }

    private string GetOccupiedOwnerName(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject owner) || owner == null) return "none";
        return owner.name;
    }

    private string GetCellPortTypeDetails(Vector2Int cell)
    {
        if (!portOwnerByCellAndSide.TryGetValue(cell, out Dictionary<CellSideMask, string> ownerBySide))
        {
            return "T:None,B:None,L:None,R:None";
        }

        return $"T:{FormatPortOwner(ownerBySide, CellSideMask.Top)}," +
               $"B:{FormatPortOwner(ownerBySide, CellSideMask.Bottom)}," +
               $"L:{FormatPortOwner(ownerBySide, CellSideMask.Left)}," +
               $"R:{FormatPortOwner(ownerBySide, CellSideMask.Right)}";
    }

    private static string FormatPortOwner(Dictionary<CellSideMask, string> ownerBySide, CellSideMask side)
    {
        if (ownerBySide == null) return "None";
        if (!ownerBySide.TryGetValue(side, out string label) || string.IsNullOrWhiteSpace(label)) return "None";
        return label;
    }

    private string GetOwnerLabelForCell(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject rawOwner) || rawOwner == null) return "None";
        if (rawOwner == artificialSatellite.gameObject) return "Core";

        GameObject partOwner = ResolveRemovablePartRoot(rawOwner);
        if (partOwner == null) return "Core";

        AssemblyPartFocus focus = partOwner.GetComponent<AssemblyPartFocus>();
        if (focus != null && focus.SourcePart != null && !string.IsNullOrWhiteSpace(focus.SourcePart.partName))
        {
            return focus.SourcePart.partName;
        }

        return partOwner.name;
    }

    private string GetNeighborConnectionDetails(Vector2Int cell)
    {
        BuildSourcePortOwnerMaps(
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
            out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys);

        Vector2Int topCell = cell + Vector2Int.up;
        Vector2Int bottomCell = cell + Vector2Int.down;
        Vector2Int leftCell = cell + Vector2Int.left;
        Vector2Int rightCell = cell + Vector2Int.right;

        return $"T:{FormatNeighborConnection(cell, topCell, inputOwnersByKey, outputOwnersByKey, coreOutputKeys)}," +
               $"B:{FormatNeighborConnection(cell, bottomCell, inputOwnersByKey, outputOwnersByKey, coreOutputKeys)}," +
               $"L:{FormatNeighborConnection(cell, leftCell, inputOwnersByKey, outputOwnersByKey, coreOutputKeys)}," +
               $"R:{FormatNeighborConnection(cell, rightCell, inputOwnersByKey, outputOwnersByKey, coreOutputKeys)}";
    }

    private string GetPipeEndDetailsForCell(Vector2Int cell)
    {
        if (!TryGetPipeOwnerForCell(cell, out GameObject pipeOwner)) return string.Empty;
        if (artificialSatellite == null) return "pipe(end=unknown reason=no-satellite)";

        AssemblyPartPortLayout layout = pipeOwner.GetComponent<AssemblyPartPortLayout>();
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0)
        {
            return "pipe(end=unknown reason=no-layout)";
        }

        BuildSourcePortOwnerMaps(
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
            out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys);

        List<string> nonPipeReasons = new List<string>(2);
        int relevantPortCount = 0;
        int nonTerminalPortCount = 0;

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null) continue;
            if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;

            if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) continue;
            if (sourceCell != cell || portSide == CellSideMask.None) continue;

            relevantPortCount++;
            if (IsPipePortConnectedToPipe(
                sourceCell,
                portSide,
                entry.portType,
                inputOwnersByKey,
                outputOwnersByKey,
                coreOutputKeys,
                out string failureReason))
            {
                continue;
            }

            if (!HasTerminalBoundaryOwner(pipeOwner, sourceCell, portSide, entry.portType, inputOwnersByKey, outputOwnersByKey, coreOutputKeys))
            {
                nonTerminalPortCount++;
                continue;
            }

            nonPipeReasons.Add($"{FormatCellSide(portSide)} {failureReason}");
        }

        if (relevantPortCount == 0)
        {
            return "pipe(end=unknown reason=no-port-on-cell)";
        }

        if (nonPipeReasons.Count == 0)
        {
            return nonTerminalPortCount > 0
                ? "pipe(end=false reason=no-terminal-boundary-owner)"
                : "pipe(end=false reason=all-ports-connected-to-pipe)";
        }

        return $"pipe(end=true reason={string.Join("; ", nonPipeReasons)})";
    }

    private bool IsPipePortConnectedToPipe(
        Vector2Int sourceCell,
        CellSideMask portSide,
        AssemblyPortType portType,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys,
        out string failureReason)
    {
        failureReason = string.Empty;

        Vector2Int neighborCell = sourceCell + SideToCellOffset(portSide);
        if (!IsInsideGrid(neighborCell))
        {
            failureReason = "connected-to=out";
            return false;
        }

        CellSideMask neighborSide = OppositeSide(portSide);
        if (neighborSide == CellSideMask.None)
        {
            failureReason = "connected-to=invalid";
            return false;
        }

        if (portType == AssemblyPortType.Input)
        {
            if (TryGetOwnersByPortKey(outputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> outputOwners))
            {
                if (ContainsPipeOwner(outputOwners)) return true;
                failureReason = $"connected-to={FormatOwnerLabels(outputOwners)}";
                return false;
            }

            if (coreOutputKeys != null && coreOutputKeys.Contains((neighborCell, neighborSide)))
            {
                failureReason = "connected-to=Core";
                return false;
            }
        }
        else
        {
            if (TryGetOwnersByPortKey(inputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> inputOwners))
            {
                if (ContainsPipeOwner(inputOwners)) return true;
                failureReason = $"connected-to={FormatOwnerLabels(inputOwners)}";
                return false;
            }
        }

        if (!occupiedCells.TryGetValue(neighborCell, out GameObject neighborOwner) || neighborOwner == null)
        {
            failureReason = "connected-to=empty";
            return false;
        }

        failureReason = $"link=false({GetOwnerLabelForCell(neighborCell)})";
        return false;
    }

    private bool ContainsPipeOwner(IEnumerable<GameObject> owners)
    {
        if (owners == null) return false;

        foreach (GameObject owner in owners)
        {
            if (IsPipeOwner(owner)) return true;
        }

        return false;
    }

    private string FormatOwnerLabels(IEnumerable<GameObject> owners)
    {
        if (owners == null) return "Unknown";

        HashSet<string> labels = new HashSet<string>();
        foreach (GameObject owner in owners)
        {
            string label = GetOwnerLabelForObject(owner);
            if (string.IsNullOrWhiteSpace(label)) continue;
            labels.Add(label);
        }

        if (labels.Count == 0) return "Unknown";
        return string.Join("+", labels);
    }

    private string GetOwnerLabelForObject(GameObject owner)
    {
        if (owner == null) return "Unknown";
        if (artificialSatellite != null && owner == artificialSatellite.gameObject) return "Core";

        GameObject partOwner = ResolveRemovablePartRoot(owner);
        if (partOwner == null) return "Core";

        AssemblyPartFocus focus = partOwner.GetComponent<AssemblyPartFocus>();
        if (focus != null && focus.SourcePart != null && !string.IsNullOrWhiteSpace(focus.SourcePart.partName))
        {
            return focus.SourcePart.partName;
        }

        return partOwner.name;
    }

    private bool TryGetPipeOwnerForCell(Vector2Int cell, out GameObject pipeOwner)
    {
        pipeOwner = null;
        if (!occupiedCells.TryGetValue(cell, out GameObject rawOwner) || rawOwner == null) return false;

        GameObject resolvedOwner = ResolveRemovablePartRoot(rawOwner);
        if (resolvedOwner == null) return false;
        if (!IsPipeOwner(resolvedOwner)) return false;

        pipeOwner = resolvedOwner;
        return true;
    }

    private static bool IsPipeOwner(GameObject owner)
    {
        if (owner == null) return false;

        AssemblyPartFocus focus = owner.GetComponent<AssemblyPartFocus>();
        if (focus != null && IsPipePart(focus.SourcePart))
        {
            return true;
        }

        return ContainsNameTokenInHierarchy(owner.transform, "Pipe");
    }

    private static string FormatCellSide(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return "Top";
            case CellSideMask.Bottom: return "Bottom";
            case CellSideMask.Left: return "Left";
            case CellSideMask.Right: return "Right";
            default: return "Unknown";
        }
    }
    private string FormatNeighborConnection(
        Vector2Int originCell,
        Vector2Int neighborCell,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys)
    {
        if (!IsInsideGrid(neighborCell))
        {
            return "out";
        }

        string neighborOwnerLabel = GetOwnerLabelForCell(neighborCell);
        if (!occupiedCells.TryGetValue(originCell, out GameObject originRaw) || originRaw == null)
        {
            return $"{neighborOwnerLabel}|empty";
        }

        if (!occupiedCells.TryGetValue(neighborCell, out GameObject neighborRaw) || neighborRaw == null)
        {
            return $"{neighborOwnerLabel}|empty";
        }

        bool isConnected = AreAdjacentCellsConnectedByOppositePorts(
            originCell,
            neighborCell,
            inputOwnersByKey,
            outputOwnersByKey,
            coreOutputKeys);
        return $"{neighborOwnerLabel}|link={isConnected}";
    }

    private bool TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)
    {
        localPoint = Vector3.zero;
        if (artificialSatellite == null) return false;
        if (!TryEnsureMainCamera()) return false;

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        float cameraSpaceDepth = Vector3.Dot(
            artificialSatellite.transform.position - mainCamera.transform.position,
            mainCamera.transform.forward
        );
        cameraSpaceDepth = Mathf.Max(MinCameraSpaceDepth, cameraSpaceDepth);

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cameraSpaceDepth));
        localPoint = artificialSatellite.transform.InverseTransformPoint(worldPoint);
        return true;
    }

    private void DrawDebugConnectionGraph()
    {
        if (!debugConnectionGraph || artificialSatellite == null)
        {
            return;
        }

        if (!debugHoverCellPortMapping)
        {
            RefreshOutputPortsNow();
        }

        DrawConnectedPortLinkGraph();
        DrawPipeGraphOverlays();
    }

    private void DrawConnectedPortLinkGraph()
    {
        BuildSourcePortOwnerMaps(
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
            out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys);

        Vector2Int[] directions = { Vector2Int.right, Vector2Int.up };
        foreach (KeyValuePair<Vector2Int, GameObject> pair in occupiedCells)
        {
            Vector2Int cell = pair.Key;
            for (int dirIndex = 0; dirIndex < directions.Length; dirIndex++)
            {
                Vector2Int neighborCell = cell + directions[dirIndex];
                if (!occupiedCells.ContainsKey(neighborCell))
                {
                    continue;
                }

                if (!IsCoreOrPipeCell(cell) && !IsCoreOrPipeCell(neighborCell))
                {
                    continue;
                }

                if (!AreAdjacentCellsConnectedByOppositePorts(cell, neighborCell, inputOwnersByKey, outputOwnersByKey, coreOutputKeys))
                {
                    continue;
                }

                DrawDebugCellLink(cell, neighborCell, ResolveConnectionGraphColor(cell, neighborCell), 0.004f);
            }
        }
    }

    private void DrawPipeGraphOverlays()
    {
        Dictionary<Vector2Int, List<Vector2Int>> physicalAdjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        HashSet<Vector2Int> physicalPipeCells = new HashSet<Vector2Int>();
        PipeConnectivityUtility.BuildConnectedPipeAdjacency(artificialSatellite, physicalAdjacency, physicalPipeCells);
        DrawPipeAdjacency(physicalAdjacency, physicalPipeCells, new Color(1f, 0.78f, 0.16f, 1f), 0.006f);

        if (!debugActiveSplitGraph)
        {
            return;
        }

        Dictionary<Vector2Int, List<Vector2Int>> activeAdjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        HashSet<Vector2Int> activePipeCells = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell = new Dictionary<Vector2Int, SplitPipeState>();
        PipeConnectivityUtility.BuildActivePipeAdjacency(artificialSatellite, activeAdjacency, activePipeCells, inputDirectionsByCell, outputDirectionsByCell, splitStatesByCell);
        DrawPipeAdjacency(activeAdjacency, activePipeCells, new Color(1f, 0.15f, 0.85f, 1f), 0.009f);
    }

    private void DrawPipeAdjacency(
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<Vector2Int> pipeCells,
        Color color,
        float localZOffset)
    {
        if (adjacency == null || pipeCells == null)
        {
            return;
        }

        foreach (Vector2Int cell in pipeCells)
        {
            DrawDebugCellMarker(cell, color, localZOffset);
            if (!adjacency.TryGetValue(cell, out List<Vector2Int> neighbors) || neighbors == null)
            {
                continue;
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                DrawDebugCellLink(cell, neighbors[i], color, localZOffset);
            }
        }
    }

    private bool IsCoreOrPipeCell(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject rawOwner) || rawOwner == null)
        {
            return false;
        }

        if (rawOwner == artificialSatellite.gameObject)
        {
            return true;
        }

        return TryGetPipeOwnerForCell(cell, out _);
    }

    private Color ResolveConnectionGraphColor(Vector2Int cellA, Vector2Int cellB)
    {
        bool aCore = occupiedCells.TryGetValue(cellA, out GameObject ownerA) && ownerA == artificialSatellite.gameObject;
        bool bCore = occupiedCells.TryGetValue(cellB, out GameObject ownerB) && ownerB == artificialSatellite.gameObject;
        bool aPipe = TryGetPipeOwnerForCell(cellA, out _);
        bool bPipe = TryGetPipeOwnerForCell(cellB, out _);

        if (aCore || bCore)
        {
            return new Color(0.2f, 0.9f, 1f, 1f);
        }

        if (aPipe && bPipe)
        {
            return new Color(1f, 0.82f, 0.12f, 1f);
        }

        if (aPipe || bPipe)
        {
            return new Color(0.35f, 1f, 0.45f, 1f);
        }

        return new Color(0.8f, 0.8f, 0.8f, 1f);
    }

    private void DrawDebugCellLink(Vector2Int startCell, Vector2Int endCell, Color color, float localZOffset)
    {
        Vector3 startWorld = GetDebugCellWorldPoint(startCell, localZOffset);
        Vector3 endWorld = GetDebugCellWorldPoint(endCell, localZOffset);
        Debug.DrawLine(startWorld, endWorld, color, 0f, false);
    }

    private void DrawDebugCellMarker(Vector2Int cell, Color color, float localZOffset)
    {
        Vector3 center = GetDebugCellWorldPoint(cell, localZOffset);
        Vector3 right = artificialSatellite.transform.TransformDirection(Vector3.right) * (cellSize * 0.12f);
        Vector3 up = artificialSatellite.transform.TransformDirection(Vector3.up) * (cellSize * 0.12f);
        Debug.DrawLine(center - right, center + right, color, 0f, false);
        Debug.DrawLine(center - up, center + up, color, 0f, false);
    }

    private Vector3 GetDebugCellWorldPoint(Vector2Int cell, float localZOffset)
    {
        Vector3 localPoint = GridToLocalPosition(cell, ZeroSnapOffset);
        localPoint.z += localZOffset;
        return artificialSatellite.transform.TransformPoint(localPoint);
    }
}