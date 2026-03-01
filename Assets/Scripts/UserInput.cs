using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(32000)]
[RequireComponent(typeof(PlayerInput))]
public class UserInput : MonoBehaviour
{
    public static UserInput Instance { get; private set; }
    public event Action<Transform> focusEvent;
    public event Action<Icon> hoverEvent;
    public event Action<Vector2> dragOffsetEvent;
    public event Action<float> zoomOffsetEvent;

    private bool isDrag = false;
    private Vector3 oldMousePos;

    private PlayerInput playerInput;
    private int focusRaycastMask;
    [SerializeField] private bool enableClickDebugLog = true;
    [SerializeField] private bool showRayGizmos = true;
    [SerializeField, Min(1f)] private float rayGizmoLength = 10000f;

    // Temp
    public GameObject artificialSatellitePrefab;
    public event Action<bool> destroyEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        playerInput = GetComponent<PlayerInput>();
        playerInput.actions["FocusParent"].performed += FocusParent;
        playerInput.actions["AssemblyMode"].performed += AssemblyMode;

        int smallScaleLayer = SmallScaleLayerUtility.GetLayer();
        focusRaycastMask = Physics.DefaultRaycastLayers;
        if (smallScaleLayer >= 0) focusRaycastMask |= 1 << smallScaleLayer;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        Drag();
        Zoom();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null) return;

        FocusHover();
    }

    private void Drag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDrag = true;
            oldMousePos = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDrag = false;
        }

        if (!isDrag) return;

        Vector3 currentMousePos = Mouse.current.position.ReadValue();
        if (Vector3.Distance(currentMousePos, oldMousePos) <= 0.1f) return;

        float dragPlaneZ = CameraManager.Instance != null ? CameraManager.Instance.GetDragPlaneZ() : 0f;
        Vector3 oldScreenMousePos = Utility.screenMousePos(oldMousePos, dragPlaneZ);
        Vector3 currentScreenMousePos = Utility.screenMousePos(currentMousePos, dragPlaneZ);
        Vector3 screenDelta = currentScreenMousePos - oldScreenMousePos;

        dragOffsetEvent?.Invoke(screenDelta);
        oldMousePos = currentMousePos;
    }

    private void Zoom()
    {
        float mouseScroll = Mouse.current.scroll.ReadValue().y;
        if (mouseScroll == 0f) return;

        mouseScroll /= 120f;
        zoomOffsetEvent?.Invoke(mouseScroll);
    }

    private void FocusHover()
    {
        if (AssemblyManager.Instance != null && AssemblyManager.Instance.isAssembling)
        {
            hoverEvent?.Invoke(null);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            hoverEvent?.Invoke(null);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (showRayGizmos)
        {
            Debug.DrawRay(ray.origin, ray.direction * Mathf.Max(1f, rayGizmoLength), Color.red, 0f, false);
        }

        // Transforms are shifted in LateUpdate by large-world systems, so sync before physics queries.
        Physics.SyncTransforms();

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, focusRaycastMask, QueryTriggerInteraction.Collide))
        {
            hoverEvent?.Invoke(null);
            return;
        }

        if (showRayGizmos)
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green, 0f, false);
            Debug.DrawRay(hit.point, hit.normal * 2f, Color.yellow, 0f, false);
        }

        Icon hoveredIcon = ResolveHoveredIcon(hit.transform);
        hoverEvent?.Invoke(hoveredIcon);

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (enableClickDebugLog && hit.collider != null)
        {
            GameObject go = hit.collider.gameObject;
            Debug.Log($"[ClickHit] name={go.name}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), tag={go.tag}");
        }

        Transform hitTarget = hit.collider != null ? hit.collider.transform : hit.transform;
        Transform resolvedTransform = ResolveFocusTransform(hitTarget);
        focusEvent?.Invoke(resolvedTransform);
    }

    private static Icon ResolveHoveredIcon(Transform hitTransform)
    {
        if (hitTransform == null) return null;

        Icon icon = hitTransform.GetComponent<Icon>();
        if (icon != null) return icon;

        return hitTransform.GetComponentInParent<Icon>();
    }

    private void FocusParent(InputAction.CallbackContext context)
    {
        if (FocusManager.currentFocus == null) return;
        if (AssemblyManager.Instance != null && AssemblyManager.Instance.isAssembling) return;

        AssemblyPartFocus partFocus = FocusManager.currentFocus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            focusEvent?.Invoke(partFocus.OwnerSatellite.transform);
            return;
        }

        OrbitRevolution orbit = FocusManager.currentFocus.GetComponent<OrbitRevolution>();
        if (orbit != null && orbit.center != null)
        {
            focusEvent?.Invoke(orbit.center.transform);
        }
    }

    private void AssemblyMode(InputAction.CallbackContext context)
    {
        if (FocusManager.currentFocus == null) return;

        ArtificialSatellite focusSatellite = FocusManager.currentFocus.GetComponent<ArtificialSatellite>();
        if (focusSatellite != null)
        {
            AssemblyManager.Instance.StartAssemblyMode(focusSatellite);
        }
    }

    private Transform ResolveFocusTransform(Transform hitTransform)
    {
        if (hitTransform == null) return null;

        bool clickedIcon = hitTransform.CompareTag("Icon") || hitTransform.name == "Icon";
        Transform candidate = hitTransform;
        if (clickedIcon)
        {
            candidate = candidate.parent != null ? candidate.parent : candidate;
        }

        ArtificialSatellite ownerSatellite = candidate.GetComponentInParent<ArtificialSatellite>();
        if (clickedIcon && ownerSatellite != null)
        {
            // Icon click in satellite hierarchy should always step to satellite-level focus.
            return ownerSatellite.transform;
        }

        AssemblyPartFocus partFocus = candidate.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            // Part focus is only allowed when the owner satellite is already focused.
            ArtificialSatellite owner = partFocus.OwnerSatellite;
            if (owner != null)
            {
                bool ownerFocused = FocusManager.currentFocus == owner.transform;
                bool siblingPartFocused = false;

                if (!ownerFocused && FocusManager.currentFocus != null)
                {
                    AssemblyPartFocus currentPartFocus = FocusManager.currentFocus.GetComponent<AssemblyPartFocus>();
                    siblingPartFocused = currentPartFocus != null && currentPartFocus.OwnerSatellite == owner;
                }

                if (!ownerFocused && !siblingPartFocused)
                {
                    return owner.transform;
                }
            }
            return partFocus.transform;
        }

        ScaledSpaceProxyTarget proxyTarget = candidate.GetComponentInParent<ScaledSpaceProxyTarget>();
        if (proxyTarget != null && proxyTarget.source != null)
        {
            return proxyTarget.source;
        }

        Transform cursor = candidate;
        while (cursor != null)
        {
            UpdateFocusInfo provider = cursor.GetComponent<UpdateFocusInfo>();
            if (provider != null)
            {
                return cursor;
            }
            cursor = cursor.parent;
        }

        return candidate;
    }
}
