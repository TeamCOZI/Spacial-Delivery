using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(30000)]
public class LargeWorldCoordinator : MonoBehaviour
{
    public static LargeWorldCoordinator Instance { get; private set; }

    [Header("Reference Origin (double)")]
    public Double3 worldOrigin = Double3.Zero;

    [Header("Sync")]
    public bool syncEveryLateUpdate = true;

    private readonly HashSet<WorldPosition> tracked = new HashSet<WorldPosition>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        bool originChanged = TryRecenterVisualWorldOrigin();
        if (originChanged)
        {
            SyncAllTransforms(includeOrbitDriven: true, forceTransformSync: true);
            Physics.SyncTransforms();
            return;
        }

        if (!syncEveryLateUpdate) return;

        // Physics-driven objects are finalized in FixedUpdate. LateUpdate only catches
        // non-physics tracked roots and visual-only consumers after normal motion settles.
        SyncAllTransforms(includeOrbitDriven: false);
        Physics.SyncTransforms();
    }

    private void FixedUpdate()
    {
        bool originChanged = TryRecenterPhysicsWorldOrigin();
        SyncAllTransformsForSimulationStep(originChanged);
    }

    public void Register(WorldPosition wp)
    {
        if (wp == null) return;
        tracked.Add(wp);
    }

    public void Deregister(WorldPosition wp)
    {
        if (wp == null) return;
        tracked.Remove(wp);
    }

    public void SetWorldOrigin(Double3 newOrigin)
    {
        worldOrigin = newOrigin;
    }

    public Vector3 ToLocal(Double3 worldPos)
    {
        Double3 local = worldPos - worldOrigin;
        return local.ToVector3();
    }

    public Double3 ToWorld(Vector3 localPos)
    {
        return worldOrigin + (Double3)localPos;
    }

    public void SyncAllTransforms(bool includeOrbitDriven = true, bool forceTransformSync = false)
    {
        foreach (WorldPosition wp in tracked)
        {
            if (wp == null) continue;

            if (!includeOrbitDriven &&
                (wp.GetComponent<OrbitRevolution>() != null || wp.GetComponent<Spaceship>() != null))
            {
                continue;
            }

            Vector3 localPosition = ToLocal(wp.worldPosition);
            Rigidbody rb = wp.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (forceTransformSync)
                {
                    SuspendOrbitInterpolationForOriginRebase(wp);
                }

                rb.position = localPosition;
                if (forceTransformSync)
                {
                    wp.transform.position = localPosition;
                }
            }
            else
            {
                wp.transform.position = localPosition;
            }
        }
    }
    private static void SuspendOrbitInterpolationForOriginRebase(WorldPosition wp)
    {
        if (wp == null) return;

        OrbitRevolution orbitRevolution = wp.GetComponent<OrbitRevolution>();
        if (orbitRevolution != null)
        {
            orbitRevolution.SuspendInterpolationForOriginRebase();
        }
    }
    private bool TryRecenterVisualWorldOrigin()
    {
        if (!TryResolveDesiredWorldOrigin(out Double3 nextOrigin))
        {
            return false;
        }

        Double3 delta = worldOrigin - nextOrigin;
        if (Mathf.Approximately((float)delta.x, 0f) &&
            Mathf.Approximately((float)delta.y, 0f) &&
            Mathf.Approximately((float)delta.z, 0f))
        {
            return false;
        }

        SetWorldOrigin(nextOrigin);
        CameraManager.Instance?.ApplyWorldOriginShift(delta.ToVector3());
        return true;
    }

    private bool TryRecenterPhysicsWorldOrigin()
    {
        Transform focus = FocusManager.currentFocus;
        if (!TryResolveSpaceshipFocus(focus, out _))
        {
            return false;
        }

        if (!TryResolveFocusDesiredWorldOrigin(focus, out Double3 nextOrigin))
        {
            return false;
        }

        Double3 delta = worldOrigin - nextOrigin;
        if (Mathf.Approximately((float)delta.x, 0f) &&
            Mathf.Approximately((float)delta.y, 0f) &&
            Mathf.Approximately((float)delta.z, 0f))
        {
            return false;
        }

        SetWorldOrigin(nextOrigin);
        CameraManager.Instance?.ApplyWorldOriginShift(delta.ToVector3());
        return true;
    }

    private bool TryResolveDesiredWorldOrigin(out Double3 nextOrigin)
    {
        Transform focus = FocusManager.currentFocus;
        if (focus != null && TryResolveFocusDesiredWorldOrigin(focus, out nextOrigin))
        {
            return true;
        }

        return TryResolveCameraDesiredWorldOrigin(out nextOrigin);
    }

    private bool TryResolveFocusDesiredWorldOrigin(Transform focus, out Double3 nextOrigin)
    {
        nextOrigin = worldOrigin;
        if (focus == null) return false;

        Double3 focusWorldPosition = ResolveFocusWorldPosition(focus);
        if (TryResolveSpaceshipFocus(focus, out _))
        {
            if (!ShouldRecenterPhysicsFocus(focusWorldPosition))
            {
                return false;
            }
        }

        nextOrigin = focusWorldPosition;
        return true;
    }

    private bool TryResolveCameraDesiredWorldOrigin(out Double3 nextOrigin)
    {
        nextOrigin = worldOrigin;
        if (ShouldDeferCameraFallbackRecenter())
        {
            return false;
        }

        CameraManager cameraManager = CameraManager.Instance;
        Vector3 cameraLocalPosition;
        if (cameraManager != null)
        {
            cameraLocalPosition = cameraManager.GetPlannedCameraLocalPosition();
        }
        else
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            cameraLocalPosition = camera.transform.position;
        }

        Vector2 planarCameraPosition = new Vector2(cameraLocalPosition.x, cameraLocalPosition.y);
        float threshold = GetCameraFallbackThreshold();
        if (planarCameraPosition.sqrMagnitude < threshold * threshold)
        {
            return false;
        }

        Double3 cameraWorldPosition = ToWorld(cameraLocalPosition);
        nextOrigin = new Double3(cameraWorldPosition.x, cameraWorldPosition.y, worldOrigin.z);
        return true;
    }

    private static bool ShouldDeferCameraFallbackRecenter()
    {
        UserInput input = UserInput.Instance;
        if (input == null)
        {
            return false;
        }

        // Free-camera dragging is a direct user-controlled pan. Rebasing in the same drag
        // loop introduces a visible pop because the pan delta and frame-of-reference change
        // are both applied inside the same interaction. Defer fallback rebasing until drag ends.
        return input.IsCameraDragging;
    }

    private Double3 ResolveFocusWorldPosition(Transform focus)
    {
        if (focus == null) return Double3.Zero;

        if (TryResolveSpaceshipFocus(focus, out Spaceship spaceship))
        {
            WorldPosition spaceshipWorldPosition = spaceship.GetComponent<WorldPosition>();
            if (spaceshipWorldPosition != null)
            {
                return spaceshipWorldPosition.worldPosition;
            }

            return ToWorld(spaceship.transform.position);
        }

        if (TryResolveOwnerSatelliteFocus(focus, out ArtificialSatellite ownerSatellite))
        {
            WorldPosition ownerWorldPosition = ownerSatellite.GetComponent<WorldPosition>();
            if (ownerWorldPosition != null)
            {
                return ownerWorldPosition.worldPosition;
            }

            return ToWorld(ownerSatellite.transform.position);
        }

        WorldPosition focusWorldPosition = focus.GetComponent<WorldPosition>();
        if (focusWorldPosition != null)
        {
            return focusWorldPosition.worldPosition;
        }

        return ToWorld(focus.position);
    }

    private bool ShouldRecenterPhysicsFocus(Double3 focusWorldPosition)
    {
        Vector3 focusLocalPosition = ToLocal(focusWorldPosition);
        focusLocalPosition.z = 0f;

        float threshold = GetCameraFallbackThreshold();
        return focusLocalPosition.sqrMagnitude >= threshold * threshold;
    }

    private static bool TryResolveSpaceshipFocus(Transform focus, out Spaceship spaceship)
    {
        return SpaceshipFocusUtility.TryResolveSpaceship(focus, out spaceship);
    }

    private static bool TryResolveOwnerSatelliteFocus(Transform focus, out ArtificialSatellite ownerSatellite)
    {
        ownerSatellite = null;
        if (focus == null) return false;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.TryGetOwnerSatellite(out ownerSatellite) && ownerSatellite != null)
        {
            return true;
        }

        ownerSatellite = focus.GetComponentInParent<ArtificialSatellite>();
        return ownerSatellite != null;
    }

    private static float GetCameraFallbackThreshold()
    {
        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager == null)
        {
            return 2000f;
        }

        return Mathf.Max(1f, cameraManager.physicsFocusRecenterThreshold);
    }

    public void SyncAllTransformsForPhysicsStep(bool includeOrbitDriven = true)
    {
        foreach (WorldPosition wp in tracked)
        {
            if (wp == null) continue;

            if (!includeOrbitDriven &&
                (wp.GetComponent<OrbitRevolution>() != null || wp.GetComponent<Spaceship>() != null))
            {
                continue;
            }

            Vector3 localPosition = ToLocal(wp.worldPosition);
            Rigidbody rb = wp.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (rb.isKinematic)
                {
                    // Origin rebasing is a frame-of-reference change, not physical motion.
                    // Teleport kinematic bodies to the rebased pose so interpolation does not
                    // blend old-origin and new-origin positions into visible jitter.
                    rb.position = localPosition;
                }
                else
                {
                    rb.position = localPosition;
                }
            }
            else
            {
                wp.transform.position = localPosition;
            }
        }
    }

    private void SyncAllTransformsForSimulationStep(bool forceTeleport)
    {
        foreach (WorldPosition wp in tracked)
        {
            if (wp == null) continue;

            Vector3 localPosition = ToLocal(wp.worldPosition);
            Rigidbody rb = wp.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (forceTeleport)
                {
                    SuspendOrbitInterpolationForOriginRebase(wp);
                    rb.position = localPosition;
                    wp.transform.position = localPosition;
                }
                else
                {
                    rb.MovePosition(localPosition);
                }
            }
            else
            {
                wp.transform.position = localPosition;
            }
        }
    }
}


