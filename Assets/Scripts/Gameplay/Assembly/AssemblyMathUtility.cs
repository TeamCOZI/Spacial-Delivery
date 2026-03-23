using System.Collections.Generic;
using UnityEngine;

public static class AssemblyMathUtility
{
    public static int GetQuarterTurns(Quaternion localRotation)
    {
        float z = Mathf.Repeat(localRotation.eulerAngles.z, 360f);
        int turns = Mathf.RoundToInt(z / 90f) % 4;
        if (turns < 0) turns += 4;
        return turns;
    }

    public static Vector2Int RotateCellOffset(Vector2Int value, int quarterTurns)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;
        Vector2Int result = value;
        for (int i = 0; i < turns; i++)
        {
            result = new Vector2Int(-result.y, result.x);
        }

        return result;
    }

    public static Vector2Int GetPathDirection(List<Vector2Int> path, int fromIndex, int toIndex)
    {
        if (path == null) return Vector2Int.zero;
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= path.Count || toIndex >= path.Count) return Vector2Int.zero;
        return path[toIndex] - path[fromIndex];
    }

    public static bool IsCornerSegment(Vector2Int previousDir, Vector2Int nextDir)
    {
        if (previousDir == Vector2Int.zero || nextDir == Vector2Int.zero) return false;
        return previousDir != nextDir;
    }

    public static Quaternion GetPipeSegmentRotation(Vector2Int previousDir, Vector2Int nextDir)
    {
        Vector2Int reference = nextDir != Vector2Int.zero ? nextDir : previousDir;
        if (reference == Vector2Int.right) return Quaternion.identity;
        if (reference == Vector2Int.up) return Quaternion.Euler(0f, 0f, 90f);
        if (reference == Vector2Int.left) return Quaternion.Euler(0f, 0f, 180f);
        if (reference == Vector2Int.down) return Quaternion.Euler(0f, 0f, 270f);
        return Quaternion.identity;
    }

    public static Quaternion GetPipeCornerVisualRotation(Vector2Int inputDir, Vector2Int outputDir)
    {
        if (!AreAdjacentCardinalDirections(inputDir, outputDir)) return Quaternion.identity;

        if (inputDir == Vector2Int.right) return Quaternion.identity;
        if (inputDir == Vector2Int.up) return Quaternion.Euler(0f, 0f, 90f);
        if (inputDir == Vector2Int.left) return Quaternion.Euler(0f, 0f, 180f);
        if (inputDir == Vector2Int.down) return Quaternion.Euler(0f, 0f, 270f);
        return Quaternion.identity;
    }

    public static bool ShouldMirrorPipeCornerVisual(Vector2Int inputDir, Vector2Int outputDir)
    {
        if (inputDir == Vector2Int.zero || outputDir == Vector2Int.zero) return false;
        int cross = inputDir.x * outputDir.y - inputDir.y * outputDir.x;
        return cross < 0;
    }

    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static bool AreAdjacentCardinalDirections(Vector2Int inputDir, Vector2Int outputDir)
    {
        if (inputDir == Vector2Int.zero || outputDir == Vector2Int.zero) return false;
        if (inputDir == outputDir || inputDir == -outputDir) return false;
        return Mathf.Abs(inputDir.x) + Mathf.Abs(inputDir.y) == 1
            && Mathf.Abs(outputDir.x) + Mathf.Abs(outputDir.y) == 1;
    }

    public static int QuantizeToCellIndex(float valueInCells)
    {
        if (valueInCells >= 0f) return Mathf.FloorToInt(valueInCells + 0.5f);
        return Mathf.CeilToInt(valueInCells - 0.5f);
    }

    public static void ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        List<Vector2Int> path)
    {
        path.Clear();
        path.Add(current);

        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
    }
}


