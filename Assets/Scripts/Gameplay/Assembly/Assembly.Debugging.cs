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
        int cellIndex = hoveredCell.y * gridSize + hoveredCell.x;
        Debug.Log($"[CellInfo] gridCell={hoveredCell} index={cellIndex} owner={owner} ports({portTypes}) neighbors({neighbors})");
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
