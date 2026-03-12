using UnityEngine;

public partial class CameraManager
{
    protected override void HandleFocusChanged(Transform focused)
    {
        UpdateFocus(focused);
    }

    private void UpdateFocus(Transform newFocus)
    {
        bool canFollow = CanFollowFocusRotation(newFocus);
        float newTargetRotation = canFollow ? FocusPolicy.ResolveTargetRotationZ(newFocus) : 0f;
        float currentCameraRotation = transform.eulerAngles.z;

        targetRotationZ = newTargetRotation;
        rotationOffsetZ = Mathf.DeltaAngle(targetRotationZ, currentCameraRotation);
        rotationOffsetVelocity = 0f;

        if (newFocus != null)
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
            else
            {
                if (!FocusPolicy.IsSpaceshipFocus(newFocus))
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
            target = new Vector3(cameraPosition.x, cameraPosition.y, 0);
        }

        Vector3 offsetBasePosition = cameraComponent != null ? cameraComponent.transform.position : transform.position;
        offset = offsetBasePosition - target;
        dragOffset = Vector2.zero;

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

            // Unfocus by XY.
            float gravityRadius = gravity.GravityRadius;
            if (this.dragOffset.sqrMagnitude > gravityRadius * gravityRadius)
            {
                FocusManager.Instance?.SetFocus(null);
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
                maxZ = FocusPolicy.GetZoomScaleForFocus(oldFocus) * -1.1f;
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

