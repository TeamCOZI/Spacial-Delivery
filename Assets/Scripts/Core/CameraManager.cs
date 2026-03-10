using UnityEngine;
using System;

[DefaultExecutionOrder(29500)]
[RequireComponent(typeof(Camera), typeof(Rigidbody), typeof(SmallScaleOverlayCamera))]
public partial class CameraManager : FocusEventSubscriber
{
    public static CameraManager Instance { get; private set; }

    [Header("Default Settings")]
    public float minZ;
    public float defaultZ;
    public float smoothTime;
    
    [Header("Follow Settings")]
    public float followZ;

    [Header("Assembly Camera")]
    [Min(0.01f)] public float assemblyMinDistance = 1f;
    [Min(0.01f)] public float assemblyMinOrthoSize = 0.1f;
    [Min(0.01f)] public float assemblyDefaultDistance = 1f;
    [Min(0.1f)] public float assemblyFocusDistanceMultiplier = 2f;
    [Min(0.1f)] public float assemblyFocusDistance = 10f;

    [Header("Zoom Settings")]
    public float zoomSpeed;

    [Header("Focus Rotation")]
    public bool followFocusZRotation = true;
    public bool followFocusZRotationInAssembly = false;
    [Min(0.001f)] public float focusRotationSmoothTime = 0.08f;

    [Header("Drag Settings")]
    [Min(0.01f)] public float maxDragDeltaPerFrame = 500f;

    [Header("Physics Focus Origin")]
    [Min(1f)] public float physicsFocusRecenterThreshold = 2000f;

    private float starScale;

    private Transform oldFocus;
    private Vector3 target;
    private Vector3 offset;
    private Vector2 dragOffset;
    private float zoomOffset;

    private Camera cameraComponent;
    private SmallScaleOverlayCamera smallScaleOverlayCamera;

    private Vector3 currentVelocity;
    private float AV;
    private float targetRotationZ;
    private float rotationOffsetZ;
    private float rotationOffsetVelocity;
    private bool forceInstantCameraUpdate;

    private bool isAssemblyMode = false;
    private Action<float> starScaleHandler;
    private bool worldScaleApplied;
    private bool isUserInputSubscribed;
    private IInputService subscribedInputService;
    private ICameraFocusPolicy cameraFocusPolicy;

    private ICameraFocusPolicy FocusPolicy
    {
        get
        {
            if (cameraFocusPolicy != null) return cameraFocusPolicy;
            if (!CoreRuntimeAccess.TryGetCameraFocusPolicy(out cameraFocusPolicy) || cameraFocusPolicy == null)
            {
                cameraFocusPolicy = new DefaultCameraFocusPolicy();
            }
            return cameraFocusPolicy;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!worldScaleApplied)
        {
            minZ = WorldScale.ScaleLength(minZ);
            defaultZ = WorldScale.ScaleLength(defaultZ);
            followZ = WorldScale.ScaleLength(followZ);
            assemblyMinDistance = WorldScale.ScaleLength(assemblyMinDistance);
            assemblyMinOrthoSize = WorldScale.ScaleLength(assemblyMinOrthoSize);
            assemblyDefaultDistance = WorldScale.ScaleLength(assemblyDefaultDistance);
            assemblyFocusDistance = WorldScale.ScaleLength(assemblyFocusDistance);
            maxDragDeltaPerFrame = WorldScale.ScaleLength(maxDragDeltaPerFrame);
            physicsFocusRecenterThreshold = Mathf.Max(1f, WorldScale.ScaleLength(physicsFocusRecenterThreshold));
            transform.position = WorldScale.ScaleVector(transform.position);
            worldScaleApplied = true;
        }

        cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) cameraComponent = Camera.main;
        smallScaleOverlayCamera = GetComponent<SmallScaleOverlayCamera>();
        _ = FocusPolicy;
        target = Vector3.zero;
        targetRotationZ = transform.eulerAngles.z;
        rotationOffsetZ = 0f;
        rotationOffsetVelocity = 0f;

        dragOffset = Vector2.zero;
        zoomOffset = defaultZ;

        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(cameraComponent.nearClipPlane));
            cameraComponent.farClipPlane = Mathf.Max(cameraComponent.nearClipPlane + 1f, WorldScale.ScaleLength(cameraComponent.farClipPlane));
        }

    }

    private void Start()
    {
        UserInput.InstanceChanged += HandleUserInputInstanceChanged;
        TrySubscribeUserInputEvents();

        starScaleHandler = parameter => starScale = parameter * -1.1f;
        SolarSystemGenerator.starScale += starScaleHandler;
        WorldOriginManager.worldShifted += HandleWorldShift;
    }

    private void OnDestroy()
    {
        TryUnsubscribeUserInputEvents();

        if (starScaleHandler != null)
        {
            SolarSystemGenerator.starScale -= starScaleHandler;
        }
        WorldOriginManager.worldShifted -= HandleWorldShift;
        UserInput.InstanceChanged -= HandleUserInputInstanceChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected override void LateUpdate()
    {
        UpdateDynamicWorldOrigin();
        UpdateCameraPos();
    }

    private void HandleUserInputInstanceChanged(UserInput _)
    {
        RebindUserInputEvents();
    }

    private void UpdateCameraPos()
    {
        if (oldFocus != null) target = ResolveFocusFollowPosition(oldFocus);

        if (forceInstantCameraUpdate)
        {
            currentVelocity = Vector3.zero;
            AV = 0f;
            offset = new Vector3(0f, 0f, zoomOffset);
        }
        else
        {
            offset = Vector3.SmoothDamp(
                offset,
                new Vector3(0, 0, zoomOffset),
                ref currentVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }

        if (isAssemblyMode)
        {
            float targetOrthoSize = Mathf.Max(assemblyMinOrthoSize, -zoomOffset / 10f);
            bool hasCamera = TryEnsureCameraComponent();
            if (hasCamera)
            {
                if (forceInstantCameraUpdate)
                {
                    cameraComponent.orthographicSize = targetOrthoSize;
                }
                else
                {
                    cameraComponent.orthographicSize = Mathf.SmoothDamp(
                        cameraComponent.orthographicSize,
                        targetOrthoSize,
                        ref AV,
                        smoothTime,
                        Mathf.Infinity,
                        Time.unscaledDeltaTime
                    );
                }
            }
            transform.position = target + new Vector3(offset.x, offset.y, zoomOffset) + new Vector3(dragOffset.x, dragOffset.y, 0);
        }
        else transform.position = target + offset + new Vector3(dragOffset.x, dragOffset.y, 0);

        UpdateFocusRotation();
        smallScaleOverlayCamera?.SyncNow();
        forceInstantCameraUpdate = false;
    }

    public void EnterAssemblyMode()
    {
        isAssemblyMode = true;
        bool hasCamera = TryEnsureCameraComponent();
        if (hasCamera) cameraComponent.orthographic = true;
        if (oldFocus != null)
        {
            zoomOffset = -Mathf.Max(assemblyMinDistance, assemblyFocusDistance);
            zoomOffset = Mathf.Min(zoomOffset, -assemblyMinDistance);
        }
        else
        {
            zoomOffset = -Mathf.Max(assemblyMinDistance, assemblyFocusDistance);
        }
        AV = 0f;
        if (hasCamera)
        {
            cameraComponent.orthographicSize = Mathf.Max(assemblyMinOrthoSize, -zoomOffset / 10f);
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(0.01f));
        }
        forceInstantCameraUpdate = true;
    }

    public void ExitAssemblyMode()
    {
        isAssemblyMode = false;
        bool hasCamera = TryEnsureCameraComponent();
        if (hasCamera)
        {
            cameraComponent.orthographic = false;
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(1f));
        }
        forceInstantCameraUpdate = true;
    }

    public float GetDragPlaneZ()
    {
        if (oldFocus != null) return ResolveFocusFollowPosition(oldFocus).z;
        return target.z;
    }

    private void TrySubscribeUserInputEvents()
    {
        if (isUserInputSubscribed) return;
        if (!CoreRuntimeAccess.TryGetInputService(out IInputService inputService)) return;

        inputService.dragOffsetEvent += UpdateDragOffset;
        inputService.zoomOffsetEvent += UpdateZoomOffset;
        subscribedInputService = inputService;
        isUserInputSubscribed = true;
    }

    private void TryUnsubscribeUserInputEvents()
    {
        if (!isUserInputSubscribed) return;
        if (subscribedInputService != null)
        {
            subscribedInputService.dragOffsetEvent -= UpdateDragOffset;
            subscribedInputService.zoomOffsetEvent -= UpdateZoomOffset;
        }
        subscribedInputService = null;
        isUserInputSubscribed = false;
    }

    private void RebindUserInputEvents()
    {
        if (isUserInputSubscribed)
        {
            TryUnsubscribeUserInputEvents();
        }
        TrySubscribeUserInputEvents();
    }

    private bool TryEnsureCameraComponent()
    {
        if (cameraComponent == null)
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent == null)
            {
                cameraComponent = Camera.main;
            }
        }

        return cameraComponent != null;
    }

}
