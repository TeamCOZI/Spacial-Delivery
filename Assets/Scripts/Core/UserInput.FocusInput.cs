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
    [SerializeField] private bool enableRaycastHitDebugLog = true;
    [SerializeField] private bool showRayGizmos = true;
    [SerializeField, Min(1f)] private float rayGizmoLength = 10000f;

    private const float VisualHelperHitDistancePenalty = 0.01f;
    private const string CoreFocusGridPlaneName = "CoreFocusGridPlane";
    private void FocusHover()
    {
        if (ShouldSuppressFocusHover())
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
        if (SpaceshipFocusUtility.TryResolveSpaceship(GetCurrentFocus(), out Spaceship focusedSpaceship))
        {
            Transform targetTransform = ResolveSpaceshipTargetTransform(resolvedTransform);
            if (targetTransform != null)
            {
                focusedSpaceship.ToggleTarget(targetTransform);
            }
            return;
        }
        if (resolvedTransform != GetCurrentFocus())
        {
            CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
        }
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
    private void ResetCurrentFocusViewByKey()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.xKey.wasPressedThisFrame) return;

        Transform current = GetCurrentFocus();
        if (current == null) return;

        CameraManager.Instance?.ResetToCurrentFocusView();
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

        Transform parentFocus = null;

        AssemblyOutputPortFocus outputPortFocus = currentFocus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null)
        {
            parentFocus = outputPortFocus.ResolveCloseFocus();
        }
        else if (currentFocus.TryGetComponent(out AssemblyPartFocus partFocus) && partFocus.OwnerSatellite != null)
        {
            parentFocus = partFocus.OwnerSatellite.transform;
        }
        else
        {
            StructureFocus structureFocus = currentFocus.GetComponent<StructureFocus>();
            if (structureFocus != null && structureFocus.OwnerSatellite != null)
            {
                parentFocus = structureFocus.OwnerSatellite.transform;
            }
            else
            {
                OrbitRevolution orbit = currentFocus.GetComponent<OrbitRevolution>();
                if (orbit != null && orbit.center != null)
                {
                    parentFocus = orbit.center.transform;
                }
            }
        }

        if (parentFocus == null || parentFocus == currentFocus) return;

        CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
        focusEvent?.Invoke(parentFocus);
    }
    private void AssemblyMode(InputAction.CallbackContext context)
    {
        Transform currentFocus = GetCurrentFocus();
        if (currentFocus == null) return;
        if (!TryGetAssemblyManager(out AssemblyManager assemblyManager)) return;
        if (assemblyManager.IsAssembling) return;

        ArtificialSatellite focusSatellite = currentFocus.GetComponent<ArtificialSatellite>();
        if (focusSatellite != null)
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
        if (TryResolveSameSatelliteChildFocus(currentFocus, candidate, ownerSatellite, out Transform sameSatelliteChildFocus))
        {
            return sameSatelliteChildFocus;
        }

        if (clickedIcon && ownerSatellite != null)
        {
            // Icon click in satellite hierarchy should always step to satellite-level focus.
            return ownerSatellite.transform;
        }

        AssemblyOutputPortFocus outputPortFocus = candidate.GetComponentInParent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null)
        {
            Transform closeFocus = outputPortFocus.ResolveCloseFocus();
            if (closeFocus != null)
            {
                return closeFocus;
            }

            ArtificialSatellite owner = outputPortFocus.OwnerSatellite;
            if (owner != null)
            {
                return owner.transform;
            }
        }

        StructureFocus structureFocus = candidate.GetComponentInParent<StructureFocus>();
        if (structureFocus != null)
        {
            ArtificialSatellite owner = structureFocus.OwnerSatellite;
            if (owner != null)
            {
                bool ownerFocused = currentFocus == owner.transform;
                bool siblingFocused = IsSatelliteSiblingFocus(currentFocus, owner);

                if (!ownerFocused && !siblingFocused)
                {
                    return owner.transform;
                }
            }

            return structureFocus.transform;
        }

        AssemblyPartFocus partFocus = candidate.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            // Part focus is only allowed when the owner satellite is already focused.
            ArtificialSatellite owner = partFocus.OwnerSatellite;
            if (owner != null)
            {
                bool ownerFocused = currentFocus == owner.transform;
                bool siblingFocused = IsSatelliteSiblingFocus(currentFocus, owner);

                if (!ownerFocused && !siblingFocused)
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

    private static bool IsSatelliteSiblingFocus(Transform currentFocus, ArtificialSatellite owner)
    {
        if (currentFocus == null || owner == null)
        {
            return false;
        }

        AssemblyOutputPortFocus currentOutputPortFocus = currentFocus.GetComponent<AssemblyOutputPortFocus>();
        if (currentOutputPortFocus != null && currentOutputPortFocus.OwnerSatellite == owner)
        {
            return true;
        }

        AssemblyPartFocus currentPartFocus = currentFocus.GetComponent<AssemblyPartFocus>();
        if (currentPartFocus != null && currentPartFocus.OwnerSatellite == owner)
        {
            return true;
        }

        StructureFocus currentStructureFocus = currentFocus.GetComponent<StructureFocus>();
        return currentStructureFocus != null && currentStructureFocus.OwnerSatellite == owner;
    }

    private static bool TryResolveSameSatelliteChildFocus(
        Transform currentFocus,
        Transform candidate,
        ArtificialSatellite candidateOwner,
        out Transform childFocus)
    {
        childFocus = null;
        if (currentFocus == null || candidate == null || candidateOwner == null)
        {
            return false;
        }

        if (!TryResolveSatelliteChildOwner(currentFocus, out ArtificialSatellite currentOwner) || currentOwner != candidateOwner)
        {
            return false;
        }

        if (TryResolveNearestSatelliteChildFocus(candidate, candidateOwner, out childFocus))
        {
            return true;
        }

        if (currentFocus != candidateOwner.transform && IsHitWithinSatellite(candidate, candidateOwner))
        {
            childFocus = currentFocus;
            return true;
        }

        return false;
    }

    private static bool TryResolveNearestSatelliteChildFocus(
        Transform candidate,
        ArtificialSatellite ownerSatellite,
        out Transform childFocus)
    {
        childFocus = null;
        if (candidate == null || ownerSatellite == null)
        {
            return false;
        }

        AssemblyOutputPortFocus outputPortFocus = candidate.GetComponentInParent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null && outputPortFocus.OwnerSatellite == ownerSatellite)
        {
            Transform closeFocus = outputPortFocus.ResolveCloseFocus();
            if (closeFocus != null && closeFocus != ownerSatellite.transform)
            {
                childFocus = closeFocus;
                return true;
            }

            AssemblyPartFocus ownerPartFocus = outputPortFocus.OwnerPartFocus;
            if (ownerPartFocus != null && ownerPartFocus.OwnerSatellite == ownerSatellite)
            {
                childFocus = ownerPartFocus.transform;
                return true;
            }
        }

        StructureFocus structureFocus = candidate.GetComponentInParent<StructureFocus>();
        if (structureFocus != null && structureFocus.OwnerSatellite == ownerSatellite)
        {
            childFocus = structureFocus.transform;
            return true;
        }

        AssemblyPartFocus partFocus = candidate.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite == ownerSatellite)
        {
            childFocus = partFocus.transform;
            return true;
        }

        return false;
    }

    private static bool TryResolveSatelliteChildOwner(Transform focus, out ArtificialSatellite ownerSatellite)
    {
        ownerSatellite = null;
        if (focus == null)
        {
            return false;
        }

        AssemblyOutputPortFocus outputPortFocus = focus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null && outputPortFocus.OwnerSatellite != null)
        {
            ownerSatellite = outputPortFocus.OwnerSatellite;
            return true;
        }

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            ownerSatellite = partFocus.OwnerSatellite;
            return true;
        }

        StructureFocus structureFocus = focus.GetComponent<StructureFocus>();
        if (structureFocus != null && structureFocus.OwnerSatellite != null)
        {
            ownerSatellite = structureFocus.OwnerSatellite;
            return true;
        }

        return false;
    }
    private static Transform ResolveSpaceshipTargetTransform(Transform candidate)
    {
        if (candidate == null) return null;
        if (candidate.GetComponent<Star>() != null) return candidate;
        if (candidate.GetComponent<Planet>() != null) return candidate;
        if (candidate.GetComponent<Satellite>() != null) return candidate;
        if (candidate.GetComponent<ArtificialSatellite>() != null) return candidate;
        return null;
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

    private static bool ShouldSuppressFocusHover()
    {
        if (IsPointerOverBlockingUi())
        {
            return true;
        }

        if (!IsAssemblyModeActive()) return false;

        Assembly assembly = Assembly.Instance;
        if (assembly == null) return false;

        return assembly.IsAssembling || assembly.IsSelectionModeActive;
    }

    private static bool IsPointerOverBlockingUi()
    {
        if (LauncherSelectionPresenter.IsWorldInputBlockedByPanel)
        {
            return true;
        }

        if (CoreLogisticsHubPresenter.IsWorldInputBlockedByPanel)
        {
            return true;
        }

        if (ModuleInventoryPresenter.IsWorldInputBlockedByPanel)
        {
            return true;
        }

        if (ModuleProcessingPresenter.IsWorldInputBlockedByPanel)
        {
            return true;
        }

        if (ModuleOutputPortSelectorPresenter.IsWorldInputBlockedByPanel)
        {
            return true;
        }

        if (GameplayRuntimeAccess.TryGetAssemblyUi(out AssemblyUI assemblyUi) &&
            assemblyUi != null &&
            assemblyUi.IsWorldInputBlockedByPopup)
        {
            return true;
        }

        return false;
    }

    private static bool IsSatelliteRelatedFocus(Transform focus)
    {
        if (focus == null) return false;
        if (focus.GetComponent<ArtificialSatellite>() != null) return true;

        AssemblyOutputPortFocus outputPortFocus = focus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null && outputPortFocus.OwnerSatellite != null) return true;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        return partFocus != null && partFocus.OwnerSatellite != null;
    }
    private bool TryRaycastFocusHit(Ray ray, out FocusHitResult selectedHit)
    {
        bool hasVisualHit = false;
        FocusHitResult visualHit = default;

        if (TryGetMainCamera(out Camera camera) && Mouse.current != null)
        {
            hasVisualHit = TryRaycastFocusedSatelliteVisualHit(camera, Mouse.current.position.ReadValue(), ray, out visualHit);
        }

        if (!hasVisualHit)
        {
            hasVisualHit = TryRaycastVisualSceneHit(ray, out visualHit);
        }

        RaycastHit[] physicsHits = Physics.RaycastAll(ray, Mathf.Infinity, focusRaycastMask, QueryTriggerInteraction.Collide);
        bool hasPhysicsHit = TrySelectPhysicsFocusHit(physicsHits, out FocusHitResult physicsHit);

        if (hasVisualHit)
        {
            LogRaycastHit("visual", visualHit, hasVisualHit, visualHit, hasPhysicsHit, physicsHit, physicsHits.Length);
            selectedHit = visualHit;
            return true;
        }

        if (hasPhysicsHit)
        {
            LogRaycastHit("ignored-physics", physicsHit, hasVisualHit, visualHit, hasPhysicsHit, physicsHit, physicsHits.Length);
        }

        selectedHit = default;
        return false;
    }

    private void LogRaycastHit(
        string selectedSource,
        FocusHitResult selectedHit,
        bool hasVisualHit,
        FocusHitResult visualHit,
        bool hasPhysicsHit,
        FocusHitResult physicsHit,
        int physicsHitCount)
    {
        if (!enableRaycastHitDebugLog) return;

        Debug.Log(
            $"[FocusRaycast] selected={selectedSource} " +
            $"visual={FormatFocusHit(hasVisualHit, visualHit)} " +
            $"physics={FormatFocusHit(hasPhysicsHit, physicsHit)} " +
            $"chosen={FormatFocusHit(true, selectedHit)} " +
            $"physicsHitCount={physicsHitCount}");
    }

    private static string FormatFocusHit(bool hasHit, FocusHitResult hit)
    {
        if (!hasHit)
        {
            return "none";
        }

        Transform hitTransform = hit.collider != null ? hit.collider.transform : hit.transform;
        if (hitTransform == null)
        {
            return "missing-transform";
        }

        GameObject gameObject = hitTransform.gameObject;
        string colliderName = hit.collider != null ? hit.collider.GetType().Name : "VisualMesh";
        string iconOwnerSuffix = BuildIconOwnerSuffix(hitTransform);
        return
            $"name={gameObject.name}," +
            $"collider={colliderName}," +
            $"layer={LayerMask.LayerToName(gameObject.layer)}({gameObject.layer})," +
            $"tag={gameObject.tag}," +
            $"point={hit.point:F2}," +
            $"normal={hit.normal:F2}," +
            $"distance={hit.distance:F2}" +
            iconOwnerSuffix;
    }

    private static string BuildIconOwnerSuffix(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return string.Empty;
        }

        bool isIconHit = hitTransform.CompareTag("Icon") || hitTransform.name == "Icon";
        if (!isIconHit)
        {
            return string.Empty;
        }

        Icon resolvedIcon = ResolveHoveredIcon(hitTransform);
        if (resolvedIcon == null)
        {
            return ",iconOwner=unresolved";
        }

        GameObject ownerObject = resolvedIcon.gameObject;
        return
            $",iconOwner={ownerObject.name}," +
            $"iconOwnerLayer={LayerMask.LayerToName(ownerObject.layer)}({ownerObject.layer})," +
            $"iconOwnerTag={ownerObject.tag}";
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

    private static bool TryRaycastFocusedSatelliteVisualHit(Camera camera, Vector2 screenPosition, Ray ray, out FocusHitResult selectedHit)
    {
        selectedHit = default;
        if (!TryResolveFocusedSatellite(out ArtificialSatellite focusedSatellite))
        {
            return false;
        }


        bool foundStructureColliderHit = false;
        float bestStructureColliderDistance = float.PositiveInfinity;
        FocusHitResult bestStructureColliderHit = default;

        bool foundStructureHelperHit = false;
        float bestStructureHelperScore = float.PositiveInfinity;
        FocusHitResult bestStructureHelperHit = default;

        StructureFocus[] structureFocuses = focusedSatellite.GetComponentsInChildren<StructureFocus>(true);
        for (int i = 0; i < structureFocuses.Length; i++)
        {
            StructureFocus structureFocus = structureFocuses[i];
            if (structureFocus == null || !structureFocus.gameObject.activeInHierarchy) continue;

            if (!TryRaycastVisualStructure(ray, structureFocus, out FocusHitResult colliderHit, out FocusHitResult helperHit))
            {
                continue;
            }

            if (colliderHit.collider != null && (!foundStructureColliderHit || colliderHit.distance < bestStructureColliderDistance))
            {
                foundStructureColliderHit = true;
                bestStructureColliderDistance = colliderHit.distance;
                bestStructureColliderHit = colliderHit;
            }

            if (helperHit.transform != null)
            {
                float helperScore = helperHit.distance + VisualHelperHitDistancePenalty;
                if (!foundStructureHelperHit || helperScore < bestStructureHelperScore)
                {
                    foundStructureHelperHit = true;
                    bestStructureHelperScore = helperScore;
                    bestStructureHelperHit = helperHit;
                }
            }
        }

        if (foundStructureHelperHit && (!foundStructureColliderHit || bestStructureHelperScore < bestStructureColliderDistance))
        {
            selectedHit = bestStructureHelperHit;
            return true;
        }

        if (foundStructureColliderHit)
        {
            selectedHit = bestStructureColliderHit;
            return true;
        }

        bool foundPartColliderHit = false;
        float bestPartColliderDistance = float.PositiveInfinity;
        FocusHitResult bestPartColliderHit = default;

        bool foundPartHelperHit = false;
        float bestPartHelperScore = float.PositiveInfinity;
        FocusHitResult bestPartHelperHit = default;


        AssemblyPartFocus[] partFocuses = focusedSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || !partFocus.gameObject.activeInHierarchy) continue;

            if (!TryRaycastVisualPart(ray, partFocus, out FocusHitResult colliderHit, out FocusHitResult helperHit))
            {
                continue;
            }

            if (colliderHit.collider != null && (!foundPartColliderHit || colliderHit.distance < bestPartColliderDistance))
            {
                foundPartColliderHit = true;
                bestPartColliderDistance = colliderHit.distance;
                bestPartColliderHit = colliderHit;
            }

            if (helperHit.transform != null)
            {
                float helperScore = helperHit.distance + VisualHelperHitDistancePenalty;
                if (!foundPartHelperHit || helperScore < bestPartHelperScore)
                {
                    foundPartHelperHit = true;
                    bestPartHelperScore = helperScore;
                    bestPartHelperHit = helperHit;
                }


            }
        }


        if (foundPartHelperHit && (!foundPartColliderHit || bestPartHelperScore < bestPartColliderDistance))
        {
            selectedHit = bestPartHelperHit;
            return true;
        }

        if (foundPartColliderHit)
        {
            selectedHit = bestPartColliderHit;
            return true;
        }

        return false;
    }

    private bool TryRaycastVisualSceneHit(Ray ray, out FocusHitResult selectedHit)
    {
        selectedHit = default;

        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        FocusHitResult bestHit = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if (((1 << collider.gameObject.layer) & focusRaycastMask) == 0) continue;
            if (!IsVisualFocusCandidate(collider.transform)) continue;

            if (!TryRaycastVisualCollider(ray, collider, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            FocusHitResult hit = new FocusHitResult(collider.transform, collider, hitPoint, hitNormal, hitDistance);
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

    private bool IsVisualFocusCandidate(Transform candidate)
    {
        if (candidate == null) return false;

        if (candidate.CompareTag("Icon") || candidate.name == "Icon")
        {
            return true;
        }

        if (candidate.GetComponentInParent<StructureFocus>() != null)
        {
            return true;
        }

        if (candidate.GetComponentInParent<AssemblyPartFocus>() != null)
        {
            return true;
        }

        if (candidate.GetComponentInParent<ScaledSpaceProxyTarget>() != null)
        {
            return true;
        }

        if (SpaceshipFocusUtility.TryResolveSpaceship(candidate, out _))
        {
            return true;
        }

        Transform cursor = candidate;
        while (cursor != null)
        {
            if (cursor.GetComponent<UpdateFocusInfo>() != null)
            {
                return true;
            }

            cursor = cursor.parent;
        }

        return false;
    }

    private static bool TryRaycastVisualPart(Ray ray, AssemblyPartFocus partFocus, out FocusHitResult colliderHit, out FocusHitResult helperHit)
    {
        colliderHit = default;
        helperHit = default;
        if (partFocus == null) return false;

        bool foundAnyHit = false;
        float bestColliderDistance = float.PositiveInfinity;
        float bestHelperDistance = float.PositiveInfinity;

        Collider[] colliders = partFocus.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled) continue;

            if (!TryRaycastVisualCollider(ray, collider, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            Transform focusTransform = ResolveSatelliteChildFocusTransform(collider.transform, partFocus.transform);
            FocusHitResult hit = new FocusHitResult(focusTransform, collider, hitPoint, hitNormal, hitDistance);
            if (hit.distance < bestColliderDistance)
            {
                foundAnyHit = true;
                bestColliderDistance = hit.distance;
                colliderHit = hit;
            }
        }

        Renderer[] renderers = partFocus.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsExplicitPartVisualHelperRenderer(renderer)) continue;

            if (!TryRaycastVisualHelperRenderer(ray, renderer, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            Transform focusTransform = ResolveSatelliteChildFocusTransform(renderer.transform, partFocus.transform);
            FocusHitResult hit = new FocusHitResult(focusTransform, null, hitPoint, hitNormal, hitDistance);
            if (hit.distance < bestHelperDistance)
            {
                foundAnyHit = true;
                bestHelperDistance = hit.distance;
                helperHit = hit;
            }
        }

        return foundAnyHit;
    }

    private static bool TryRaycastVisualStructure(Ray ray, StructureFocus structureFocus, out FocusHitResult colliderHit, out FocusHitResult helperHit)
    {
        colliderHit = default;
        helperHit = default;
        if (structureFocus == null) return false;

        bool foundAnyHit = false;
        float bestColliderDistance = float.PositiveInfinity;
        float bestHelperDistance = float.PositiveInfinity;

        Collider[] colliders = structureFocus.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled) continue;

            if (!TryRaycastVisualCollider(ray, collider, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            Transform focusTransform = ResolveSatelliteChildFocusTransform(collider.transform, structureFocus.transform);
            FocusHitResult hit = new FocusHitResult(focusTransform, collider, hitPoint, hitNormal, hitDistance);
            if (hit.distance < bestColliderDistance)
            {
                foundAnyHit = true;
                bestColliderDistance = hit.distance;
                colliderHit = hit;
            }
        }

        Renderer[] renderers = structureFocus.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;

            if (!TryRaycastVisualHelperRenderer(ray, renderer, out Vector3 hitPoint, out Vector3 hitNormal, out float hitDistance))
            {
                continue;
            }

            Transform focusTransform = ResolveSatelliteChildFocusTransform(renderer.transform, structureFocus.transform);
            FocusHitResult hit = new FocusHitResult(focusTransform, null, hitPoint, hitNormal, hitDistance);
            if (hit.distance < bestHelperDistance)
            {
                foundAnyHit = true;
                bestHelperDistance = hit.distance;
                helperHit = hit;
            }
        }

        return foundAnyHit;
    }
    private static bool IsExplicitPartVisualHelperRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled) return false;

        Transform rendererTransform = renderer.transform;
        if (rendererTransform == null || !rendererTransform.gameObject.activeInHierarchy) return false;

        if (string.Equals(rendererTransform.name, CoreFocusGridPlaneName, StringComparison.Ordinal))
        {
            return true;
        }

        return rendererTransform.GetComponentInParent<AssemblyPortVisualMarker>(true) != null;
    }

    private static bool TryRaycastVisualHelperRenderer(
        Ray ray,
        Renderer renderer,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        hitPoint = default;
        hitNormal = default;
        hitDistance = 0f;
        if (renderer == null) return false;

        if (TryGetRendererLocalBounds(renderer, out Bounds localBounds))
        {
            return TryRaycastVisualOrientedBounds(
                ray,
                renderer.transform,
                localBounds.center,
                localBounds.size,
                out hitPoint,
                out hitNormal,
                out hitDistance);
        }

        Bounds bounds = renderer.bounds;
        if (!bounds.IntersectRay(ray, out hitDistance))
        {
            return false;
        }

        hitPoint = ray.GetPoint(hitDistance);
        hitNormal = -ray.direction;
        return true;
    }

    private static bool TryGetRendererLocalBounds(Renderer renderer, out Bounds localBounds)
    {
        localBounds = default;
        if (renderer == null) return false;

        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            localBounds = skinnedMeshRenderer.localBounds;
            return true;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            localBounds = meshFilter.sharedMesh.bounds;
            return true;
        }

        return false;
    }

    private static bool TryRaycastVisualOrientedBounds(
        Ray ray,
        Transform boundsTransform,
        Vector3 boundsCenter,
        Vector3 boundsSize,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        hitPoint = default;
        hitNormal = default;
        hitDistance = 0f;
        if (boundsTransform == null) return false;

        Matrix4x4 worldToLocal = boundsTransform.worldToLocalMatrix;
        Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(ray.origin) - boundsCenter;
        Vector3 localDirection = worldToLocal.MultiplyVector(ray.direction);
        Vector3 extents = boundsSize * 0.5f;

        float tMin = 0f;
        float tMax = float.PositiveInfinity;
        Vector3 localNormal = Vector3.zero;

        if (!ClipAxis(localOrigin.x, localDirection.x, extents.x, Vector3.right, ref tMin, ref tMax, ref localNormal)) return false;
        if (!ClipAxis(localOrigin.y, localDirection.y, extents.y, Vector3.up, ref tMin, ref tMax, ref localNormal)) return false;
        if (!ClipAxis(localOrigin.z, localDirection.z, extents.z, Vector3.forward, ref tMin, ref tMax, ref localNormal)) return false;

        if (tMax < 0f) return false;

        float localHitT = tMin >= 0f ? tMin : tMax;
        Vector3 localHitPoint = localOrigin + (localDirection * localHitT) + boundsCenter;
        hitPoint = boundsTransform.localToWorldMatrix.MultiplyPoint3x4(localHitPoint);
        hitNormal = boundsTransform.TransformDirection(localNormal).normalized;
        hitDistance = Vector3.Distance(ray.origin, hitPoint);
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

        if (collider is SphereCollider sphereCollider)
        {
            return TryRaycastVisualSphereCollider(ray, sphereCollider, out hitPoint, out hitNormal, out hitDistance);
        }

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

    private static bool TryRaycastVisualSphereCollider(
        Ray ray,
        SphereCollider sphereCollider,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        hitPoint = default;
        hitNormal = default;
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
        float near = (-b - sqrtDiscriminant) / denominator;
        float far = (-b + sqrtDiscriminant) / denominator;
        if (far < 0f)
        {
            return false;
        }

        float t = near >= 0f ? near : far;
        hitPoint = ray.GetPoint(t);
        hitNormal = (hitPoint - center).normalized;
        hitDistance = t;
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

        AssemblyOutputPortFocus outputPortFocus = currentFocus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus != null)
        {
            focusedSatellite = outputPortFocus.OwnerSatellite;
        }

        if (focusedSatellite != null) return true;

        AssemblyPartFocus partFocus = currentFocus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            focusedSatellite = partFocus.OwnerSatellite;
        }

        if (focusedSatellite == null)
        {
            StructureFocus structureFocus = currentFocus.GetComponent<StructureFocus>();
            if (structureFocus != null)
            {
                focusedSatellite = structureFocus.OwnerSatellite;
            }
        }

        return focusedSatellite != null;
    }

    private static Transform ResolveSatelliteChildFocusTransform(Transform hitTransform, Transform fallbackTransform)
    {
        return fallbackTransform;
    }

    private static bool IsHitWithinSatellite(Transform hitTransform, ArtificialSatellite satellite)
    {
        if (hitTransform == null || satellite == null) return false;
        return hitTransform == satellite.transform || hitTransform.IsChildOf(satellite.transform);
    }
}













