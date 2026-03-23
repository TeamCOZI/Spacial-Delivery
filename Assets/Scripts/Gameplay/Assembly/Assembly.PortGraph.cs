using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Assembly
{
    private void RequestRefreshOutputPorts()
    {
        outputPortRefreshQueued = true;
        if (outputPortRefreshCoroutine == null)
        {
            outputPortRefreshCoroutine = StartCoroutine(RefreshOutputPortsAtEndOfFrame());
        }
    }

    private IEnumerator RefreshOutputPortsAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        outputPortRefreshCoroutine = null;
        if (!outputPortRefreshQueued) yield break;

        outputPortRefreshQueued = false;
        RefreshOutputPortsNow();
    }

    private void RefreshOutputPortsNow()
    {
        outputPorts.Clear();
        outputPortPulses.Clear();
        inputMaskByCell.Clear();
        outputMaskByCell.Clear();
        portTypesByCellAndSide.Clear();
        portOwnerByCellAndSide.Clear();
        outputPortsByCellAndSide.Clear();
        coreLayoutByPort.Clear();

        if (artificialSatellite == null) return;

        Transform[] allTransforms = artificialSatellite.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null || current == artificialSatellite.transform) continue;
            if (current.GetComponentInParent<AssemblyGhostMarker>() != null) continue;
            if (!IsOutputPortTransform(current)) continue;

            AssemblyPort outputPort = ComponentUtility.GetOrAddComponent<AssemblyPort>(current.gameObject);
            outputPort.PortType = AssemblyPortType.Output;
            if (outputPort.GetComponentInParent<AssemblyPartFocus>(true) == null)
            {
                // Core/output prefab ports: infer side once and cache as a direction vector.
                CellSideMask inferredSide = GetOutputPortSideMask(outputPort);
                if (inferredSide != CellSideMask.None)
                {
                    outputPort.LocalDirection = SideToVector3(inferredSide);
                }
            }
            outputPorts.Add(outputPort);
            ConfigureOutputPortFocusMetadata(
                outputPort,
                Vector2Int.zero,
                Vector2Int.zero,
                ResolveOutputPortSide(outputPort),
                false);
            Renderer portRenderer = current.GetComponent<Renderer>();
            AssemblyPortPulse pulse = current.GetComponent<AssemblyPortPulse>();
            if (portRenderer != null)
            {
                pulse = pulse != null ? pulse : ComponentUtility.GetOrAddComponent<AssemblyPortPulse>(current.gameObject);
                outputPortPulses.Add(pulse);
            }
            else if (pulse != null)
            {
                Destroy(pulse);
            }
        }

        BuildCoreLayoutMapping();

        // Second phase: assign mappings only after all output ports are discovered.
        for (int i = 0; i < outputPorts.Count; i++)
        {
            RegisterOutputPortCellInfo(outputPorts[i]);
        }

        RegisterPartLayoutPortMappings();
        RecalculateOutputPortOccupancyFromGrid();
        RefreshPipeEndVisualsForSatellite(artificialSatellite);
    }

    private void BuildCoreLayoutMapping()
    {
        if (artificialSatellite == null) return;

        AssemblyCorePortLayout coreLayout = artificialSatellite.GetComponentInChildren<AssemblyCorePortLayout>(true);
        if (coreLayout == null)
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning("[CorePortMap] AssemblyCorePortLayout missing on core object.");
            }
            return;
        }

        List<AssemblyCorePortLayout.OutputPortEntry> entries = coreLayout.OutputPorts;
        if (entries == null || entries.Count == 0) return;

        Vector2Int coreCenterCell = LocalPositionToGrid(coreLayout.transform.localPosition, ZeroSnapOffset);
        if (!IsInsideGrid(coreCenterCell))
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning($"[CorePortMap] coreCenterCell out of range: {coreCenterCell}");
            }
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AssemblyCorePortLayout.OutputPortEntry entry = entries[i];
            if (entry == null || entry.portTransform == null) continue;

            AssemblyPort port = entry.portTransform.GetComponent<AssemblyPort>();
            if (port == null) continue;

            CellSideMask side = ConvertCoreLayoutSide(entry.outputSide);
            if (side == CellSideMask.None) continue;

            Vector2Int sourceCell = coreCenterCell + entry.relativeSourceCell;
            coreLayoutByPort[port] = (sourceCell, side);
        }
    }

    private static CellSideMask ConvertCoreLayoutSide(AssemblyCorePortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyCorePortLayout.PortSide.Top: return CellSideMask.Top;
            case AssemblyCorePortLayout.PortSide.Bottom: return CellSideMask.Bottom;
            case AssemblyCorePortLayout.PortSide.Left: return CellSideMask.Left;
            case AssemblyCorePortLayout.PortSide.Right: return CellSideMask.Right;
            default: return CellSideMask.None;
        }
    }

    private static CellSideMask ConvertPartLayoutSide(AssemblyPartPortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top: return CellSideMask.Top;
            case AssemblyPartPortLayout.PortSide.Bottom: return CellSideMask.Bottom;
            case AssemblyPartPortLayout.PortSide.Left: return CellSideMask.Left;
            case AssemblyPartPortLayout.PortSide.Right: return CellSideMask.Right;
            default: return CellSideMask.None;
        }
    }

    private static AssemblyPartPortLayout.PortSide ConvertMaskToPartLayoutSide(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return AssemblyPartPortLayout.PortSide.Top;
            case CellSideMask.Bottom: return AssemblyPartPortLayout.PortSide.Bottom;
            case CellSideMask.Left: return AssemblyPartPortLayout.PortSide.Left;
            case CellSideMask.Right: return AssemblyPartPortLayout.PortSide.Right;
            default: return AssemblyPartPortLayout.PortSide.Right;
        }
    }

    private void SetOutputPortsPulsing(bool isPulsing)
    {
        for (int i = 0; i < outputPortPulses.Count; i++)
        {
            if (outputPortPulses[i] != null) outputPortPulses[i].SetPulsing(isPulsing);
        }
    }

    private void RegisterOutputPortCellInfo(AssemblyPort outputPort)
    {
        if (outputPort == null || artificialSatellite == null) return;

        Vector2Int sourceCell;
        CellSideMask outputSide;

        if (coreLayoutByPort.TryGetValue(outputPort, out (Vector2Int sourceCell, CellSideMask outputSide) coreMapped))
        {
            sourceCell = coreMapped.sourceCell;
            outputSide = coreMapped.outputSide;
        }
        else if (TryGetPartLayoutPortMapping(outputPort, out sourceCell, out outputSide))
        {
            // Part layout mapping succeeded.
        }
        else
        {
            bool belongsToCoreLayout = outputPort.GetComponentInParent<AssemblyCorePortLayout>(true) != null;
            if (belongsToCoreLayout)
            {
                // Core ports must be mapped only from explicit core layout data.
                if (debugPipePlacement)
                {
                    Debug.LogWarning($"[CorePortMap] Missing explicit mapping for core port: {outputPort.name}");
                }
                return;
            }

            bool belongsToPartLayout = outputPort.GetComponentInParent<AssemblyPartPortLayout>(true) != null;
            if (belongsToPartLayout)
            {
                if (debugPipePlacement)
                {
                    Debug.LogWarning($"[PartPortMap] Missing explicit mapping for part port: {outputPort.name}");
                }
                return;
            }

            outputSide = ResolveOutputPortSide(outputPort);
            if (outputSide == CellSideMask.None) return;

            Vector2Int outputDirFallback = SideToCellOffset(outputSide);
            if (outputDirFallback == Vector2Int.zero) return;

            // Default runtime parts: estimate the source cell from the output marker location.
            Vector3 localPos = artificialSatellite.transform.InverseTransformPoint(outputPort.transform.position);
            Vector3 halfCellBack = new Vector3(outputDirFallback.x * cellSize * 0.5f, outputDirFallback.y * cellSize * 0.5f, 0f);
            sourceCell = LocalPositionToGrid(localPos - halfCellBack, ZeroSnapOffset);
        }

        Vector2Int outputDir = SideToCellOffset(outputSide);
        if (outputDir == Vector2Int.zero) return;

        Vector2Int mappedCell = sourceCell + outputDir;
        if (!IsInsideGrid(mappedCell)) return;

        CellSideMask mappedSide = OppositeSide(outputSide);
        if (mappedSide == CellSideMask.None) return;

        RegisterPortTypeMapping(
            mappedCell,
            mappedSide,
            AssemblyPortType.Output,
            ResolvePortOwnerName(outputPort != null ? outputPort.transform : null));

        if (outputMaskByCell.TryGetValue(mappedCell, out CellSideMask currentMask))
        {
            outputMaskByCell[mappedCell] = currentMask | mappedSide;
        }
        else
        {
            outputMaskByCell[mappedCell] = mappedSide;
        }

        if (!outputPortsByCellAndSide.TryGetValue(mappedCell, out Dictionary<CellSideMask, AssemblyPort> perSide))
        {
            perSide = new Dictionary<CellSideMask, AssemblyPort>();
            outputPortsByCellAndSide[mappedCell] = perSide;
        }

        if (perSide.TryGetValue(mappedSide, out AssemblyPort existing) && existing != null && existing != outputPort)
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning(
                    $"[PipeStartDebug] duplicate output side mapping cell={mappedCell} side={mappedSide} " +
                    $"existing={existing.name} new={outputPort.name}");
            }
        }
        perSide[mappedSide] = outputPort;

        ConfigureOutputPortFocusMetadata(outputPort, sourceCell, mappedCell, outputSide, true);
    }

    private void ConfigureOutputPortFocusMetadata(
        AssemblyPort outputPort,
        Vector2Int sourceCell,
        Vector2Int mappedCell,
        CellSideMask outputSide,
        bool hasMapping)
    {
        if (outputPort == null) return;

        AssemblyOutputPortFocus outputPortFocus = ComponentUtility.GetOrAddComponent<AssemblyOutputPortFocus>(outputPort.gameObject);
        AssemblyPartFocus ownerPartFocus = outputPort.GetComponentInParent<AssemblyPartFocus>(true);
        ArtificialSatellite ownerSatellite = outputPort.GetComponentInParent<ArtificialSatellite>(true);
        outputPortFocus.Initialize(
            outputPort,
            ownerSatellite,
            ownerPartFocus,
            ResolvePortOwnerName(outputPort.transform),
            FormatOutputPortSideLabel(outputSide),
            sourceCell,
            mappedCell,
            hasMapping);
    }

    private static string FormatOutputPortSideLabel(CellSideMask outputSide)
    {
        switch (outputSide)
        {
            case CellSideMask.Top: return "Top";
            case CellSideMask.Bottom: return "Bottom";
            case CellSideMask.Left: return "Left";
            case CellSideMask.Right: return "Right";
            default: return string.Empty;
        }
    }

    private void RegisterPartLayoutPortMappings()
    {
        if (artificialSatellite == null) return;

        AssemblyPartPortLayout[] layouts = artificialSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null) continue;
            if (layout.GetComponentInParent<AssemblyGhostMarker>() != null) continue;

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null || entries.Count == 0) continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[j];
                if (entry == null) continue;
                if (entry.portType != AssemblyPortType.Input && entry.portType != AssemblyPortType.Output) continue;

                if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide))
                {
                    continue;
                }

                Vector2Int outputDir = SideToCellOffset(portSide);
                if (outputDir == Vector2Int.zero) continue;

                Vector2Int mappedCell = sourceCell + outputDir;
                if (!IsInsideGrid(mappedCell)) continue;

                CellSideMask mappedSide = OppositeSide(portSide);
                if (mappedSide == CellSideMask.None) continue;

                RegisterPortTypeMapping(
                    mappedCell,
                    mappedSide,
                    entry.portType,
                    ResolvePortOwnerName(entry.portTransform != null ? entry.portTransform : layout.transform));
            }
        }
    }

    private void RegisterPortTypeMapping(
        Vector2Int mappedCell,
        CellSideMask mappedSide,
        AssemblyPortType portType,
        string ownerPartName)
    {
        if (!portTypesByCellAndSide.TryGetValue(mappedCell, out Dictionary<CellSideMask, CellPortTypeMask> perSide))
        {
            perSide = new Dictionary<CellSideMask, CellPortTypeMask>();
            portTypesByCellAndSide[mappedCell] = perSide;
        }

        CellPortTypeMask nextTypeMask = portType == AssemblyPortType.Input
            ? CellPortTypeMask.Input
            : CellPortTypeMask.Output;

        if (perSide.TryGetValue(mappedSide, out CellPortTypeMask currentTypeMask))
        {
            perSide[mappedSide] = currentTypeMask | nextTypeMask;
        }
        else
        {
            perSide[mappedSide] = nextTypeMask;
        }

        if (!portOwnerByCellAndSide.TryGetValue(mappedCell, out Dictionary<CellSideMask, string> ownerBySide))
        {
            ownerBySide = new Dictionary<CellSideMask, string>();
            portOwnerByCellAndSide[mappedCell] = ownerBySide;
        }

        string nextOwnerLabel = BuildPortOwnerLabel(portType, ownerPartName);
        if (ownerBySide.TryGetValue(mappedSide, out string existingLabel) &&
            !string.IsNullOrWhiteSpace(existingLabel) &&
            !string.Equals(existingLabel, nextOwnerLabel, System.StringComparison.Ordinal))
        {
            ownerBySide[mappedSide] = existingLabel + " + " + nextOwnerLabel;
        }
        else
        {
            ownerBySide[mappedSide] = nextOwnerLabel;
        }

        if (portType == AssemblyPortType.Input)
        {
            if (inputMaskByCell.TryGetValue(mappedCell, out CellSideMask currentMask))
            {
                inputMaskByCell[mappedCell] = currentMask | mappedSide;
            }
            else
            {
                inputMaskByCell[mappedCell] = mappedSide;
            }
        }
    }

    private static string BuildPortOwnerLabel(AssemblyPortType portType, string ownerPartName)
    {
        string typeName = portType == AssemblyPortType.Input ? "Input" : "Output";
        string owner = string.IsNullOrWhiteSpace(ownerPartName) ? "Unknown" : ownerPartName;
        return $"{typeName} ({owner})";
    }

    private static string ResolvePortOwnerName(Transform portTransform)
    {
        if (portTransform == null) return "Unknown";

        AssemblyPartFocus partFocus = portTransform.GetComponentInParent<AssemblyPartFocus>(true);
        if (partFocus != null && partFocus.SourcePart != null && !string.IsNullOrWhiteSpace(partFocus.SourcePart.partName))
        {
            return partFocus.SourcePart.partName;
        }
        if (partFocus != null && ContainsNameTokenInHierarchy(partFocus.transform, "Pipe"))
        {
            return "Pipe";
        }

        if (portTransform.GetComponentInParent<AssemblyCorePortLayout>(true) != null)
        {
            return "Core";
        }

        if (ContainsNameTokenInHierarchy(portTransform, "Pipe"))
        {
            return "Pipe";
        }

        Transform current = portTransform;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.name) &&
                current.name.IndexOf("Core", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Core";
            }

            current = current.parent;
        }

        return "Unknown";
    }

    private bool TryGetPartLayoutPortMapping(AssemblyPort port, out Vector2Int sourceCell, out CellSideMask outputSide)
    {
        sourceCell = Vector2Int.zero;
        outputSide = CellSideMask.None;
        if (port == null) return false;

        AssemblyPartPortLayout layout = port.GetComponentInParent<AssemblyPartPortLayout>(true);
        if (layout == null) return false;
        if (!layout.TryGetPortEntry(port, out AssemblyPartPortLayout.PortEntry entry)) return false;
        if (entry == null || entry.portType != AssemblyPortType.Output) return false;

        return TryGetPartLayoutEntryMapping(layout, entry, out sourceCell, out outputSide);
    }

    private bool TryGetPartLayoutEntryMapping(
        AssemblyPartPortLayout layout,
        AssemblyPartPortLayout.PortEntry entry,
        out Vector2Int sourceCell,
        out CellSideMask portSide)
    {
        sourceCell = Vector2Int.zero;
        portSide = CellSideMask.None;
        if (layout == null || entry == null) return false;

        AssemblyPartFocus focus = layout.GetComponent<AssemblyPartFocus>();
        Vector2 snapOffset = GetSnapOffsetFromPartFocus(focus);
        Vector2 pivotOffset = GetPartPivotOffset(layout, layout.transform.localRotation);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition - (Vector3)pivotOffset, snapOffset);
        if (!IsInsideGrid(partCenterCell)) return false;

        int quarterTurns = GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
        if (rotatedSide == CellSideMask.None) return false;

        sourceCell = partCenterCell + rotatedRelativeCell;
        portSide = rotatedSide;
        return true;
    }

    private Vector2 GetSnapOffsetFromPartFocus(AssemblyPartFocus focus)
    {
        if (focus == null || focus.SourcePart == null) return Vector2.zero;
        AssemblyPartPortLayout layout = focus.GetComponent<AssemblyPartPortLayout>();
        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            return Vector2.zero;
        }

        int width = Mathf.Max(1, focus.SourcePart.gridWidth);
        int height = Mathf.Max(1, focus.SourcePart.gridHeight);
        float offsetX = (width % 2 == 0) ? (cellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (cellSize * 0.5f) : 0f;
        return new Vector2(offsetX, offsetY);
    }

    private static int GetQuarterTurns(Quaternion localRotation)
    {
        return AssemblyMathUtility.GetQuarterTurns(localRotation);
    }

    private static Vector2Int RotateCellOffset(Vector2Int value, int quarterTurns)
    {
        return AssemblyMathUtility.RotateCellOffset(value, quarterTurns);
    }

    private static CellSideMask RotateSide(CellSideMask side, int quarterTurns)
    {
        Vector2Int dir = SideToCellOffset(side);
        if (dir == Vector2Int.zero) return CellSideMask.None;

        Vector2Int rotated = RotateCellOffset(dir, quarterTurns);
        if (rotated == Vector2Int.up) return CellSideMask.Top;
        if (rotated == Vector2Int.down) return CellSideMask.Bottom;
        if (rotated == Vector2Int.left) return CellSideMask.Left;
        if (rotated == Vector2Int.right) return CellSideMask.Right;
        return CellSideMask.None;
    }

    private CellSideMask ResolveOutputPortSide(AssemblyPort outputPort)
    {
        if (outputPort == null || artificialSatellite == null) return CellSideMask.None;

        if (outputPort.LocalDirection.sqrMagnitude > DirectionEpsilonSqr)
        {
            Vector3 worldDir = outputPort.transform.TransformDirection(outputPort.LocalDirection.normalized);
            Vector3 satelliteLocalDir = artificialSatellite.transform.InverseTransformDirection(worldDir);
            CellSideMask byDirection = DirectionToSideMask(satelliteLocalDir);
            if (byDirection != CellSideMask.None) return byDirection;
        }

        return GetOutputPortSideMask(outputPort);
    }

    private bool TryGetAvailableOutputPort(Vector2Int cell, CellSideMask side, out AssemblyPort port)
    {
        if (!TryGetMappedOutputPort(cell, side, out port)) return false;
        if (port == null || port.IsOccupied) return false;
        return true;
    }

    private bool TryGetMappedOutputPort(Vector2Int cell, CellSideMask side, out AssemblyPort port)
    {
        port = null;
        if (!outputPortsByCellAndSide.TryGetValue(cell, out Dictionary<CellSideMask, AssemblyPort> perSide)) return false;
        if (!perSide.TryGetValue(side, out AssemblyPort candidate)) return false;
        if (candidate == null) return false;
        port = candidate;
        return true;
    }

    private static CellSideMask DirectionToSideMask(Vector3 direction)
    {
        if (direction.sqrMagnitude < DirectionEpsilonSqr) return CellSideMask.None;

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        if (absX >= absY)
        {
            return direction.x >= 0f ? CellSideMask.Right : CellSideMask.Left;
        }

        return direction.y >= 0f ? CellSideMask.Top : CellSideMask.Bottom;
    }

    private static CellSideMask GetOutputPortSideMask(AssemblyPort outputPort)
    {
        if (outputPort == null) return CellSideMask.None;

        Docking[] dockings = outputPort.GetComponentsInChildren<Docking>(true);
        for (int i = 0; i < dockings.Length; i++)
        {
            Docking docking = dockings[i];
            if (docking == null) continue;
            if (!docking.isDocking) continue;
            return DirectionToSideMask(docking.transform.localPosition);
        }

        return DirectionToSideMask(outputPort.transform.localPosition);
    }

    private static CellSideMask OppositeSide(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return CellSideMask.Bottom;
            case CellSideMask.Bottom: return CellSideMask.Top;
            case CellSideMask.Left: return CellSideMask.Right;
            case CellSideMask.Right: return CellSideMask.Left;
            default: return CellSideMask.None;
        }
    }

    private static Vector2Int SideToCellOffset(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return Vector2Int.up;
            case CellSideMask.Bottom: return Vector2Int.down;
            case CellSideMask.Left: return Vector2Int.left;
            case CellSideMask.Right: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private static Vector3 SideToVector3(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return Vector3.up;
            case CellSideMask.Bottom: return Vector3.down;
            case CellSideMask.Left: return Vector3.left;
            case CellSideMask.Right: return Vector3.right;
            default: return Vector3.zero;
        }
    }

    private void RecalculateOutputPortOccupancyFromGrid()
    {
        for (int i = 0; i < outputPorts.Count; i++)
        {
            if (outputPorts[i] != null)
            {
                outputPorts[i].SetOccupied(false);
            }
        }

        foreach (KeyValuePair<Vector2Int, Dictionary<CellSideMask, AssemblyPort>> pair in outputPortsByCellAndSide)
        {
            Vector2Int cell = pair.Key;
            if (!occupiedCells.ContainsKey(cell)) continue;

            Dictionary<CellSideMask, AssemblyPort> perSide = pair.Value;
            if (perSide == null) continue;

            foreach (KeyValuePair<CellSideMask, AssemblyPort> sidePair in perSide)
            {
                if (sidePair.Value != null)
                {
                    sidePair.Value.SetOccupied(true);
                }
            }
        }
    }
}
