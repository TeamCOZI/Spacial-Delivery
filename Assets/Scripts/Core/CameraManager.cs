using UnityEngine;
using System;

[DefaultExecutionOrder(30100)]
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
    [Min(0.01f)] public float unfocusedMinDistance = 10f;
    [Min(0f)] public float gravityZoomTransitionDuration = 0.5f;

    [Header("Collision Settings")]
    [Min(0.01f)] public float cameraCollisionSurfaceOffset = 2.75f;

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
    private bool isFocusSurfaceMinimumDistanceActive;
    private float focusSurfaceMinimumDistanceZoomOffset;
    private bool hasCameraCollisionMinimumZ;
    private float cameraCollisionMinimumZ;
    private bool resetZoomOnNextFocusChange;
    private Gravity activeSpaceshipGravityZoomSource;
    private bool isGravityZoomTransitionActive;
    private float gravityZoomTransitionCurrentTargetZ;
    private float gravityZoomTransitionVelocity;

    private Camera cameraComponent;
    private SmallScaleOverlayCamera smallScaleOverlayCamera;
    private float defaultFieldOfView;

    private Vector3 currentVelocity;
    private float AV;
    private float fieldOfViewVelocity;
    private float targetRotationZ;
    private float rotationOffsetZ;
    private float rotationOffsetVelocity;
    private bool forceInstantCameraUpdate;
    private bool hasPendingDraggedFocusTransition;
    private Transform pendingDraggedFocusTarget;
    private Vector3 pendingDraggedCameraLocalPosition;
    private float focusAutoPromoteMinObservedZoomDistance = float.PositiveInfinity;
    private bool wasOrbitCommittedFocusActive;
    private bool isAssemblyMode = false;
    private bool isAssemblyOrthographicTransitionPending;
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
            unfocusedMinDistance = WorldScale.ScaleLength(unfocusedMinDistance);
            cameraCollisionSurfaceOffset = WorldScale.ScaleLength(cameraCollisionSurfaceOffset);
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
            defaultFieldOfView = cameraComponent.fieldOfView;
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
        UpdateCameraPos();
    }

    private void HandleUserInputInstanceChanged(UserInput _)
    {
        RebindUserInputEvents();
    }

    private void UpdateCameraPos()
    {
        UpdateSpaceshipGravityFieldZoom();
        if (oldFocus != null) target = ResolveFocusFollowPosition(oldFocus);
        TryBeginCommittedOrbitCameraTransition();
        Vector3 appliedDragOffset = ResolveAppliedDragOffset(oldFocus);
        Vector3 focusAnchorPosition = target;

        if (forceInstantCameraUpdate)
        {
            currentVelocity = Vector3.zero;
            gravityZoomTransitionVelocity = 0f;
            isGravityZoomTransitionActive = false;
            gravityZoomTransitionCurrentTargetZ = zoomOffset;
            AV = 0f;
            offset = new Vector3(0f, 0f, zoomOffset);
        }
        else
        {
            float offsetZTarget = ResolveCurrentOffsetZTarget();
            offset = Vector3.SmoothDamp(
                offset,
                new Vector3(0f, 0f, offsetZTarget),
                ref currentVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }

        UpdateProjectionFov();

        Vector3 desiredCameraPosition = target + offset + appliedDragOffset;

        if (ShouldUseAssemblyOrthographicProjection(oldFocus) && TryEnsureCameraComponent() && cameraComponent.orthographic)
        {
            float targetOrthoSize = ResolveAssemblyTargetOrthoSize();
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

        transform.position = ResolveCollisionConstrainedCameraPosition(oldFocus, focusAnchorPosition, desiredCameraPosition);
        TryCompleteAssemblyOrthographicTransition();

        TryPromoteFocusToParentByZoomDistance(oldFocus, focusAnchorPosition, transform.position);

        UpdateFocusRotation();
        smallScaleOverlayCamera?.SyncNow();
        forceInstantCameraUpdate = false;
    }

    public void EnterAssemblyMode()
    {
        isAssemblyMode = true;
        bool hasCamera = TryEnsureCameraComponent();
        isAssemblyOrthographicTransitionPending = false;
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
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(0.01f));
            RefreshAssemblyProjectionModeForFocus(oldFocus);
        }
        forceInstantCameraUpdate = false;
    }

    public void ExitAssemblyMode()
    {
        isAssemblyMode = false;
        isAssemblyOrthographicTransitionPending = false;
        bool hasCamera = TryEnsureCameraComponent();
        if (hasCamera)
        {
            cameraComponent.orthographic = false;
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(1f));
        }
        forceInstantCameraUpdate = false;
    }

    public float GetDragPlaneZ()
    {
        if (oldFocus != null) return ResolveFocusFollowPosition(oldFocus).z;
        return target.z;
    }

    internal Vector3 GetPlannedCameraLocalPosition()
    {
        Vector3 plannedTarget = target;
        if (oldFocus != null)
        {
            plannedTarget = ResolveFocusFollowPosition(oldFocus);
        }

        Vector3 plannedDragOffset = ResolveAppliedDragOffset(oldFocus);
        Vector3 focusAnchorPosition = plannedTarget;
        Vector3 desiredCameraPosition = plannedTarget + offset + plannedDragOffset;

        return ResolveCollisionConstrainedCameraPosition(oldFocus, focusAnchorPosition, desiredCameraPosition, false);
    }

    private void TryCompleteAssemblyOrthographicTransition()
    {
        if (!isAssemblyMode)
        {
            isAssemblyOrthographicTransitionPending = false;
            return;
        }

        if (!ShouldUseAssemblyOrthographicProjection(oldFocus))
        {
            isAssemblyOrthographicTransitionPending = false;
            return;
        }

        if (!TryEnsureCameraComponent())
        {
            isAssemblyOrthographicTransitionPending = false;
            return;
        }

        if (cameraComponent.orthographic)
        {
            isAssemblyOrthographicTransitionPending = false;
            return;
        }

        if (!isAssemblyOrthographicTransitionPending)
        {
            return;
        }

        float transitionThreshold = Mathf.Max(assemblyMinDistance * 0.15f, 0.01f);
        if (Mathf.Abs(offset.z - zoomOffset) > transitionThreshold)
        {
            return;
        }

        float targetFieldOfView = ResolveAssemblyProjectionBlendFieldOfView();
        if (Mathf.Abs(cameraComponent.fieldOfView - targetFieldOfView) > 0.05f)
        {
            return;
        }

        cameraComponent.fieldOfView = targetFieldOfView;
        cameraComponent.orthographicSize = ResolveAssemblyTargetOrthoSize();
        cameraComponent.orthographic = true;
        AV = 0f;
        fieldOfViewVelocity = 0f;
        isAssemblyOrthographicTransitionPending = false;
    }

    private void UpdateProjectionFov()
    {
        if (!TryEnsureCameraComponent())
        {
            return;
        }

        if (defaultFieldOfView <= 0f)
        {
            defaultFieldOfView = cameraComponent.fieldOfView;
        }

        if (cameraComponent.orthographic)
        {
            fieldOfViewVelocity = 0f;
            return;
        }

        float targetFieldOfView = defaultFieldOfView;
        if (ShouldUseAssemblyOrthographicProjection(oldFocus) && isAssemblyOrthographicTransitionPending)
        {
            targetFieldOfView = ResolveAssemblyProjectionBlendFieldOfView();
        }

        if (forceInstantCameraUpdate)
        {
            cameraComponent.fieldOfView = targetFieldOfView;
            fieldOfViewVelocity = 0f;
            return;
        }

        cameraComponent.fieldOfView = Mathf.SmoothDamp(
            cameraComponent.fieldOfView,
            targetFieldOfView,
            ref fieldOfViewVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
    }

    private float ResolveAssemblyTargetOrthoSize()
    {
        return Mathf.Max(assemblyMinOrthoSize, -zoomOffset / 10f);
    }

    private float ResolveAssemblyProjectionBlendFieldOfView()
    {
        float targetDistance = Mathf.Max(0.001f, -zoomOffset);
        float targetOrthoSize = ResolveAssemblyTargetOrthoSize();
        return Mathf.Rad2Deg * 2f * Mathf.Atan2(targetOrthoSize, targetDistance);
    }
    private void RefreshAssemblyProjectionModeForFocus(Transform focus)
    {
        if (!TryEnsureCameraComponent())
        {
            return;
        }

        if (!isAssemblyMode)
        {
            isAssemblyOrthographicTransitionPending = false;
            return;
        }

        if (!ShouldUseAssemblyOrthographicProjection(focus))
        {
            isAssemblyOrthographicTransitionPending = false;
            if (cameraComponent.orthographic)
            {
                cameraComponent.fieldOfView = ResolveAssemblyPerspectiveResumeFieldOfView();
                cameraComponent.orthographic = false;
                fieldOfViewVelocity = 0f;
            }

            return;
        }

        if (!cameraComponent.orthographic)
        {
            isAssemblyOrthographicTransitionPending = true;
        }
    }

    private bool ShouldUseAssemblyOrthographicProjection(Transform focus)
    {
        return isAssemblyMode && !IsAssemblyPerspectivePartFocus(focus);
    }

    private static bool IsAssemblyPerspectivePartFocus(Transform focus)
    {
        if (focus == null) return false;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null) return false;

        string partName = partFocus.SourcePart.partName;
        if (string.IsNullOrWhiteSpace(partName)) return false;

        return string.Equals(partName, "Launcher", StringComparison.OrdinalIgnoreCase)
            || partName.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private float ResolveAssemblyPerspectiveResumeFieldOfView()
    {
        float targetDistance = Mathf.Max(0.001f, -offset.z);
        float orthoSize = cameraComponent != null && cameraComponent.orthographic
            ? cameraComponent.orthographicSize
            : ResolveAssemblyTargetOrthoSize();
        return Mathf.Rad2Deg * 2f * Mathf.Atan2(orthoSize, targetDistance);
    }

    private Vector3 ResolveAppliedDragOffset(Transform focus)
    {
        Vector3 planarDragOffset = new Vector3(dragOffset.x, dragOffset.y, 0f);
        if (!ShouldRotateDragOffsetWithFocus(focus))
        {
            return planarDragOffset;
        }

        float rotationZ = FocusPolicy.ResolveTargetRotationZ(focus);
        return Quaternion.Euler(0f, 0f, rotationZ) * planarDragOffset;
    }

    private Vector2 ResolveStoredDragDelta(Transform focus, Vector2 worldDragDelta)
    {
        if (!ShouldRotateDragOffsetWithFocus(focus))
        {
            return worldDragDelta;
        }

        float rotationZ = FocusPolicy.ResolveTargetRotationZ(focus);
        Vector3 localDragDelta = Quaternion.Inverse(Quaternion.Euler(0f, 0f, rotationZ)) * new Vector3(worldDragDelta.x, worldDragDelta.y, 0f);
        return new Vector2(localDragDelta.x, localDragDelta.y);
    }

    private bool ShouldRotateDragOffsetWithFocus(Transform focus)
    {
        return CanFollowFocusRotation(focus);
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

        if (cameraComponent != null && defaultFieldOfView <= 0f)
        {
            defaultFieldOfView = cameraComponent.fieldOfView;
        }

        return cameraComponent != null;
    }

}





