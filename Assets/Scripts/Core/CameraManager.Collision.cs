using UnityEngine;

public partial class CameraManager
{
    private const float CameraCollisionEpsilon = 0.0001f;
    private const float CameraCollisionMinDistance = 0.01f;
    private const float CameraCollisionCastExtraDistance = 1f;

    private void SetFocusSurfaceMinimumDistanceState(bool isActive, float zoomOffset = 0f)
    {
        isFocusSurfaceMinimumDistanceActive = isActive;
        focusSurfaceMinimumDistanceZoomOffset = isActive ? zoomOffset : 0f;
    }

    private void SetCameraCollisionMinimumState(bool isActive, float minimumZ = 0f)
    {
        hasCameraCollisionMinimumZ = isActive;
        cameraCollisionMinimumZ = isActive ? minimumZ : 0f;
    }

    private Vector3 ResolveCollisionConstrainedCameraPosition(
        Transform focus,
        Vector3 focusAnchorPosition,
        Vector3 desiredCameraPosition,
        bool updateFocusSurfaceMinimumDistanceState = true)
    {
        if (updateFocusSurfaceMinimumDistanceState)
        {
            SetFocusSurfaceMinimumDistanceState(false);
            SetCameraCollisionMinimumState(false);
        }

        if (isAssemblyMode)
        {
            return desiredCameraPosition;
        }

        // Match click/focus queries: large-world systems shift transforms in LateUpdate,
        // so sync before any collision query that depends on current scene poses.
        Physics.SyncTransforms();

        if (!TryResolveVerticalCameraCollisionZ(desiredCameraPosition, out float collisionLimitedZ))
        {
            return desiredCameraPosition;
        }

        if (updateFocusSurfaceMinimumDistanceState)
        {
            SetCameraCollisionMinimumState(true, collisionLimitedZ);
        }

        if (desiredCameraPosition.z <= collisionLimitedZ + CameraCollisionEpsilon)
        {
            return desiredCameraPosition;
        }

        Vector3 resolvedCameraPosition = desiredCameraPosition;
        resolvedCameraPosition.z = collisionLimitedZ;

        if (updateFocusSurfaceMinimumDistanceState)
        {
            SetFocusSurfaceMinimumDistanceState(true, resolvedCameraPosition.z);
        }

        return resolvedCameraPosition;
    }

    private bool TryResolveVerticalCameraCollisionZ(Vector3 desiredCameraPosition, out float collisionLimitedZ)
    {
        collisionLimitedZ = desiredCameraPosition.z;

        float surfaceOffset = GetCameraCollisionSurfaceOffset();
        float castDistance = CameraCollisionCastExtraDistance - desiredCameraPosition.z;
        if (castDistance <= CameraCollisionEpsilon)
        {
            return false;
        }

        Ray ray = new Ray(desiredCameraPosition, Vector3.forward);
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        bool found = false;
        float closestHitDistance = float.PositiveInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!IsUsableCameraCollisionCollider(collider))
            {
                continue;
            }

            if (!TryRaycastVisualCollider(ray, collider, out float hitDistance))
            {
                continue;
            }

            if (hitDistance > castDistance + CameraCollisionEpsilon)
            {
                continue;
            }

            if (hitDistance >= closestHitDistance)
            {
                continue;
            }

            closestHitDistance = hitDistance;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        collisionLimitedZ = desiredCameraPosition.z + closestHitDistance - surfaceOffset;
        return true;
    }

    private float GetMinimumFocusCameraDistance()
    {
        return GetCameraCollisionSurfaceOffset();
    }

    private float GetCameraCollisionSurfaceOffset()
    {
        return Mathf.Max(CameraCollisionMinDistance, cameraCollisionSurfaceOffset);
    }

    private static bool TryRaycastVisualCollider(Ray ray, Collider collider, out float hitDistance)
    {
        hitDistance = 0f;
        if (collider == null) return false;

        if (collider is SphereCollider sphereCollider)
        {
            return TryRaycastVisualSphereCollider(ray, sphereCollider, out hitDistance);
        }

        if (collider is BoxCollider boxCollider)
        {
            return TryRaycastVisualBoxCollider(ray, boxCollider, out hitDistance);
        }

        if (collider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            hitDistance = hit.distance;
            return hitDistance >= 0f;
        }

        Bounds bounds = collider.bounds;
        if (!bounds.IntersectRay(ray, out hitDistance))
        {
            return false;
        }

        return hitDistance >= 0f;
    }

    private static bool TryRaycastVisualSphereCollider(Ray ray, SphereCollider sphereCollider, out float hitDistance)
    {
        hitDistance = 0f;
        if (sphereCollider == null) return false;

        Transform colliderTransform = sphereCollider.transform;
        Vector3 center = colliderTransform.TransformPoint(sphereCollider.center);
        Vector3 lossyScale = colliderTransform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
        float radius = sphereCollider.radius * Mathf.Max(0.0001f, maxScale);

        Vector3 offset = ray.origin - center;
        float a = Vector3.Dot(ray.direction, ray.direction);
        float b = 2f * Vector3.Dot(ray.direction, offset);
        float c = Vector3.Dot(offset, offset) - (radius * radius);
        float discriminant = (b * b) - (4f * a * c);
        if (discriminant < 0f)
        {
            return false;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float denominator = 2f * a;
        float entry = (-b - sqrtDiscriminant) / denominator;
        float exit = (-b + sqrtDiscriminant) / denominator;
        if (exit < 0f)
        {
            return false;
        }

        // Keep the signed entry distance so inside-volume queries resolve the back surface.
        hitDistance = entry;
        return true;
    }

    private static bool TryRaycastVisualBoxCollider(Ray ray, BoxCollider boxCollider, out float hitDistance)
    {
        hitDistance = 0f;
        if (boxCollider == null) return false;

        Matrix4x4 worldToLocal = boxCollider.transform.worldToLocalMatrix;
        Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(ray.origin) - boxCollider.center;
        Vector3 localDirection = worldToLocal.MultiplyVector(ray.direction);
        Vector3 extents = boxCollider.size * 0.5f;

        float tMin = 0f;
        float tMax = float.PositiveInfinity;

        if (!ClipAxis(localOrigin.x, localDirection.x, extents.x, ref tMin, ref tMax)) return false;
        if (!ClipAxis(localOrigin.y, localDirection.y, extents.y, ref tMin, ref tMax)) return false;
        if (!ClipAxis(localOrigin.z, localDirection.z, extents.z, ref tMin, ref tMax)) return false;

        if (tMax < 0f) return false;

        // Keep the signed entry distance so inside-volume queries resolve the back face.
        hitDistance = tMin;
        return true;
    }

    private static bool ClipAxis(float origin, float direction, float extent, ref float tMin, ref float tMax)
    {
        const float Epsilon = 1e-6f;
        if (Mathf.Abs(direction) < Epsilon)
        {
            return origin >= -extent && origin <= extent;
        }

        float invDirection = 1f / direction;
        float t1 = (-extent - origin) * invDirection;
        float t2 = (extent - origin) * invDirection;

        if (t1 > t2)
        {
            float swapT = t1;
            t1 = t2;
            t2 = swapT;
        }

        if (t1 > tMin)
        {
            tMin = t1;
        }

        if (t2 < tMax)
        {
            tMax = t2;
        }

        return tMin <= tMax;
    }

    private static bool IsUsableCameraCollisionCollider(Collider collider)
    {
        return collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy
            && !collider.isTrigger;
    }
}


