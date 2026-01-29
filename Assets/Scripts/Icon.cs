using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Gravity), typeof(GravityField))]
public class Icon : MonoBehaviour
{
    [Header("Icon Settings")]
    public Material iconMaterial;
    public float iconScaleMultiply;
    public float iconHeight;

    [Header("Hover Settings")]
    public float hoverScaleMultiply = 2f;

    private GameObject icon;
    private float tanFovHalf;
    private bool isFocus = false;
    private bool isHover = false;

    private void Awake()
    {
        if (iconMaterial == null)
        {
            Debug.LogError("Focus is missing required component.");
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
        icon = new GameObject("Icon");
        icon.tag = "Icon";
        icon.transform.SetParent(transform);

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
        float iconScale = 2.0f * cameraZ * tanFovHalf * iconHeight * GameSettings.IconSize;

        Gravity gravity = GetComponent<Gravity>();
        if (gravity == null)
        {
            Debug.LogError("Focus is missing required component.");
            return;
        }

        Transform parent = transform.parent;

        if (iconScale < gravity.GravityRadius * 2)
        {
            if (icon.activeSelf) icon.SetActive(false);

            if (FocusManager.currentFocus == transform) isFocus = true;

            return;
        }
        else if (iconScale > parent.lossyScale.x && parent.GetComponent<Star>() == null)
        {
            if (icon.activeSelf) icon.SetActive(false);

            return;
        }
        else
        {
            bool parentIsIcon = false;

            while (!parentIsIcon && parent != null && parent.GetComponent<Icon>() != null)
            {
                parentIsIcon = parent.GetComponent<Icon>().icon.activeSelf;

                if (parentIsIcon)
                {
                    icon.SetActive(false);

                    return;
                }

                parent = parent.parent;
            }
            
            if (!icon.activeSelf) icon.SetActive(true);

            float finalIconScale = isHover ? iconScale * hoverScaleMultiply : iconScale;

            icon.transform.localScale = Vector3.one * finalIconScale / transform.lossyScale.x;

            // Unfocus by Z.
            if (isFocus)
            {
                if (FocusManager.currentFocus == transform) FocusManager.Instance.SetFocus(null);
                
                isFocus = false;
            }
        }
    }
    
    public bool IsHover
    {
        set { isHover = value; }
    }
}