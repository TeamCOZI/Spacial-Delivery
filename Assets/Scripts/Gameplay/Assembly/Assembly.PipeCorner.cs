using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private void BuildPipePathGhosts(List<Vector2Int> path, bool isValid, Vector2Int terminalDirection)
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
            Vector2Int nextDir = GetPathSegmentNextDir(path, i, terminalDirection);
            bool isCorner = IsCornerSegment(previousDir, nextDir);
            Quaternion rot = isCorner
                ? GetPipeCornerRotation(previousDir, nextDir)
                : GetPipeSegmentRotation(previousDir, nextDir);
            Vector3 localPos = GridToLocalPosition(cell, snapOffset);

            GameObject ghostSegment = isCorner
                ? CreatePipeCornerObject(GetPipeCornerTemplate(part, true), part.ghostPrefab, true, previousDir, nextDir)
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
                rot,
                showInputPort: i == 0,
                showOutputPort: i == path.Count - 1);
        }

        RefreshPipePathGhostEnds(ghostColor);
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

        GameObject cornerRoot = CreatePipeCornerObject(
            GetPipeCornerTemplate(targetPart, false),
            targetPart.partPrefab,
            false,
            previousDir,
            nextDir);
        Vector3 inputLocal = GetPipeCornerPortLocal(localRot, -previousDir);
        Vector3 outputLocal = GetPipeCornerPortLocal(localRot, nextDir);
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

    private static GameObject GetPipeCornerTemplate(Part targetPart, bool isGhost)
    {
        if (targetPart == null) return null;
        return isGhost ? targetPart.cornerGhostPrefab : targetPart.cornerPrefab;
    }

    private GameObject CreatePipeCornerObject(
        GameObject cornerTemplatePrefab,
        GameObject straightTemplatePrefab,
        bool isGhost,
        Vector2Int previousDir,
        Vector2Int nextDir)
    {
        if (cornerTemplatePrefab != null)
        {
            GameObject instance = Instantiate(cornerTemplatePrefab);
            instance.name = isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime";
            ApplyPipeCornerPrefabChirality(instance.transform, cornerTemplatePrefab != null ? cornerTemplatePrefab.transform : null, previousDir, nextDir);
            return instance;
        }

        GameObject root = new GameObject(isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime");
        if (straightTemplatePrefab == null) return root;

        GameObject armA = Instantiate(straightTemplatePrefab, root.transform);
        GameObject armB = Instantiate(straightTemplatePrefab, root.transform);

        ConfigureCornerArm(armA.transform, -previousDir);
        ConfigureCornerArm(armB.transform, nextDir);

        if (isGhost)
        {
            EnsureRendererTransparencyRecursive(root.transform);
        }

        return root;
    }

    private void ApplyPipeCornerPrefabChirality(Transform cornerRoot, Transform templateRoot, Vector2Int previousDir, Vector2Int nextDir)
    {
        if (cornerRoot == null) return;

        Vector2Int inputDir = -previousDir;
        Vector2Int outputDir = nextDir;
        bool shouldMirror = AssemblyMathUtility.ShouldMirrorPipeCornerVisual(inputDir, outputDir);
        ApplyPipeCornerVisualVariant(cornerRoot, templateRoot, shouldMirror);
    }

    private static Transform FindPipeCornerPrimaryVisual(Transform cornerRoot)
    {
        if (cornerRoot == null) return null;

        for (int i = 0; i < cornerRoot.childCount; i++)
        {
            Transform child = cornerRoot.GetChild(i);
            if (child == null) continue;
            if (string.Equals(child.name, RuntimePortsRootName, System.StringComparison.Ordinal)) continue;
            if (string.Equals(child.name, PipeReplacementVisualName, System.StringComparison.Ordinal)) continue;
            return child;
        }

        return null;
    }


    private static void ApplyPipeCornerVisualVariant(Transform cornerRoot, Transform templateRoot, bool clockwise)
    {
        if (cornerRoot == null) return;

        Transform targetVisual = FindPipeCornerPrimaryVisual(cornerRoot);
        if (targetVisual == null) return;

        Transform templateVisual = FindPipeCornerPrimaryVisual(templateRoot != null ? templateRoot : cornerRoot);
        if (templateVisual == null) return;

        targetVisual.localPosition = templateVisual.localPosition;
        targetVisual.localRotation = templateVisual.localRotation;

        Vector3 baseScale = templateVisual.localScale;
        float magnitudeY = Mathf.Abs(baseScale.y);
        if (magnitudeY < 0.0001f) magnitudeY = 1f;
        baseScale.y = clockwise ? -magnitudeY : magnitudeY;
        targetVisual.localScale = baseScale;

        if (clockwise)
        {
            targetVisual.localRotation = templateVisual.localRotation * Quaternion.Euler(180f, 0f, 0f);
        }
    }

    private static bool IsClockwisePipeCornerVisual(Transform targetVisual)
    {
        return targetVisual != null && targetVisual.localScale.y < 0f;
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

    private static Quaternion GetPipeCornerRotation(Vector2Int previousDir, Vector2Int nextDir)
    {
        Vector2Int inputDir = -previousDir;
        Vector2Int outputDir = nextDir;
        return AssemblyMathUtility.GetPipeCornerVisualRotation(inputDir, outputDir);
    }

    private Vector3 GetPipeCornerPortLocal(Quaternion cornerRotation, Vector2Int desiredSatelliteDirection)
    {
        Vector3 desiredLocal = new Vector3(desiredSatelliteDirection.x, desiredSatelliteDirection.y, 0f) * (cellSize * 0.5f);
        return Quaternion.Inverse(cornerRotation) * desiredLocal;
    }

    private void ConfigurePipeGhostPortsLikePlacedSegment(
        GameObject ghostSegment,
        bool isCorner,
        Vector2Int previousDir,
        Vector2Int nextDir,
        Quaternion segmentRotation,
        bool showInputPort,
        bool showOutputPort)
    {
        if (ghostSegment == null || part == null) return;

        AssemblyPartPortProfile profile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(ghostSegment);
        if (isCorner)
        {
            Vector3 inputLocal = GetPipeCornerPortLocal(segmentRotation, -previousDir);
            Vector3 outputLocal = GetPipeCornerPortLocal(segmentRotation, nextDir);
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

