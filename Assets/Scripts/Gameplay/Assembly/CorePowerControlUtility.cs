using UnityEngine;

public static class CorePowerControlUtility
{
    private const string CorePowerControlStructureName = "Core Power Control";

    public static bool IsCorePowerControlStructure(Structure structure)
    {
        return structure != null
            && !string.IsNullOrWhiteSpace(structure.structureName)
            && string.Equals(structure.structureName, CorePowerControlStructureName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCorePowerControlFocus(StructureFocus structureFocus)
    {
        return structureFocus != null && IsCorePowerControlStructure(structureFocus.SourceStructure);
    }

    public static CorePowerControlState ResolveState(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        StructureFocus structureFocus = root.GetComponent<StructureFocus>();
        if (structureFocus == null)
        {
            structureFocus = root.GetComponentInParent<StructureFocus>();
        }

        if (!IsCorePowerControlFocus(structureFocus))
        {
            return null;
        }

        CorePowerControlState state = structureFocus.GetComponent<CorePowerControlState>();
        return state != null ? state : ComponentUtility.GetOrAddComponent<CorePowerControlState>(structureFocus.gameObject);
    }
}
