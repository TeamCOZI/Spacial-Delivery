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
    private float tanFovHalf;
    private bool isHover = false;
    private float baseLocalScale;
    private bool isExpandedForChildren;

    private void Awake()
    {
        if (iconMaterial == null)
        {
            Debug.LogError("Icon is missing required component.");
            return;
        }

        if (Camera.main != null) tanFovHalf = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    private void Start()
    {
        GenerateIcon();
    }

    private void LateUpdate()
    {
        UpdateIcon();
    }

    private void OnEnable()
    {
        FocusManager.Instance.RegisterIcon(this);
    }

    private void OnDisable()
    {
        FocusManager.Instance.DeregisterIcon(this);
    }

    private void GenerateIcon()
    {
        icon = new GameObject("Icon")
        {
            tag = "Icon"
        };
        icon.transform.SetParent(transform, false);

        icon.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
        icon.AddComponent<MeshCollider>();

        MeshRenderer iconMeshRenderer = icon.AddComponent<MeshRenderer>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            List<Material> materials = new List<Material>(meshRenderer.materials) { iconMaterial };

            iconMeshRenderer.materials = materials.ToArray();
        }
    }

    private void UpdateIcon()
    {
        icon.transform.position = transform.position;

        if (Camera.main == null) return;
        
        float cameraZ = Mathf.Abs(Camera.main.transform.position.z);
        float cameraZUnscaled = WorldScale.UnscaleLength(cameraZ);
        float objectScaleUnscaled = WorldScale.UnscaleLength(transform.lossyScale.x);
        float iconScaleBase = cameraZUnscaled * tanFovHalf * Mathf.Log(objectScaleUnscaled + 100f) * GameSettings.IconSize / 50f;
        float iconScale = WorldScale.ScaleLength(iconScaleBase);
        baseLocalScale = iconScale / Mathf.Max(0.0001f, transform.lossyScale.x);
        UpdateExpandedState();

        if (IsHiddenByCenterHierarchy())
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
