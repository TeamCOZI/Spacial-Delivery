using UnityEngine;

public partial class CameraManager
{
    private const float CameraCollisionEpsilon = 0.0001f;
    private const float CameraCollisionMinDistance = 0.01f;
    private const float CameraCollisionCastExtraDistance = 1f;
    private const int CameraCollisionHitBufferSize = 16;

    private readonly RaycastHit[] cameraCollisionHits = new RaycastHit[CameraCollisionHitBufferSize];

    private void SetFocusSurfaceMinimumDistanceState(bool isActive, float zoomOffset = 0f)
    {
        isFocusSurfaceMinimumDistanceActive = isActive;
        focusSurfaceMinimumDistanceZoomOffset = isActive ? zoomOffset : 0f;
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
        }

        if (isAssemblyMode)
        {
            return desiredCameraPosition;
        }

        if (!TryResolveVerticalCameraCollisionZ(desiredCameraPosition, out float collisionLimitedZ))
        {
            return desiredCameraPosition;
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
        float castOriginZ = Mathf.Min(minZ, desiredCameraPosition.z) - CameraCollisionCastExtraDistance;
        float castEndZ = CameraCollisionCastExtraDistance;
        float castDistance = castEndZ - castOriginZ;
        if (castDistance <= CameraCollisionEpsilon)
        {
            return false;
        }

        Ray ray = new Ray(new Vector3(desiredCameraPosition.x, desiredCameraPosition.y, castOriginZ), Vector3.forward);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            cameraCollisionHits,
            castDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float closestHitDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraCollisionHits[i];
            Collider collider = hit.collider;
            if (!IsUsableCameraCollisionCollider(collider))
            {
                continue;
            }

            if (hit.distance >= closestHitDistance)
            {
                continue;
            }

            closestHitDistance = hit.distance;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        collisionLimitedZ = castOriginZ + closestHitDistance - surfaceOffset;
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

    private static bool IsUsableCameraCollisionCollider(Collider collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger;
    }
}
