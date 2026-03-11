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
        bool hasVisualHit = TryRaycastFocusedSatelliteVisualHit(ray, out FocusHitResult visualHit);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, focusRaycastMask, QueryTriggerInteraction.Collide);
        bool hasPhysicsHit = TrySelectPhysicsFocusHit(hits, out FocusHitResult physicsHit);

        if (hasVisualHit && hasPhysicsHit)
        {
            if (TryResolveFocusedSatellite(out ArtificialSatellite focusedSatellite) &&
                IsHitWithinSatellite(physicsHit.transform, focusedSatellite))
            {
                selectedHit = visualHit;
                return true;
            }

            selectedHit = physicsHit.distance + 0.001f < visualHit.distance
                ? physicsHit
                : visualHit;
            return true;
        }

        if (hasVisualHit)
        {
            selectedHit = visualHit;
            return true;
        }

        if (hasPhysicsHit)
        {
            selectedHit = physicsHit;
            return true;
        }

        selectedHit = default;
        return false;
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

    private static bool TryRaycastFocusedSatelliteVisualHit(Ray ray, out FocusHitResult selectedHit)
    {
        selectedHit = default;
        if (!TryResolveFocusedSatellite(out ArtificialSatellite focusedSatellite))
        {
            return false;
        }

        AssemblyPartFocus[] partFocuses = focusedSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        FocusHitResult bestHit = default;

        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || !partFocus.gameObject.activeInHierarchy) continue;

            if (!TryRaycastVisualPart(ray, partFocus, out FocusHitResult hit))
            {
                continue;
            }

            if (!found || hit.distance < bestDistance)
            {
                found = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        if (!found)
        {
            return false;
        }

        selectedHit = bestHit;
        return true;
    }

    private static bool TryRaycastVisualPart(Ray ray, AssemblyPartFocus partFocus, out FocusHitResult selectedHit)
    {
        selectedHit = default;
        if (partFocus == null) return false;

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        FocusHitResult bestHit = default;

        Collider[] colliders = partFocus.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled) continue;

            if (!TryRaycastVisualCollider(ray, collider, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            FocusHitResult hit = new FocusHitResult(partFocus.transform, collider, hitPoint, hitNormal, hitDistance);
            if (!found || hit.distance < bestDistance)
            {
                found = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        if (!found)
        {
            Renderer[] renderers = partFocus.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;

                Bounds bounds = renderer.bounds;
                if (!bounds.IntersectRay(ray, out float hitDistance)) continue;

                Vector3 hitPoint = ray.GetPoint(hitDistance);
                Vector3 hitNormal = -ray.direction;
                FocusHitResult hit = new FocusHitResult(partFocus.transform, null, hitPoint, hitNormal, hitDistance);
                if (!found || hit.distance < bestDistance)
                {
                    found = true;
                    bestDistance = hit.distance;
                    bestHit = hit;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        selectedHit = bestHit;
        return true;
    }

    private static bool TryRaycastVisualCollider(
        Ray ray,
        Collider collider,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        hitPoint = default;
        hitNormal = default;
        hitDistance = 0f;
        if (collider == null) return false;

        if (collider is BoxCollider boxCollider)
        {
            return TryRaycastVisualBoxCollider(ray, boxCollider, out hitPoint, out hitNormal, out hitDistance);
        }

        Bounds bounds = collider.bounds;
        if (!bounds.IntersectRay(ray, out hitDistance))
        {
            return false;
        }

        hitPoint = ray.GetPoint(hitDistance);
        hitNormal = -ray.direction;
        return true;
    }

    private static bool TryRaycastVisualBoxCollider(
        Ray ray,
        BoxCollider boxCollider,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        hitPoint = default;
        hitNormal = default;
        hitDistance = 0f;
        if (boxCollider == null) return false;

        Matrix4x4 worldToLocal = boxCollider.transform.worldToLocalMatrix;
        Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(ray.origin) - boxCollider.center;
        Vector3 localDirection = worldToLocal.MultiplyVector(ray.direction);
        Vector3 extents = boxCollider.size * 0.5f;

        float tMin = 0f;
        float tMax = float.PositiveInfinity;
        Vector3 localNormal = Vector3.zero;

        if (!ClipAxis(localOrigin.x, localDirection.x, extents.x, Vector3.right, ref tMin, ref tMax, ref localNormal)) return false;
        if (!ClipAxis(localOrigin.y, localDirection.y, extents.y, Vector3.up, ref tMin, ref tMax, ref localNormal)) return false;
        if (!ClipAxis(localOrigin.z, localDirection.z, extents.z, Vector3.forward, ref tMin, ref tMax, ref localNormal)) return false;

        if (tMax < 0f) return false;

        float localHitT = tMin >= 0f ? tMin : tMax;
        Vector3 localHitPoint = localOrigin + (localDirection * localHitT) + boxCollider.center;
        hitPoint = boxCollider.transform.localToWorldMatrix.MultiplyPoint3x4(localHitPoint);
        hitNormal = boxCollider.transform.TransformDirection(localNormal).normalized;
        hitDistance = Vector3.Distance(ray.origin, hitPoint);
        return true;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float extent,
        Vector3 axisNormal,
        ref float tMin,
        ref float tMax,
        ref Vector3 hitNormal)
    {
        const float Epsilon = 1e-6f;
        if (Mathf.Abs(direction) < Epsilon)
        {
            return origin >= -extent && origin <= extent;
        }

        float invDirection = 1f / direction;
        float t1 = (-extent - origin) * invDirection;
        float t2 = (extent - origin) * invDirection;
        Vector3 normalForT1 = direction > 0f ? -axisNormal : axisNormal;
        Vector3 normalForT2 = direction > 0f ? axisNormal : -axisNormal;

        if (t1 > t2)
        {
            float swapT = t1;
            t1 = t2;
            t2 = swapT;

            Vector3 swapNormal = normalForT1;
            normalForT1 = normalForT2;
            normalForT2 = swapNormal;
        }

        if (t1 > tMin)
        {
            tMin = t1;
            hitNormal = normalForT1;
        }

        if (t2 < tMax)
        {
            tMax = t2;
        }

        return tMin <= tMax;
    }

    private static bool TryResolveFocusedSatellite(out ArtificialSatellite focusedSatellite)
    {
        focusedSatellite = null;
        Transform currentFocus = GetCurrentFocus();
        if (currentFocus == null) return false;

        focusedSatellite = currentFocus.GetComponent<ArtificialSatellite>();
        if (focusedSatellite != null) return true;

        AssemblyPartFocus partFocus = currentFocus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            focusedSatellite = partFocus.OwnerSatellite;
        }

        return focusedSatellite != null;
    }

    private static bool IsHitWithinSatellite(Transform hitTransform, ArtificialSatellite satellite)
    {
        if (hitTransform == null || satellite == null) return false;
        return hitTransform == satellite.transform || hitTransform.IsChildOf(satellite.transform);
    }
}
