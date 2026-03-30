using System;

public static class AssemblyPortTypeUtility
{
    public static bool IsInputCompatible(AssemblyPortType portType)
    {
        return portType == AssemblyPortType.Input || portType == AssemblyPortType.Neutral;
    }

    public static bool IsOutputCompatible(AssemblyPortType portType)
    {
        return portType == AssemblyPortType.Output || portType == AssemblyPortType.Neutral;
    }

    public static string GetDisplayName(AssemblyPortType portType)
    {
        switch (portType)
        {
            case AssemblyPortType.Input:
                return "Input";
            case AssemblyPortType.Output:
                return "Output";
            case AssemblyPortType.Neutral:
                return "Neutral";
            default:
                return portType.ToString();
        }
    }
}
