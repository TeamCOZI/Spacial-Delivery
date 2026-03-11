using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(29000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(WorldPosition))]
[RequireComponent(typeof(GravityAffectedMover))]
[RequireComponent(typeof(SpaceshipFuel))]
[RequireComponent(typeof(SpaceshipFlightController))]
public class Spaceship : MonoBehaviour
{
    [SerializeField] private bool logWorldPositionEveryFrame = false;
    [SerializeField] private bool autoFitHullCollider = true;
    [SerializeField, Min(0.1f)] private float hullColliderPadding = 1f;

    private Rigidbody rigidbodyComponent;
    private WorldPosition worldPosition;
    private GravityAffectedMover gravityMover;
    private SpaceshipFuel fuel;
    private SpaceshipFlightController flightController;
    private SpaceshipFocusProxy focusProxy;
    private IFocusService subscribedFocusService;
    private bool isFocusSubscribed;
    private int appliedRenderLayer = -1;

    public event Action<SpaceshipState> StateChanged;
    public SpaceshipState CurrentState { get; private set; } = SpaceshipState.Idle;
    public Transform FocusTarget => focusProxy != null ? focusProxy.transform : transform;

    private void Awake()
    {
        CacheRequiredComponents();
        FitHullColliderToMesh();
        EnsureFocusProxy();
        DisableRootIcon();
        SetState(SpaceshipState.Idle);
    }

    private void OnEnable()
    {
        FocusManager.InstanceChanged += HandleFocusManagerInstanceChanged;
        TrySubscribeFocusEvents();
    }

    private void OnDisable()
    {
        FocusManager.InstanceChanged -= HandleFocusManagerInstanceChanged;
        TryUnsubscribeFocusEvents();
    }

    public void Launch(Vector3 velocity)
    {
        if (CurrentState == SpaceshipState.Destroyed) return;

        CacheRequiredComponents();
        if (gravityMover == null)
        {
            Debug.LogError("Spaceship: GravityAffectedMover is missing.");
            return;
        }

        gravityMover.Launch(new Vector3(velocity.x, velocity.y, 0f));
        ApplyFocusedRenderInterpolation();
        SetState(SpaceshipState.Launched);
    }

    private void LateUpdate()
    {
        if (!logWorldPositionEveryFrame) return;
        if (gravityMover == null || !gravityMover.IsLaunched) return;
        if (worldPosition == null) CacheRequiredComponents();
        if (worldPosition == null) return;

        Vector3 localPosition = transform.position;
        Debug.Log($"[SpaceshipFrame] local={localPosition} world={worldPosition.worldPosition} vel={gravityMover.CurrentVelocity}");
    }

    private void CacheRequiredComponents()
    {
        if (rigidbodyComponent == null) rigidbodyComponent = GetComponent<Rigidbody>();
        if (worldPosition == null) worldPosition = GetComponent<WorldPosition>();
        if (gravityMover == null) gravityMover = GetComponent<GravityAffectedMover>();
        if (fuel == null) fuel = GetComponent<SpaceshipFuel>();
        if (flightController == null) flightController = GetComponent<SpaceshipFlightController>();

        if (rigidbodyComponent == null || worldPosition == null || gravityMover == null || fuel == null || flightController == null)
        {
            Debug.LogError("Spaceship: Required components are missing on prefab.");
        }
    }

    public Dictionary<string, string> BuildFocusInfo()
    {
        float fuelValue = fuel != null ? fuel.CurrentFuel : 0f;
        float maxFuelValue = fuel != null ? fuel.MaxFuel : 0f;
        float speed = gravityMover != null ? gravityMover.CurrentVelocity.magnitude : 0f;

        return new Dictionary<string, string>
        {
            { "Name", name },
            { "Category", "Spaceship" },
            { "State", CurrentState.ToString() },
            { "Fuel", $"{Mathf.CeilToInt(fuelValue)} / {Mathf.CeilToInt(maxFuelValue)}" },
            { "Speed", speed.ToString("0.##") }
        };
    }

    private void HandleFocusManagerInstanceChanged(FocusManager _)
    {
        RebindFocusEvents();
    }

    private void TrySubscribeFocusEvents()
    {
        if (isFocusSubscribed) return;
        if (!CoreRuntimeAccess.TryGetFocusService(out IFocusService focusService)) return;

        focusService.RegisterFocusListener(HandleFocusChanged, true);
        subscribedFocusService = focusService;
        isFocusSubscribed = true;
    }

    private void TryUnsubscribeFocusEvents()
    {
        if (!isFocusSubscribed) return;
        if (subscribedFocusService != null)
        {
            subscribedFocusService.DeregisterFocusListener(HandleFocusChanged);
        }

        subscribedFocusService = null;
        isFocusSubscribed = false;
    }

    private void RebindFocusEvents()
    {
        if (isFocusSubscribed)
        {
            TryUnsubscribeFocusEvents();
        }

        TrySubscribeFocusEvents();
    }

    private void HandleFocusChanged(Transform focused)
    {
        int smallScaleLayer = SmallScaleLayerUtility.GetLayer();
        if (smallScaleLayer < 0) return;

        bool isFocused = SpaceshipFocusUtility.TryResolveSpaceship(focused, out Spaceship focusedSpaceship)
            && focusedSpaceship == this;
        ApplyFocusedRenderInterpolation(isFocused);
        int targetLayer = isFocused ? 0 : smallScaleLayer;
        ApplyRenderLayer(targetLayer);
        focusProxy?.SetInteractionEnabled(!isFocused);
    }

    private void ApplyFocusedRenderInterpolation()
    {
        bool isFocused = SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out Spaceship focusedSpaceship)
            && focusedSpaceship == this;
        ApplyFocusedRenderInterpolation(isFocused);
    }

    private void ApplyFocusedRenderInterpolation(bool isFocused)
    {
        CacheRequiredComponents();
        if (rigidbodyComponent == null || gravityMover == null) return;

        // When a launched ship is the camera anchor, match its rendered pose to the
        // interpolated celestial bodies so the background does not appear to jitter.
        rigidbodyComponent.interpolation = (isFocused && gravityMover.IsLaunched)
            ? RigidbodyInterpolation.Interpolate
            : RigidbodyInterpolation.None;
    }

    private void ApplyRenderLayer(int targetLayer)
    {
        if (appliedRenderLayer == targetLayer) return;

        SetLayerRecursively(transform, targetLayer);
        focusProxy?.SetInteractionLayer(targetLayer);
        appliedRenderLayer = targetLayer;
    }

    private void EnsureFocusProxy()
    {
        if (focusProxy != null) return;

        GameObject proxyObject = new GameObject(name + " FocusProxy");
        focusProxy = proxyObject.AddComponent<SpaceshipFocusProxy>();
        focusProxy.Initialize(this);
    }

    private void DisableRootIcon()
    {
        Icon rootIcon = GetComponent<Icon>();
        if (rootIcon == null) return;

        rootIcon.enabled = false;
        Transform generatedIcon = transform.Find("Icon");
        if (generatedIcon != null)
        {
            generatedIcon.gameObject.SetActive(false);
        }
    }

    private void FitHullColliderToMesh()
    {
        if (!autoFitHullCollider) return;

        BoxCollider hullCollider = GetComponent<BoxCollider>();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (hullCollider == null || meshFilter == null || meshFilter.sharedMesh == null) return;
        if (hullCollider.size.sqrMagnitude > 0.0001f) return;

        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        float padding = Mathf.Max(0.1f, hullColliderPadding);
        hullCollider.center = meshBounds.center;
        hullCollider.size = meshBounds.size * padding;
    }

    private static void SetLayerRecursively(Transform root, int targetLayer)
    {
        if (root == null) return;

        root.gameObject.layer = targetLayer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), targetLayer);
        }
    }

    private void OnDestroy()
    {
        TryUnsubscribeFocusEvents();
        if (focusProxy != null)
        {
            Destroy(focusProxy.gameObject);
        }
        SetState(SpaceshipState.Destroyed);
    }

    public void DestroyByCelestialCollision(Collider otherCollider)
    {
        if (CurrentState == SpaceshipState.Destroyed) return;

        string targetName = otherCollider != null ? otherCollider.name : "<null>";
        string rootName = (otherCollider != null && otherCollider.transform != null && otherCollider.transform.root != null)
            ? otherCollider.transform.root.name
            : "<null>";

        Debug.Log($"[SpaceshipDestroyed] ship={name} cause=CelestialCollision target={targetName} root={rootName}");
        SetState(SpaceshipState.Destroyed);
        Destroy(gameObject);
    }

    private void SetState(SpaceshipState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        StateChanged?.Invoke(newState);
    }
}
