using System;
using UnityEngine;

public static class ModulePartInventoryUtility
{
    private const string LauncherPartName = "Launcher";

    public static bool UsesSplitInventories(Part part)
    {
        if (part == null)
        {
            return false;
        }

        if (part.partType == PartType.Core || part.partType == PartType.Pipe)
        {
            return false;
        }

        return !string.Equals(part.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsurePartInventories(GameObject ownerObject, Part part)
    {
        if (ownerObject == null || part == null)
        {
            return;
        }

        if (!UsesSplitInventories(part))
        {
            StructureResourceInventory inventory = GetOrCreateInventory(ownerObject, part, StructureResourceInventoryKind.General);
            inventory.InitializeForPart(part, StructureResourceInventoryKind.General);
            return;
        }

        _ = GetOrCreateInventory(ownerObject, part, StructureResourceInventoryKind.Input);
        if (PowerGeneratorRecipeCatalog.IsGeneratorPart(part))
        {
            _ = ComponentUtility.GetOrAddComponent<PowerGeneratorState>(ownerObject);
            return;
        }

        _ = GetOrCreateInventory(ownerObject, part, StructureResourceInventoryKind.Output);
        _ = ComponentUtility.GetOrAddComponent<ModuleProcessingState>(ownerObject);
    }

    public static bool TryResolveDisplayedInventory(AssemblyPartFocus partFocus, out StructureResourceInventory inventory)
    {
        inventory = null;
        if (partFocus == null || partFocus.SourcePart == null)
        {
            return false;
        }

        if (!UsesSplitInventories(partFocus.SourcePart))
        {
            return TryResolvePartInventory(partFocus, StructureResourceInventoryKind.General, out inventory);
        }

        _ = TryResolvePartInventory(partFocus, StructureResourceInventoryKind.Input, out StructureResourceInventory inputInventory);
        _ = TryResolvePartInventory(partFocus, StructureResourceInventoryKind.Output, out StructureResourceInventory outputInventory);

        if (outputInventory != null && outputInventory.TotalAmount > 0)
        {
            inventory = outputInventory;
            return true;
        }

        if (inputInventory != null && inputInventory.TotalAmount > 0)
        {
            inventory = inputInventory;
            return true;
        }

        if (outputInventory != null)
        {
            inventory = outputInventory;
            return true;
        }

        inventory = inputInventory;
        return inventory != null;
    }

    public static bool TryResolveInputInventory(AssemblyPartFocus partFocus, out StructureResourceInventory inventory)
    {
        return TryResolvePartInventory(partFocus, StructureResourceInventoryKind.Input, out inventory);
    }

    public static bool TryResolveOutputInventory(AssemblyPartFocus partFocus, out StructureResourceInventory inventory)
    {
        return TryResolvePartInventory(partFocus, StructureResourceInventoryKind.Output, out inventory);
    }

    public static bool TryResolveOutputInventory(AssemblyOutputPortFocus outputPort, out StructureResourceInventory inventory)
    {
        inventory = null;
        if (outputPort == null)
        {
            return false;
        }

        return TryResolveOutputInventory(outputPort.OwnerPartFocus, out inventory);
    }

    public static bool TryResolvePartInventory(
        AssemblyPartFocus partFocus,
        StructureResourceInventoryKind kind,
        out StructureResourceInventory inventory)
    {
        inventory = null;
        if (partFocus == null || partFocus.SourcePart == null)
        {
            return false;
        }

        Part sourcePart = partFocus.SourcePart;
        if (UsesSplitInventories(sourcePart))
        {
            if (kind == StructureResourceInventoryKind.General)
            {
                return false;
            }

            if (PowerGeneratorRecipeCatalog.IsGeneratorPart(sourcePart))
            {
                if (kind != StructureResourceInventoryKind.Input)
                {
                    return false;
                }

                _ = ComponentUtility.GetOrAddComponent<PowerGeneratorState>(partFocus.gameObject);
                inventory = GetOrCreateInventory(partFocus.gameObject, sourcePart, StructureResourceInventoryKind.Input);
                return inventory != null;
            }

            _ = ComponentUtility.GetOrAddComponent<ModuleProcessingState>(partFocus.gameObject);
            inventory = GetOrCreateInventory(partFocus.gameObject, sourcePart, kind);
            return inventory != null;
        }

        if (kind != StructureResourceInventoryKind.General)
        {
            return false;
        }

        inventory = GetOrCreateInventory(partFocus.gameObject, sourcePart, StructureResourceInventoryKind.General);
        return inventory != null;
    }

    private static StructureResourceInventory GetOrCreateInventory(
        GameObject ownerObject,
        Part part,
        StructureResourceInventoryKind kind)
    {
        if (ownerObject == null || part == null)
        {
            return null;
        }

        StructureResourceInventory[] inventories = ownerObject.GetComponents<StructureResourceInventory>();
        for (int i = 0; i < inventories.Length; i++)
        {
            StructureResourceInventory candidate = inventories[i];
            if (candidate == null || candidate.InventoryKind != kind)
            {
                continue;
            }

            candidate.InitializeForPart(part, kind);
            return candidate;
        }

        StructureResourceInventory inventory = ownerObject.AddComponent<StructureResourceInventory>();
        inventory.InitializeForPart(part, kind);
        return inventory;
    }
}