#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AssemblyMathUtilityTests
{
    [Test]
    public void GetQuarterTurns_NormalizesAnglesToExpectedRange()
    {
        Assert.AreEqual(0, AssemblyMathUtility.GetQuarterTurns(Quaternion.Euler(0f, 0f, 0f)));
        Assert.AreEqual(1, AssemblyMathUtility.GetQuarterTurns(Quaternion.Euler(0f, 0f, 90f)));
        Assert.AreEqual(2, AssemblyMathUtility.GetQuarterTurns(Quaternion.Euler(0f, 0f, 180f)));
        Assert.AreEqual(3, AssemblyMathUtility.GetQuarterTurns(Quaternion.Euler(0f, 0f, 270f)));
        Assert.AreEqual(1, AssemblyMathUtility.GetQuarterTurns(Quaternion.Euler(0f, 0f, -270f)));
    }

    [Test]
    public void RotateCellOffset_RotatesCounterClockwiseByQuarterTurns()
    {
        Vector2Int input = new Vector2Int(2, 1);

        Assert.AreEqual(new Vector2Int(2, 1), AssemblyMathUtility.RotateCellOffset(input, 0));
        Assert.AreEqual(new Vector2Int(-1, 2), AssemblyMathUtility.RotateCellOffset(input, 1));
        Assert.AreEqual(new Vector2Int(-2, -1), AssemblyMathUtility.RotateCellOffset(input, 2));
        Assert.AreEqual(new Vector2Int(1, -2), AssemblyMathUtility.RotateCellOffset(input, 3));
    }

    [Test]
    public void QuantizeToCellIndex_RoundsSymmetricallyAroundZero()
    {
        Assert.AreEqual(2, AssemblyMathUtility.QuantizeToCellIndex(1.6f));
        Assert.AreEqual(1, AssemblyMathUtility.QuantizeToCellIndex(1.4f));
        Assert.AreEqual(-2, AssemblyMathUtility.QuantizeToCellIndex(-1.6f));
        Assert.AreEqual(-1, AssemblyMathUtility.QuantizeToCellIndex(-1.4f));
    }

    [Test]
    public void ReconstructPath_BuildsOrderedPathFromCameFromMap()
    {
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>
        {
            [new Vector2Int(1, 0)] = new Vector2Int(0, 0),
            [new Vector2Int(2, 0)] = new Vector2Int(1, 0),
            [new Vector2Int(2, 1)] = new Vector2Int(2, 0),
        };
        var path = new List<Vector2Int>();

        AssemblyMathUtility.ReconstructPath(cameFrom, new Vector2Int(2, 1), path);

        CollectionAssert.AreEqual(
            new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(2, 1)
            },
            path);
    }
}
#endif
