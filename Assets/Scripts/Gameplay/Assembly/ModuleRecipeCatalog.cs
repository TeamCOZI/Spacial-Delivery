using System;
using System.Collections.Generic;

public struct ModuleRecipeDefinition
{
    public InventoryResourceType inputResourceType;
    public int inputAmount;
    public InventoryResourceType secondaryInputResourceType;
    public int secondaryInputAmount;
    public InventoryResourceType tertiaryInputResourceType;
    public int tertiaryInputAmount;
    public InventoryResourceType quaternaryInputResourceType;
    public int quaternaryInputAmount;
    public InventoryResourceType outputResourceType;
    public int outputAmount;
    public float processSeconds;

    public bool HasSecondaryInput => secondaryInputAmount > 0;
    public bool HasTertiaryInput => tertiaryInputAmount > 0;
    public bool HasQuaternaryInput => quaternaryInputAmount > 0;

    public ModuleRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        InventoryResourceType outputResourceType,
        int outputAmount,
        float processSeconds)
        : this(inputResourceType, inputAmount, default, 0, default, 0, default, 0, outputResourceType, outputAmount, processSeconds)
    {
    }

    public ModuleRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        InventoryResourceType secondaryInputResourceType,
        int secondaryInputAmount,
        InventoryResourceType outputResourceType,
        int outputAmount,
        float processSeconds)
        : this(inputResourceType, inputAmount, secondaryInputResourceType, secondaryInputAmount, default, 0, default, 0, outputResourceType, outputAmount, processSeconds)
    {
    }

    public ModuleRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        InventoryResourceType secondaryInputResourceType,
        int secondaryInputAmount,
        InventoryResourceType tertiaryInputResourceType,
        int tertiaryInputAmount,
        InventoryResourceType outputResourceType,
        int outputAmount,
        float processSeconds)
        : this(inputResourceType, inputAmount, secondaryInputResourceType, secondaryInputAmount, tertiaryInputResourceType, tertiaryInputAmount, default, 0, outputResourceType, outputAmount, processSeconds)
    {
    }

    public ModuleRecipeDefinition(
        InventoryResourceType inputResourceType,
        int inputAmount,
        InventoryResourceType secondaryInputResourceType,
        int secondaryInputAmount,
        InventoryResourceType tertiaryInputResourceType,
        int tertiaryInputAmount,
        InventoryResourceType quaternaryInputResourceType,
        int quaternaryInputAmount,
        InventoryResourceType outputResourceType,
        int outputAmount,
        float processSeconds)
    {
        this.inputResourceType = inputResourceType;
        this.inputAmount = inputAmount;
        this.secondaryInputResourceType = secondaryInputResourceType;
        this.secondaryInputAmount = secondaryInputAmount;
        this.tertiaryInputResourceType = tertiaryInputResourceType;
        this.tertiaryInputAmount = tertiaryInputAmount;
        this.quaternaryInputResourceType = quaternaryInputResourceType;
        this.quaternaryInputAmount = quaternaryInputAmount;
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
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Crystal, 1, InventoryResourceType.Territe_Liquid, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Liquid, 1, InventoryResourceType.Territe_Gas, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Crystal, 1, InventoryResourceType.Aquid_Liquid, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Liquid, 1, InventoryResourceType.Aquid_Gas, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Crystal, 1, InventoryResourceType.Nitain_Liquid, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Liquid, 1, InventoryResourceType.Nitain_Gas, 2, 2f),
    };

    private static readonly ModuleRecipeDefinition[] coolerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Liquid, 2, InventoryResourceType.Territe_Crystal, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Gas, 2, InventoryResourceType.Territe_Liquid, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Liquid, 2, InventoryResourceType.Aquid_Crystal, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Gas, 2, InventoryResourceType.Aquid_Liquid, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Liquid, 2, InventoryResourceType.Nitain_Crystal, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Gas, 2, InventoryResourceType.Nitain_Liquid, 1, 2f),
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

    private static readonly ModuleRecipeDefinition[] molderRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Ingot, 1, InventoryResourceType.Screw, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Ingot, 1, InventoryResourceType.Pipe, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Ingot, 1, InventoryResourceType.Gear, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Ingot, 1, InventoryResourceType.Device, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Ingot, 1, InventoryResourceType.Wire, 1, 2f),
    };

    private static readonly ModuleRecipeDefinition[] purifierRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Territe_Liquid, 1, InventoryResourceType.Activator, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Aquid_Liquid, 1, InventoryResourceType.Solvent, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Nitain_Liquid, 1, InventoryResourceType.Catalyst, 2, 2f),
    };

    private static readonly ModuleRecipeDefinition[] assemblerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 1, InventoryResourceType.Screw, 2, InventoryResourceType.Hard_Plate, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 1, InventoryResourceType.Gear, 2, InventoryResourceType.Transmission, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Beam, 1, InventoryResourceType.Screw, 2, InventoryResourceType.Framework, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Beam, 1, InventoryResourceType.Gear, 2, InventoryResourceType.Rotator, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 2, InventoryResourceType.Device, 2, InventoryResourceType.Circuit, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 1, InventoryResourceType.Wire, 2, InventoryResourceType.Terminal, 2, 2f),
    };

    private static readonly ModuleRecipeDefinition[] precisionAssemblerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 1, InventoryResourceType.Device, 2, InventoryResourceType.Circuit, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Plate, 1, InventoryResourceType.Wire, 2, InventoryResourceType.Terminal, 2, 2f),
    };

    private static readonly ModuleRecipeDefinition[] manufacturerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Hard_Plate, 2, InventoryResourceType.Framework, 2, InventoryResourceType.Screw, 8, InventoryResourceType.Heavy_Plate, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Rotator, 2, InventoryResourceType.Transmission, 2, InventoryResourceType.Gear, 8, InventoryResourceType.Motor, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Tank, 2, InventoryResourceType.Rotator, 2, InventoryResourceType.Pipe, 8, InventoryResourceType.Pump, 1, 2f),
    };

    private static readonly ModuleRecipeDefinition[] preciseManufacturerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Hard_Plate, 2, InventoryResourceType.Screw, 8, InventoryResourceType.Circuit, 4, InventoryResourceType.Computing_Circuit, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Hard_Plate, 2, InventoryResourceType.Screw, 8, InventoryResourceType.Terminal, 4, InventoryResourceType.Sensing_Circuit, 1, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Hard_Plate, 2, InventoryResourceType.Circuit, 4, InventoryResourceType.Terminal, 4, InventoryResourceType.Control_Circuit, 1, 2f),
    };    private static readonly ModuleRecipeDefinition[] mergerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Heavy_Plate, 2, InventoryResourceType.Hard_Plate, 4, InventoryResourceType.Plate, 4, InventoryResourceType.Screw, 8, InventoryResourceType.Improved_Heavy_Plate, 1, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Motor, 2, InventoryResourceType.Pump, 2, InventoryResourceType.Sensing_Circuit, 2, InventoryResourceType.Control_Circuit, 2, InventoryResourceType.Engine, 1, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Heavy_Plate, 2, InventoryResourceType.Computing_Circuit, 2, InventoryResourceType.Sensing_Circuit, 2, InventoryResourceType.Screw, 8, InventoryResourceType.Computer, 1, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Heavy_Plate, 2, InventoryResourceType.Sensing_Circuit, 2, InventoryResourceType.Control_Circuit, 2, InventoryResourceType.Screw, 8, InventoryResourceType.Communication_Chip, 1, 8f),
    };

    private static readonly ModuleRecipeDefinition[] mixerRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Activator, 1, InventoryResourceType.Catalyst, 1, InventoryResourceType.Compound, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Catalyst, 1, InventoryResourceType.Nitain_Liquid, 1, InventoryResourceType.Fuel, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Catalyst, 1, InventoryResourceType.Aquid_Liquid, 1, InventoryResourceType.Solar_Fuel_A, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Solvent, 1, InventoryResourceType.Nitain_Liquid, 1, InventoryResourceType.Cold_Resistance, 2, 2f),
        new ModuleRecipeDefinition(InventoryResourceType.Solvent, 1, InventoryResourceType.Aquid_Liquid, 1, InventoryResourceType.Heat_Resistance, 2, 2f),
    };

    private static readonly ModuleRecipeDefinition[] reactorRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Fuel, 4, InventoryResourceType.Catalyst, 2, InventoryResourceType.Improved_Fuel, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Fuel, 2, InventoryResourceType.Catalyst, 4, InventoryResourceType.Fuel_Catalyst, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Solar_Fuel_A, 4, InventoryResourceType.Catalyst, 2, InventoryResourceType.Solar_Fuel_B, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Solar_Fuel_A, 2, InventoryResourceType.Catalyst, 4, InventoryResourceType.Nuclear_Fusion_Catalyst, 4, 4f),
    };

    private static readonly ModuleRecipeDefinition[] synthesisReactorRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Improved_Fuel, 8, InventoryResourceType.Compound, 4, InventoryResourceType.Catalyst, 4, InventoryResourceType.Advanced_Fuel, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Fuel_Catalyst, 8, InventoryResourceType.Compound, 4, InventoryResourceType.Catalyst, 4, InventoryResourceType.Advanced_Fuel_Catalyst, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Solar_Fuel_B, 8, InventoryResourceType.Compound, 4, InventoryResourceType.Catalyst, 4, InventoryResourceType.Solar_Fuel_C, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Nuclear_Fusion_Catalyst, 8, InventoryResourceType.Compound, 4, InventoryResourceType.Catalyst, 4, InventoryResourceType.Advanced_Nuclear_Fusion_Catalyst, 8, 8f),
    };

    private static readonly ModuleRecipeDefinition[] dissolverRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Cold_Resistance, 4, InventoryResourceType.Solvent, 2, InventoryResourceType.Improved_Cold_Resistance, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Cold_Resistance, 2, InventoryResourceType.Solvent, 4, InventoryResourceType.Heater, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Heat_Resistance, 4, InventoryResourceType.Solvent, 2, InventoryResourceType.Improved_Heat_Resistance, 4, 4f),
        new ModuleRecipeDefinition(InventoryResourceType.Heat_Resistance, 2, InventoryResourceType.Solvent, 4, InventoryResourceType.Cooler, 4, 4f),
    };

    private static readonly ModuleRecipeDefinition[] synthesisDissolverRecipes =
    {
        new ModuleRecipeDefinition(InventoryResourceType.Improved_Cold_Resistance, 8, InventoryResourceType.Compound, 4, InventoryResourceType.Solvent, 4, InventoryResourceType.Adaptive_Thermostat, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Heater, 16, InventoryResourceType.Compound, 4, InventoryResourceType.Solvent, 4, InventoryResourceType.Thermoelectric_Fluid, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Improved_Heat_Resistance, 16, InventoryResourceType.Compound, 4, InventoryResourceType.Solvent, 4, InventoryResourceType.Conductive_Thermostat, 8, 8f),
        new ModuleRecipeDefinition(InventoryResourceType.Cooler, 16, InventoryResourceType.Compound, 4, InventoryResourceType.Solvent, 4, InventoryResourceType.Power_Saving_Fluid, 8, 8f),
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
        if (string.Equals(partName, "Heater", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partName, "Furnace", StringComparison.OrdinalIgnoreCase))
        {
            return heaterRecipes;
        }

        if (string.Equals(partName, "Cooler", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partName, "Chiller", StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(partName, "Molder", StringComparison.OrdinalIgnoreCase))
        {
            return molderRecipes;
        }

        if (string.Equals(partName, "Purifier", StringComparison.OrdinalIgnoreCase))
        {
            return purifierRecipes;
        }

        if (string.Equals(partName, "Assembler", StringComparison.OrdinalIgnoreCase))
        {
            return assemblerRecipes;
        }
        if (string.Equals(partName, "Precision Assembler", StringComparison.OrdinalIgnoreCase))
        {
            return precisionAssemblerRecipes;
        }

        if (string.Equals(partName, "Manufacturer", StringComparison.OrdinalIgnoreCase))
        {
            return manufacturerRecipes;
        }

        if (string.Equals(partName, "Precise Manufacturer", StringComparison.OrdinalIgnoreCase))
        {
            return preciseManufacturerRecipes;
        }

        if (string.Equals(partName, "Merger", StringComparison.OrdinalIgnoreCase))
        {
            return mergerRecipes;
        }

        if (string.Equals(partName, "Mixer", StringComparison.OrdinalIgnoreCase))
        {
            return mixerRecipes;
        }

        if (string.Equals(partName, "Reactor", StringComparison.OrdinalIgnoreCase))
        {
            return reactorRecipes;
        }

        if (string.Equals(partName, "Synthesis Reactor", StringComparison.OrdinalIgnoreCase))
        {
            return synthesisReactorRecipes;
        }

        if (string.Equals(partName, "Dissolver", StringComparison.OrdinalIgnoreCase))
        {
            return dissolverRecipes;
        }

        if (string.Equals(partName, "Synthesis Dissolver", StringComparison.OrdinalIgnoreCase))
        {
            return synthesisDissolverRecipes;
        }

        return emptyRecipes;
    }
}