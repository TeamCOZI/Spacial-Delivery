using System;
using System.Collections.Generic;
using UnityEngine;

public static class OutputPortProductionUtility
{
    private const string LauncherPartName = "Launcher";
    private const float CellSize = 0.1f;
    private const int GridSize = 99;
    private const float PipeTokenLocalZ = -0.06f;
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    private static readonly string[] FamilyNames = { "Aquid", "Nitain", "Territe" };
    private static readonly string[] PhaseNames = { "Crystal", "Liquid", "Gas" };

    public static bool SupportsProduction(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return false;
        }

        AssemblyPartFocus ownerPartFocus = outputPort.OwnerPartFocus;
        if (ownerPartFocus == null || ownerPartFocus.SourcePart == null)
        {
            return false;
        }

        Part sourcePart = ownerPartFocus.SourcePart;
        if (sourcePart.partType == PartType.Pipe || sourcePart.partType == PartType.Core)
        {
            return false;
        }

        return !string.Equals(sourcePart.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveModuleLabel(AssemblyOutputPortFocus outputPort)
    {
        if (outputPort == null)
        {
            return "-";
        }

        AssemblyPartFocus ownerPartFocus = outputPort.OwnerPartFocus;
        if (ownerPartFocus != null && ownerPartFocus.SourcePart != null && !string.IsNullOrWhiteSpace(ownerPartFocus.SourcePart.partName))
        {
            return ownerPartFocus.SourcePart.partName;
        }

        return outputPort.OwnerLabel;
    }

    public static bool TryResolveOwnerModuleInventory(AssemblyOutputPortFocus outputPort, out StructureResourceInventory ownerInventory)
    {
        ownerInventory = null;
        if (!SupportsProduction(outputPort))
        {
            return false;
        }

        AssemblyPartFocus ownerPartFocus = outputPort.OwnerPartFocus;
        if (ownerPartFocus == null || ownerPartFocus.SourcePart == null)
        {
            return false;
        }

        ownerInventory = ownerPartFocus.GetComponent<StructureResourceInventory>();
        if (ownerInventory == null)
        {
            ownerInventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(ownerPartFocus.gameObject);
            ownerInventory.InitializeForPart(ownerPartFocus.SourcePart);
        }

        return ownerInventory != null;
    }

    public static void ResolveAccessibleSourceInventories(AssemblyOutputPortFocus outputPort, List<StructureResourceInventory> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (outputPort == null)
        {
            return;
        }

        HashSet<StructureResourceInventory> uniqueInventories = new HashSet<StructureResourceInventory>();
        AssemblyPartFocus ownerPartFocus = outputPort.OwnerPartFocus;
        if (ownerPartFocus != null && ownerPartFocus.SourcePart != null)
        {
            StructureResourceInventory ownerInventory = ownerPartFocus.GetComponent<StructureResourceInventory>();
            if (ownerInventory == null)
            {
                ownerInventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(ownerPartFocus.gameObject);
                ownerInventory.InitializeForPart(ownerPartFocus.SourcePart);
            }

            AppendInventory(ownerInventory, uniqueInventories, results);
        }

        ArtificialSatellite ownerSatellite = ownerPartFocus != null ? ownerPartFocus.OwnerSatellite : outputPort.OwnerSatellite;
        if (ShouldIncludeSharedCoreInventory(ownerPartFocus, outputPort.OwnerLabel))
        {
            AppendSharedCoreStructureInventories(ownerSatellite, uniqueInventories, results);
        }
    }

    public static void BuildMergedResourceAmounts(
        AssemblyOutputPortFocus outputPort,
        List<StructureResourceInventory.ResourceAmount> results,
        out int totalCapacity)
    {
        totalCapacity = 0;
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (outputPort == null)
        {
            return;
        }

        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        ResolveAccessibleSourceInventories(outputPort, inventories);

        Dictionary<InventoryResourceType, int> mergedAmounts = new Dictionary<InventoryResourceType, int>();
        for (int i = 0; i < inventories.Count; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            totalCapacity += inventory.Capacity;
            for (int resourceIndex = 0; resourceIndex < inventory.Resources.Count; resourceIndex++)
            {
                StructureResourceInventory.ResourceAmount resourceAmount = inventory.Resources[resourceIndex];
                if (mergedAmounts.TryGetValue(resourceAmount.resourceType, out int currentAmount))
                {
                    mergedAmounts[resourceAmount.resourceType] = currentAmount + Mathf.Max(0, resourceAmount.amount);
                }
                else
                {
                    mergedAmounts.Add(resourceAmount.resourceType, Mathf.Max(0, resourceAmount.amount));
                }
            }
        }

        InventoryResourceType[] resourceTypes = InventoryResourceCatalog.All;
        for (int i = 0; i < resourceTypes.Length; i++)
        {
            InventoryResourceType resourceType = resourceTypes[i];
            if (!mergedAmounts.TryGetValue(resourceType, out int amount) || amount <= 0)
            {
                continue;
            }

            results.Add(new StructureResourceInventory.ResourceAmount(resourceType, amount));
        }
    }

    public static int GetMergedResourceAmount(AssemblyOutputPortFocus outputPort, InventoryResourceType resourceType)
    {
        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        ResolveAccessibleSourceInventories(outputPort, inventories);

        int totalAmount = 0;
        for (int i = 0; i < inventories.Count; i++)
        {
            if (inventories[i] == null)
            {
                continue;
            }

            totalAmount += inventories[i].GetAmount(resourceType);
        }

        return totalAmount;
    }

    public static int PredictRemovalFromInventory(
        AssemblyOutputPortFocus outputPort,
        InventoryResourceType resourceType,
        StructureResourceInventory targetInventory,
        int amount)
    {
        if (targetInventory == null || amount <= 0)
        {
            return 0;
        }

        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        ResolveAccessibleSourceInventories(outputPort, inventories);

        int remaining = amount;
        int removedFromTarget = 0;
        for (int i = 0; i < inventories.Count && remaining > 0; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            int available = inventory.GetAmount(resourceType);
            int removeAmount = Mathf.Min(remaining, available);
            if (removeAmount <= 0)
            {
                continue;
            }

            if (inventory == targetInventory)
            {
                removedFromTarget += removeAmount;
            }

            remaining -= removeAmount;
        }

        return removedFromTarget;
    }

    public static bool TryConsumeResource(AssemblyOutputPortFocus outputPort, InventoryResourceType resourceType, int amount)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return true;
        }

        List<StructureResourceInventory> inventories = new List<StructureResourceInventory>();
        ResolveAccessibleSourceInventories(outputPort, inventories);

        int totalAvailable = 0;
        for (int i = 0; i < inventories.Count; i++)
        {
            if (inventories[i] == null)
            {
                continue;
            }

            totalAvailable += inventories[i].GetAmount(resourceType);
        }

        if (totalAvailable < sanitizedAmount)
        {
            return false;
        }

        int remaining = sanitizedAmount;
        for (int i = 0; i < inventories.Count && remaining > 0; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (inventory == null)
            {
                continue;
            }

            int available = inventory.GetAmount(resourceType);
            int removeAmount = Mathf.Min(remaining, available);
            if (removeAmount <= 0)
            {
                continue;
            }

            if (inventory.TryRemove(resourceType, removeAmount))
            {
                remaining -= removeAmount;
            }
        }

        return remaining <= 0;
    }

    public static bool TryBuildPipePathLocalPoints(AssemblyOutputPortFocus outputPort, List<Vector3> localPoints, out int pipeCellCount)
    {
        pipeCellCount = 0;
        if (localPoints == null)
        {
            return false;
        }

        localPoints.Clear();
        if (outputPort == null || outputPort.OwnerSatellite == null || !outputPort.HasCellMapping)
        {
            return false;
        }

        HashSet<Vector2Int> pipeCells = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, List<Vector2Int>> adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        PipeConnectivityUtility.BuildConnectedPipeAdjacency(outputPort.OwnerSatellite, adjacency, pipeCells);

        Vector2Int destinationCell = outputPort.MappedCell;
        if (!pipeCells.Contains(destinationCell))
        {
            return false;
        }
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Dictionary<Vector2Int, Vector2Int> parents = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue(destinationCell);
        distances[destinationCell] = 0;

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (!adjacency.TryGetValue(current, out List<Vector2Int> neighbors))
            {
                continue;
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector2Int neighbor = neighbors[i];
                if (distances.ContainsKey(neighbor))
                {
                    continue;
                }

                distances[neighbor] = distances[current] + 1;
                parents[neighbor] = current;
                open.Enqueue(neighbor);
            }
        }

        Vector2Int sourceCell = destinationCell;
        int bestDistance = 0;
        bool foundLeaf = false;
        foreach (KeyValuePair<Vector2Int, int> pair in distances)
        {
            Vector2Int cell = pair.Key;
            int degree = adjacency.TryGetValue(cell, out List<Vector2Int> neighbors) ? neighbors.Count : 0;
            bool isLeaf = cell != destinationCell && degree <= 1;
            if (!isLeaf)
            {
                continue;
            }

            foundLeaf = true;
            if (pair.Value > bestDistance)
            {
                bestDistance = pair.Value;
                sourceCell = cell;
            }
        }

        if (!foundLeaf)
        {
            foreach (KeyValuePair<Vector2Int, int> pair in distances)
            {
                if (pair.Value <= bestDistance)
                {
                    continue;
                }

                bestDistance = pair.Value;
                sourceCell = pair.Key;
            }
        }

        List<Vector2Int> cellPath = new List<Vector2Int>();
        Vector2Int cursor = sourceCell;
        cellPath.Add(cursor);
        while (cursor != destinationCell && parents.TryGetValue(cursor, out Vector2Int nextCell))
        {
            cursor = nextCell;
            cellPath.Add(cursor);
        }

        if (cellPath.Count <= 0 || cellPath[cellPath.Count - 1] != destinationCell)
        {
            return false;
        }

        pipeCellCount = cellPath.Count;
        for (int i = 0; i < cellPath.Count; i++)
        {
            localPoints.Add(GridToLocalPosition(cellPath[i], PipeTokenLocalZ));
        }

        Vector3 moduleLocalPoint = outputPort.OwnerSatellite.transform.InverseTransformPoint(outputPort.transform.position);
        moduleLocalPoint.z = PipeTokenLocalZ;
        if (localPoints.Count == 0 || (localPoints[localPoints.Count - 1] - moduleLocalPoint).sqrMagnitude > 0.0000001f)
        {
            localPoints.Add(moduleLocalPoint);
        }

        return localPoints.Count >= 2;
    }

    public static bool TryResolveProducedResource(
        AssemblyOutputPortFocus outputPort,
        InventoryResourceType inputResource,
        out InventoryResourceType outputResource)
    {
        return TryResolveProducedResource(ResolveModuleLabel(outputPort), inputResource, out outputResource);
    }

    public static bool TryResolveProducedResource(
        string moduleName,
        InventoryResourceType inputResource,
        out InventoryResourceType outputResource)
    {
        outputResource = inputResource;
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return false;
        }

        if (!TryDecomposeResource(inputResource, out int familyIndex, out int phaseIndex))
        {
            return false;
        }

        switch (moduleName)
        {
            case "Cooler":
            case "Chiller":
                phaseIndex = Mathf.Max(0, phaseIndex - 1);
                break;

            case "Heater":
            case "Furnace":
                phaseIndex = Mathf.Min(PhaseNames.Length - 1, phaseIndex + 1);
                break;

            case "Refiner":
                familyIndex = PositiveModulo(familyIndex + 1, FamilyNames.Length);
                break;

            case "Molder":
                phaseIndex = 0;
                break;

            case "Assembler":
                familyIndex = PositiveModulo(familyIndex + 1, FamilyNames.Length);
                phaseIndex = 0;
                break;

            case "Manufacturer":
                familyIndex = PositiveModulo(familyIndex + 2, FamilyNames.Length);
                break;

            case "Merger":
                familyIndex = PositiveModulo(familyIndex + 1, FamilyNames.Length);
                phaseIndex = Mathf.Min(PhaseNames.Length - 1, phaseIndex + 1);
                break;

            case "Processor":
            default:
                break;
        }

        outputResource = ComposeResource(familyIndex, phaseIndex, inputResource);
        return true;
    }

    private static void AppendSharedCoreStructureInventories(
        ArtificialSatellite ownerSatellite,
        HashSet<StructureResourceInventory> uniqueInventories,
        List<StructureResourceInventory> results)
    {
        if (ownerSatellite == null)
        {
            return;
        }

        StructureResourceInventory[] inventories = ownerSatellite.GetComponentsInChildren<StructureResourceInventory>(true);
        for (int i = 0; i < inventories.Length; i++)
        {
            StructureResourceInventory inventory = inventories[i];
            if (!TryResolveOwnedStructure(inventory, out _))
            {
                continue;
            }

            AppendInventory(inventory, uniqueInventories, results);
        }
    }

    private static void AppendInventory(
        StructureResourceInventory inventory,
        HashSet<StructureResourceInventory> uniqueInventories,
        List<StructureResourceInventory> results)
    {
        if (inventory == null || uniqueInventories == null || results == null)
        {
            return;
        }

        if (!uniqueInventories.Add(inventory))
        {
            return;
        }

        results.Add(inventory);
    }

    private static bool TryResolveOwnedStructure(StructureResourceInventory resourceInventory, out Structure ownerStructure)
    {
        ownerStructure = null;
        if (resourceInventory == null)
        {
            return false;
        }

        StructureInstance structureInstance = resourceInventory.GetComponent<StructureInstance>();
        if (structureInstance == null)
        {
            return false;
        }

        ownerStructure = structureInstance.SourceStructure;
        return ownerStructure != null;
    }

    private static bool ShouldIncludeSharedCoreInventory(AssemblyPartFocus ownerPartFocus, string ownerLabel)
    {
        if (ownerPartFocus == null)
        {
            return true;
        }

        if (IsCorePart(ownerPartFocus))
        {
            return true;
        }

        if (string.Equals(ownerLabel, "Core", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsDirectlyAttachedToCore(ownerPartFocus);
    }

    private static bool IsCorePart(AssemblyPartFocus ownerPartFocus)
    {
        return ownerPartFocus != null &&
               ownerPartFocus.SourcePart != null &&
               string.Equals(ownerPartFocus.SourcePart.partName, "Core", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectlyAttachedToCore(AssemblyPartFocus ownerPartFocus)
    {
        if (ownerPartFocus == null || ownerPartFocus.SourcePart == null)
        {
            return false;
        }

        if (ownerPartFocus.SourcePart.partType == PartType.Pipe)
        {
            return false;
        }

        ArtificialSatellite ownerSatellite = ownerPartFocus.OwnerSatellite;
        AssemblyPartPortLayout ownerLayout = ownerPartFocus.GetComponent<AssemblyPartPortLayout>();
        if (ownerLayout == null)
        {
            ownerLayout = ownerPartFocus.GetComponentInChildren<AssemblyPartPortLayout>(true);
        }

        if (ownerSatellite == null || ownerLayout == null)
        {
            return false;
        }

        AssemblyCorePortLayout[] coreLayouts = ownerSatellite.GetComponentsInChildren<AssemblyCorePortLayout>(true);
        const float CorePortMatchDistanceSqr = 0.0004f;

        List<AssemblyPartPortLayout.PortEntry> ownerPorts = ownerLayout.Ports;
        for (int i = 0; i < ownerPorts.Count; i++)
        {
            AssemblyPartPortLayout.PortEntry ownerPort = ownerPorts[i];
            if (ownerPort == null || ownerPort.portType != AssemblyPortType.Input || ownerPort.portTransform == null)
            {
                continue;
            }

            Vector2 ownerInputLocal = ToSatelliteLocalPoint(ownerSatellite, ownerPort.portTransform);
            for (int layoutIndex = 0; layoutIndex < coreLayouts.Length; layoutIndex++)
            {
                AssemblyCorePortLayout coreLayout = coreLayouts[layoutIndex];
                if (coreLayout == null)
                {
                    continue;
                }

                List<AssemblyCorePortLayout.OutputPortEntry> corePorts = coreLayout.OutputPorts;
                for (int portIndex = 0; portIndex < corePorts.Count; portIndex++)
                {
                    AssemblyCorePortLayout.OutputPortEntry corePort = corePorts[portIndex];
                    if (corePort == null || corePort.portTransform == null)
                    {
                        continue;
                    }

                    Vector2 coreOutputLocal = ToSatelliteLocalPoint(ownerSatellite, corePort.portTransform);
                    if ((ownerInputLocal - coreOutputLocal).sqrMagnitude <= CorePortMatchDistanceSqr)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Vector2 ToSatelliteLocalPoint(ArtificialSatellite ownerSatellite, Transform target)
    {
        if (ownerSatellite == null || target == null)
        {
            return Vector2.zero;
        }

        Vector3 localPoint = ownerSatellite.transform.InverseTransformPoint(target.position);
        return new Vector2(localPoint.x, localPoint.y);
    }

    private static Dictionary<Vector2Int, List<Vector2Int>> BuildPipeAdjacency(HashSet<Vector2Int> pipeCells)
    {
        Dictionary<Vector2Int, List<Vector2Int>> adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (Vector2Int cell in pipeCells)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int neighbor = cell + CardinalDirections[i];
                if (pipeCells.Contains(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            adjacency[cell] = neighbors;
        }

        return adjacency;
    }

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition)
    {
        int gridCenter = GridSize / 2;
        return new Vector2Int(
            AssemblyMathUtility.QuantizeToCellIndex(localPosition.x / CellSize) + gridCenter,
            AssemblyMathUtility.QuantizeToCellIndex(localPosition.y / CellSize) + gridCenter);
    }

    private static Vector3 GridToLocalPosition(Vector2Int gridPosition, float z)
    {
        int gridCenter = GridSize / 2;
        float x = (gridPosition.x - gridCenter) * CellSize;
        float y = (gridPosition.y - gridCenter) * CellSize;
        return new Vector3(x, y, z);
    }

    private static bool TryDecomposeResource(InventoryResourceType resourceType, out int familyIndex, out int phaseIndex)
    {
        familyIndex = 0;
        phaseIndex = 0;

        string[] tokens = resourceType.ToString().Split('_');
        if (tokens.Length != 2)
        {
            return false;
        }

        familyIndex = Array.IndexOf(FamilyNames, tokens[0]);
        phaseIndex = Array.IndexOf(PhaseNames, tokens[1]);
        return familyIndex >= 0 && phaseIndex >= 0;
    }

    private static InventoryResourceType ComposeResource(int familyIndex, int phaseIndex, InventoryResourceType fallback)
    {
        familyIndex = PositiveModulo(familyIndex, FamilyNames.Length);
        phaseIndex = Mathf.Clamp(phaseIndex, 0, PhaseNames.Length - 1);
        string resourceName = FamilyNames[familyIndex] + "_" + PhaseNames[phaseIndex];
        return Enum.TryParse(resourceName, out InventoryResourceType parsedType)
            ? parsedType
            : fallback;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        if (result < 0)
        {
            result += modulo;
        }

        return result;
    }
}
