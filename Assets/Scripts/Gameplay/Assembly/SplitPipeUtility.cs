using UnityEngine;

public static class SplitPipeUtility
{
    private const string SplitPipeName = "Split Pipe";

    public static bool IsSplitPipePart(Part targetPart)
    {
        return targetPart != null
            && targetPart.partType == PartType.Pipe
            && !string.IsNullOrWhiteSpace(targetPart.partName)
            && string.Equals(targetPart.partName, SplitPipeName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static SplitPipeState ResolveState(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        SplitPipeState state = root.GetComponent<SplitPipeState>();
        if (state != null)
        {
            return state;
        }

        AssemblyPartFocus focus = root.GetComponent<AssemblyPartFocus>();
        if (focus == null || !IsSplitPipePart(focus.SourcePart))
        {
            return null;
        }

        return ComponentUtility.GetOrAddComponent<SplitPipeState>(root);
    }
}
