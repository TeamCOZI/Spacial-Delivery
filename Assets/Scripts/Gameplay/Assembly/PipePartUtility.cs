using System;

public static class PipePartUtility
{
    private const string StandardPipeName = "Pipe";

    public static bool IsPipePart(Part targetPart)
    {
        return targetPart != null && targetPart.partType == PartType.Pipe;
    }

    public static bool UsesFixedPipePorts(Part targetPart)
    {
        return IsPipePart(targetPart) && targetPart.usesFixedPipePorts;
    }

    public static bool UsesPathPlacement(Part targetPart)
    {
        return IsPipePart(targetPart) && !UsesFixedPipePorts(targetPart);
    }

    public static bool IsStandardPipePart(Part targetPart)
    {
        return UsesPathPlacement(targetPart)
            && !string.IsNullOrWhiteSpace(targetPart.partName)
            && string.Equals(targetPart.partName, StandardPipeName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanReplaceInstalledPipe(Part targetPart)
    {
        return UsesFixedPipePorts(targetPart);
    }
}
