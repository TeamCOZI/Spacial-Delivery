using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private void ConfigureRuntimePorts(GameObject root, Part targetPart, AssemblyPartPortProfile profile, bool isGhost)
    {
        if (root == null || targetPart == null || profile == null) return;

        Transform runtimePortsRoot = root.transform.Find(RuntimePortsRootName);
        if (runtimePortsRoot != null)
        {
            Destroy(runtimePortsRoot.gameObject);
        }

        // Non-pipe parts can define their own input port profile on prefab.
        // Keep prefab-authored profile so auto-orientation can use real input direction.
        if (targetPart.partType != PartType.Pipe) return;

        float widthWorld = Mathf.Max(cellSize, targetPart.gridWidth * cellSize);
        float halfWidthWorld = widthWorld * 0.5f;
        float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(root.transform.localScale.x));
        float halfWidthLocal = halfWidthWorld / rootScaleX;

        Vector3 inputLocal = new Vector3(-halfWidthLocal, 0f, 0f);
        Vector3 outputLocal = new Vector3(halfWidthLocal, 0f, 0f);
        ConfigureRuntimePipePorts(root, profile, isGhost, inputLocal, outputLocal);
    }

    private void ConfigureRuntimePipePorts(
        GameObject root,
        AssemblyPartPortProfile profile,
        bool isGhost,
        Vector3 inputLocal,
        Vector3 outputLocal)
    {
        if (root == null || profile == null) return;

        Transform runtimePortsRoot = root.transform.Find(RuntimePortsRootName);
        if (runtimePortsRoot != null)
        {
            Destroy(runtimePortsRoot.gameObject);
        }

        profile.SetInputPortLocalPositions(new[] { inputLocal });

        GameObject portsRootObject = new GameObject(RuntimePortsRootName);
        portsRootObject.transform.SetParent(root.transform, false);

        AssemblyPort inputPort = CreateRuntimePortMarker(
            portsRootObject.transform,
            "InputPort",
            AssemblyPortType.Input,
            inputLocal,
            isGhost);
        AssemblyPort outputPort = CreateRuntimePortMarker(
            portsRootObject.transform,
            "OutputPort",
            AssemblyPortType.Output,
            outputLocal,
            isGhost);
        ConfigureRuntimePartPortLayout(root, inputPort, outputPort, inputLocal, outputLocal);
    }

    private AssemblyPort CreateRuntimePortMarker(
        Transform parent,
        string name,
        AssemblyPortType portType,
        Vector3 localPosition,
        bool isGhost)
    {
        GameObject marker = CreatePortVisualClone();
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        MatchWorldScale(marker.transform, GetPortVisualWorldScale());

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        _ = ComponentUtility.GetOrAddComponent<AssemblyPortVisualMarker>(marker);
        AssemblyPort assemblyPort = ComponentUtility.GetOrAddComponent<AssemblyPort>(marker);
        assemblyPort.PortType = portType;
        assemblyPort.SetOccupied(false);
        assemblyPort.LocalDirection = localPosition.sqrMagnitude > DirectionEpsilonSqr
            ? localPosition.normalized
            : DefaultPortDirection;
        SetDockingDirection(marker.transform, localPosition);

        if (portType == AssemblyPortType.Input)
        {
            Color inputColor = InputPortColor;
            if (isGhost) inputColor.a = GhostValidColor.a;
            if (isGhost) EnsureRendererTransparencyRecursive(marker.transform);
            SetRendererColorRecursive(marker.transform, inputColor);
        }
        else if (isGhost)
        {
            EnsureRendererTransparencyRecursive(marker.transform);
            SetRendererAlphaRecursive(marker.transform, GhostValidColor.a);
        }

        return assemblyPort;
    }

    private void ConfigureRuntimePartPortLayout(
        GameObject root,
        AssemblyPort inputPort,
        AssemblyPort outputPort,
        Vector3 inputLocal,
        Vector3 outputLocal)
    {
        if (root == null || inputPort == null || outputPort == null) return;

        AssemblyPartPortLayout layout = root.GetComponent<AssemblyPartPortLayout>();
        if (layout == null) layout = root.AddComponent<AssemblyPartPortLayout>();

        List<AssemblyPartPortLayout.PortEntry> entries = new List<AssemblyPartPortLayout.PortEntry>(2)
        {
            new AssemblyPartPortLayout.PortEntry
            {
                portTransform = inputPort.transform,
                relativeSourceCell = Vector2Int.zero,
                side = ConvertMaskToPartLayoutSide(DirectionToSideMask(inputLocal)),
                portType = AssemblyPortType.Input
            },
            new AssemblyPartPortLayout.PortEntry
            {
                portTransform = outputPort.transform,
                relativeSourceCell = Vector2Int.zero,
                side = ConvertMaskToPartLayoutSide(DirectionToSideMask(outputLocal)),
                portType = AssemblyPortType.Output
            }
        };
        layout.SetPorts(entries);
    }

    private GameObject CreatePortVisualClone()
    {
        GameObject template = GetOutputPortTemplate();
        GameObject marker;

        if (template != null)
        {
            marker = Instantiate(template);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        Collider[] colliders = marker.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) Destroy(colliders[i]);
        }

        // Template can come from combined-only renderers, so force visibility on cloned port visuals.
        Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].enabled = true;
        }

        // Runtime port visuals should not inherit pulse behavior from source output ports.
        AssemblyPortPulse[] pulses = marker.GetComponentsInChildren<AssemblyPortPulse>(true);
        for (int i = 0; i < pulses.Length; i++)
        {
            if (pulses[i] != null) Destroy(pulses[i]);
        }

        return marker;
    }

    private Vector3 GetBestOutputPortWorldAnchorForInput(AssemblyPort outputPort, Vector3 inputWorldPoint)
    {
        if (outputPort == null) return Vector3.zero;

        Docking[] dockings = outputPort.GetComponentsInChildren<Docking>(true);
        if (dockings != null && dockings.Length > 0)
        {
            float bestDistanceSq = float.MaxValue;
            Vector3 bestAnchor = outputPort.transform.position;

            for (int i = 0; i < dockings.Length; i++)
            {
                Docking docking = dockings[i];
                if (docking == null) continue;

                float distanceSq = (docking.transform.position - inputWorldPoint).sqrMagnitude;
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                bestAnchor = docking.transform.position;
            }

            return bestAnchor;
        }

        return outputPort.transform.position;
    }

    private void SetDockingDirection(Transform portRoot, Vector3 localDirectionHint)
    {
        if (portRoot == null) return;

        Docking[] dockings = portRoot.GetComponentsInChildren<Docking>(true);
        if (dockings == null || dockings.Length == 0) return;

        int bestIndex = -1;
        float bestDot = float.NegativeInfinity;

        Vector3 hint = localDirectionHint;
        if (hint.sqrMagnitude < DirectionEpsilonSqr) hint = DefaultPortDirection;
        hint.Normalize();

        for (int i = 0; i < dockings.Length; i++)
        {
            Docking docking = dockings[i];
            if (docking == null) continue;

            Transform dTransform = docking.transform;
            Vector3 candidateLocalDir = (dTransform.localPosition.sqrMagnitude < DirectionEpsilonSqr)
                ? DefaultPortDirection
                : dTransform.localPosition.normalized;
            float dot = Vector3.Dot(candidateLocalDir, hint);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        for (int i = 0; i < dockings.Length; i++)
        {
            if (dockings[i] != null) dockings[i].isDocking = (i == bestIndex);
        }
    }

    private Vector3 GetPortVisualWorldScale()
    {
        GameObject template = GetOutputPortTemplate();
        if (template != null) return template.transform.lossyScale;
        return Vector3.one * 0.03f;
    }

    private GameObject GetOutputPortTemplate()
    {
        if (outputPortTemplate != null) return outputPortTemplate;

        outputPortTemplate = FindOutputPortTemplate();
        if (outputPortTemplate != null) return outputPortTemplate;

        for (int i = 0; i < outputPorts.Count; i++)
        {
            if (outputPorts[i] != null) return outputPorts[i].gameObject;
        }

        return null;
    }

    private GameObject FindOutputPortTemplate()
    {
        if (artificialSatellite == null) return null;

        Transform[] allTransforms = artificialSatellite.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null) continue;
            if (current.GetComponentInParent<AssemblyGhostMarker>() != null) continue;
            if (IsOutputPortTransform(current)) return current.gameObject;
        }

        return null;
    }

    private static void MatchWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        if (target == null) return;

        Transform parent = target.parent;
        Vector3 parentLossyScale = parent != null ? parent.lossyScale : Vector3.one;

        float scaleX = Mathf.Abs(parentLossyScale.x) < 0.0001f ? desiredWorldScale.x : desiredWorldScale.x / parentLossyScale.x;
        float scaleY = Mathf.Abs(parentLossyScale.y) < 0.0001f ? desiredWorldScale.y : desiredWorldScale.y / parentLossyScale.y;
        float scaleZ = Mathf.Abs(parentLossyScale.z) < 0.0001f ? desiredWorldScale.z : desiredWorldScale.z / parentLossyScale.z;
        target.localScale = new Vector3(scaleX, scaleY, scaleZ);
    }

    private static bool IsOutputPortTransform(Transform target)
    {
        return target != null && target.name.Contains(OutputPortNameToken);
    }
}
