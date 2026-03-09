using UnityEngine;

public partial class Assembly
{
    private void CacheGhostRenderers()
    {
        ghostRenderers.Clear();
        ghostPropertyBlocks.Clear();

        if (partGhost == null) return;

        Renderer[] renderers = partGhost.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            ghostRenderers.Add(renderers[i]);
            ghostPropertyBlocks.Add(new MaterialPropertyBlock());
        }
    }

    private void SetGhostPlacementVisual(bool isValidPlacement)
    {
        Color targetColor = isValidPlacement ? GhostValidColor : GhostInvalidColor;

        for (int i = 0; i < ghostRenderers.Count; i++)
        {
            Renderer renderer = ghostRenderers[i];
            if (renderer == null) continue;
            if (renderer.GetComponentInParent<AssemblyPortVisualMarker>() != null) continue;
            if (ShouldKeepGhostRendererOriginalColor(renderer.transform)) continue;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null) continue;

            bool hasBaseColor = sharedMaterial.HasProperty(BaseColorId);
            bool hasColor = sharedMaterial.HasProperty(ColorId);
            if (!hasBaseColor && !hasColor) continue;

            MaterialPropertyBlock block = ghostPropertyBlocks[i];
            renderer.GetPropertyBlock(block);
            if (hasBaseColor) block.SetColor(BaseColorId, targetColor);
            if (hasColor) block.SetColor(ColorId, targetColor);
            renderer.SetPropertyBlock(block);
        }
    }

    private static bool ShouldKeepGhostRendererOriginalColor(Transform target)
    {
        if (target == null) return false;
        if (ContainsNameTokenInHierarchy(target, "Port")) return true;
        if (ContainsNameTokenInHierarchy(target, "Dock")) return true;
        return false;
    }

    private void SetRendererColorRecursive(Transform root, Color color)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, color);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    private void SetRendererAlphaRecursive(Transform root, float alpha)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Color baseColor = Color.white;
            if (renderer.sharedMaterial.HasProperty(BaseColorId))
            {
                baseColor = renderer.sharedMaterial.GetColor(BaseColorId);
            }
            else if (renderer.sharedMaterial.HasProperty(ColorId))
            {
                baseColor = renderer.sharedMaterial.GetColor(ColorId);
            }

            Color updated = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, updated);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, updated);
            renderer.SetPropertyBlock(block);
        }
    }

    private void EnsureRendererTransparencyRecursive(Transform root)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Material runtimeMaterial = renderer.material;
            if (runtimeMaterial == null) continue;

            if (runtimeMaterial.HasProperty("_Surface")) runtimeMaterial.SetFloat("_Surface", 1f);
            if (runtimeMaterial.HasProperty("_ZWrite")) runtimeMaterial.SetFloat("_ZWrite", 0f);
            if (runtimeMaterial.HasProperty("_SrcBlend")) runtimeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (runtimeMaterial.HasProperty("_DstBlend")) runtimeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (runtimeMaterial.HasProperty("_AlphaClip")) runtimeMaterial.SetFloat("_AlphaClip", 0f);

            runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
            runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            runtimeMaterial.SetOverrideTag("RenderType", "Transparent");
        }
    }
}
