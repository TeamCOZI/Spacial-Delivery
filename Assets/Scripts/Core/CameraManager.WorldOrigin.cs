using System;
using UnityEngine;

public partial class CameraManager
{
    private void HandleWorldShift(Vector3 shiftDelta)
    {
        target += shiftDelta;
    }

    private void FixedUpdate()
    {
        UpdatePhysicsDrivenWorldOrigin();
    }

    private static Double3 ResolveFocusWorldOrigin(Transform focus)
    {
        if (focus == null) return Double3.Zero;

        if (TryResolveFocusSpaceship(focus, out Spaceship spaceship))
        {
            WorldPosition spaceshipWp = spaceship.GetComponent<WorldPosition>();
            if (spaceshipWp != null) return spaceshipWp.worldPosition;
            return (Double3)spaceship.transform.position;
        }

        if (TryResolveFocusOwnerSatellite(focus, out ArtificialSatellite ownerSatellite))
        {
            WorldPosition ownerWp = ownerSatellite.GetComponent<WorldPosition>();
            if (ownerWp != null) return ownerWp.worldPosition;
            return (Double3)ownerSatellite.transform.position;
        }

        WorldPosition wp = focus.GetComponent<WorldPosition>();
        if (wp != null) return wp.worldPosition;

        return (Double3)focus.position;
    }

    private static Double3 ResolveStableFocusWorldOrigin(Transform focus, LargeWorldCoordinator coordinator)
    {
        if (focus == null) return Double3.Zero;

        if (TryResolveFocusSpaceship(focus, out Spaceship spaceship))
        {
            WorldPosition spaceshipWorldPosition = spaceship.GetComponent<WorldPosition>();
            if (spaceshipWorldPosition != null) return spaceshipWorldPosition.worldPosition;

            Transform spaceshipTransform = spaceship.transform;
            if (coordinator != null)
            {
                return coordinator.ToWorld(spaceshipTransform.position);
            }

            return (Double3)spaceshipTransform.position;
        }

        return ResolveFocusWorldOrigin(focus);
    }

    private void UpdateDynamicWorldOrigin()
    {
        if (oldFocus == null) return;
        LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;
        if (coordinator == null) return;
        if (IsPhysicsDrivenFocus(oldFocus)) return;

        Double3 previousOrigin = coordinator.worldOrigin;
        Double3 focusWorldPosition = ResolveInterpolatedFocusWorldOrigin(oldFocus, coordinator);
        RecenterWorldOrigin(coordinator, previousOrigin, focusWorldPosition);
    }

    private void UpdatePhysicsDrivenWorldOrigin()
    {
        if (oldFocus == null) return;
        LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;
        if (coordinator == null) return;
        if (!IsPhysicsDrivenFocus(oldFocus)) return;

        Double3 previousOrigin = coordinator.worldOrigin;
        Double3 focusWorldPosition = ResolveStableFocusWorldOrigin(oldFocus, coordinator);
        if (!ShouldRecenterPhysicsFocus(coordinator, focusWorldPosition)) return;
        RecenterWorldOriginForPhysicsStep(coordinator, previousOrigin, focusWorldPosition);
    }

    private static Double3 ResolveInterpolatedFocusWorldOrigin(Transform focus, LargeWorldCoordinator coordinator)
    {
        if (focus == null || coordinator == null) return Double3.Zero;

        if (TryResolveFocusSpaceship(focus, out Spaceship spaceship))
        {
            WorldPosition spaceshipWorldPosition = spaceship.GetComponent<WorldPosition>();
            if (spaceshipWorldPosition != null)
            {
                return spaceshipWorldPosition.worldPosition;
            }

            return coordinator.ToWorld(spaceship.transform.position);
        }

        if (IsPhysicsDrivenFocus(focus))
        {
            return coordinator.ToWorld(focus.position);
        }

        WorldPosition focusWorldPosition = focus.GetComponent<WorldPosition>();
        if (focusWorldPosition != null)
        {
            return focusWorldPosition.worldPosition;
        }

        Transform originTransform = focus;
        if (TryResolveFocusOwnerSatellite(focus, out ArtificialSatellite ownerSatellite))
        {
            WorldPosition ownerWp = ownerSatellite.GetComponent<WorldPosition>();
            if (ownerWp != null)
            {
                return ownerWp.worldPosition;
            }
            originTransform = ownerSatellite.transform;
        }

        return coordinator.ToWorld(originTransform.position);
    }

    private static bool TryResolveFocusOwnerSatellite(Transform focus, out ArtificialSatellite ownerSatellite)
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

    private static bool TryResolveFocusSpaceship(Transform focus, out Spaceship spaceship)
    {
        return SpaceshipFocusUtility.TryResolveSpaceship(focus, out spaceship);
    }

    private static bool IsPhysicsDrivenFocus(Transform focus)
    {
        if (focus == null) return false;

        return TryResolveFocusSpaceship(focus, out _);
    }

    private bool ShouldRecenterPhysicsFocus(LargeWorldCoordinator coordinator, Double3 focusWorldPosition)
    {
        if (coordinator == null) return false;

        Vector3 focusLocalPosition = coordinator.ToLocal(focusWorldPosition);
        focusLocalPosition.z = 0f;
        float threshold = Mathf.Max(1f, physicsFocusRecenterThreshold);
        return focusLocalPosition.sqrMagnitude >= threshold * threshold;
    }

    private void RecenterWorldOrigin(
        LargeWorldCoordinator coordinator,
        Double3 previousOrigin,
        Double3 nextOrigin)
    {
        Double3 delta = previousOrigin - nextOrigin;
        if (Math.Abs(delta.x) < 1e-9 && Math.Abs(delta.y) < 1e-9 && Math.Abs(delta.z) < 1e-9)
        {
            return;
        }

        coordinator.SetWorldOrigin(nextOrigin);
        coordinator.SyncAllTransforms();

        // Apply the same shift to the camera rig so the view stays continuous.
        Vector3 originShift = delta.ToVector3();
        transform.position += originShift;
        target += originShift;
    }

    private void RecenterWorldOriginForPhysicsStep(
        LargeWorldCoordinator coordinator,
        Double3 previousOrigin,
        Double3 nextOrigin)
    {
        Double3 delta = previousOrigin - nextOrigin;
        if (Math.Abs(delta.x) < 1e-9 && Math.Abs(delta.y) < 1e-9 && Math.Abs(delta.z) < 1e-9)
        {
            return;
        }

        coordinator.SetWorldOrigin(nextOrigin);
        coordinator.SyncAllTransformsForPhysicsStep();

        Vector3 originShift = delta.ToVector3();
        transform.position += originShift;
        target += originShift;
    }
}
