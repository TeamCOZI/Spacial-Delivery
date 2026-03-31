using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private Vector2 GetCurrentPartSnapOffset()
    {
        if (HasGhostLayout())
        {
            return Vector2.zero;
        }

        Vector2Int span = GetCurrentPartCellSpanForCurrentRotation();
        int width = span.x;
        int height = span.y;

        float offsetX = (width % 2 == 0) ? (cellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (cellSize * 0.5f) : 0f;

        return new Vector2(offsetX, offsetY);
    }

    private Vector2 GetCurrentPartPivotOffset()
    {
        if (partGhost == null) return Vector2.zero;

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        if (!TryBuildLayoutCellCenterOffset(layout, GetQuarterTurns(partGhost.transform.localRotation), out Vector2 offset))
        {
            return Vector2.zero;
        }

        return offset * cellSize;
    }

    private Vector2 GetPartPivotOffset(AssemblyPartPortLayout layout, Quaternion rotation)
    {
        if (!TryBuildLayoutCellCenterOffset(layout, GetQuarterTurns(rotation), out Vector2 offset))
        {
            return Vector2.zero;
        }

        return offset * cellSize;
    }

    private bool HasGhostLayout()
    {
        if (partGhost == null) return false;

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        return layout != null && layout.Ports != null && layout.Ports.Count > 0;
    }

    private static bool TryBuildLayoutCellCenterOffset(
        AssemblyPartPortLayout layout,
        int quarterTurns,
        out Vector2 offsetInCells)
    {
        offsetInCells = Vector2.zero;
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0) return false;

        bool hasCell = false;
        int minX = 0;
        int maxX = 0;
        int minY = 0;
        int maxY = 0;
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null) continue;

            Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
            if (!uniqueCells.Add(rotatedCell)) continue;

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

        if (!hasCell) return false;

        offsetInCells = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        return true;
    }

    private Vector2Int GetCurrentPartCellSpanForCurrentRotation()
    {
        Vector2Int baseSpan = GetCurrentPartCellSpan();
        if (partGhost == null) return baseSpan;

        return RotateCellSpan(baseSpan, partGhost.transform.localRotation);
    }

    private Vector2Int GetCellSpanForRotation(Quaternion rotation)
    {
        Vector2Int baseSpan = GetCurrentPartCellSpan();
        return RotateCellSpan(baseSpan, rotation);
    }

    private static Vector2Int RotateCellSpan(Vector2Int baseSpan, Quaternion rotation)
    {
        int quarterTurns = GetQuarterTurns(rotation);
        bool swapAxes = (Mathf.Abs(quarterTurns) % 2) == 1;
        return swapAxes
            ? new Vector2Int(baseSpan.y, baseSpan.x)
            : baseSpan;
    }

    private Vector2Int GetCurrentPartCellSpan()
    {
        if (part != null)
        {
            return new Vector2Int(Mathf.Max(1, part.gridWidth), Mathf.Max(1, part.gridHeight));
        }

        if (partGhost != null)
        {
            Vector3 scale = partGhost.transform.localScale;
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) / cellSize));
            int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.y) / cellSize));
            return new Vector2Int(width, height);
        }

        return Vector2Int.one;
    }

    private Vector2Int LocalPositionToGrid(Vector3 localPos, Vector2 snapOffset)
    {
        int gridCenter = gridSize / 2;
        return new Vector2Int
        (
            QuantizeToCellIndex((localPos.x - snapOffset.x) / cellSize) + gridCenter,
            QuantizeToCellIndex((localPos.y - snapOffset.y) / cellSize) + gridCenter
        );
    }

    private Vector3 GridToLocalPosition(Vector2Int gridPos, Vector2 snapOffset)
    {
        int gridCenter = gridSize / 2;
        float x = (gridPos.x - gridCenter) * cellSize + snapOffset.x;
        float y = (gridPos.y - gridCenter) * cellSize + snapOffset.y;
        return new Vector3(x, y, 0f);
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridSize && cell.y >= 0 && cell.y < gridSize;
    }

    private void RegisterCoreOccupiedCells(Vector2Int centerCell)
    {
        Vector2Int span = GetCoreCellSpan();
        RegisterOccupiedRect(centerCell, span, artificialSatellite != null ? artificialSatellite.gameObject : null);
    }

    private Vector2Int GetCoreCellSpan()
    {
        Part corePart = GameplayRuntimeAccess.TryGetPartDb(out PartDB partDb)
            ? partDb.GetPartByName("Core")
            : null;
        if (corePart != null)
        {
            return new Vector2Int(Mathf.Max(1, corePart.gridWidth), Mathf.Max(1, corePart.gridHeight));
        }

        if (artificialSatellite != null)
        {
            Vector3 scale = artificialSatellite.transform.localScale;
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) / cellSize));
            int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.y) / cellSize));
            return new Vector2Int(width, height);
        }

        return Vector2Int.one;
    }

    private void RegisterOccupiedRect(Vector2Int centerCell, Vector2Int span, GameObject owner)
    {
        int width = Mathf.Max(1, span.x);
        int height = Mathf.Max(1, span.y);

        int minX = -(width / 2);
        int maxX = width - (width / 2) - 1;
        int minY = -(height / 2);
        int maxY = height - (height / 2) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (!IsInsideGrid(cell)) continue;
                occupiedCells[cell] = owner;
            }
        }
    }

    private bool IsCurrentGhostPlacementAreaFree()
    {
        if (partGhost == null || part == null) return false;

        Vector2Int center = GetCurrentGhostCenterCell();
        return IsAreaFree(center, partGhost.transform.localRotation);
    }

    private bool TryCollectPipeReplacementRoots(
        Vector2Int centerCell,
        Quaternion rotation,
        out List<GameObject> replacementRoots)
    {
        replacementRoots = new List<GameObject>();
        if (!CanReplaceInstalledPipe(part))
        {
            return false;
        }

        if (!TryGetCurrentPartFootprintCells(centerCell, rotation, out List<Vector2Int> occupiedFootprint))
        {
            return false;
        }

        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();
        for (int i = 0; i < occupiedFootprint.Count; i++)
        {
            Vector2Int cell = occupiedFootprint[i];
            if (!occupiedCells.TryGetValue(cell, out GameObject owner) || owner == null)
            {
                continue;
            }

            GameObject existingRoot = ResolveRemovablePartRoot(owner);
            if (existingRoot == null || !uniqueRoots.Add(existingRoot))
            {
                continue;
            }

            if (!CanReplacePipeRoot(existingRoot, centerCell, rotation))
            {
                replacementRoots.Clear();
                return false;
            }

            replacementRoots.Add(existingRoot);
        }

        return replacementRoots.Count > 0;
    }

    private Vector2Int GetCurrentGhostCenterCell()
    {
        Vector2 snapOffset = GetCurrentPartSnapOffset();
        Vector2 pivotOffset = GetCurrentPartPivotOffset();
        return LocalPositionToGrid(partGhost.transform.localPosition - (Vector3)pivotOffset, snapOffset);
    }

    private bool IsAreaFree(Vector2Int centerCell, Quaternion rotation)
    {
        if (TryGetCurrentPartFootprintCells(centerCell, rotation, out List<Vector2Int> occupiedFootprint))
        {
            for (int i = 0; i < occupiedFootprint.Count; i++)
            {
                Vector2Int cell = occupiedFootprint[i];
                if (!IsInsideGrid(cell)) return false;
                if (occupiedCells.ContainsKey(cell) && !CanReplaceGhostFootprintAtCell(cell, centerCell, rotation)) return false;
            }

            return occupiedFootprint.Count > 0;
        }

        Vector2Int span = GetCellSpanForRotation(rotation);
        return IsAreaFree(centerCell, span);
    }

    private bool CanReplaceGhostFootprintAtCell(Vector2Int occupiedCell, Vector2Int ghostCenterCell, Quaternion ghostRotation)
    {
        if (!CanReplaceInstalledPipe(part))
        {
            return false;
        }

        if (!occupiedCells.TryGetValue(occupiedCell, out GameObject owner) || owner == null)
        {
            return true;
        }

        GameObject existingRoot = ResolveRemovablePartRoot(owner);
        return existingRoot != null && CanReplacePipeRoot(existingRoot, ghostCenterCell, ghostRotation);
    }

    private bool CanReplacePipeRoot(GameObject existingRoot, Vector2Int ghostCenterCell, Quaternion ghostRotation)
    {
        if (existingRoot == null)
        {
            return false;
        }

        AssemblyPartFocus existingFocus = existingRoot.GetComponent<AssemblyPartFocus>();
        if (existingFocus == null || existingFocus.SourcePart == null || !PipePartUtility.IsStandardPipePart(existingFocus.SourcePart))
        {
            return false;
        }

        if (!TryBuildPortRequirementMapForPart(existingRoot, out Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType> existingPorts) || existingPorts.Count == 0)
        {
            return false;
        }

        if (!TryBuildGhostPortRequirementMap(ghostCenterCell, ghostRotation, out Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType> ghostPorts) || ghostPorts.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<(Vector2Int cell, CellSideMask side), AssemblyPortType> pair in existingPorts)
        {
            if (!ghostPorts.TryGetValue(pair.Key, out AssemblyPortType ghostPortType))
            {
                return false;
            }

            if (AssemblyPortTypeUtility.IsInputCompatible(pair.Value) && !AssemblyPortTypeUtility.IsInputCompatible(ghostPortType))
            {
                return false;
            }

            if (AssemblyPortTypeUtility.IsOutputCompatible(pair.Value) && !AssemblyPortTypeUtility.IsOutputCompatible(ghostPortType))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryBuildPortRequirementMapForPart(
        GameObject partRoot,
        out Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType> portMap)
    {
        portMap = new Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType>();
        if (partRoot == null)
        {
            return false;
        }

        AssemblyPartPortLayout layout = partRoot.GetComponent<AssemblyPartPortLayout>();
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null || (!AssemblyPortTypeUtility.IsInputCompatible(entry.portType) && !AssemblyPortTypeUtility.IsOutputCompatible(entry.portType)))
            {
                continue;
            }

            if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide) || portSide == CellSideMask.None)
            {
                continue;
            }

            portMap[(sourceCell, portSide)] = entry.portType;
        }

        return portMap.Count > 0;
    }

    private bool TryBuildGhostPortRequirementMap(
        Vector2Int centerCell,
        Quaternion rotation,
        out Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType> portMap)
    {
        portMap = new Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType>();
        if (partGhost == null)
        {
            return false;
        }

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0)
        {
            return false;
        }

        int quarterTurns = GetQuarterTurns(rotation);
        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null || (!AssemblyPortTypeUtility.IsInputCompatible(entry.portType) && !AssemblyPortTypeUtility.IsOutputCompatible(entry.portType)))
            {
                continue;
            }

            Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
            CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
            if (rotatedSide == CellSideMask.None)
            {
                continue;
            }

            portMap[(centerCell + rotatedCell, rotatedSide)] = entry.portType;
        }

        return portMap.Count > 0;
    }

    private bool TryGetCurrentPartFootprintCells(
        Vector2Int centerCell,
        Quaternion rotation,
        out List<Vector2Int> cells)
    {
        cells = new List<Vector2Int>();
        if (partGhost == null) return false;

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        return TryGetFootprintCellsFromLayout(layout, centerCell, rotation, out cells);
    }

    private bool TryGetFootprintCellsFromLayout(
        AssemblyPartPortLayout layout,
        Vector2Int centerCell,
        Quaternion rotation,
        out List<Vector2Int> cells)
    {
        cells = new List<Vector2Int>();
        if (!TryGetLayoutBoundsInCells(layout, GetQuarterTurns(rotation), out int minX, out int maxX, out int minY, out int maxY))
        {
            return false;
        }

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                cells.Add(new Vector2Int(centerCell.x + x, centerCell.y + y));
            }
        }

        return cells.Count > 0;
    }

    private static bool TryGetLayoutBoundsInCells(
        AssemblyPartPortLayout layout,
        int quarterTurns,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY)
    {
        minX = maxX = minY = maxY = 0;
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0) return false;

        bool hasCell = false;
        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null) continue;

            Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
            if (!uniqueCells.Add(rotatedCell)) continue;

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

        return hasCell;
    }

    private bool IsAreaFree(Vector2Int centerCell, Vector2Int span)
    {
        int width = Mathf.Max(1, span.x);
        int height = Mathf.Max(1, span.y);

        int minX = -(width / 2);
        int maxX = width - (width / 2) - 1;
        int minY = -(height / 2);
        int maxY = height - (height / 2) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (!IsInsideGrid(cell)) return false;
                if (occupiedCells.ContainsKey(cell)) return false;
            }
        }

        return true;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return AssemblyMathUtility.Manhattan(a, b);
    }

    private static int QuantizeToCellIndex(float valueInCells)
    {
        return AssemblyMathUtility.QuantizeToCellIndex(valueInCells);
    }

    private static void ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        List<Vector2Int> path)
    {
        AssemblyMathUtility.ReconstructPath(cameFrom, current, path);
    }

    private static Vector2Int GetPathDirection(List<Vector2Int> path, int fromIndex, int toIndex)
    {
        return AssemblyMathUtility.GetPathDirection(path, fromIndex, toIndex);
    }

    private Vector2Int GetPathSegmentPreviousDir(List<Vector2Int> path, int index)
    {
        if (path == null || index < 0 || index >= path.Count) return Vector2Int.zero;
        if (index == 0 && pipePathStartSelected && pipePathStartIncomingDir != Vector2Int.zero)
        {
            return pipePathStartIncomingDir;
        }

        return GetPathDirection(path, index - 1, index);
    }

    private Vector2Int GetPathSegmentNextDir(List<Vector2Int> path, int index, Vector2Int terminalDirection)
    {
        if (path == null || index < 0 || index >= path.Count) return Vector2Int.zero;
        if (index == path.Count - 1 && terminalDirection != Vector2Int.zero)
        {
            return terminalDirection;
        }

        return GetPathDirection(path, index, index + 1);
    }

    private bool TryResolvePipePreviewPath(
        Vector2Int hoverCell,
        bool hasPreviousHoverCell,
        Vector2Int previousHoverCell,
        out List<Vector2Int> path,
        out Vector2Int terminalDirection)
    {
        terminalDirection = Vector2Int.zero;
        if (TryFindPipePath(pipePathStartCell, hoverCell, out path))
        {
            return true;
        }

        return TryFindPipePathToHoveredInputCell(
            hoverCell,
            hasPreviousHoverCell,
            previousHoverCell,
            out path,
            out terminalDirection);
    }

    private bool TryFindPipePathToHoveredInputCell(
        Vector2Int hoveredOccupiedCell,
        bool hasPreviousHoverCell,
        Vector2Int previousHoverCell,
        out List<Vector2Int> path,
        out Vector2Int terminalDirection)
    {
        path = new List<Vector2Int>();
        terminalDirection = Vector2Int.zero;
        if (!occupiedCells.ContainsKey(hoveredOccupiedCell)) return false;

        if (hasPreviousHoverCell &&
            TryFindPipePathToSpecificInputApproachCell(
                hoveredOccupiedCell,
                previousHoverCell,
                out path,
                out terminalDirection))
        {
            return true;
        }

        Vector2Int[] approachDirections =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        int bestScore = int.MaxValue;
        List<Vector2Int> bestPath = null;
        Vector2Int bestTerminalDirection = Vector2Int.zero;

        for (int i = 0; i < approachDirections.Length; i++)
        {
            Vector2Int terminalDir = approachDirections[i];
            Vector2Int approachCell = hoveredOccupiedCell - terminalDir;
            if (!TryFindPipePathToSpecificInputApproachCell(
                hoveredOccupiedCell,
                approachCell,
                out List<Vector2Int> candidatePath,
                out Vector2Int candidateTerminalDirection))
            {
                continue;
            }

            int score = candidatePath.Count;
            if (hasPreviousHoverCell)
            {
                score += Manhattan(approachCell, previousHoverCell);
            }

            if (bestPath != null && score >= bestScore) continue;

            bestScore = score;
            bestPath = candidatePath;
            bestTerminalDirection = candidateTerminalDirection;
        }

        if (bestPath == null) return false;

        path = bestPath;
        terminalDirection = bestTerminalDirection;
        return true;
    }

    private bool TryFindPipePathToSpecificInputApproachCell(
        Vector2Int hoveredOccupiedCell,
        Vector2Int approachCell,
        out List<Vector2Int> path,
        out Vector2Int terminalDirection)
    {
        path = new List<Vector2Int>();
        terminalDirection = Vector2Int.zero;
        if (!IsInsideGrid(hoveredOccupiedCell) || !IsInsideGrid(approachCell)) return false;

        Vector2Int direction = hoveredOccupiedCell - approachCell;
        if (!IsCardinalDirection(direction)) return false;
        if (!CellHasInputPortFacingDirection(approachCell, direction)) return false;
        if (approachCell != pipePathStartCell && occupiedCells.ContainsKey(approachCell)) return false;
        if (!TryFindPipePath(pipePathStartCell, approachCell, out path)) return false;

        terminalDirection = direction;
        return path.Count > 0;
    }

    private bool CellHasInputPortFacingDirection(Vector2Int cell, Vector2Int direction)
    {
        if (!inputMaskByCell.TryGetValue(cell, out CellSideMask inputMask)) return false;

        CellSideMask side = DirectionToSideMask(new Vector3(direction.x, direction.y, 0f));
        if (side == CellSideMask.None) return false;
        return (inputMask & side) != 0;
    }

    private static bool IsCardinalDirection(Vector2Int direction)
    {
        return (Mathf.Abs(direction.x) == 1 && direction.y == 0)
            || (Mathf.Abs(direction.y) == 1 && direction.x == 0);
    }
    private Vector2Int GetPipeStartIncomingDirection()
    {
        if (partGhost == null || ghostPortProfile == null) return Vector2Int.zero;
        if (ghostPortProfile.InputPortCount <= 0) return Vector2Int.zero;

        Vector3 inputLocal = ghostPortProfile.GetInputPortLocalPosition(0);
        Vector3 inputDirOnSatellite = partGhost.transform.localRotation * inputLocal;
        CellSideMask inputSide = DirectionToSideMask(inputDirOnSatellite);
        if (inputSide == CellSideMask.None) return Vector2Int.zero;
        return -SideToCellOffset(inputSide);
    }

    private static bool IsCornerSegment(Vector2Int previousDir, Vector2Int nextDir)
    {
        return AssemblyMathUtility.IsCornerSegment(previousDir, nextDir);
    }

    private static Quaternion GetPipeSegmentRotation(Vector2Int previousDir, Vector2Int nextDir)
    {
        return AssemblyMathUtility.GetPipeSegmentRotation(previousDir, nextDir);
    }

    private bool TryFindPipePath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (!IsInsideGrid(start) || !IsInsideGrid(goal)) return false;

        if (goal == start)
        {
            if (occupiedCells.ContainsKey(start)) return false;
            path.Add(start);
            return true;
        }

        Vector2Int startIncomingDirection = pipePathStartSelected && IsCardinalDirection(pipePathStartIncomingDir)
            ? pipePathStartIncomingDir
            : Vector2Int.zero;
        PipePathNode startNode = new PipePathNode(start, startIncomingDirection);
        Queue<PipePathNode> open = new Queue<PipePathNode>();
        HashSet<PipePathNode> visited = new HashSet<PipePathNode>();
        Dictionary<PipePathNode, PipePathNode> cameFrom = new Dictionary<PipePathNode, PipePathNode>();
        open.Enqueue(startNode);
        visited.Add(startNode);

        Vector2Int[] directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        while (open.Count > 0)
        {
            PipePathNode current = open.Dequeue();
            if (current.Cell == goal)
            {
                ReconstructPipePath(cameFrom, current, path);
                return path.Count > 0;
            }

            bool hasForcedOutputDirection = TryGetForcedPipePathOutputDirection(current.Cell, current.IncomingSideDirection, out Vector2Int forcedOutputDirection);
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int stepDirection = directions[i];
                if (hasForcedOutputDirection && stepDirection != forcedOutputDirection)
                {
                    continue;
                }

                Vector2Int neighbor = current.Cell + stepDirection;
                if (!IsInsideGrid(neighbor)) continue;
                if (visited.Contains(new PipePathNode(neighbor, -stepDirection))) continue;

                bool isOccupied = occupiedCells.TryGetValue(neighbor, out GameObject owner) && owner != null;
                if (isOccupied && !CanEnterPipePathCell(neighbor, -stepDirection))
                {
                    continue;
                }

                PipePathNode neighborNode = new PipePathNode(neighbor, -stepDirection);
                if (!visited.Add(neighborNode))
                {
                    continue;
                }

                cameFrom[neighborNode] = current;
                open.Enqueue(neighborNode);
            }
        }

        return false;
    }
}
