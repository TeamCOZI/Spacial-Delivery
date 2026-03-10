using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(30500)]
[DisallowMultipleComponent]
public class SpaceshipFocusProxy : MonoBehaviour, UpdateFocusInfo
{
    private const float ColliderPadding = 1.05f;
    private const string RuntimeGroupName = "SpaceshipFocusTargets";

    private Spaceship ownerSpaceship;
    private MeshFilter proxyMeshFilter;
    private MeshRenderer proxyMeshRenderer;
    private BoxCollider proxyCollider;
    private Icon proxyIcon;
    private bool interactionEnabled = true;

    public Spaceship OwnerSpaceship => ownerSpaceship;

    public void Initialize(Spaceship owner)
    {
        ownerSpaceship = owner;
        EnsureProxyComponents();
        SyncToOwner();
    }

    private void Awake()
    {
        EnsureProxyComponents();
    }

    private void LateUpdate()
    {
        if (ownerSpaceship == null)
        {
            Destroy(gameObject);
            return;
        }

        EnsureProxyComponents();
        SyncToOwner();
    }

    public void SetInteractionLayer(int layer)
    {
        SetLayerRecursively(transform, layer);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (proxyCollider != null)
        {
            proxyCollider.enabled = enabled;
        }

        Transform generatedIcon = transform.Find("Icon");
        if (generatedIcon != null)
        {
            Collider iconCollider = generatedIcon.GetComponent<Collider>();
            if (iconCollider != null)
            {
                iconCollider.enabled = enabled;
            }
        }
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        if (ownerSpaceship == null)
        {
            return new Dictionary<string, string>
            {
                { "Name", name },
                { "Category", "Spaceship" }
            };
        }

        return ownerSpaceship.BuildFocusInfo();
    }

    private void EnsureProxyComponents()
    {
        if (ownerSpaceship == null) return;

        Transform runtimeGroup = RuntimeHierarchyOrganizer.GetOrCreateGroup(RuntimeGroupName);
        if (transform.parent != runtimeGroup)
        {
            transform.SetParent(runtimeGroup, false);
        }

        if (proxyMeshFilter == null) proxyMeshFilter = GetComponent<MeshFilter>();
        if (proxyMeshFilter == null) proxyMeshFilter = gameObject.AddComponent<MeshFilter>();

        if (proxyMeshRenderer == null) proxyMeshRenderer = GetComponent<MeshRenderer>();
        if (proxyMeshRenderer == null) proxyMeshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (proxyCollider == null) proxyCollider = GetComponent<BoxCollider>();
        if (proxyCollider == null) proxyCollider = gameObject.AddComponent<BoxCollider>();

        CopyOwnerVisualSource();
        CopyOwnerCollider();
        CopyOwnerIcon();
        proxyMeshRenderer.enabled = false;
        proxyCollider.isTrigger = true;
        proxyCollider.enabled = interactionEnabled;
    }

    private void CopyOwnerVisualSource()
    {
        if (ownerSpaceship == null || proxyMeshFilter == null || proxyMeshRenderer == null) return;

        MeshFilter ownerMeshFilter = ownerSpaceship.GetComponent<MeshFilter>();
        MeshRenderer ownerMeshRenderer = ownerSpaceship.GetComponent<MeshRenderer>();
        if (ownerMeshFilter != null)
        {
            proxyMeshFilter.sharedMesh = ownerMeshFilter.sharedMesh;
        }

        if (ownerMeshRenderer != null)
        {
            proxyMeshRenderer.sharedMaterials = ownerMeshRenderer.sharedMaterials;
        }
    }

    private void CopyOwnerCollider()
    {
        if (ownerSpaceship == null || proxyCollider == null) return;

        BoxCollider ownerCollider = ownerSpaceship.GetComponent<BoxCollider>();
        if (ownerCollider != null)
        {
            proxyCollider.center = ownerCollider.center;
            proxyCollider.size = ownerCollider.size * ColliderPadding;
            return;
        }

        Renderer[] renderers = ownerSpaceship.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            proxyCollider.center = Vector3.zero;
            proxyCollider.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            bounds.Encapsulate(renderers[i].bounds);
        }

        proxyCollider.center = ownerSpaceship.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = ownerSpaceship.transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        proxyCollider.size = localSize * ColliderPadding;
    }

    private void CopyOwnerIcon()
    {
        if (ownerSpaceship == null) return;

        Icon ownerIcon = ownerSpaceship.GetComponent<Icon>();
        if (ownerIcon == null) return;

        if (proxyIcon == null) proxyIcon = GetComponent<Icon>();
        if (proxyIcon == null) proxyIcon = gameObject.AddComponent<Icon>();

        proxyIcon.iconMaterial = ownerIcon.iconMaterial;
        proxyIcon.iconScaleMultiply = ownerIcon.iconScaleMultiply;
        proxyIcon.iconHeight = ownerIcon.iconHeight;
        proxyIcon.hoverScaleMultiply = ownerIcon.hoverScaleMultiply;
        proxyIcon.parentExpandThreshold = ownerIcon.parentExpandThreshold;
        proxyIcon.parentExpandHysteresis = ownerIcon.parentExpandHysteresis;
    }

    private void SyncToOwner()
    {
        if (ownerSpaceship == null) return;

        name = ownerSpaceship.name + " FocusProxy";
        transform.position = ownerSpaceship.transform.position;
        transform.rotation = ownerSpaceship.transform.rotation;
        transform.localScale = ownerSpaceship.transform.lossyScale;
        SetInteractionLayer(ownerSpaceship.gameObject.layer);
        SetInteractionEnabled(interactionEnabled);

        if (proxyMeshRenderer != null)
        {
            proxyMeshRenderer.enabled = false;
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
