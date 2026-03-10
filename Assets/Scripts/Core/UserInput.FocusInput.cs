using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class UserInput
{
    private readonly struct FocusHitResult
    {
        public readonly Transform transform;
        public readonly Collider collider;
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly float distance;

        public FocusHitResult(Transform transform, Collider collider, Vector3 point, Vector3 normal, float distance)
        {
            this.transform = transform;
            this.collider = collider;
            this.point = point;
            this.normal = normal;
            this.distance = distance;
        }

        public static FocusHitResult FromPhysicsHit(RaycastHit hit)
        {
            return new FocusHitResult(hit.transform, hit.collider, hit.point, hit.normal, hit.distance);
        }
    }

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

        bool hasFocusHit = TryRaycastFocusHit(ray, out FocusHitResult hit);
        if (!hasFocusHit)
        {
            hoverEvent?.Invoke(null);
            return;
        }

        Transform hitTransform = hit.transform;
        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;

        if (showRayGizmos)
        {
            Debug.DrawLine(ray.origin, hitPoint, Color.green, 0f, false);
            Debug.DrawRay(hitPoint, hitNormal * 2f, Color.yellow, 0f, false);
        }

        Icon hoveredIcon = ResolveHoveredIcon(hitTransform);
        hoverEvent?.Invoke(hoveredIcon);

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (enableClickDebugLog)
        {
            if (hit.collider != null)
            {
                GameObject go = hit.collider.gameObject;
                Debug.Log($"[ClickHit] name={go.name}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), tag={go.tag}");
            }
            else if (hitTransform != null)
            {
                GameObject go = hitTransform.gameObject;
                Debug.Log($"[ClickHit] name={go.name}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), tag={go.tag}, source=VisualMesh");
            }
        }

        Transform hitTarget = hit.collider != null ? hit.collider.transform : hitTransform;
        Transform resolvedTransform = ResolveFocusTransform(hitTarget);
        focusEvent?.Invoke(resolvedTransform);
    }

    private void ClearSpaceshipFocusByKey()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.cKey.wasPressedThisFrame) return;

        Transform current = GetCurrentFocus();
        if (current == null) return;
        if (!SpaceshipFocusUtility.TryResolveSpaceship(current, out _)) return;

        focusEvent?.Invoke(null);
    }

    private static Icon ResolveHoveredIcon(Transform hitTransform)
    {
        if (hitTransform == null) return null;

        if (SpaceshipFocusUtility.TryResolveSpaceship(hitTransform, out Spaceship spaceship))
        {
            Transform focusTarget = SpaceshipFocusUtility.ResolveFocusTarget(spaceship);
            if (focusTarget != null)
            {
                Icon focusTargetIcon = focusTarget.GetComponent<Icon>();
                if (focusTargetIcon != null && focusTargetIcon.enabled)
                {
                    return focusTargetIcon;
                }
            }
        }

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

        if (SpaceshipFocusUtility.TryResolveSpaceship(candidate, out Spaceship spaceship))
        {
            return SpaceshipFocusUtility.ResolveFocusTarget(spaceship);
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

    private bool TryRaycastFocusHit(Ray ray, out FocusHitResult selectedHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, focusRaycastMask, QueryTriggerInteraction.Collide);
        return TrySelectPhysicsFocusHit(hits, out selectedHit);
    }

    private static bool TrySelectPhysicsFocusHit(RaycastHit[] hits, out FocusHitResult selectedHit)
    {
        selectedHit = default;
        if (hits == null || hits.Length == 0) return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryBuildPreferredPhysicsHit(hits, i, out selectedHit))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildPreferredPhysicsHit(RaycastHit[] hits, int candidateIndex, out FocusHitResult selectedHit)
    {
        selectedHit = default;
        if (hits == null || candidateIndex < 0 || candidateIndex >= hits.Length) return false;

        RaycastHit primaryHit = hits[candidateIndex];
        if (primaryHit.transform == null) return false;
        if (ShouldSkipSpaceshipAuxiliaryHit(primaryHit)) return false;

        selectedHit = FocusHitResult.FromPhysicsHit(primaryHit);
        return true;
    }

    private static bool ShouldSkipSpaceshipAuxiliaryHit(RaycastHit hit)
    {
        if (hit.transform == null) return true;
        if (SpaceshipFocusUtility.TryResolveFocusProxy(hit.transform, out _)) return true;

        Collider hitCollider = hit.collider;
        if (hitCollider != null && hitCollider.isTrigger)
        {
            return SpaceshipFocusUtility.TryResolveSpaceship(hit.transform, out _);
        }

        return false;
    }
}
