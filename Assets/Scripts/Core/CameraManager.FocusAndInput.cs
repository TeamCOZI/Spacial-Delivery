using UnityEngine;

public partial class CameraManager
{
    protected override void HandleFocusChanged(Transform focused)
    {
        bool shouldResetZoom = resetZoomOnNextFocusChange;
        resetZoomOnNextFocusChange = false;
        UpdateFocus(focused, shouldResetZoom);
    }

    private void UpdateFocus(Transform newFocus, bool resetZoom = false)
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
            if (resetZoom && !preserveDraggedView)
            {
                zoomOffset = ResolveResetZoomOffset(newFocus);
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
        if (!TryResolveFocusSpaceship(newFocus, out _))
        {
            activeSpaceshipGravityZoomSource = null;
            CancelGravityZoomTransition();
        }

        focusAutoPromoteMinObservedZoomDistance = float.PositiveInfinity;
        oldFocus = newFocus;
    }

    internal void ResetToCurrentFocusView()
    {
        if (oldFocus == null) return;
        if (IsCurrentFocusViewResetPendingOrApplied()) return;

        ClearPendingDraggedFocusTransition();
        UpdateFocus(oldFocus, true);
        forceInstantCameraUpdate = false;
    }

    internal void RequestZoomResetOnNextFocusChange()
    {
        resetZoomOnNextFocusChange = true;
    }

    internal void ApplyImmediateLauncherLaunchZoom(Transform launcher)
    {
        if (launcher == null || isAssemblyMode) return;
        if (!TryResolveFocusSpaceship(oldFocus, out _)) return;
        if (!TryResolveLaunchOwnerSatellite(launcher, out ArtificialSatellite ownerSatellite)) return;

        Transform launchZoomReference = ResolveLaunchImmediateZoomReference(ownerSatellite);
        zoomOffset = ResolveAlignedFocusZoomOffset(launchZoomReference);
        offset.z = zoomOffset;
        currentVelocity.z = 0f;
        gravityZoomTransitionCurrentTargetZ = zoomOffset;
        gravityZoomTransitionVelocity = 0f;
        isGravityZoomTransitionActive = false;

        Gravity ownerGravity = launchZoomReference != null ? launchZoomReference.GetComponent<Gravity>() : null;
        if (ownerGravity != null)
        {
            activeSpaceshipGravityZoomSource = ownerGravity;
        }
    }
    private Vector3 ResolveFocusFollowPosition(Transform focus)
    {
        if (focus == null) return target;

        if (SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship))
        {
            return spaceship.transform.position;
        }

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            return partFocus.transform.position;
        }

        return focus.position;
    }

    private static bool TryResolveLaunchOwnerSatellite(Transform launcher, out ArtificialSatellite ownerSatellite)
    {
        ownerSatellite = null;
        if (launcher == null) return false;

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = launcher.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            ownerSatellite = partFocus.OwnerSatellite;
            return true;
        }

        ownerSatellite = launcher.GetComponentInParent<ArtificialSatellite>();
        return ownerSatellite != null;
    }

    private static Transform ResolveLaunchImmediateZoomReference(ArtificialSatellite ownerSatellite)
    {
        if (ownerSatellite == null) return null;

        OrbitRevolution orbit = ownerSatellite.GetComponent<OrbitRevolution>();
        if (orbit != null && orbit.center != null)
        {
            return orbit.center.transform;
        }

        return ownerSatellite.transform;
    }

    private float ResolveResetZoomOffset(Transform focus)
    {
        if (focus == null)
        {
            Vector3 cameraPosition = cameraComponent != null ? cameraComponent.transform.position : transform.position;
            return cameraPosition.z;
        }

        if (FocusPolicy.IsLauncherPartFocus(focus))
        {
            return -10000f;
        }

        if (isAssemblyMode)
        {
            float assemblyZoom = -Mathf.Max(assemblyMinDistance, assemblyFocusDistance);
            return Mathf.Min(assemblyZoom, -assemblyMinDistance);
        }

        if (FocusPolicy.IsSpaceshipFocus(focus))
        {
            return zoomOffset;
        }

        return ResolveAlignedFocusZoomOffset(focus);
    }

    private float ResolveAlignedFocusZoomOffset(Transform focus)
    {
        if (focus == null) return followZ;

        float focusScale = Mathf.Max(0.01f, FocusPolicy.GetZoomScaleForFocus(focus));
        return focusScale * followZ;
    }

    private bool IsCurrentFocusViewResetPendingOrApplied()
    {
        if (hasPendingDraggedFocusTransition)
        {
            return false;
        }

        if (dragOffset.sqrMagnitude > 0.0001f)
        {
            return false;
        }

        float resetZoomOffset = ResolveResetZoomOffset(oldFocus);
        return Mathf.Abs(zoomOffset - resetZoomOffset) <= 0.001f;
    }

    private void UpdateDragOffset(Vector2 dragDelta)
    {
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

    private bool TryPromoteFocusToParentByZoomDistance(
        Transform currentFocus,
        Vector3 focusAnchorPosition,
        Vector3 cameraPosition)
    {
        if (isAssemblyMode || currentFocus == null) return false;
        if (FocusPolicy.IsLauncherPartFocus(currentFocus)) return false;
        if (!TryResolveParentFocusTarget(currentFocus, out Transform parentFocus)) return false;

        float focusScale = FocusPolicy.GetZoomScaleForFocus(currentFocus);
        if (focusScale <= 0f) return false;

        float zoomDistance = Mathf.Abs(zoomOffset);
        focusAutoPromoteMinObservedZoomDistance = Mathf.Min(focusAutoPromoteMinObservedZoomDistance, zoomDistance);

        float minimumObservedThreshold = focusAutoPromoteMinObservedZoomDistance * 1.05f;
        float promotionThreshold = Mathf.Max(focusScale * 100f, minimumObservedThreshold);
        if (zoomDistance <= promotionThreshold) return false;

        FocusManager focusManager = FocusManager.Instance;
        if (focusManager == null) return false;

        BeginPendingDraggedFocusTransition(parentFocus, cameraPosition);
        focusManager.SetFocus(parentFocus);
        return true;
    }

    private static bool TryResolveParentFocusTarget(Transform currentFocus, out Transform parentFocus)
    {
        parentFocus = null;
        if (currentFocus == null) return false;

        AssemblyPartFocus partFocus = currentFocus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            parentFocus = partFocus.OwnerSatellite.transform;
            return true;
        }

        OrbitRevolution orbit = currentFocus.GetComponent<OrbitRevolution>();
        if (orbit != null && orbit.center != null)
        {
            parentFocus = orbit.center.transform;
            return true;
        }

        return false;
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
        float preservedZoomOffset = zoomOffset;
        float preservedLocalZ = preservedCameraLocalPosition.z - target.z;

        Vector3 desiredOffset = preservedCameraLocalPosition - target;
        Vector3 desiredPlanarOffset = desiredOffset;
        desiredPlanarOffset.z = 0f;

        dragOffset = ResolveStoredDragDelta(newFocus, new Vector2(desiredPlanarOffset.x, desiredPlanarOffset.y));
        Vector3 appliedDragOffset = ResolveAppliedDragOffset(newFocus);
        offset = preservedCameraLocalPosition - target - appliedDragOffset;
        offset.z = preservedLocalZ;
        zoomOffset = preservedZoomOffset;
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

    private void UpdateSpaceshipGravityFieldZoom()
    {
        if (isAssemblyMode)
        {
            activeSpaceshipGravityZoomSource = null;
            CancelGravityZoomTransition();
            return;
        }

        if (!TryResolveFocusSpaceship(oldFocus, out _))
        {
            activeSpaceshipGravityZoomSource = null;
            CancelGravityZoomTransition();
            return;
        }

        Gravity previousGravitySource = activeSpaceshipGravityZoomSource;
        Gravity nextGravitySource = ResolveCurrentSpaceshipGravityZoomSource(oldFocus);
        if (nextGravitySource == previousGravitySource)
        {
            return;
        }

        activeSpaceshipGravityZoomSource = nextGravitySource;
        if (nextGravitySource == null)
        {
            return;
        }

        zoomOffset = ResolveAlignedFocusZoomOffset(nextGravitySource.transform);
        if (ShouldUseSlowGravityExitTransition(previousGravitySource, nextGravitySource))
        {
            BeginGravityZoomTransition();
            return;
        }

        CancelGravityZoomTransition();
    }

    private void BeginGravityZoomTransition()
    {
        if (gravityZoomTransitionDuration <= 0f)
        {
            CancelGravityZoomTransition();
            return;
        }

        isGravityZoomTransitionActive = true;
        gravityZoomTransitionCurrentTargetZ = offset.z;
        gravityZoomTransitionVelocity = 0f;
    }

    private bool ShouldUseSlowGravityExitTransition(Gravity previousGravitySource, Gravity nextGravitySource)
    {
        if (previousGravitySource == null || nextGravitySource == null)
        {
            return false;
        }

        float previousZoomOffset = ResolveAlignedFocusZoomOffset(previousGravitySource.transform);
        float nextZoomOffset = ResolveAlignedFocusZoomOffset(nextGravitySource.transform);
        return nextZoomOffset < previousZoomOffset - 0.001f;
    }

    private void CancelGravityZoomTransition(bool suppressCurrentGravitySource = false)
    {
        isGravityZoomTransitionActive = false;
        gravityZoomTransitionVelocity = 0f;
        gravityZoomTransitionCurrentTargetZ = zoomOffset;

        if (!suppressCurrentGravitySource)
        {
            return;
        }

        if (isAssemblyMode || !TryResolveFocusSpaceship(oldFocus, out _))
        {
            return;
        }

        activeSpaceshipGravityZoomSource = ResolveCurrentSpaceshipGravityZoomSource(oldFocus);
    }

    private float ResolveCurrentOffsetZTarget()
    {
        if (!ShouldUseGravityZoomTransition())
        {
            gravityZoomTransitionCurrentTargetZ = zoomOffset;
            return zoomOffset;
        }

        gravityZoomTransitionCurrentTargetZ = Mathf.SmoothDamp(
            gravityZoomTransitionCurrentTargetZ,
            zoomOffset,
            ref gravityZoomTransitionVelocity,
            Mathf.Max(0.0001f, gravityZoomTransitionDuration),
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        const float positionEpsilon = 0.001f;
        const float velocityEpsilon = 0.001f;
        if (Mathf.Abs(gravityZoomTransitionCurrentTargetZ - zoomOffset) <= positionEpsilon
            && Mathf.Abs(gravityZoomTransitionVelocity) <= velocityEpsilon)
        {
            CancelGravityZoomTransition();
            gravityZoomTransitionCurrentTargetZ = zoomOffset;
            return zoomOffset;
        }

        return gravityZoomTransitionCurrentTargetZ;
    }

    private bool ShouldUseGravityZoomTransition()
    {
        return isGravityZoomTransitionActive
            && !isAssemblyMode
            && activeSpaceshipGravityZoomSource != null
            && TryResolveFocusSpaceship(oldFocus, out _);
    }
    private Gravity ResolveCurrentSpaceshipGravityZoomSource(Transform focus)
    {
        if (focus == null) return null;

        Double3 spaceshipWorldPoint = ResolveFocusWorldOrigin(focus);
        var gravities = Gravity.ActiveGravities;
        if (gravities == null || gravities.Count == 0)
        {
            return null;
        }

        Gravity bestGravity = null;
        int bestDepth = int.MinValue;
        int bestRadius = int.MaxValue;
        double bestDistanceSq = double.MaxValue;

        for (int i = 0; i < gravities.Count; i++)
        {
            Gravity gravity = gravities[i];
            if (gravity == null) continue;

            int gravityRadius = gravity.GravityRadius;
            if (gravityRadius <= 0) continue;

            Double3 gravityWorldPoint = ResolveFocusWorldOrigin(gravity.transform);
            double distanceSq = GetPlanarDistanceSq(spaceshipWorldPoint, gravityWorldPoint);
            double radiusSq = (double)gravityRadius * gravityRadius;
            if (distanceSq > radiusSq)
            {
                continue;
            }

            int hierarchyDepth = GetGravityHierarchyDepth(gravity.transform);
            bool isBetterCandidate = hierarchyDepth > bestDepth;
            if (!isBetterCandidate && hierarchyDepth == bestDepth)
            {
                isBetterCandidate = gravityRadius < bestRadius;
            }
            if (!isBetterCandidate && hierarchyDepth == bestDepth && gravityRadius == bestRadius)
            {
                isBetterCandidate = distanceSq < bestDistanceSq;
            }
            if (!isBetterCandidate)
            {
                continue;
            }

            bestGravity = gravity;
            bestDepth = hierarchyDepth;
            bestRadius = gravityRadius;
            bestDistanceSq = distanceSq;
        }

        return bestGravity;
    }

    private static int GetGravityHierarchyDepth(Transform gravitySource)
    {
        if (gravitySource == null) return int.MinValue;

        int depth = 0;
        Transform current = gravitySource;
        while (current != null)
        {
            OrbitRevolution orbit = current.GetComponent<OrbitRevolution>();
            if (orbit == null || orbit.center == null)
            {
                break;
            }

            if (orbit.center.GetComponent<Gravity>() != null)
            {
                depth++;
            }

            current = orbit.center.transform;
        }

        return depth;
    }

    private void UpdateZoomOffset(float zoomDelta)
    {
        if (!Mathf.Approximately(zoomDelta, 0f))
        {
            CancelGravityZoomTransition(suppressCurrentGravitySource: true);
        }

        float maxZ;
        if (isAssemblyMode)
        {
            maxZ = -assemblyMinDistance;
        }
        else if (oldFocus != null)
        {
            maxZ = -GetMinimumFocusCameraDistance();
        }
        else
        {
            maxZ = -Mathf.Max(unfocusedMinDistance, GetMinimumFocusCameraDistance());
        }

        if (zoomDelta < 0f && isFocusSurfaceMinimumDistanceActive)
        {
            float previousZoomOffset = this.zoomOffset;
            this.zoomOffset = focusSurfaceMinimumDistanceZoomOffset;
            Debug.Log($"[CameraZoomSurfaceSync] frame={Time.frameCount} input={zoomDelta:F4} previousZoomOffset={previousZoomOffset:F4} syncedZoomOffset={this.zoomOffset:F4} cameraZ={transform.position.z:F4}");
            isFocusSurfaceMinimumDistanceActive = false;
        }

        this.zoomOffset = Mathf.Clamp(this.zoomOffset + zoomDelta * Mathf.Abs(this.zoomOffset) * zoomSpeed, minZ, maxZ);
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





































