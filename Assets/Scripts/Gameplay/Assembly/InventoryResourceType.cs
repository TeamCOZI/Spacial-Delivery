using System;
using System.Collections.Generic;
using UnityEngine;

public enum InventoryResourceType
{
    Activator,
    Adaptive_Thermostat,
    Advanced_Fuel,
    Advanced_Fuel_Catalyst,
    Advanced_Nuclear_Fusion_Catalyst,
    Aquid_Crystal,
    Aquid_Gas,
    Aquid_Liquid,
    Beam,
    Circuit,
    Catalyst,
    Cold_Resistance,
    Communication_Chip,
    Computer,
    Conductive_Thermostat,
    Compound,
    Computing_Circuit,
    Control_Circuit,
    Cooler,
    Device,
    Engine,
    Framework,
    Fuel,
    Fuel_Catalyst,
    Gear,
    Hard_Plate,
    Heater,
    Heat_Resistance,
    Heavy_Plate,
    Improved_Cold_Resistance,
    Improved_Fuel,
    Improved_Heat_Resistance,
    Improved_Heavy_Plate,
    Motor,
    Nitain_Crystal,
    Nitain_Gas,
    Nitain_Ingot,
    Nitain_Liquid,
    Nuclear_Fusion_Catalyst,
    Pipe,
    Plate,
    Power_Saving_Fluid,
    Pump,
    Rotator,
    Screw,
    Sensing_Circuit,
    Solar_Fuel_A,
    Solar_Fuel_B,
    Solar_Fuel_C,
    Solvent,
    Tank,
    Terminal,
    Territe_Crystal,
    Territe_Gas,
    Territe_Ingot,
    Territe_Liquid,
    Thermoelectric_Fluid,
    Transmission,
    Wire,
}

public static class InventoryResourceCatalog
{
    private const string ResourceSpriteFolder = "Sprites/";

    private static readonly InventoryResourceType[] all = (InventoryResourceType[])Enum.GetValues(typeof(InventoryResourceType));
    private static readonly Dictionary<InventoryResourceType, Sprite> cachedSlotSprites = new Dictionary<InventoryResourceType, Sprite>();

    public static InventoryResourceType[] All => all;

    public static string GetDisplayName(InventoryResourceType resourceType)
    {
        return resourceType.ToString();
    }

    public static string GetCompactLabel(InventoryResourceType resourceType)
    {
        return GetDisplayName(resourceType).Replace("_", "\n");
    }

    public static Sprite GetSlotSprite(InventoryResourceType resourceType)
    {
        if (cachedSlotSprites.TryGetValue(resourceType, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        string resourceName = GetDisplayName(resourceType);
        Texture2D texture = Resources.Load<Texture2D>(ResourceSpriteFolder + resourceName);
        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = resourceName + "_RuntimeSprite";
        cachedSlotSprites[resourceType] = sprite;
        return sprite;
    }
}