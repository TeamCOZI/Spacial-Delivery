using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private sealed class CrossPipePlacementData
    {
        public GameObject existingRoot;
        public Vector2Int existingInputSideDirection;
        public Vector2Int existingOutputDirection;
        public Vector2Int newInputSideDirection;
        public Vector2Int newOutputDirection;
    }

    private readonly struct PipePathNode : System.IEquatable<PipePathNode>
    {
        public PipePathNode(Vector2Int cell, Vector2Int incomingSideDirection)
        {
            Cell = cell;
            IncomingSideDirection = incomingSideDirection;
        }

        public Vector2Int Cell { get; }
        public Vector2Int IncomingSideDirection { get; }

        public bool Equals(PipePathNode other)
        {
            return Cell == other.Cell && IncomingSideDirection == other.IncomingSideDirection;
        }

        public override bool Equals(object obj)
        {
            return obj is PipePathNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Cell, IncomingSideDirection).GetHashCode();
        }
    }

    private Part ResolveCrossPipePart()
    {
        return CrossPipeUtility.ResolvePart();
    }

    private bool TryCollectCrossPipePlacements(
        List<Vector2Int> path,
        Vector2Int terminalDirection,
        out Dictionary<Vector2Int, CrossPipePlacementData> placementsByCell,
        out List<GameObject> replacementRoots)
    {
        placementsByCell = new Dictionary<Vector2Int, CrossPipePlacementData>();
        replacementRoots = new List<GameObject>();
        if (path == null || path.Count <= 0)
        {
            return false;
        }

        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();
        for (int i = 0; i < path.Count; i++)
        {
            if (!TryResolvePipePathCrossPlacementAtIndex(path, i, terminalDirection, out CrossPipePlacementData placementData))
            {
                continue;
            }

            placementsByCell[path[i]] = placementData;
            if (placementData.existingRoot != null && uniqueRoots.Add(placementData.existingRoot))
            {
                replacementRoots.Add(placementData.existingRoot);
            }
        }

        return placementsByCell.Count > 0;
    }

    private bool TryResolvePipePathCrossPlacementAtIndex(
        List<Vector2Int> path,
        int index,
        Vector2Int terminalDirection,
        out CrossPipePlacementData placementData)
    {
        placementData = null;
        if (ResolveCrossPipePart() == null || path == null || index < 0 || index >= path.Count)
        {
            return false;
        }

        Vector2Int previousDir = GetPathSegmentPreviousDir(path, index);
        Vector2Int nextDir = GetPathSegmentNextDir(path, index, terminalDirection);
        if (!IsCardinalDirection(previousDir) || !IsCardinalDirection(nextDir) || previousDir != nextDir)
        {
            return false;
        }

        Vector2Int cell = path[index];
        if (!TryResolveInstalledStraightPipeDirections(cell, out GameObject existingRoot, out Vector2Int existingInputSideDirection, out Vector2Int existingOutputDirection))
        {
            return false;
        }

        Vector2Int newInputSideDirection = -previousDir;
        Vector2Int newOutputDirection = nextDir;
        if (!IsCardinalDirection(newInputSideDirection) || !IsCardinalDirection(newOutputDirection) || !ArePerpendicular(existingOutputDirection, newOutputDirection))
        {
            return false;
        }

        placementData = new CrossPipePlacementData
        {
            existingRoot = existingRoot,
            existingInputSideDirection = existingInputSideDirection,
            existingOutputDirection = existingOutputDirection,
            newInputSideDirection = newInputSideDirection,
            newOutputDirection = newOutputDirection
        };
        return true;
    }

    private bool TryResolveInstalledStraightPipeDirections(
        Vector2Int cell,
        out GameObject pipeRoot,
        out Vector2Int inputSideDirection,
        out Vector2Int outputDirection)
    {
        pipeRoot = null;
        inputSideDirection = Vector2Int.zero;
        outputDirection = Vector2Int.zero;
        if (!occupiedCells.TryGetValue(cell, out GameObject owner) || owner == null)
        {
            return false;
        }

        GameObject resolvedRoot = ResolveRemovablePartRoot(owner);
        if (resolvedRoot == null || resolvedRoot.GetComponent<CrossPipeState>() != null)
        {
            return false;
        }

        AssemblyPartFocus focus = resolvedRoot.GetComponent<AssemblyPartFocus>();
        if (focus == null || focus.SourcePart == null || !PipePartUtility.IsStandardPipePart(focus.SourcePart))
        {
            return false;
        }

        if (!TryBuildPortRequirementMapForPart(resolvedRoot, out Dictionary<(Vector2Int cell, CellSideMask side), AssemblyPortType> portMap) || portMap.Count != 2)
        {
            return false;
        }

        foreach (KeyValuePair<(Vector2Int cell, CellSideMask side), AssemblyPortType> pair in portMap)
        {
            if (pair.Key.cell != cell)
            {
                return false;
            }

            Vector2Int sideDirection = SideToCellOffset(pair.Key.side);
            if (!IsCardinalDirection(sideDirection))
            {
                return false;
            }

            if (AssemblyPortTypeUtility.IsInputCompatible(pair.Value) && !AssemblyPortTypeUtility.IsOutputCompatible(pair.Value))
            {
                inputSideDirection = sideDirection;
                continue;
            }

            if (AssemblyPortTypeUtility.IsOutputCompatible(pair.Value) && !AssemblyPortTypeUtility.IsInputCompatible(pair.Value))
            {
                outputDirection = sideDirection;
                continue;
            }

            return false;
        }

        if (!IsCardinalDirection(inputSideDirection) || !IsCardinalDirection(outputDirection) || inputSideDirection != -outputDirection)
        {
            return false;
        }

        pipeRoot = resolvedRoot;
        return true;
    }

    private GameObject CreateCrossPipeGhost(CrossPipePlacementData placementData)
    {
        Part crossPart = ResolveCrossPipePart();
        if (crossPart == null)
        {
            return null;
        }

        GameObject template = crossPart.ghostPrefab != null ? crossPart.ghostPrefab : crossPart.partPrefab;
        if (template == null)
        {
            return null;
        }

        GameObject ghostRoot = Instantiate(template);
        AssemblyPartPortProfile profile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(ghostRoot);
        BuildCrossPipePortTypes(placementData, out AssemblyPortType topPortType, out AssemblyPortType bottomPortType, out AssemblyPortType leftPortType, out AssemblyPortType rightPortType);
        ConfigureRuntimeCustomFixedPipePorts(ghostRoot, profile, true, topPortType, bottomPortType, leftPortType, rightPortType);
        return ghostRoot;
    }

    private GameObject AddCrossPipeToSatellite(ArtificialSatellite targetSatellite, Vector3 localPos, CrossPipePlacementData placementData)
    {
        Part crossPart = ResolveCrossPipePart();
        if (targetSatellite == null || crossPart == null)
        {
            return null;
        }

        GameObject template = crossPart.partPrefab != null ? crossPart.partPrefab : crossPart.ghostPrefab;
        if (template == null)
        {
            return null;
        }

        GameObject crossRoot = Instantiate(template);
        GameObject placedRoot = AddPartObjectToSatellite(targetSatellite, crossPart, crossRoot, localPos, Quaternion.identity, false, Vector3.zero, Vector3.zero);
        if (placedRoot == null)
        {
            return null;
        }

        AssemblyPartPortProfile profile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(placedRoot);
        BuildCrossPipePortTypes(placementData, out AssemblyPortType topPortType, out AssemblyPortType bottomPortType, out AssemblyPortType leftPortType, out AssemblyPortType rightPortType);
        ConfigureRuntimeCustomFixedPipePorts(placedRoot, profile, false, topPortType, bottomPortType, leftPortType, rightPortType);

        CrossPipeState state = CrossPipeUtility.ResolveState(placedRoot);
        if (state != null)
        {
            state.Configure(
                placementData.existingInputSideDirection,
                placementData.existingOutputDirection,
                placementData.newInputSideDirection,
                placementData.newOutputDirection);
        }

        return placedRoot;
    }

    private void BuildCrossPipePortTypes(
        CrossPipePlacementData placementData,
        out AssemblyPortType topPortType,
        out AssemblyPortType bottomPortType,
        out AssemblyPortType leftPortType,
        out AssemblyPortType rightPortType)
    {
        topPortType = AssemblyPortType.Neutral;
        bottomPortType = AssemblyPortType.Neutral;
        leftPortType = AssemblyPortType.Neutral;
        rightPortType = AssemblyPortType.Neutral;

        AssignCrossPipePortType(placementData != null ? placementData.existingInputSideDirection : Vector2Int.zero, AssemblyPortType.Input, ref topPortType, ref bottomPortType, ref leftPortType, ref rightPortType);
        AssignCrossPipePortType(placementData != null ? placementData.existingOutputDirection : Vector2Int.zero, AssemblyPortType.Output, ref topPortType, ref bottomPortType, ref leftPortType, ref rightPortType);
        AssignCrossPipePortType(placementData != null ? placementData.newInputSideDirection : Vector2Int.zero, AssemblyPortType.Input, ref topPortType, ref bottomPortType, ref leftPortType, ref rightPortType);
        AssignCrossPipePortType(placementData != null ? placementData.newOutputDirection : Vector2Int.zero, AssemblyPortType.Output, ref topPortType, ref bottomPortType, ref leftPortType, ref rightPortType);
    }

    private static void AssignCrossPipePortType(
        Vector2Int direction,
        AssemblyPortType portType,
        ref AssemblyPortType topPortType,
        ref AssemblyPortType bottomPortType,
        ref AssemblyPortType leftPortType,
        ref AssemblyPortType rightPortType)
    {
        if (direction == Vector2Int.up)
        {
            topPortType = portType;
        }
        else if (direction == Vector2Int.down)
        {
            bottomPortType = portType;
        }
        else if (direction == Vector2Int.left)
        {
            leftPortType = portType;
        }
        else if (direction == Vector2Int.right)
        {
            rightPortType = portType;
        }
    }

    private bool TryResolvePipePathStartReplacementRoot(out GameObject replacementRoot)
    {
        replacementRoot = null;
        if (pipePreviewPath == null || pipePreviewPath.Count <= 0)
        {
            return false;
        }

        Vector2Int startCell = pipePreviewPath[0];
        if (!occupiedCells.TryGetValue(startCell, out GameObject owner) || owner == null)
        {
            return false;
        }

        GameObject removableRoot = ResolveRemovablePartRoot(owner);
        AssemblyPartFocus removableFocus = removableRoot != null ? removableRoot.GetComponent<AssemblyPartFocus>() : null;
        if (removableFocus == null || !PipePartUtility.IsStandardPipePart(removableFocus.SourcePart))
        {
            return false;
        }

        replacementRoot = removableRoot;
        return true;
    }

    private bool TryGetForcedPipePathOutputDirection(Vector2Int cell, Vector2Int incomingSideDirection, out Vector2Int outputDirection)
    {
        outputDirection = Vector2Int.zero;
        if (ResolveCrossPipePart() == null || !IsCardinalDirection(incomingSideDirection))
        {
            return false;
        }

        if (!TryResolveInstalledStraightPipeDirections(cell, out _, out _, out Vector2Int existingOutputDirection) || !ArePerpendicular(existingOutputDirection, incomingSideDirection))
        {
            return false;
        }

        outputDirection = -incomingSideDirection;
        return IsCardinalDirection(outputDirection);
    }

    private bool CanEnterPipePathCell(Vector2Int cell, Vector2Int incomingSideDirection)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject owner) || owner == null)
        {
            return true;
        }

        return TryGetForcedPipePathOutputDirection(cell, incomingSideDirection, out _);
    }

    private void ReconstructPipePath(Dictionary<PipePathNode, PipePathNode> cameFrom, PipePathNode current, List<Vector2Int> path)
    {
        if (path == null)
        {
            return;
        }

        path.Clear();
        path.Add(current.Cell);
        while (cameFrom.TryGetValue(current, out PipePathNode previous))
        {
            current = previous;
            path.Add(current.Cell);
        }

        path.Reverse();
    }

    private static bool ArePerpendicular(Vector2Int a, Vector2Int b)
    {
        return a != Vector2Int.zero && b != Vector2Int.zero && (a.x * b.x + a.y * b.y) == 0;
    }
}
