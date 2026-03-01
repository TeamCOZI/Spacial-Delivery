using UnityEngine;

public static class SmallScaleLayerUtility
{
    public const string LayerName = "SmallScale";

    private static int cachedLayer = int.MinValue;

    public static int GetLayer()
    {
        if (cachedLayer != int.MinValue) return cachedLayer;

        cachedLayer = LayerMask.NameToLayer(LayerName);
        return cachedLayer;
    }

    public static void ApplyRecursively(Transform root)
    {
        int layer = GetLayer();
        if (layer < 0 || root == null) return;

        ApplyRecursively(root, layer);
    }

    private static void ApplyRecursively(Transform current, int layer)
    {
        if (current == null) return;
        current.gameObject.layer = layer;

        for (int i = 0; i < current.childCount; i++)
        {
            ApplyRecursively(current.GetChild(i), layer);
        }
    }
}
