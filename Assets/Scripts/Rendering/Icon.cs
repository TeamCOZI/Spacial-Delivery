using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(31000)]
[RequireComponent(typeof(Renderer))]
public class Icon : MonoBehaviour
{
    [Header("Icon Settings")]
    public Material iconMaterial;
    public float iconScaleMultiply = 1.2f;
    public float iconHeight = 0.05f;

    [Header("Hover Settings")]
    public float hoverScaleMultiply = 2f;

    [Header("Hierarchy Visibility")]
    public float parentExpandThreshold = 1f;
    [Min(0f)] public float parentExpandHysteresis = 0.15f;

    private GameObject icon;
    private Camera mainCamera;
    private float tanFovHalf;
    private bool isHover = false;
    private float baseLocalScale;
    private bool isExpandedForChildren;
    private bool triedMissingMaterialLog;

    private void Awake()
    {
        TryEnsureMainCamera();
    }

    private void Start()
    {
        EnsureIconInitialized();
    }

    private void LateUpdate()
    {
        if (!EnsureIconInitialized()) return;
        UpdateIcon();
    }

    private void GenerateIcon()
    {
        if (iconMaterial == null) return;

        icon = new GameObject("Icon")
        {
            tag = "Icon"
        };
        icon.transform.SetParent(transform, false);

        MeshFilter sourceMeshFilter = GetComponent<MeshFilter>();
        MeshFilter iconMeshFilter = icon.AddComponent<MeshFilter>();
        iconMeshFilter.sharedMesh = sourceMeshFilter != null ? sourceMeshFilter.sharedMesh : null;

        SphereCollider iconCollider = icon.AddComponent<SphereCollider>();
        iconCollider.isTrigger = true;
        if (iconMeshFilter.sharedMesh != null)
        {
            Bounds meshBounds = iconMeshFilter.sharedMesh.bounds;
            iconCollider.center = meshBounds.center;
            iconCollider.radius = Mathf.Max(meshBounds.extents.x, meshBounds.extents.y, meshBounds.extents.z);
        }
        else
        {
            iconCollider.center = Vector3.zero;
            iconCollider.radius = 0.5f;
        }

        MeshRenderer iconMeshRenderer = icon.AddComponent<MeshRenderer>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            List<Material> materials = new List<Material>(meshRenderer.materials) { iconMaterial };

            iconMeshRenderer.materials = materials.ToArray();
        }
    }

    private bool EnsureIconInitialized()
    {
        if (icon != null) return true;

        if (iconMaterial == null)
        {
            Icon[] allIcons = FindObjectsByType<Icon>(FindObjectsSortMode.None);
            for (int i = 0; i < allIcons.Length; i++)
            {
                Icon template = allIcons[i];
                if (template == null || template == this) continue;
                if (!template.isActiveAndEnabled) continue;
                if (template.iconMaterial == null) continue;
                iconMaterial = template.iconMaterial;
                break;
            }
        }

        if (iconMaterial == null)
        {
            if (!triedMissingMaterialLog)
            {
                Debug.LogWarning($"Icon material is missing on {name}. Waiting for assignment.");
                triedMissingMaterialLog = true;
            }
            return false;
        }

        triedMissingMaterialLog = false;
        GenerateIcon();
        return icon != null;
    }

    private void UpdateIcon()
    {
        if (icon == null) return;

        icon.transform.position = transform.position + new Vector3(0f, iconHeight, 0f);

        if (!TryEnsureMainCamera()) return;
        
        float cameraZ = Mathf.Abs(mainCamera.transform.position.z);
        float cameraZUnscaled = WorldScale.UnscaleLength(cameraZ);
        float objectScaleUnscaled = WorldScale.UnscaleLength(transform.lossyScale.x);
        float iconScaleBase = cameraZUnscaled * tanFovHalf * Mathf.Log(objectScaleUnscaled + 100f) * GameSettings.IconSize / 50f;
        float scaleMultiplier = Mathf.Max(0.0001f, iconScaleMultiply);
        float iconScale = WorldScale.ScaleLength(iconScaleBase) * scaleMultiplier;
        baseLocalScale = iconScale / Mathf.Max(0.0001f, transform.lossyScale.x);
        UpdateExpandedState();

        if (IsHiddenByArtificialSatelliteFocus() || IsHiddenByCenterHierarchy())
        {
            if (icon.activeSelf) icon.SetActive(false);
        }
        else
        {
            if (!icon.activeSelf) icon.SetActive(true);
            
            float finalIconScale = isHover ? iconScale * hoverScaleMultiply : iconScale;

            icon.transform.localScale = Vector3.one * finalIconScale / Mathf.Max(0.0001f, transform.lossyScale.x);
        }
    }

    private bool TryEnsureMainCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return false;
            tanFovHalf = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        return true;
    }

    private void UpdateExpandedState()
    {
        float threshold = Mathf.Max(0f, parentExpandThreshold);
        float hysteresis = Mathf.Max(0f, parentExpandHysteresis);

        if (isExpandedForChildren)
        {
            if (baseLocalScale < threshold - hysteresis)
            {
                isExpandedForChildren = false;
            }
        }
        else
        {
            if (baseLocalScale > threshold + hysteresis)
            {
                isExpandedForChildren = true;
            }
        }
    }

    private bool IsHiddenByCenterHierarchy()
    {
        OrbitRevolution orbit = GetComponent<OrbitRevolution>();
        while (orbit != null && orbit.center != null)
        {
            Icon centerIcon = orbit.center.GetComponent<Icon>();
            if (centerIcon != null && centerIcon.IsIconExpanded())
            {
                return true;
            }

            orbit = orbit.center.GetComponent<OrbitRevolution>();
        }

        return false;
    }

    private static bool IsHiddenByArtificialSatelliteFocus()
    {
        Transform focused = FocusManager.currentFocus;
        if (focused == null) return false;

        if (focused.GetComponent<ArtificialSatellite>() != null) return true;

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        return partFocus != null && partFocus.OwnerSatellite != null;
    }

    private bool IsIconExpanded()
    {
        return icon != null && icon.activeSelf && isExpandedForChildren;
    }
    
    public bool IsHover
    {
        set { isHover = value; }
    }

    public bool IsVisibleForInteraction()
    {
        return enabled &&
               gameObject.activeInHierarchy &&
               icon != null &&
               icon.activeInHierarchy;
    }
}
