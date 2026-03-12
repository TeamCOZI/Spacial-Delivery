using UnityEngine;

public partial class CameraManager
{
    protected override void HandleFocusChanged(Transform focused)
    {
        UpdateFocus(focused);
    }

    private void UpdateFocus(Transform newFocus)
    {
        bool preserveDraggedView = TryConsumePendingDraggedFocusTransition(newFocus, out Vector3 preservedCameraLocalPosition);
        bool canFollow = CanFollowFocusRotation(newFocus);
        float newTargetRotation = canFollow ? FocusPolicy.ResolveTargetRotationZ(newFocus) : 0f;
        float currentCameraRotation = transform.eulerAngles.z;

        targetRotationZ = newTargetRotation;
        rotationOffsetZ = Mathf.DeltaAngle(targetRotationZ, currentCameraRotation);
        rotationOffsetVelocity = 0f;

        if (newFocus != null)
        {
            if (!preserveDraggedView)
            {
                if (FocusPolicy.IsLauncherPartFocus(newFocus))
                {
                    zoomOffset = -10000f;
                }
                else if (isAssemblyMode)
                {
                    zoomOffset = -Mathf.Max(assemblyMinDistance, assemblyFocusDistance);
                    zoomOffset = Mathf.Min(zoomOffset, -assemblyMinDistance);
                }
                else if (!FocusPolicy.IsSpaceshipFocus(newFocus))
                {
                    zoomOffset = FocusPolicy.GetZoomScaleForFocus(newFocus) * followZ;
                }
            }

            target = ResolveFocusFollowPosition(newFocus);
        }
        else if (oldFocus != null)
        {
            Vector3 cameraPosition = cameraComponent != null ? cameraComponent.transform.position : transform.position;
            zoomOffset = cameraPosition.z;
            target = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
        }

        if (preserveDraggedView && newFocus != null)
        {
            ApplyDraggedFocusTransition(newFocus, preservedCameraLocalPosition);
        }
        else
        {
            Vector3 offsetBasePosition = cameraComponent != null ? cameraComponent.transform.position : transform.position;
            offset = offsetBasePosition - target;
            dragOffset = Vector2.zero;
        }

        oldFocus = newFocus;
    }

    private Vector3 ResolveFocusFollowPosition(Transform focus)
    {
        if (focus == null) return target;

        if (SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship))
        {
            return spaceship.transform.position;
        }

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform.position;
        }

        return focus.position;
    }

    private void UpdateDragOffset(Vector2 dragDelta)
    {
        bool shouldClampDragDelta = oldFocus != null;
        if (shouldClampDragDelta &&
            dragDelta.sqrMagnitude > maxDragDeltaPerFrame * maxDragDeltaPerFrame)
        {
            dragDelta = dragDelta.normalized * maxDragDeltaPerFrame;
        }

        dragDelta = ResolveStoredDragDelta(oldFocus, dragDelta);
        this.dragOffset -= dragDelta;

        if (isAssemblyMode)
        {
            return;
        }

        if (oldFocus != null)
        {
            Gravity gravity = oldFocus.GetComponent<Gravity>();
            if (gravity == null) return;

            float gravityRadius = gravity.GravityRadius;
            if (this.dragOffset.sqrMagnitude > gravityRadius * gravityRadius)
            {
                HandleDraggedFocusExit(oldFocus);
            }
        }
    }

    private void HandleDraggedFocusExit(Transform previousFocus)
    {
        Vector3 plannedCameraLocalPosition = GetPlannedCameraLocalPosition();
        Double3 viewedWorldPoint = ResolveDraggedViewWorldPoint(plannedCameraLocalPosition);
        Transform nextFocus = ResolveDraggedCelestialFocus(previousFocus, viewedWorldPoint);
        if (nextFocus == previousFocus)
        {
            return;
        }

        FocusManager focusManager = FocusManager.Instance;
        if (nextFocus != null)
        {
            BeginPendingDraggedFocusTransition(nextFocus, plannedCameraLocalPosition);
        }
        else
        {
            ClearPendingDraggedFocusTransition();
        }

        if (focusManager != null)
        {
            focusManager.SetFocus(nextFocus);
            return;
        }

        ClearPendingDraggedFocusTransition();
        if (nextFocus == null)
        {
            dragOffset = Vector2.zero;
        }
    }

    private Transform ResolveDraggedCelestialFocus(Transform previousFocus, Double3 viewedWorldPoint)
    {
        if (!IsNaturalCelestialFocus(previousFocus))
        {
            return null;
        }

        if (TryResolveNaturalCelestialFocusAtWorldPoint(viewedWorldPoint, out Transform nextFocus))
        {
            return nextFocus;
        }

        return null;
    }

    private static Double3 ResolveDraggedViewWorldPoint(Vector3 plannedCameraLocalPosition)
    {
        LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;
        Vector3 planarLocalPoint = new Vector3(plannedCameraLocalPosition.x, plannedCameraLocalPosition.y, 0f);
        if (coordinator != null)
        {
            return coordinator.ToWorld(planarLocalPoint);
        }

        return (Double3)planarLocalPoint;
    }

    private void BeginPendingDraggedFocusTransition(Transform nextFocus, Vector3 plannedCameraLocalPosition)
    {
        hasPendingDraggedFocusTransition = nextFocus != null;
        pendingDraggedFocusTarget = nextFocus;
        pendingDraggedCameraLocalPosition = plannedCameraLocalPosition;
    }

    private bool TryConsumePendingDraggedFocusTransition(Transform newFocus, out Vector3 preservedCameraLocalPosition)
    {
        preservedCameraLocalPosition = Vector3.zero;
        if (!hasPendingDraggedFocusTransition || newFocus == null || pendingDraggedFocusTarget != newFocus)
        {
            ClearPendingDraggedFocusTransition();
            return false;
        }

        preservedCameraLocalPosition = pendingDraggedCameraLocalPosition;
        ClearPendingDraggedFocusTransition();
        return true;
    }

    private void ClearPendingDraggedFocusTransition()
    {
        hasPendingDraggedFocusTransition = false;
        pendingDraggedFocusTarget = null;
        pendingDraggedCameraLocalPosition = Vector3.zero;
    }

    private void ApplyDraggedFocusTransition(Transform newFocus, Vector3 preservedCameraLocalPosition)
    {
        Vector3 desiredOffset = preservedCameraLocalPosition - target;
        zoomOffset = desiredOffset.z;

        Vector3 desiredPlanarOffset = desiredOffset;
        desiredPlanarOffset.z = 0f;

        dragOffset = ResolveStoredDragDelta(newFocus, new Vector2(desiredPlanarOffset.x, desiredPlanarOffset.y));
        Vector3 appliedDragOffset = ResolveAppliedDragOffset(newFocus);
        offset = preservedCameraLocalPosition - target - appliedDragOffset;
        offset.z = zoomOffset;
    }

    private static bool TryResolveNaturalCelestialFocusAtWorldPoint(Double3 worldPoint, out Transform focusTarget)
    {
        focusTarget = null;

        int bestDepth = int.MinValue;
        int bestRadius = int.MaxValue;
        double bestDistanceSq = double.MaxValue;
        var gravities = Gravity.ActiveGravities;
        for (int i = 0; i < gravities.Count; i++)
        {
            Gravity gravity = gravities[i];
            if (!TryResolveNaturalCelestialFocusCandidate(gravity, out Transform candidateFocus, out int candidateDepth))
            {
                continue;
            }

            Double3 candidateWorldPoint = ResolveFocusWorldOrigin(candidateFocus);
            int candidateRadius = gravity.GravityRadius;
            double distanceSq = GetPlanarDistanceSq(worldPoint, candidateWorldPoint);
            double radiusSq = (double)candidateRadius * candidateRadius;
            if (distanceSq > radiusSq)
            {
                continue;
            }

            bool isBetterCandidate = candidateDepth > bestDepth;
            if (!isBetterCandidate && candidateDepth == bestDepth)
            {
                isBetterCandidate = candidateRadius < bestRadius;
            }
            if (!isBetterCandidate && candidateDepth == bestDepth && candidateRadius == bestRadius)
            {
                isBetterCandidate = distanceSq < bestDistanceSq;
            }
            if (!isBetterCandidate)
            {
                continue;
            }

            bestDepth = candidateDepth;
            bestRadius = candidateRadius;
            bestDistanceSq = distanceSq;
            focusTarget = candidateFocus;
        }

        return focusTarget != null;
    }

    private static bool TryResolveNaturalCelestialFocusCandidate(
        Gravity gravity,
        out Transform focusTarget,
        out int hierarchyDepth)
    {
        focusTarget = null;
        hierarchyDepth = int.MinValue;
        if (gravity == null) return false;

        Transform candidate = gravity.transform;
        if (!IsNaturalCelestialFocus(candidate)) return false;

        focusTarget = candidate;
        hierarchyDepth = GetNaturalCelestialHierarchyDepth(candidate);
        return true;
    }

    private static bool IsNaturalCelestialFocus(Transform focus)
    {
        if (focus == null) return false;

        return focus.GetComponent<Star>() != null
            || focus.GetComponent<Planet>() != null
            || focus.GetComponent<Satellite>() != null;
    }

    private static int GetNaturalCelestialHierarchyDepth(Transform focus)
    {
        if (focus == null) return int.MinValue;

        int depth = 0;
        Transform current = focus;
        while (current != null)
        {
            OrbitRevolution orbit = current.GetComponent<OrbitRevolution>();
            if (orbit == null || orbit.center == null)
            {
                break;
            }

            Transform parent = orbit.center.transform;
            if (IsNaturalCelestialFocus(parent))
            {
                depth++;
            }

            current = parent;
        }

        return depth;
    }

    private static double GetPlanarDistanceSq(Double3 a, Double3 b)
    {
        double dx = a.x - b.x;
        double dy = a.y - b.y;
        return dx * dx + dy * dy;
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
                maxZ = -GetMinimumFocusCameraDistance();
            }
        }

        this.zoomOffset = Mathf.Clamp(this.zoomOffset + zoomOffset * Mathf.Abs(this.zoomOffset) * zoomSpeed, minZ, maxZ);
    }

    private void UpdateFocusRotation()
    {
        bool canFollow = CanFollowFocusRotation(oldFocus);
        targetRotationZ = canFollow ? FocusPolicy.ResolveTargetRotationZ(oldFocus) : 0f;

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
                Mathf.Max(0.001f, focusRotationSmoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }

        float finalZ = targetRotationZ + rotationOffsetZ;
        transform.rotation = Quaternion.Euler(0f, 0f, finalZ);
    }

    private bool CanFollowFocusRotation(Transform focus)
    {
        return followFocusZRotation
            && focus != null
            && (isAssemblyMode || FocusPolicy.IsSatelliteRelatedFocus(focus));
    }
}


