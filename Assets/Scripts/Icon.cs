using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Gravity), typeof(GravityField))]
public class Icon : MonoBehaviour
{
    [Header("Icon Settings")]
    public Material iconMaterial;
    public float iconScaleMultiply = 1.2f;
    public float iconHeight = 0.05f;

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
        icon = new GameObject("Icon");
        icon.tag = "Icon";
        icon.transform.SetParent(transform);
        icon.transform.localRotation = Quaternion.identity;

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
        float iconScale = cameraZ * tanFovHalf * Mathf.Log(transform.lossyScale.x) * GameSettings.IconSize / 50f;

        Gravity gravity = GetComponent<Gravity>();
        if (gravity == null)
        {
            Debug.LogError("Focus is missing required component.");
            return;
        }

        Icon parentIcon = transform.parent.GetComponent<Icon>();
        if (parentIcon != null && parentIcon.icon.transform.localScale.x > 1f)
        {
            if (icon.activeSelf) icon.SetActive(false);
        }
        else
        {
            if (!icon.activeSelf) icon.SetActive(true);
            
            float finalIconScale = isHover ? iconScale * hoverScaleMultiply : iconScale;

            icon.transform.localScale = Vector3.one * finalIconScale / transform.lossyScale.x;
        }
    }
    
    public bool IsHover
    {
        set { isHover = value; }
    }
}