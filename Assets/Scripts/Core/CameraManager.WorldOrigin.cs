using System;
using UnityEngine;

public partial class CameraManager
{
    private void HandleWorldShift(Vector3 shiftDelta)
    {
        target += shiftDelta;
    }

    private static Double3 ResolveFocusWorldOrigin(Transform focus)
    {
        if (focus == null) return Double3.Zero;

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

    private void UpdateDynamicWorldOrigin()
    {
        if (oldFocus == null) return;
        LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;
        if (coordinator == null) return;
        if (IsPhysicsDrivenFocus(oldFocus)) return;

        Double3 previousOrigin = coordinator.worldOrigin;
        Double3 focusWorldPosition = ResolveInterpolatedFocusWorldOrigin(oldFocus, coordinator);
        Double3 delta = previousOrigin - focusWorldPosition;

        if (Math.Abs(delta.x) < 1e-9 && Math.Abs(delta.y) < 1e-9 && Math.Abs(delta.z) < 1e-9)
        {
            return;
        }

        coordinator.SetWorldOrigin(focusWorldPosition);
        coordinator.SyncAllTransforms();

        // Keep camera continuity while origin follows the focused body every frame.
        Vector3 originShift = delta.ToVector3();
        transform.position += originShift;
        target += originShift;
    }

    private static Double3 ResolveInterpolatedFocusWorldOrigin(Transform focus, LargeWorldCoordinator coordinator)
    {
        if (focus == null || coordinator == null) return Double3.Zero;

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

    private static bool IsPhysicsDrivenFocus(Transform focus)
    {
        if (focus == null) return false;

        Spaceship spaceship = focus.GetComponentInParent<Spaceship>();
        if (spaceship == null) return false;

        Rigidbody spaceshipRb = spaceship.GetComponent<Rigidbody>();
        return spaceshipRb != null && !spaceshipRb.isKinematic;
    }
}
