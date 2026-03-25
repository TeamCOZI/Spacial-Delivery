using System;
using System.Collections.Generic;

public struct ModuleRecipeDefinition
{
    public InventoryResourceType inputResourceType;
    public int inputAmount;
    public InventoryResourceType outputResourceType;
    public int outputAmount;
    public float processSeconds;

    public ModuleRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        InventoryResourceType outputResourceType,
        int outputAmount,
        float processSeconds)
    {
        this.inputResourceType = inputResourceType;
        this.inputAmount = inputAmount;
        this.outputResourceType = outputResourceType;
        this.outputAmount = outputAmount;
        this.processSeconds = processSeconds;
    }
}

public static class ModuleRecipeCatalog
{
    private static readonly ModuleRecipeDefinition[] emptyRecipes = Array.Empty<ModuleRecipeDefinition>();
    private static readonly ModuleRecipeDefinition[] heaterRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Crystal, 1, InventoryResourceType.Territe_Liquid, 2, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Liquid, 1, InventoryResourceType.Territe_Gas, 2, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Crystal, 1, InventoryResourceType.Aquid_Liquid, 2, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Liquid, 1, InventoryResourceType.Aquid_Gas, 2, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Crystal, 1, InventoryResourceType.Nitain_Liquid, 2, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Liquid, 1, InventoryResourceType.Nitain_Gas, 2, 1f),
    };
    private static readonly ModuleRecipeDefinition[] coolerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Liquid, 2, InventoryResourceType.Territe_Crystal, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Gas, 2, InventoryResourceType.Territe_Liquid, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Liquid, 2, InventoryResourceType.Aquid_Crystal, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Gas, 2, InventoryResourceType.Aquid_Liquid, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Liquid, 2, InventoryResourceType.Nitain_Crystal, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Gas, 2, InventoryResourceType.Nitain_Liquid, 1, 1f),
    };
    private static readonly ModuleRecipeDefinition[] refinerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Crystal, 1, InventoryResourceType.Territe_Ingot, 1, 1f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Crystal, 1, InventoryResourceType.Nitain_Ingot, 1, 1f),
    };
    private static readonly ModuleRecipeDefinition[] processorRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Ingot, 2, InventoryResourceType.Beam, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Ingot, 2, InventoryResourceType.Plate, 1, 2f),
    };

    public static IReadOnlyList<ModuleRecipeDefinition> GetRecipes(AssemblyPartFocus partFocus)
    {
        return partFocus != null && partFocus.SourcePart != null
            ? GetRecipes(partFocus.SourcePart.partName)
            : emptyRecipes;
    }

    public static IReadOnlyList<ModuleRecipeDefinition> GetRecipes(Part part)
    {
        return part != null ? GetRecipes(part.partName) : emptyRecipes;
    }

    public static IReadOnlyList<ModuleRecipeDefinition> GetRecipes(string partName)
    {
        if (string.Equals(partName, "Heater", StringComparison.OrdinalIgnoreCase))
        {
            return heaterRecipes;
        }

        if (string.Equals(partName, "Cooler", StringComparison.OrdinalIgnoreCase))
        {
            return coolerRecipes;
        }

        if (string.Equals(partName, "Refiner", StringComparison.OrdinalIgnoreCase))
        {
            return refinerRecipes;
        }

        if (string.Equals(partName, "Processor", StringComparison.OrdinalIgnoreCase))
        {
            return processorRecipes;
        }

        return emptyRecipes;
    }
}



