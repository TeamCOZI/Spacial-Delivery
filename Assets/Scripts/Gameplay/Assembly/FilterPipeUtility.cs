using System.Collections.Generic;
using UnityEngine;

public static class FilterPipeUtility
{
    private const string FilterPipeName = "Filter Pipe";
    private const float CellSize = 0.1f;
    private const int GridSize = 99;

    public static bool IsFilterPipePart(Part targetPart)
    {
        return targetPart != null
            && targetPart.partType == PartType.Pipe
            && !string.IsNullOrWhiteSpace(targetPart.partName)
            && string.Equals(targetPart.partName, FilterPipeName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static Part ResolvePart()
    {
        if (GameplayRuntimeAccess.TryGetPartDb(out PartDB partDb))
        {
            Part resolvedPart = partDb.GetPartByName(FilterPipeName);
            if (resolvedPart != null)
            {
                return resolvedPart;
            }
        }

        return Resources.Load<Part>("Parts/FilterPipe");
    }

    public static FilterPipeState ResolveState(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        AssemblyPartFocus focus = root.GetComponent<AssemblyPartFocus>();
        if (focus == null)
        {
            focus = root.GetComponentInParent<AssemblyPartFocus>();
        }

        if (focus == null || !IsFilterPipePart(focus.SourcePart))
        {
            return null;
        }

        FilterPipeState state = focus.GetComponent<FilterPipeState>();
        return state != null ? state : ComponentUtility.GetOrAddComponent<FilterPipeState>(focus.gameObject);
    }

    public static bool TryResolveStateForCell(ArtificialSatellite ownerSatellite, Vector2Int cell, out FilterPipeState state)
    {
        state = null;
        if (ownerSatellite == null)
        {
            return false;
        }

        AssemblyPartFocus[] partFocuses = ownerSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || !IsFilterPipePart(partFocus.SourcePart))
            {
                continue;
            }

            if (!TryResolveFilterPipeCell(partFocus, out Vector2Int resolvedCell) || resolvedCell != cell)
            {
                continue;
            }

            state = ResolveState(partFocus.gameObject);
            return state != null;
        }

        return false;
    }

    private static bool TryResolveFilterPipeCell(AssemblyPartFocus partFocus, out Vector2Int sourceCell)
    {
        sourceCell = Vector2Int.zero;
        if (partFocus == null)
        {
            return false;
        }

        AssemblyPartPortLayout layout = partFocus.GetComponent<AssemblyPartPortLayout>();
        if (layout == null)
        {
            layout = partFocus.GetComponentInChildren<AssemblyPartPortLayout>(true);
        }

        if (layout == null || layout.Ports == null || layout.Ports.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null)
            {
                continue;
            }

            if (TryResolvePipeEntrySourceCell(partFocus, layout, entry, out sourceCell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolvePipeEntrySourceCell(
        AssemblyPartFocus partFocus,
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int sourceCell)
    {
        sourceCell = Vector2Int.zero;
        if (partFocus == null || partFocus.SourcePart == null || layout == null || entry == null)
        {
            return false;
        }

        Vector2 snapOffset = GetSnapOffset(partFocus, layout);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = AssemblyMathUtility.RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        sourceCell = partCenterCell + rotatedRelativeCell;
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

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition, Vector2 snapOffset)
    {
        int center = GridSize / 2;
        int x = AssemblyMathUtility.QuantizeToCellIndex((localPosition.x - snapOffset.x) / CellSize) + center;
        int y = AssemblyMathUtility.QuantizeToCellIndex((localPosition.y - snapOffset.y) / CellSize) + center;
        return new Vector2Int(x, y);
    }
}
