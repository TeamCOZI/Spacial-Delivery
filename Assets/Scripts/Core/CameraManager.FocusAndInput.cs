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
            LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;
            if (coordinator != null && !IsPhysicsDrivenFocus(newFocus))
            {
                Double3 previousOrigin = coordinator.worldOrigin;
                Double3 focusWorldPosition = ResolveFocusWorldOrigin(newFocus);
                coordinator.SetWorldOrigin(focusWorldPosition);
                coordinator.SyncAllTransforms();

                // Preserve camera continuity when origin changes to avoid one-frame "fly away" jumps.
                Vector3 originShift = (previousOrigin - focusWorldPosition).ToVector3();
                transform.position += originShift;
                target += originShift;
            }

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
            target = newFocus.position;
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
