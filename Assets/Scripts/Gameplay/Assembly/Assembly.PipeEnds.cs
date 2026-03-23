using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private const string PipeReplacementVisualName = "PipeReplacementVisual";

    private readonly struct PipeBoundaryRecord
    {
        public readonly Transform ownerRoot;
        public readonly AssemblyPortType portType;

        public PipeBoundaryRecord(Transform ownerRoot, AssemblyPortType portType)
        {
            this.ownerRoot = ownerRoot;
            this.portType = portType;
        }
    }

    private readonly struct PipeTerminalOwnerRecord
    {
        public readonly Transform ownerRoot;
        public readonly GameObject terminalTemplate;
        public readonly bool isCorner;
        public readonly Vector3[] ownerLocalPortPositions;
        public readonly (Vector2Int cell, CellSideMask side)[] boundaryKeys;
        public readonly AssemblyPortType[] portTypes;

        public PipeTerminalOwnerRecord(
            Transform ownerRoot,
            GameObject terminalTemplate,
            bool isCorner,
            Vector3[] ownerLocalPortPositions,
            (Vector2Int cell, CellSideMask side)[] boundaryKeys,
            AssemblyPortType[] portTypes)
        {
            this.ownerRoot = ownerRoot;
            this.terminalTemplate = terminalTemplate;
            this.isCorner = isCorner;
            this.ownerLocalPortPositions = ownerLocalPortPositions;
            this.boundaryKeys = boundaryKeys;
            this.portTypes = portTypes;
        }
    }

    private void RefreshPipePathGhostEnds(Color ghostColor)
    {
        if (pipePathGhostRoot == null || part == null || artificialSatellite == null) return;

        RefreshPipeTerminalVisualsInContainer(
            pipePathGhostRoot.transform,
            artificialSatellite,
            part,
            true,
            ghostColor);
    }

    private void RefreshCurrentPipePlacementGhostEnds(Color ghostColor)
    {
        if (partGhost == null || part == null || artificialSatellite == null) return;
        if (part.partType != PartType.Pipe) return;
        if (pipePathStartSelected) return;

        RefreshPipeTerminalVisualsInContainer(
            partGhost.transform,
            artificialSatellite,
            part,
            true,
            ghostColor);
    }

    private void RefreshPipeEndVisualsForSatellite(ArtificialSatellite targetSatellite)
    {
        if (targetSatellite == null) return;

        RefreshPipeTerminalVisualsInContainer(
            targetSatellite.transform,
            targetSatellite,
            null,
            false,
            Color.clear);
    }

    private void RefreshPipeTerminalVisualsInContainer(
        Transform ownerContainerRoot,
        ArtificialSatellite boundarySatellite,
        Part ghostPart,
        bool isGhost,
        Color ghostColor)
    {
        if (ownerContainerRoot == null || boundarySatellite == null) return;

        List<PipeTerminalOwnerRecord> owners = CollectPipeTerminalOwners(ownerContainerRoot, ghostPart, isGhost);
        if (owners.Count == 0)
        {
            ClearPipeReplacementVisuals(ownerContainerRoot);
            return;
        }

        BuildPipeOwnerPortMaps(
            boundarySatellite,
            isGhost,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
            out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys);

        for (int i = 0; i < owners.Count; i++)
        {
            ApplyPipeTerminalVisualState(owners[i], inputOwnersByKey, outputOwnersByKey, coreOutputKeys, isGhost, ghostColor);
        }
    }

    private List<PipeTerminalOwnerRecord> CollectPipeTerminalOwners(Transform ownerContainerRoot, Part ghostPart, bool isGhost)
    {
        List<PipeTerminalOwnerRecord> owners = new List<PipeTerminalOwnerRecord>();
        AssemblyPartPortLayout[] layouts = ownerContainerRoot.GetComponentsInChildren<AssemblyPartPortLayout>(true);

        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null) continue;

            Transform ownerRoot = layout.transform;
            if (!TryResolvePipeTerminalOwner(ownerRoot, ghostPart, isGhost, out GameObject terminalTemplate, out bool isCorner))
            {
                continue;
            }

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null || entries.Count == 0) continue;

            List<Vector3> ownerLocalPortPositions = new List<Vector3>(2);
            List<(Vector2Int cell, CellSideMask side)> boundaryKeys = new List<(Vector2Int cell, CellSideMask side)>(2);
            List<AssemblyPortType> portTypes = new List<AssemblyPortType>(2);

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[j];
                if (entry == null || entry.portTransform == null) continue;
                if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;
                if (!TryGetLayoutEntryBoundaryKey(layout, entry, out Vector2Int mappedCell, out CellSideMask mappedSide)) continue;

                ownerLocalPortPositions.Add(ownerRoot.InverseTransformPoint(entry.portTransform.position));
                boundaryKeys.Add((mappedCell, mappedSide));
                portTypes.Add(entry.portType);
            }

            if (ownerLocalPortPositions.Count == 0) continue;

            owners.Add(new PipeTerminalOwnerRecord(
                ownerRoot,
                terminalTemplate,
                isCorner,
                ownerLocalPortPositions.ToArray(),
                boundaryKeys.ToArray(),
                portTypes.ToArray()));
        }

        return owners;
    }

    private Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>> CollectPipeBoundaryRecords(
        Transform root,
        bool includeGhost)
    {
        Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>> recordsByKey =
            new Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>>();

        CollectPipeBoundaryRecords(root, includeGhost, recordsByKey);
        return recordsByKey;
    }

    private void CollectPipeBoundaryRecords(
        Transform root,
        bool includeGhost,
        Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>> recordsByKey)
    {
        if (root == null || recordsByKey == null) return;

        AddCoreBoundaryRecords(root, recordsByKey);

        AssemblyPartPortLayout[] layouts = root.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null) continue;
            if (!includeGhost && layout.GetComponentInParent<AssemblyGhostMarker>(true) != null) continue;

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null || entries.Count == 0) continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[j];
                if (entry == null) continue;
                if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;
                if (!TryGetLayoutEntryBoundaryKey(layout, entry, out Vector2Int mappedCell, out CellSideMask mappedSide)) continue;

                AddBoundaryRecord(recordsByKey, mappedCell, mappedSide, layout.transform, entry.portType);
            }
        }
    }

    private void AddCoreBoundaryRecords(
        Transform root,
        Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>> recordsByKey)
    {
        if (root == null || recordsByKey == null) return;

        AssemblyCorePortLayout[] coreLayouts = root.GetComponentsInChildren<AssemblyCorePortLayout>(true);
        for (int i = 0; i < coreLayouts.Length; i++)
        {
            AssemblyCorePortLayout coreLayout = coreLayouts[i];
            if (coreLayout == null) continue;

            List<AssemblyCorePortLayout.OutputPortEntry> entries = coreLayout.OutputPorts;
            if (entries == null || entries.Count == 0) continue;

            Vector2Int coreCenterCell = LocalPositionToGrid(coreLayout.transform.localPosition, ZeroSnapOffset);
            if (!IsInsideGrid(coreCenterCell)) continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyCorePortLayout.OutputPortEntry entry = entries[j];
                if (entry == null || entry.portTransform == null) continue;

                CellSideMask outputSide = ConvertCoreLayoutSide(entry.outputSide);
                if (outputSide == CellSideMask.None) continue;

                Vector2Int outputDir = SideToCellOffset(outputSide);
                if (outputDir == Vector2Int.zero) continue;

                Vector2Int sourceCell = coreCenterCell + entry.relativeSourceCell;
                Vector2Int mappedCell = sourceCell + outputDir;
                CellSideMask mappedSide = OppositeSide(outputSide);
                if (!IsInsideGrid(mappedCell) || mappedSide == CellSideMask.None) continue;

                AddBoundaryRecord(recordsByKey, mappedCell, mappedSide, coreLayout.transform, AssemblyPortType.Output);
            }
        }
    }

    private static void AddBoundaryRecord(
        Dictionary<(Vector2Int cell, CellSideMask side), List<PipeBoundaryRecord>> recordsByKey,
        Vector2Int mappedCell,
        CellSideMask mappedSide,
        Transform ownerRoot,
        AssemblyPortType portType)
    {
        if (recordsByKey == null || ownerRoot == null || mappedSide == CellSideMask.None) return;

        (Vector2Int cell, CellSideMask side) key = (mappedCell, mappedSide);
        if (!recordsByKey.TryGetValue(key, out List<PipeBoundaryRecord> records))
        {
            records = new List<PipeBoundaryRecord>(2);
            recordsByKey[key] = records;
        }

        records.Add(new PipeBoundaryRecord(ownerRoot, portType));
    }

    private bool TryGetLayoutEntryBoundaryKey(
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int mappedCell,
        out CellSideMask mappedSide)
    {
        mappedCell = Vector2Int.zero;
        mappedSide = CellSideMask.None;
        if (layout == null || entry == null) return false;
        if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) return false;

        Vector2Int outputDir = SideToCellOffset(portSide);
        if (outputDir == Vector2Int.zero) return false;

        mappedCell = sourceCell + outputDir;
        mappedSide = OppositeSide(portSide);
        return IsInsideGrid(mappedCell) && mappedSide != CellSideMask.None;
    }

    private static bool TryResolvePipeTerminalOwner(
        Transform ownerRoot,
        Part ghostPart,
        bool isGhost,
        out GameObject terminalTemplate,
        out bool isCorner)
    {
        terminalTemplate = null;
        isCorner = false;

        if (ownerRoot == null) return false;
        if (string.Equals(ownerRoot.name, PipeReplacementVisualName, System.StringComparison.Ordinal)) return false;

        bool hasGhostMarker = ownerRoot.GetComponentInParent<AssemblyGhostMarker>(true) != null;
        if (isGhost != hasGhostMarker) return false;

        isCorner = ContainsNameTokenInHierarchy(ownerRoot, "PipeCorner");

        if (isGhost)
        {
            if (ghostPart == null || ghostPart.partType != PartType.Pipe) return false;
            if (!ContainsNameTokenInHierarchy(ownerRoot, "Pipe")) return false;
            terminalTemplate = ghostPart.endGhostPrefab;
            return true;
        }

        AssemblyPartFocus focus = ownerRoot.GetComponent<AssemblyPartFocus>();
        if (focus != null && focus.SourcePart != null)
        {
            if (focus.SourcePart.partType != PartType.Pipe) return false;
            terminalTemplate = focus.SourcePart.endPrefab;
            return true;
        }

        if (!ContainsNameTokenInHierarchy(ownerRoot, "Pipe")) return false;
        return true;
    }

    private void ApplyPipeTerminalVisualState(
        PipeTerminalOwnerRecord owner,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys,
        bool isGhost,
        Color ghostColor)
    {
        if (owner.ownerRoot == null) return;

        Transform primaryVisual = FindPrimaryPipeVisual(owner.ownerRoot);
        Transform existingReplacement = owner.ownerRoot.Find(PipeReplacementVisualName);
        bool shouldUseTerminal = false;
        Vector3 outerLocalPosition = Vector3.right;

        if (TryResolvePipeTerminalState(owner.ownerRoot, inputOwnersByKey, outputOwnersByKey, coreOutputKeys, out bool foundOpenSide, out Vector3 resolvedOuterLocalPosition, out _))
        {
            shouldUseTerminal = foundOpenSide;
            outerLocalPosition = resolvedOuterLocalPosition;
        }
        else if (owner.ownerLocalPortPositions != null && owner.ownerLocalPortPositions.Length > 0)
        {
            outerLocalPosition = owner.ownerLocalPortPositions[0];
        }

        if (!shouldUseTerminal || owner.terminalTemplate == null)
        {
            if (existingReplacement != null)
            {
                Object.Destroy(existingReplacement.gameObject);
            }

            if (primaryVisual != null && !primaryVisual.gameObject.activeSelf)
            {
                primaryVisual.gameObject.SetActive(true);
            }
            return;
        }

        if (isGhost)
        {
            if (primaryVisual != null && !primaryVisual.gameObject.activeSelf)
            {
                primaryVisual.gameObject.SetActive(true);
            }
        }
        else if (primaryVisual != null && primaryVisual.gameObject.activeSelf)
        {
            primaryVisual.gameObject.SetActive(false);
        }

        GameObject replacement = existingReplacement != null
            ? existingReplacement.gameObject
            : Object.Instantiate(owner.terminalTemplate, owner.ownerRoot, false);

        replacement.name = PipeReplacementVisualName;
        replacement.transform.localPosition = Vector3.zero;
        replacement.transform.localRotation = GetPipeEndRotation(outerLocalPosition);
        MatchWorldScale(replacement.transform, owner.terminalTemplate.transform.lossyScale);
        SmallScaleLayerUtility.ApplyRecursively(replacement.transform);

        if (isGhost)
        {
            EnsureRendererTransparencyRecursiveStatic(replacement.transform);
            SetRendererColorRecursiveStatic(replacement.transform, ghostColor);
        }
    }

    private void BuildPipeOwnerPortMaps(
        ArtificialSatellite satellite,
        bool includeGhost,
        out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys)
    {
        inputOwnersByKey = new Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>>();
        outputOwnersByKey = new Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>>();
        coreOutputKeys = new HashSet<(Vector2Int cell, CellSideMask side)>();
        if (satellite == null) return;

        AssemblyPartPortLayout[] layouts = satellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null) continue;
            if (!includeGhost && layout.GetComponentInParent<AssemblyGhostMarker>(true) != null) continue;

            GameObject owner = ResolvePipeMapOwner(layout.transform);
            if (owner == null) continue;

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null || entries.Count == 0) continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[j];
                if (entry == null) continue;
                if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;
                if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) continue;
                if (!IsInsideGrid(sourceCell) || portSide == CellSideMask.None) continue;

                if (entry.portType == AssemblyPortType.Input)
                {
                    AddOwnerToPortMap(inputOwnersByKey, (sourceCell, portSide), owner);
                }
                else
                {
                    AddOwnerToPortMap(outputOwnersByKey, (sourceCell, portSide), owner);
                }
            }
        }

        foreach (KeyValuePair<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)> pair in coreLayoutByPort)
        {
            Vector2Int sourceCell = pair.Value.sourceCell;
            CellSideMask side = pair.Value.outputSide;
            if (!IsInsideGrid(sourceCell) || side == CellSideMask.None) continue;
            coreOutputKeys.Add((sourceCell, side));
        }
    }

    private GameObject ResolvePipeMapOwner(Transform ownerRoot)
    {
        if (ownerRoot == null) return null;

        AssemblyPartFocus focus = ownerRoot.GetComponent<AssemblyPartFocus>();
        if (focus != null)
        {
            return focus.gameObject;
        }

        return ownerRoot.gameObject;
    }

    private bool TryResolvePipeTerminalState(
        Transform ownerRoot,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys,
        out bool shouldUseTerminal,
        out Vector3 outerLocalPosition,
        out AssemblyPortType openPortType)
    {
        shouldUseTerminal = false;
        outerLocalPosition = Vector3.right;
        openPortType = AssemblyPortType.Output;
        if (ownerRoot == null) return false;

        AssemblyPartPortLayout layout = ownerRoot.GetComponent<AssemblyPartPortLayout>();
        if (layout == null || layout.Ports == null || layout.Ports.Count == 0) return false;

        GameObject ownerObject = ResolvePipeMapOwner(ownerRoot);
        bool foundAnyPort = false;
        bool foundOpenPort = false;

        for (int i = 0; i < layout.Ports.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
            if (entry == null || entry.portTransform == null) continue;
            if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;
            if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) continue;
            if (portSide == CellSideMask.None) continue;

            Vector3 portLocal = ownerRoot.InverseTransformPoint(entry.portTransform.position);
            if (!foundAnyPort)
            {
                outerLocalPosition = portLocal;
                foundAnyPort = true;
            }

            if (IsPipeConnectedToPipeOwner(ownerObject, sourceCell, portSide, entry.portType, inputOwnersByKey, outputOwnersByKey))
            {
                continue;
            }

            if (!HasTerminalBoundaryOwner(ownerObject, sourceCell, portSide, entry.portType, inputOwnersByKey, outputOwnersByKey, coreOutputKeys))
            {
                continue;
            }

            if (!foundOpenPort)
            {
                outerLocalPosition = portLocal;
                openPortType = entry.portType;
                foundOpenPort = true;
            }
        }

        shouldUseTerminal = foundOpenPort;
        return foundAnyPort;
    }

    private bool IsPipeConnectedToPipeOwner(
        GameObject ownerObject,
        Vector2Int sourceCell,
        CellSideMask portSide,
        AssemblyPortType portType,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey)
    {
        Vector2Int neighborCell = sourceCell + SideToCellOffset(portSide);
        if (!IsInsideGrid(neighborCell)) return false;

        CellSideMask neighborSide = OppositeSide(portSide);
        if (neighborSide == CellSideMask.None) return false;

        if (portType == AssemblyPortType.Input)
        {
            return TryGetOwnersByPortKey(outputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> outputOwners)
                && ContainsPipeOwner(outputOwners, ownerObject);
        }

        return TryGetOwnersByPortKey(inputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> inputOwners)
            && ContainsPipeOwner(inputOwners, ownerObject);
    }

    private bool HasTerminalBoundaryOwner(
        GameObject ownerObject,
        Vector2Int sourceCell,
        CellSideMask portSide,
        AssemblyPortType portType,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys)
    {
        if (portSide == CellSideMask.None) return false;

        Vector2Int neighborCell = sourceCell + SideToCellOffset(portSide);
        if (!IsInsideGrid(neighborCell)) return false;

        CellSideMask neighborSide = OppositeSide(portSide);
        if (neighborSide == CellSideMask.None) return false;

        if (portType == AssemblyPortType.Input)
        {
            if (TryGetOwnersByPortKey(outputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> outputOwners)
                && ContainsNonExcludedOwner(outputOwners, ownerObject))
            {
                return true;
            }

            return coreOutputKeys != null && coreOutputKeys.Contains((neighborCell, neighborSide));
        }

        return TryGetOwnersByPortKey(inputOwnersByKey, (neighborCell, neighborSide), out HashSet<GameObject> inputOwners)
            && ContainsNonExcludedOwner(inputOwners, ownerObject);
    }

    private static bool ContainsNonExcludedOwner(IEnumerable<GameObject> owners, GameObject excludedOwner)
    {
        if (owners == null) return false;

        foreach (GameObject owner in owners)
        {
            if (owner == null || owner == excludedOwner) continue;
            return true;
        }

        return false;
    }

    private static bool ContainsPipeOwner(IEnumerable<GameObject> owners, GameObject excludedOwner)
    {
        if (owners == null) return false;

        foreach (GameObject owner in owners)
        {
            if (owner == null || owner == excludedOwner) continue;
            if (IsPipeBoundaryOwner(owner.transform)) return true;
        }

        return false;
    }

    private static bool IsPipeBoundaryOwner(Transform ownerRoot)
    {
        if (ownerRoot == null) return false;

        AssemblyPartFocus focus = ownerRoot.GetComponent<AssemblyPartFocus>();
        if (focus != null && focus.SourcePart != null)
        {
            return focus.SourcePart.partType == PartType.Pipe;
        }

        return ContainsNameTokenInHierarchy(ownerRoot, "Pipe");
    }

    private static Transform FindPrimaryPipeVisual(Transform ownerRoot)
    {
        if (ownerRoot == null) return null;

        for (int i = 0; i < ownerRoot.childCount; i++)
        {
            Transform child = ownerRoot.GetChild(i);
            if (child == null) continue;
            if (string.Equals(child.name, RuntimePortsRootName, System.StringComparison.Ordinal)) continue;
            if (string.Equals(child.name, PipeReplacementVisualName, System.StringComparison.Ordinal)) continue;
            return child;
        }

        return null;
    }

    private static void ApplyPipeCornerEndVisualLocalRotation(
        Transform replacementRoot,
        AssemblyPortType openPortType)
    {
        if (replacementRoot == null) return;
        if (openPortType != AssemblyPortType.Input) return;

        Transform targetVisual = FindPipeCornerPrimaryVisual(replacementRoot);
        if (targetVisual == null) return;

        targetVisual.localRotation = Quaternion.Euler(0f, 90f, 180f);
    }

    private static void ClearPipeReplacementVisuals(Transform containerRoot)
    {
        if (containerRoot == null) return;

        Transform[] allTransforms = containerRoot.GetComponentsInChildren<Transform>(true);
        for (int i = allTransforms.Length - 1; i >= 0; i--)
        {
            Transform current = allTransforms[i];
            if (current == null) continue;
            if (current == containerRoot) continue;
            if (!string.Equals(current.name, PipeReplacementVisualName, System.StringComparison.Ordinal)) continue;

            Transform ownerRoot = current.parent;
            Object.Destroy(current.gameObject);

            Transform primaryVisual = FindPrimaryPipeVisual(ownerRoot);
            if (primaryVisual != null && !primaryVisual.gameObject.activeSelf)
            {
                primaryVisual.gameObject.SetActive(true);
            }
        }
    }

    private static Quaternion GetPipeEndRotation(Vector3 ownerLocalPosition)
    {
        CellSideMask side = DirectionToSideMask(ownerLocalPosition);
        switch (side)
        {
            case CellSideMask.Top: return Quaternion.Euler(0f, 0f, 90f);
            case CellSideMask.Left: return Quaternion.Euler(0f, 0f, 180f);
            case CellSideMask.Bottom: return Quaternion.Euler(0f, 0f, 270f);
            case CellSideMask.Right:
            default:
                return Quaternion.identity;
        }
    }


    private static void EnsureRendererTransparencyRecursiveStatic(Transform root)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null) continue;
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
    }

    private static void SetRendererColorRecursiveStatic(Transform root, Color color)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null) continue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }
    }
}











