using UnityEngine;

public static class WarehouseUtility
{
    private const string WarehousePartName = "Warehouse";
    private const float DefaultTransitCoreTimeSeconds = 10f;

    public static bool IsWarehousePart(Part targetPart)
    {
        return targetPart != null
            && !string.IsNullOrWhiteSpace(targetPart.partName)
            && string.Equals(targetPart.partName, WarehousePartName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static Part ResolvePart()
    {
        if (GameplayRuntimeAccess.TryGetPartDb(out PartDB partDb))
        {
            Part resolvedPart = partDb.GetPartByName(WarehousePartName);
            if (resolvedPart != null)
            {
                return resolvedPart;
            }
        }

        return Resources.Load<Part>("Parts/Warehouse");
    }

    public static WarehouseState ResolveState(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        AssemblyPartFocus focus = root.GetComponent<AssemblyPartFocus>();
        if (focus == null)
        {
            focus = root.GetComponentInParent<AssemblyPartFocus>();
        }

        if (focus == null || !IsWarehousePart(focus.SourcePart))
        {
            return null;
        }

        WarehouseState state = focus.GetComponent<WarehouseState>();
        return state != null ? state : ComponentUtility.GetOrAddComponent<WarehouseState>(focus.gameObject);
    }

    public static float ResolveTransitCoreTimeSeconds(Part sourcePart)
    {
        return Mathf.Max(0.01f, sourcePart != null && sourcePart.transitCoreTime > 0f
            ? sourcePart.transitCoreTime
            : DefaultTransitCoreTimeSeconds);
    }
}
