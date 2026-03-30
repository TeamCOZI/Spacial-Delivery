using System.Collections.Generic;
using UnityEngine;

public static class PipeConnectivityUtility
{
    private const float CellSize = 0.1f;
    private const int GridSize = 99;

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public static void BuildConnectedPipeAdjacency(
        ArtificialSatellite ownerSatellite,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<Vector2Int> pipeCells)
    {
        if (adjacency == null)
        {
            return;
        }

        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell = new Dictionary<Vector2Int, SplitPipeState>();
        BuildPipePortMaps(ownerSatellite, inputDirectionsByCell, outputDirectionsByCell, splitStatesByCell, pipeCells);
        InitializePipeAdjacency(adjacency, pipeCells);
        BuildUndirectedAdjacency(adjacency, pipeCells, inputDirectionsByCell, outputDirectionsByCell);
    }

    public static void BuildActivePipeAdjacency(
        ArtificialSatellite ownerSatellite,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell)
    {
        if (adjacency == null)
        {
            return;
        }

        BuildPipePortMaps(ownerSatellite, inputDirectionsByCell, outputDirectionsByCell, splitStatesByCell, pipeCells);
        InitializePipeAdjacency(adjacency, pipeCells);
        if (pipeCells == null)
        {
            return;
        }

        List<Vector2Int> cells = new List<Vector2Int>(pipeCells);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!adjacency.TryGetValue(cell, out List<Vector2Int> neighbors))
            {
                continue;
            }

            if (splitStatesByCell != null && splitStatesByCell.TryGetValue(cell, out SplitPipeState splitPipeState) && splitPipeState != null)
            {
                if (TryGetSelectedSplitOutputDirection(cell, splitPipeState, inputDirectionsByCell, outputDirectionsByCell, pipeCells, out Vector2Int selectedDirection))
                {
                    Vector2Int neighborCell = cell + selectedDirection;
                    if (pipeCells.Contains(neighborCell))
                    {
                        neighbors.Add(neighborCell);
                    }
                }

                continue;
            }

            for (int dirIndex = 0; dirIndex < CardinalDirections.Length; dirIndex++)
            {
                Vector2Int direction = CardinalDirections[dirIndex];
                Vector2Int neighborCell = cell + direction;
                if (CanTraverseDirected(cell, direction, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
                {
                    neighbors.Add(neighborCell);
                }
            }
        }
    }

    public static void BuildPipePortMaps(
        ArtificialSatellite ownerSatellite,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        Dictionary<Vector2Int, SplitPipeState> splitStatesByCell,
        HashSet<Vector2Int> pipeCells)
    {
        inputDirectionsByCell?.Clear();
        outputDirectionsByCell?.Clear();
        splitStatesByCell?.Clear();
        pipeCells?.Clear();
        if (ownerSatellite == null)
        {
            return;
        }

        AssemblyPartPortLayout[] layouts = ownerSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null || layout.GetComponentInParent<AssemblyGhostMarker>() != null)
            {
                continue;
            }

            AssemblyPartFocus partFocus = layout.GetComponent<AssemblyPartFocus>();
            if (partFocus == null)
            {
                partFocus = layout.GetComponentInParent<AssemblyPartFocus>();
            }

            if (partFocus == null || partFocus.SourcePart == null || partFocus.SourcePart.partType != PartType.Pipe)
            {
                continue;
            }

            bool addedPortMapping = false;
            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries != null)
            {
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    AssemblyPartPortLayout.PortEntry entry = entries[entryIndex];
                    if (entry == null || (!AssemblyPortTypeUtility.IsInputCompatible(entry.portType) && !AssemblyPortTypeUtility.IsOutputCompatible(entry.portType)))
                    {
                        continue;
                    }

                    if (!TryGetPipeEntryMapping(layout, partFocus, entry, out Vector2Int sourceCell, out Vector2Int sideDirection))
                    {
                        continue;
                    }

                    addedPortMapping = true;
                    pipeCells?.Add(sourceCell);
                    if (AssemblyPortTypeUtility.IsInputCompatible(entry.portType))
                    {
                        AddDirection(inputDirectionsByCell, sourceCell, sideDirection);
                    }

                    if (AssemblyPortTypeUtility.IsOutputCompatible(entry.portType))
                    {
                        AddDirection(outputDirectionsByCell, sourceCell, sideDirection);
                    }

                    if (splitStatesByCell != null && SplitPipeUtility.IsSplitPipePart(partFocus.SourcePart))
                    {
                        SplitPipeState splitPipeState = SplitPipeUtility.ResolveState(partFocus.gameObject);
                        if (splitPipeState != null)
                        {
                            splitStatesByCell[sourceCell] = splitPipeState;
                        }
                    }
                }
            }

            if (!addedPortMapping && TryResolvePartCenterCell(layout, partFocus, out Vector2Int centerCell))
            {
                pipeCells?.Add(centerCell);
                if (splitStatesByCell != null && SplitPipeUtility.IsSplitPipePart(partFocus.SourcePart))
                {
                    SplitPipeState splitPipeState = SplitPipeUtility.ResolveState(partFocus.gameObject);
                    if (splitPipeState != null)
                    {
                        splitStatesByCell[centerCell] = splitPipeState;
                    }
                }
            }
        }
    }

    public static bool TryGetSelectedSplitOutputDirection(
        Vector2Int currentCell,
        SplitPipeState splitPipeState,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell,
        HashSet<Vector2Int> pipeCells,
        out Vector2Int selectedDirection)
    {
        selectedDirection = Vector2Int.zero;
        if (splitPipeState == null || outputDirectionsByCell == null || !outputDirectionsByCell.TryGetValue(currentCell, out HashSet<Vector2Int> outputDirections) || outputDirections == null)
        {
            return false;
        }

        List<Vector2Int> availableDirections = new List<Vector2Int>(outputDirections.Count);
        foreach (Vector2Int outputDirection in outputDirections)
        {
            Vector2Int neighborCell = currentCell + outputDirection;
            if (!CanTraverseDirected(currentCell, outputDirection, neighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells))
            {
                continue;
            }

            availableDirections.Add(outputDirection);
        }

        if (!splitPipeState.TrySelectPreviewOutputDirection(availableDirections, out selectedDirection))
        {
            return false;
        }

        Vector2Int selectedNeighborCell = currentCell + selectedDirection;
        return CanTraverseDirected(currentCell, selectedDirection, selectedNeighborCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells);
    }

    private static void InitializePipeAdjacency(Dictionary<Vector2Int, List<Vector2Int>> adjacency, HashSet<Vector2Int> pipeCells)
    {
        adjacency.Clear();
        if (pipeCells == null)
        {
            return;
        }

        foreach (Vector2Int cell in pipeCells)
        {
            if (!adjacency.ContainsKey(cell))
            {
                adjacency[cell] = new List<Vector2Int>();
            }
        }
    }

    private static void BuildUndirectedAdjacency(
        Dictionary<Vector2Int, List<Vector2Int>> adjacency,
        HashSet<Vector2Int> pipeCells,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell)
    {
        if (adjacency == null || pipeCells == null)
        {
            return;
        }

        List<Vector2Int> cells = new List<Vector2Int>(pipeCells);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!adjacency.TryGetValue(cell, out List<Vector2Int> neighbors))
            {
                continue;
            }

            for (int dirIndex = 0; dirIndex < CardinalDirections.Length; dirIndex++)
            {
                Vector2Int direction = CardinalDirections[dirIndex];
                Vector2Int neighborCell = cell + direction;
                if (!pipeCells.Contains(neighborCell))
                {
                    continue;
                }

                if (ArePipeCellsLinked(cell, direction, neighborCell, inputDirectionsByCell, outputDirectionsByCell))
                {
                    neighbors.Add(neighborCell);
                }
            }
        }
    }

    private static bool TryGetPipeEntryMapping(
        AssemblyPartPortLayout layout,
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int sourceCell,
        out Vector2Int sideDirection)
    {
        sourceCell = Vector2Int.zero;
        sideDirection = Vector2Int.zero;
        if (layout == null || entry == null || partFocus == null || partFocus.SourcePart == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffsetFromPartFocus(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = AssemblyMathUtility.RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        Vector2Int rotatedSideDirection = RotatePortSide(entry.side, quarterTurns);
        if (rotatedSideDirection == Vector2Int.zero)
        {
            return false;
        }

        sourceCell = partCenterCell + rotatedRelativeCell;
        sideDirection = rotatedSideDirection;
        return true;
    }

    private static bool TryResolvePartCenterCell(AssemblyPartPortLayout layout, AssemblyPartFocus partFocus, out Vector2Int centerCell)
    {
        centerCell = Vector2Int.zero;
        if (layout == null || partFocus == null || partFocus.SourcePart == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffsetFromPartFocus(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        centerCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        return true;
    }

    public static bool CanTraversePhysicalDirection(
        ArtificialSatellite ownerSatellite,
        Vector2Int currentCell,
        Vector2Int nextCell)
    {
        if (ownerSatellite == null)
        {
            return false;
        }

        Vector2Int direction = nextCell - currentCell;
        if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
        {
            return false;
        }

        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        HashSet<Vector2Int> pipeCells = new HashSet<Vector2Int>();
        BuildPipePortMaps(ownerSatellite, inputDirectionsByCell, outputDirectionsByCell, null, pipeCells);
        return CanTraverseDirected(currentCell, direction, nextCell, inputDirectionsByCell, outputDirectionsByCell, pipeCells);
    }
    private static bool ArePipeCellsLinked(
        Vector2Int cell,
        Vector2Int direction,
        Vector2Int neighborCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> inputDirectionsByCell,
        Dictionary<Vector2Int, HashSet<Vector2Int>> outputDirectionsByCell)
    {
        Vector2Int oppositeDirection = -direction;
        bool canReceiveFromNeighbor = HasDirection(inputDirectionsByCell, cell, direction) && HasDirection(outputDirectionsByCell, neighborCell, oppositeDirection);
        bool canSendToNeighbor = HasDirection(outputDirectionsByCell, cell, direction) && HasDirection(inputDirectionsByCell, neighborCell, oppositeDirection);
        return canReceiveFromNeighbor || canSendToNeighbor;
    }

    private static bool CanTraverseDirected(
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

    private static Vector2 GetSnapOffsetFromPartFocus(AssemblyPartFocus partFocus, AssemblyPartPortLayout layout)
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

    private static bool TryBuildLayoutCellCenterOffset(AssemblyPartPortLayout layout, int quarterTurns, out Vector2 offsetInCells)
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

    private static Vector2Int RotatePortSide(AssemblyPartPortLayout.PortSide side, int quarterTurns)
    {
        return AssemblyMathUtility.RotateCellOffset(ConvertSideToDirection(side), quarterTurns);
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

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition, Vector2 snapOffset)
    {
        int gridCenter = GridSize / 2;
        return new Vector2Int(
            AssemblyMathUtility.QuantizeToCellIndex((localPosition.x - snapOffset.x) / CellSize) + gridCenter,
            AssemblyMathUtility.QuantizeToCellIndex((localPosition.y - snapOffset.y) / CellSize) + gridCenter);
    }
}
