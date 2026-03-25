using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(34001)]
[DisallowMultipleComponent]
public class AssemblyFocusedPartHighlightPresenter : FocusEventSubscriber
{
    private const string OverlayObjectName = "__FocusedPartHighlight";
    private const string OutlineShaderName = "Unlit/Outline";
    private const string CoreFocusGridPlaneName = "CoreFocusGridPlane";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

    [SerializeField] private Color outlineColor = new Color(1f, 0.78f, 0.22f, 1f);
    [SerializeField, Range(0.01f, 0.2f)] private float outlineThickness = 0.05f;

    private AssemblyPartFocus highlightedPart;
    private Material outlineMaterial;
    private bool missingOutlineShaderLogged;

    protected override void HandleFocusChanged(Transform focused)
    {
        AssemblyPartFocus nextPart = ResolveFocusedSatellitePart(focused);
        if (highlightedPart == nextPart)
        {
            return;
        }

        ClearCurrentHighlight();
        highlightedPart = nextPart;
        EnsureCurrentHighlight();
    }

    protected override void LateUpdate()
    {
        if (highlightedPart == null)
        {
            return;
        }

        EnsureCurrentHighlight();
    }

    private void OnDestroy()
    {
        ClearCurrentHighlight();
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
            outlineMaterial = null;
        }
    }

    private void EnsureCurrentHighlight()
    {
        if (highlightedPart == null)
        {
            return;
        }

        if (HasHighlightOverlay(highlightedPart))
        {
            return;
        }

        if (!TryEnsureOutlineMaterial())
        {
            return;
        }

        CreateHighlightOverlay(highlightedPart);
    }

    private void CreateHighlightOverlay(AssemblyPartFocus partFocus)
    {
        if (partFocus == null)
        {
            return;
        }

        Renderer[] renderers = partFocus.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldHighlightRenderer(renderer))
            {
                continue;
            }

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedMeshOverlay(skinnedMeshRenderer);
                continue;
            }

            MeshRenderer meshRenderer = renderer as MeshRenderer;
            MeshFilter meshFilter = meshRenderer != null ? meshRenderer.GetComponent<MeshFilter>() : null;
            if (meshRenderer == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            CreateMeshOverlay(meshRenderer, meshFilter.sharedMesh);
        }
    }

    private void CreateMeshOverlay(MeshRenderer sourceRenderer, Mesh sharedMesh)
    {
        if (sourceRenderer == null || sharedMesh == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            OverlayObjectName,
            typeof(AssemblyFocusHighlightMarker),
            typeof(MeshFilter),
            typeof(MeshRenderer));
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.transform.localPosition = Vector3.zero;
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = Vector3.one;
        SmallScaleLayerUtility.ApplyRecursively(overlayObject.transform);

        MeshFilter overlayFilter = overlayObject.GetComponent<MeshFilter>();
        overlayFilter.sharedMesh = sharedMesh;

        MeshRenderer overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
        ConfigureOverlayRenderer(sourceRenderer, overlayRenderer, sharedMesh.subMeshCount);
    }

    private void CreateSkinnedMeshOverlay(SkinnedMeshRenderer sourceRenderer)
    {
        if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject(
            OverlayObjectName,
            typeof(AssemblyFocusHighlightMarker),
            typeof(SkinnedMeshRenderer));
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.transform.localPosition = Vector3.zero;
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = Vector3.one;
        SmallScaleLayerUtility.ApplyRecursively(overlayObject.transform);

        SkinnedMeshRenderer overlayRenderer = overlayObject.GetComponent<SkinnedMeshRenderer>();
        overlayRenderer.sharedMesh = sourceRenderer.sharedMesh;
        overlayRenderer.rootBone = sourceRenderer.rootBone;
        overlayRenderer.bones = sourceRenderer.bones;
        overlayRenderer.localBounds = sourceRenderer.localBounds;
        overlayRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        ConfigureOverlayRenderer(sourceRenderer, overlayRenderer, sourceRenderer.sharedMesh.subMeshCount);
    }

    private void ConfigureOverlayRenderer(Renderer sourceRenderer, Renderer overlayRenderer, int subMeshCount)
    {
        if (sourceRenderer == null || overlayRenderer == null || outlineMaterial == null)
        {
            return;
        }

        int sourceMaterialCount = sourceRenderer.sharedMaterials != null ? sourceRenderer.sharedMaterials.Length : 0;
        int materialCount = Mathf.Max(1, Mathf.Max(subMeshCount, sourceMaterialCount));
        Material[] overlayMaterials = new Material[materialCount];
        for (int i = 0; i < overlayMaterials.Length; i++)
        {
            overlayMaterials[i] = outlineMaterial;
        }

        overlayRenderer.sharedMaterials = overlayMaterials;
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        overlayRenderer.enabled = true;
    }

    private bool TryEnsureOutlineMaterial()
    {
        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            if (!missingOutlineShaderLogged)
            {
                Debug.LogWarning($"AssemblyFocusedPartHighlightPresenter: Shader '{OutlineShaderName}' is missing.");
                missingOutlineShaderLogged = true;
            }
            return false;
        }

        missingOutlineShaderLogged = false;

        if (outlineMaterial == null || outlineMaterial.shader != outlineShader)
        {
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }

            outlineMaterial = new Material(outlineShader)
            {
                name = "FocusedPartOutlineRuntime"
            };
            outlineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        outlineMaterial.SetColor(OutlineColorId, outlineColor);
        outlineMaterial.SetFloat(ThicknessId, outlineThickness);
        return true;
    }

    private void ClearCurrentHighlight()
    {
        if (highlightedPart == null)
        {
            return;
        }

        AssemblyFocusHighlightMarker[] markers = highlightedPart.GetComponentsInChildren<AssemblyFocusHighlightMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            AssemblyFocusHighlightMarker marker = markers[i];
            if (marker != null)
            {
                Destroy(marker.gameObject);
            }
        }

        highlightedPart = null;
    }

    private static AssemblyPartFocus ResolveFocusedSatellitePart(Transform focused)
    {
        if (focused == null)
        {
            return null;
        }

        if (focused.GetComponent<AssemblyOutputPortFocus>() != null)
        {
            return null;
        }

        if (focused.GetComponent<StructureFocus>() != null)
        {
            return null;
        }

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.OwnerSatellite == null)
        {
            return null;
        }

        return partFocus;
    }

    private static bool HasHighlightOverlay(AssemblyPartFocus partFocus)
    {
        if (partFocus == null)
        {
            return false;
        }

        return partFocus.GetComponentInChildren<AssemblyFocusHighlightMarker>(true) != null;
    }

    private static bool ShouldHighlightRenderer(Renderer renderer)
    {
        if (renderer == null || renderer.GetComponentInParent<AssemblyFocusHighlightMarker>(true) != null)
        {
            return false;
        }

        if (!renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (renderer.CompareTag("Icon") || string.Equals(renderer.name, "Icon", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(renderer.name, CoreFocusGridPlaneName, System.StringComparison.Ordinal))
        {
            return false;
        }

        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }
}
