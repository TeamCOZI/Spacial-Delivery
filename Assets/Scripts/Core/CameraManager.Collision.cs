using UnityEngine;

public partial class CameraManager
{
    private const float CameraCollisionEpsilon = 0.0001f;
    private const float CameraCollisionMinDistance = 0.01f;
    private const float CameraCollisionPaddingMultiplier = 1.5f;
    private const float CameraCollisionProbeRadiusMultiplier = 1.25f;
    private const int CameraCollisionHitBufferSize = 16;

    private readonly RaycastHit[] cameraCollisionHits = new RaycastHit[CameraCollisionHitBufferSize];

    private Vector3 ResolveCollisionConstrainedCameraPosition(
        Transform focus,
        Vector3 focusAnchorPosition,
        Vector3 desiredCameraPosition)
    {
        if (focus == null || isAssemblyMode)
        {
            return desiredCameraPosition;
        }

        if (!TryEnsureCameraComponent())
        {
            return desiredCameraPosition;
        }

        Vector3 armVector = desiredCameraPosition - focusAnchorPosition;
        float desiredDistance = armVector.magnitude;
        if (desiredDistance <= CameraCollisionEpsilon)
        {
            return desiredCameraPosition;
        }

        Vector3 armDirection = armVector / desiredDistance;
        float probeRadius = GetCameraCollisionProbeRadius();
        float collisionPadding = GetCameraCollisionPadding();
        Transform collisionRoot = ResolveCameraCollisionRoot(focus);

        float minimumDistance = GetMinimumFocusCameraDistance();
        if (TryResolveFocusSurfaceDistance(collisionRoot, focusAnchorPosition, armDirection, desiredDistance, out float surfaceDistance))
        {
            minimumDistance = Mathf.Max(minimumDistance, surfaceDistance + probeRadius + collisionPadding);
        }

        float resolvedDistance = Mathf.Max(desiredDistance, minimumDistance);
        if (desiredDistance > minimumDistance)
        {
            Vector3 castOrigin = focusAnchorPosition + armDirection * minimumDistance;
            float castDistance = desiredDistance - minimumDistance;
            if (TryResolveObstacleDistance(collisionRoot, castOrigin, armDirection, castDistance, out float obstacleDistance))
            {
                resolvedDistance = Mathf.Min(
                    resolvedDistance,
                    minimumDistance + Mathf.Max(0f, obstacleDistance - collisionPadding));
            }
        }

        return focusAnchorPosition + armDirection * resolvedDistance;
    }

    private float GetMinimumFocusCameraDistance()
    {
        return Mathf.Max(CameraCollisionMinDistance, GetCameraCollisionProbeRadius() + GetCameraCollisionPadding());
    }

    private float GetCameraCollisionPadding()
    {
        if (!TryEnsureCameraComponent())
        {
            return CameraCollisionMinDistance;
        }

        return Mathf.Max(CameraCollisionMinDistance, cameraComponent.nearClipPlane * CameraCollisionPaddingMultiplier);
    }

    private float GetCameraCollisionProbeRadius()
    {
        if (!TryEnsureCameraComponent())
        {
            return CameraCollisionMinDistance;
        }

        return Mathf.Max(CameraCollisionMinDistance, cameraComponent.nearClipPlane * CameraCollisionProbeRadiusMultiplier);
    }

    private bool TryResolveFocusSurfaceDistance(
        Transform collisionRoot,
        Vector3 focusAnchorPosition,
        Vector3 armDirection,
        float desiredDistance,
        out float surfaceDistance)
    {
        surfaceDistance = 0f;
        if (collisionRoot == null)
        {
            return false;
        }

        Collider[] colliders = collisionRoot.GetComponentsInChildren<Collider>(true);
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!IsUsableCameraCollisionCollider(collider))
            {
                continue;
            }

            if (!TryResolveSurfaceDistanceAlongArm(collider, focusAnchorPosition, armDirection, desiredDistance, out float colliderSurfaceDistance))
            {
                continue;
            }

            surfaceDistance = Mathf.Max(surfaceDistance, colliderSurfaceDistance);
            found = true;
        }

        return found;
    }

    private bool TryResolveSurfaceDistanceAlongArm(
        Collider collider,
        Vector3 focusAnchorPosition,
        Vector3 armDirection,
        float desiredDistance,
        out float surfaceDistance)
    {
        surfaceDistance = 0f;
        if (collider == null)
        {
            return false;
        }

        float overshoot = desiredDistance + collider.bounds.extents.magnitude + GetMinimumFocusCameraDistance();
        Vector3 rayOrigin = focusAnchorPosition + armDirection * overshoot;
        Ray ray = new Ray(rayOrigin, -armDirection);
        if (!collider.Raycast(ray, out RaycastHit hit, overshoot * 2f))
        {
            return false;
        }

        float projectedDistance = Vector3.Dot(hit.point - focusAnchorPosition, armDirection);
        if (projectedDistance < 0f)
        {
            return false;
        }

        surfaceDistance = projectedDistance;
        return true;
    }

    private bool TryResolveObstacleDistance(
        Transform collisionRoot,
        Vector3 castOrigin,
        Vector3 armDirection,
        float castDistance,
        out float obstacleDistance)
    {
        obstacleDistance = 0f;
        if (castDistance <= CameraCollisionEpsilon)
        {
            return false;
        }

        float probeRadius = GetCameraCollisionProbeRadius();
        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            probeRadius,
            armDirection,
            cameraCollisionHits,
            castDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = castDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraCollisionHits[i];
            Collider collider = hit.collider;
            if (!IsUsableCameraCollisionCollider(collider))
            {
                continue;
            }

            if (collisionRoot != null && collider.transform.IsChildOf(collisionRoot))
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            found = true;
        }

        obstacleDistance = bestDistance;
        return found;
    }

    private static Transform ResolveCameraCollisionRoot(Transform focus)
    {
        if (focus == null)
        {
            return null;
        }

        if (SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship) && spaceship != null)
        {
            return spaceship.transform;
        }

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        return focus;
    }

    private static bool IsUsableCameraCollisionCollider(Collider collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger;
    }
}
