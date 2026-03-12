using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private void BuildPipePathGhosts(List<Vector2Int> path, bool isValid)
    {
        ClearPipePathGhosts();
        if (part == null || part.ghostPrefab == null || artificialSatellite == null) return;
        if (path == null || path.Count == 0) return;

        pipePathGhostRoot = new GameObject("PipePathGhostRoot");
        pipePathGhostRoot.transform.SetParent(artificialSatellite.transform, false);
        pipePathGhostRoot.AddComponent<AssemblyGhostMarker>();

        Color ghostColor = isValid ? GhostValidColor : GhostInvalidColor;
        Vector2 snapOffset = GetCurrentPartSnapOffset();

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int cell = path[i];
            Vector2Int previousDir = GetPathSegmentPreviousDir(path, i);
            Vector2Int nextDir = GetPathDirection(path, i, i + 1);
            bool isCorner = IsCornerSegment(previousDir, nextDir);
            Quaternion rot = isCorner
                ? Quaternion.identity
                : GetPipeSegmentRotation(previousDir, nextDir);
            Vector3 localPos = GridToLocalPosition(cell, snapOffset);

            GameObject ghostSegment = isCorner
                ? CreatePipeCornerObject(part.ghostPrefab, true, previousDir, nextDir)
                : Instantiate(part.ghostPrefab);

            ghostSegment.name = isCorner ? "PipeCornerGhost" : "PipeGhost";
            ghostSegment.transform.SetParent(pipePathGhostRoot.transform, false);
            ghostSegment.transform.localPosition = localPos;
            ghostSegment.transform.localRotation = rot;

            _ = ComponentUtility.GetOrAddComponent<AssemblyGhostMarker>(ghostSegment);
            SmallScaleLayerUtility.ApplyRecursively(ghostSegment.transform);
            SetRendererColorRecursive(ghostSegment.transform, ghostColor);
            EnsureRendererTransparencyRecursive(ghostSegment.transform);
            ConfigurePipeGhostPortsLikePlacedSegment(
                ghostSegment,
                isCorner,
                previousDir,
                nextDir,
                showInputPort: i == 0,
                showOutputPort: i == path.Count - 1);
        }
    }

    private void ClearPipePathGhosts()
    {
        if (pipePathGhostRoot != null)
        {
            pipePathGhostRoot.SetActive(false);
            Destroy(pipePathGhostRoot);
            pipePathGhostRoot = null;
        }
    }

    private GameObject AddPipeCornerToSatellite(
        ArtificialSatellite targetSatellite,
        Part targetPart,
        Vector3 localPos,
        Quaternion localRot,
        Vector2Int previousDir,
        Vector2Int nextDir)
    {
        if (targetSatellite == null || targetPart == null) return null;

        GameObject cornerRoot = CreatePipeCornerObject(targetPart.partPrefab, false, previousDir, nextDir);
        Vector3 inputLocal = new Vector3(-previousDir.x, -previousDir.y, 0f) * (cellSize * 0.5f);
        Vector3 outputLocal = new Vector3(nextDir.x, nextDir.y, 0f) * (cellSize * 0.5f);
        return AddPartObjectToSatellite(
            targetSatellite,
            targetPart,
            cornerRoot,
            localPos,
            localRot,
            true,
            inputLocal,
            outputLocal);
    }

    private GameObject CreatePipeCornerObject(GameObject templatePrefab, bool isGhost, Vector2Int previousDir, Vector2Int nextDir)
    {
        if (templatePrefab == null) return new GameObject(isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime");

        GameObject root = new GameObject(isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime");
        GameObject armA = Instantiate(templatePrefab, root.transform);
        GameObject armB = Instantiate(templatePrefab, root.transform);

        // previousDir points from previous cell -> current cell,
        // so this arm must point back toward the previous cell.
        ConfigureCornerArm(armA.transform, -previousDir);
        ConfigureCornerArm(armB.transform, nextDir);

        if (isGhost)
        {
            EnsureRendererTransparencyRecursive(root.transform);
        }

        return root;
    }

    private void ConfigureCornerArm(Transform arm, Vector2Int dir)
    {
        if (arm == null) return;

        float length = cellSize * 0.5f;
        float thickness = cellSize * 0.5f;

        if (dir == Vector2Int.zero) dir = Vector2Int.right;
        Vector3 normalizedDir = new Vector3(dir.x, dir.y, 0f).normalized;

        arm.localRotation = (dir.x != 0) ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
        arm.localPosition = normalizedDir * (cellSize * 0.25f);
        arm.localScale = new Vector3(length, thickness, 1f);
    }

    private void ConfigurePipeGhostPortsLikePlacedSegment(
        GameObject ghostSegment,
        bool isCorner,
        Vector2Int previousDir,
        Vector2Int nextDir,
        bool showInputPort,
        bool showOutputPort)
    {
        if (ghostSegment == null || part == null) return;

        AssemblyPartPortProfile profile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(ghostSegment);
        if (isCorner)
        {
            Vector3 inputLocal = new Vector3(-previousDir.x, -previousDir.y, 0f) * (cellSize * 0.5f);
            Vector3 outputLocal = new Vector3(nextDir.x, nextDir.y, 0f) * (cellSize * 0.5f);
            ConfigureRuntimePipePorts(
                ghostSegment,
                profile,
                true,
                inputLocal,
                outputLocal,
                showInputPort,
                showOutputPort);
        }
        else
        {
            ConfigureRuntimePorts(
                ghostSegment,
                part,
                profile,
                true,
                showInputPort,
                showOutputPort);
        }

        SmallScaleLayerUtility.ApplyRecursively(ghostSegment.transform);
    }
}
