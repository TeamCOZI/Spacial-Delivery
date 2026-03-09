using UnityEngine;
using UnityEngine.InputSystem;

public partial class UserInput
{
    [SerializeField] private bool enableClickDebugLog = true;
    [SerializeField] private bool showRayGizmos = true;
    [SerializeField, Min(1f)] private float rayGizmoLength = 10000f;

    private void FocusHover()
    {
        if (IsAssemblyModeActive())
        {
            hoverEvent?.Invoke(null);
            return;
        }

        if (!TryGetMainCamera(out Camera camera))
        {
            hoverEvent?.Invoke(null);
            return;
        }

        Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (showRayGizmos)
        {
            Debug.DrawRay(ray.origin, ray.direction * Mathf.Max(1f, rayGizmoLength), Color.red, 0f, false);
        }

        // Transforms are shifted in LateUpdate by large-world systems, so sync before physics queries.
        Physics.SyncTransforms();

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, focusRaycastMask, QueryTriggerInteraction.Collide))
        {
            hoverEvent?.Invoke(null);
            return;
        }

        if (showRayGizmos)
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green, 0f, false);
            Debug.DrawRay(hit.point, hit.normal * 2f, Color.yellow, 0f, false);
        }

        Icon hoveredIcon = ResolveHoveredIcon(hit.transform);
        hoverEvent?.Invoke(hoveredIcon);

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (enableClickDebugLog && hit.collider != null)
        {
            GameObject go = hit.collider.gameObject;
            Debug.Log($"[ClickHit] name={go.name}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), tag={go.tag}");
        }

        Transform hitTarget = hit.collider != null ? hit.collider.transform : hit.transform;
        Transform resolvedTransform = ResolveFocusTransform(hitTarget);
        focusEvent?.Invoke(resolvedTransform);
    }

    private void ClearSpaceshipFocusByKey()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.cKey.wasPressedThisFrame) return;

        Transform current = GetCurrentFocus();
        if (current == null) return;

        Spaceship focusedSpaceship = current.GetComponentInParent<Spaceship>();
        if (focusedSpaceship == null) return;

        focusEvent?.Invoke(null);
    }

    private static Icon ResolveHoveredIcon(Transform hitTransform)
    {
        if (hitTransform == null) return null;

        Icon icon = hitTransform.GetComponent<Icon>();
        if (icon != null) return icon;

        return hitTransform.GetComponentInParent<Icon>();
    }

    private void FocusParent(InputAction.CallbackContext context)
    {
        Transform currentFocus = GetCurrentFocus();
        if (currentFocus == null) return;
        if (IsAssemblyModeActive()) return;

        AssemblyPartFocus partFocus = currentFocus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            focusEvent?.Invoke(partFocus.OwnerSatellite.transform);
            return;
        }

        OrbitRevolution orbit = currentFocus.GetComponent<OrbitRevolution>();
        if (orbit != null && orbit.center != null)
        {
            focusEvent?.Invoke(orbit.center.transform);
        }
    }

    private void AssemblyMode(InputAction.CallbackContext context)
    {
        Transform currentFocus = GetCurrentFocus();
        if (currentFocus == null) return;

        ArtificialSatellite focusSatellite = currentFocus.GetComponent<ArtificialSatellite>();
        if (focusSatellite != null && TryGetAssemblyManager(out AssemblyManager assemblyManager))
        {
            assemblyManager.StartAssemblyMode(focusSatellite);
        }
    }

    private Transform ResolveFocusTransform(Transform hitTransform)
    {
        if (hitTransform == null) return null;
        Transform currentFocus = GetCurrentFocus();

        bool clickedIcon = hitTransform.CompareTag("Icon") || hitTransform.name == "Icon";
        Transform candidate = hitTransform;
        if (clickedIcon)
        {
            candidate = candidate.parent != null ? candidate.parent : candidate;
        }

        ArtificialSatellite ownerSatellite = candidate.GetComponentInParent<ArtificialSatellite>();
        if (clickedIcon && ownerSatellite != null)
        {
            // Icon click in satellite hierarchy should always step to satellite-level focus.
            return ownerSatellite.transform;
        }

        AssemblyPartFocus partFocus = candidate.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            // Part focus is only allowed when the owner satellite is already focused.
            ArtificialSatellite owner = partFocus.OwnerSatellite;
            if (owner != null)
            {
                bool ownerFocused = currentFocus == owner.transform;
                bool siblingPartFocused = false;

                if (!ownerFocused && currentFocus != null)
                {
                    AssemblyPartFocus currentPartFocus = currentFocus.GetComponent<AssemblyPartFocus>();
                    siblingPartFocused = currentPartFocus != null && currentPartFocus.OwnerSatellite == owner;
                }

                if (!ownerFocused && !siblingPartFocused)
                {
                    return owner.transform;
                }
            }
            return partFocus.transform;
        }

        ScaledSpaceProxyTarget proxyTarget = candidate.GetComponentInParent<ScaledSpaceProxyTarget>();
        if (proxyTarget != null && proxyTarget.source != null)
        {
            return proxyTarget.source;
        }

        Transform cursor = candidate;
        while (cursor != null)
        {
            UpdateFocusInfo provider = cursor.GetComponent<UpdateFocusInfo>();
            if (provider != null)
            {
                return cursor;
            }
            cursor = cursor.parent;
        }

        return candidate;
    }

    private static Transform GetCurrentFocus()
    {
        return FocusManager.currentFocus;
    }

    private static bool TryGetAssemblyManager(out AssemblyManager assemblyManager)
    {
        assemblyManager = AssemblyManager.Instance;
        return assemblyManager != null;
    }

    private static bool IsAssemblyModeActive()
    {
        return TryGetAssemblyManager(out AssemblyManager assemblyManager) && assemblyManager.IsAssembling;
    }
}
