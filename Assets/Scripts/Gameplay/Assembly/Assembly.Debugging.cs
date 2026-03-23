using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Assembly
{
    private static bool IsPipePart(Part targetPart)
    {
        return targetPart != null && targetPart.partType == PartType.Pipe;
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
}
