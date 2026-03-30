using UnityEngine;

public static class SolarTurbineUtility
{
    public const string SolarTurbinePartName = "Solar Turbine";
    public const float DefaultGenerationSeconds = 10f;
    public const float DefaultWindIntensity = 1f;

    public static bool IsSolarTurbinePart(AssemblyPartFocus partFocus)
    {
        return partFocus != null && IsSolarTurbinePart(partFocus.SourcePart);
    }

    public static bool IsSolarTurbinePart(Part part)
    {
        return part != null && IsSolarTurbinePart(part.partName);
    }

    public static bool IsSolarTurbinePart(string partName)
    {
        return string.Equals(partName, SolarTurbinePartName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static float ResolveWindPowerRatio(Part part)
    {
        return part != null ? Mathf.Max(0f, part.windPowerRatio) : 0f;
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