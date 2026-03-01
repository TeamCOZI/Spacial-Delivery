using UnityEngine;
using System;

[DefaultExecutionOrder(29500)]
[RequireComponent(typeof(Camera), typeof(Rigidbody))]
public class CameraManager : MonoBehaviour
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

    private float starScale;

    private Transform oldFocus;
    private Vector3 target;
    private Vector3 offset;
    private Vector2 dragOffset;
    private float zoomOffset;

    private Camera cameraComponent;

    private Vector3 currentVelocity;
    private float AV;
    private float targetRotationZ;
    private float rotationOffsetZ;
    private float rotationOffsetVelocity;
    private bool forceInstantCameraUpdate;

    private bool isAssemblyMode = false;
    private Action<float> starScaleHandler;
    private bool worldScaleApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

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
            transform.position = WorldScale.ScaleVector(transform.position);
            worldScaleApplied = true;
        }

        cameraComponent = Camera.main;
        target = Vector3.zero;
        targetRotationZ = transform.eulerAngles.z;
        rotationOffsetZ = 0f;
        rotationOffsetVelocity = 0f;

        dragOffset = Vector2.zero;
        zoomOffset = defaultZ;

        if (GetComponent<SmallScaleOverlayCamera>() == null)
        {
            gameObject.AddComponent<SmallScaleOverlayCamera>();
        }

        if (cameraComponent != null)
        {
            cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(cameraComponent.nearClipPlane));
            cameraComponent.farClipPlane = Mathf.Max(cameraComponent.nearClipPlane + 1f, WorldScale.ScaleLength(cameraComponent.farClipPlane));
        }

    }

    private void Start()
    {
        UserInput.Instance.dragOffsetEvent += UpdateDragOffset;
        UserInput.Instance.zoomOffsetEvent += UpdateZoomOffset;

        FocusManager.Instance.focusEvent += UpdateFocus;

        starScaleHandler = parameter => starScale = parameter * -1.1f;
        SolarSystemGenerator.starScale += starScaleHandler;
        WorldOriginManager.worldShifted += HandleWorldShift;
    }

    private void OnDestroy()
    {
        if (UserInput.Instance != null)
        {
            UserInput.Instance.dragOffsetEvent -= UpdateDragOffset;
            UserInput.Instance.zoomOffsetEvent -= UpdateZoomOffset;
        }

        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.focusEvent -= UpdateFocus;
        }

        if (starScaleHandler != null)
        {
            SolarSystemGenerator.starScale -= starScaleHandler;
        }
        WorldOriginManager.worldShifted -= HandleWorldShift;
    }

    private void LateUpdate()
    {
        UpdateDynamicWorldOrigin();
        UpdateCameraPos();
    }

    private void UpdateCameraPos()
    {
        if (oldFocus != null) target = oldFocus.transform.position;

        if (forceInstantCameraUpdate)
        {
            currentVelocity = Vector3.zero;
            AV = 0f;
            offset = new Vector3(0f, 0f, zoomOffset);
        }
        else
        {
            offset = Vector3.SmoothDamp(offset, new Vector3(0, 0, zoomOffset), ref currentVelocity, smoothTime);
        }

        if (isAssemblyMode)
        {
            float targetOrthoSize = Mathf.Max(assemblyMinOrthoSize, -zoomOffset / 10f);
            if (forceInstantCameraUpdate)
            {
                cameraComponent.orthographicSize = targetOrthoSize;
            }
            else
            {
                cameraComponent.orthographicSize = Mathf.SmoothDamp(cameraComponent.orthographicSize, targetOrthoSize, ref AV, smoothTime);
            }
            transform.position = target + new Vector3(offset.x, offset.y, zoomOffset) + new Vector3(dragOffset.x, dragOffset.y, 0);
        }
        else transform.position = target + offset + new Vector3(dragOffset.x, dragOffset.y, 0);

        UpdateFocusRotation();
        forceInstantCameraUpdate = false;
    }

    private void UpdateFocus(Transform newFocus)
    {
        bool canFollow = followFocusZRotation
            && newFocus != null
            && (isAssemblyMode || IsSatelliteRelatedFocus(newFocus));
        float newTargetRotation = canFollow ? ResolveTargetRotationZ(newFocus) : 0f;
        float currentCameraRotation = transform.eulerAngles.z;

        targetRotationZ = newTargetRotation;
        rotationOffsetZ = Mathf.DeltaAngle(targetRotationZ, currentCameraRotation);
        rotationOffsetVelocity = 0f;

        if (newFocus != null)
        {
            if (LargeWorldCoordinator.Instance != null)
            {
                Double3 previousOrigin = LargeWorldCoordinator.Instance.worldOrigin;
                Double3 focusWorldPosition = ResolveFocusWorldOrigin(newFocus);
                LargeWorldCoordinator.Instance.SetWorldOrigin(focusWorldPosition);
                LargeWorldCoordinator.Instance.SyncAllTransforms();

                // Preserve camera continuity when origin changes to avoid one-frame "fly away" jumps.
                Vector3 originShift = (previousOrigin - focusWorldPosition).ToVector3();
                transform.position += originShift;
                target += originShift;
            }

            if (isAssemblyMode)
            {
                zoomOffset = -Mathf.Max(assemblyMinDistance, assemblyFocusDistance);
                zoomOffset = Mathf.Min(zoomOffset, -assemblyMinDistance);
            }
            else
            {
                zoomOffset = GetFocusScale(newFocus) * followZ;
            }
            target = newFocus.position;
        }
        else if (oldFocus != null)
        {
            zoomOffset = cameraComponent.transform.position.z;
            target = new Vector3(cameraComponent.transform.position.x, cameraComponent.transform.position.y, 0);
        }

        offset = cameraComponent.transform.position - target;
        dragOffset = Vector2.zero;

        oldFocus = newFocus;
    }

    private void UpdateDragOffset(Vector2 dragOffset)
    {
        if (dragOffset.sqrMagnitude > maxDragDeltaPerFrame * maxDragDeltaPerFrame)
        {
            dragOffset = dragOffset.normalized * maxDragDeltaPerFrame;
        }

        this.dragOffset -= dragOffset;

        if (isAssemblyMode)
        {
            return;
        }

        if (oldFocus != null)
        {
            Gravity gravity = oldFocus.GetComponent<Gravity>();
            if (gravity == null) return;

            // Unfocus by XY.
            if (Vector2.Distance(this.dragOffset, Vector2.zero) > gravity.GravityRadius)
            {
                FocusManager.Instance.SetFocus(null);
                this.dragOffset = Vector2.zero;
            }
        }
    }

    private void UpdateZoomOffset(float zoomOffset)
    {
        float maxZ = starScale;
        if (oldFocus != null)
        {
            if (isAssemblyMode)
            {
                maxZ = -assemblyMinDistance;
            }
            else
            {
                maxZ = GetFocusScale(oldFocus) * -1.1f;
            }
        }

        this.zoomOffset = Mathf.Clamp(this.zoomOffset + zoomOffset * Mathf.Abs(this.zoomOffset) * zoomSpeed, minZ, maxZ);
    }

    public void EnterAssemblyMode()
    {
        isAssemblyMode = true;
        cameraComponent.orthographic = true;
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
        cameraComponent.orthographicSize = Mathf.Max(assemblyMinOrthoSize, -zoomOffset / 10f);
        cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(0.01f));
        forceInstantCameraUpdate = true;
    }

    public void ExitAssemblyMode()
    {
        isAssemblyMode = false;
        cameraComponent.orthographic = false;
        cameraComponent.nearClipPlane = Mathf.Max(0.001f, WorldScale.ScaleLength(1f));
        forceInstantCameraUpdate = true;
    }

    public float GetDragPlaneZ()
    {
        if (oldFocus != null) return oldFocus.position.z;
        return target.z;
    }

    private void HandleWorldShift(Vector3 shiftDelta)
    {
        target += shiftDelta;
    }

    private static float GetFocusScale(Transform focus)
    {
        if (focus == null) return 1f;

        Vector3 scale = focus.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.01f);
    }

    private static Double3 ResolveFocusWorldOrigin(Transform focus)
    {
        if (focus == null) return Double3.Zero;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            WorldPosition ownerWp = partFocus.OwnerSatellite.GetComponent<WorldPosition>();
            if (ownerWp != null) return ownerWp.worldPosition;
            return (Double3)partFocus.OwnerSatellite.transform.position;
        }

        WorldPosition wp = focus.GetComponent<WorldPosition>();
        if (wp != null) return wp.worldPosition;

        return (Double3)focus.position;
    }

    private void UpdateDynamicWorldOrigin()
    {
        if (oldFocus == null) return;
        if (LargeWorldCoordinator.Instance == null) return;

        Double3 previousOrigin = LargeWorldCoordinator.Instance.worldOrigin;
        Double3 focusWorldPosition = ResolveInterpolatedFocusWorldOrigin(oldFocus);
        Double3 delta = previousOrigin - focusWorldPosition;

        if (Math.Abs(delta.x) < 1e-9 && Math.Abs(delta.y) < 1e-9 && Math.Abs(delta.z) < 1e-9)
        {
            return;
        }

        LargeWorldCoordinator.Instance.SetWorldOrigin(focusWorldPosition);
        LargeWorldCoordinator.Instance.SyncAllTransforms();

        // Keep camera continuity while origin follows the focused body every frame.
        Vector3 originShift = delta.ToVector3();
        transform.position += originShift;
        target += originShift;
    }

    private static Double3 ResolveInterpolatedFocusWorldOrigin(Transform focus)
    {
        if (focus == null || LargeWorldCoordinator.Instance == null) return Double3.Zero;

        Transform originTransform = focus;
        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            originTransform = partFocus.OwnerSatellite.transform;
        }

        return LargeWorldCoordinator.Instance.ToWorld(originTransform.position);
    }

    private void UpdateFocusRotation()
    {
        bool canFollow = followFocusZRotation
            && oldFocus != null
            && (isAssemblyMode || IsSatelliteRelatedFocus(oldFocus));
        targetRotationZ = canFollow ? ResolveTargetRotationZ(oldFocus) : 0f;

        if (forceInstantCameraUpdate)
        {
            rotationOffsetZ = 0f;
            rotationOffsetVelocity = 0f;
        }
        else
        {
            rotationOffsetZ = Mathf.SmoothDampAngle(
                rotationOffsetZ,
                0f,
                ref rotationOffsetVelocity,
                Mathf.Max(0.001f, focusRotationSmoothTime)
            );
        }

        float finalZ = targetRotationZ + rotationOffsetZ;
        transform.rotation = Quaternion.Euler(0f, 0f, finalZ);
    }

    private static float ResolveTargetRotationZ(Transform focus)
    {
        Transform rotationSource = ResolveFocusRotationSource(focus);
        return rotationSource != null ? rotationSource.eulerAngles.z : 0f;
    }

    private static Transform ResolveFocusRotationSource(Transform focus)
    {
        if (focus == null) return null;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        return focus;
    }

    private static bool IsSatelliteRelatedFocus(Transform focus)
    {
        if (focus == null) return false;

        if (focus.GetComponent<ArtificialSatellite>() != null) return true;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        return partFocus != null && partFocus.OwnerSatellite != null;
    }
}
