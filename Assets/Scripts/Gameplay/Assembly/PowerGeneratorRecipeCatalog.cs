using System;
using System.Collections.Generic;

public struct PowerGeneratorRecipeDefinition
{
    public InventoryResourceType inputResourceType;
    public int inputAmount;
    public int powerAmount;
    public float generationSeconds;

    public PowerGeneratorRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        int powerAmount,
        float generationSeconds)
    {
        this.inputResourceType = inputResourceType;
        this.inputAmount = inputAmount;
        this.powerAmount = powerAmount;
        this.generationSeconds = generationSeconds;
    }
}

public static class PowerGeneratorRecipeCatalog
{
    private static readonly PowerGeneratorRecipeDefinition[] emptyRecipes = Array.Empty<PowerGeneratorRecipeDefinition>();
    private static readonly PowerGeneratorRecipeDefinition[] nitainPowerGeneratorRecipes =
    {
        new PowerGeneratorRecipeDefinition(InventoryResourceType.Nitain_Crystal, 1, 20, 10f),
        new PowerGeneratorRecipeDefinition(InventoryResourceType.Nitain_Crystal, 1, 10, 10f),
        new PowerGeneratorRecipeDefinition(InventoryResourceType.Nitain_Gas, 1, 5, 10f),
    };

    public static bool IsGeneratorPart(Part part)
    {
        return part != null && IsGeneratorPart(part.partName);
    }

    public static bool IsGeneratorPart(AssemblyPartFocus partFocus)
    {
        return partFocus != null && IsGeneratorPart(partFocus.SourcePart);
    }

    public static bool IsGeneratorPart(string partName)
    {
        return string.Equals(partName, "Nitain Power Generator", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<PowerGeneratorRecipeDefinition> GetRecipes(AssemblyPartFocus partFocus)
    {
        return partFocus != null && partFocus.SourcePart != null
            ? GetRecipes(partFocus.SourcePart.partName)
            : emptyRecipes;
    }

    public static IReadOnlyList<PowerGeneratorRecipeDefinition> GetRecipes(Part part)
    {
        return part != null ? GetRecipes(part.partName) : emptyRecipes;
    }

    public static IReadOnlyList<PowerGeneratorRecipeDefinition> GetRecipes(string partName)
    {
        if (string.Equals(partName, "Nitain Power Generator", StringComparison.OrdinalIgnoreCase))
        {
            return nitainPowerGeneratorRecipes;
        }

        return emptyRecipes;
    }
}