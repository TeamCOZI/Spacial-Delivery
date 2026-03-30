using UnityEngine;

public static class SolarPanelUtility
{
    public const string SolarPanelPartName = "Solar Panel";
    public const float DefaultGenerationSeconds = 10f;

    public static bool IsSolarPanelPart(AssemblyPartFocus partFocus)
    {
        return partFocus != null && IsSolarPanelPart(partFocus.SourcePart);
    }

    public static bool IsSolarPanelPart(Part part)
    {
        return part != null && IsSolarPanelPart(part.partName);
    }

    public static bool IsSolarPanelPart(string partName)
    {
        return string.Equals(partName, SolarPanelPartName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static float ResolveLightPowerRatio(Part part)
    {
        return part != null ? Mathf.Max(0f, part.lightPowerRatio) : 0f;
    }

    public static float ResolveGenerationSeconds(Part part)
    {
        if (part == null)
        {
            return DefaultGenerationSeconds;
        }

        if (part.generationTime > 0f)
        {
            return part.generationTime;
        }

        return DefaultGenerationSeconds;
    }
}
