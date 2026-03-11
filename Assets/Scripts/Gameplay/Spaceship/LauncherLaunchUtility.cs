using UnityEngine;

public static class LauncherLaunchUtility
{
    public readonly struct LauncherVisualPose
    {
        public readonly Vector3 worldAnchor;
        public readonly Vector3 worldDirection;
        public readonly Vector3 screenPosition;
        public readonly bool isInFrontOfCamera;

        public LauncherVisualPose(Vector3 worldAnchor, Vector3 worldDirection, Vector3 screenPosition, bool isInFrontOfCamera)
        {
            this.worldAnchor = worldAnchor;
            this.worldDirection = worldDirection;
            this.screenPosition = screenPosition;
            this.isInFrontOfCamera = isInFrontOfCamera;
        }
    }

    private static Transform cachedLauncher;
    private static Transform cachedReferenceTransform;
    private static WorldPosition cachedReferenceWorldPosition;
    private static Vector3 cachedReferenceLocalAnchor;
    private static Vector3 cachedReferenceLocalDirection = Vector3.up;
    private static Transform cachedVisualBasisTransform;
    private static Vector3 cachedVisualBasisLocalAnchor;
    private static Vector3 cachedVisualBasisLocalDirection = Vector3.up;

    private static int cachedVisualPoseFrame = -1;
    private static Transform cachedVisualPoseLauncher;
    private static Camera cachedVisualPoseCamera;
    private static LauncherVisualPose cachedVisualPose;

    public static Transform ResolveFocusedLauncher(Transform focused)
    {
        if (focused == null) return null;

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null) return null;
        if (partFocus.SourcePart == null) return null;

        string partName = partFocus.SourcePart.partName;
        if (string.IsNullOrWhiteSpace(partName)) return null;
        if (!string.Equals(partName, "Launcher", System.StringComparison.OrdinalIgnoreCase)) return null;
        return partFocus.transform;
    }

    public static Vector3 GetPlanarDirection(Vector3 direction)
    {
        Vector3 planar = new Vector3(direction.x, direction.y, 0f);
        if (planar.sqrMagnitude < 0.000001f)
        {
            planar = Vector3.right;
        }

        return planar.normalized;
    }

    public static Vector3 GetLauncherWorldDirection(Transform launcher)
    {
        if (TryGetStableLauncherPose(launcher, out Vector3 _, out Vector3 direction))
        {
            return direction;
        }

        Renderer renderer = launcher != null ? launcher.GetComponentInChildren<Renderer>(true) : null;
        if (renderer == null) return GetPlanarDirection(launcher != null ? launcher.up : Vector3.up);

        Transform basis = renderer.transform;
        return GetPlanarDirection(basis.up);
    }

    public static Vector3 GetLauncherWorldAnchor(Transform launcher)
    {
        if (launcher == null) return Vector3.zero;

        Renderer renderer = launcher.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
        {
            return launcher.position + GetLauncherWorldDirection(launcher) * 0.05f;
        }

        MeshFilter mf = renderer.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds localBounds = mf.sharedMesh.bounds;
            Vector3 localTip = localBounds.center + Vector3.up * localBounds.extents.y;
            return mf.transform.TransformPoint(localTip);
        }

        Vector3 dir = GetLauncherWorldDirection(launcher);
        return renderer.bounds.center + dir * renderer.bounds.extents.magnitude;
    }

    public static Vector3 GetLauncherStableWorldAnchor(Transform launcher)
    {
        return TryGetStableLauncherPose(launcher, out Vector3 anchor, out Vector3 _)
            ? anchor
            : GetLauncherWorldAnchor(launcher);
    }

    public static bool TryGetLauncherVisualPose(Transform launcher, Camera worldCamera, out LauncherVisualPose pose)
    {
        pose = default;
        if (launcher == null) return false;

        if (cachedVisualPoseFrame == Time.frameCount &&
            cachedVisualPoseLauncher == launcher &&
            cachedVisualPoseCamera == worldCamera)
        {
            pose = cachedVisualPose;
            return true;
        }

        if (!TryGetRenderedLauncherPose(launcher, out Vector3 worldAnchor, out Vector3 worldDirection))
        {
            return false;
        }

        Vector3 screenPosition = worldCamera != null
            ? worldCamera.WorldToScreenPoint(worldAnchor)
            : Vector3.zero;
        bool isInFrontOfCamera = worldCamera == null || screenPosition.z > 0f;

        pose = new LauncherVisualPose(worldAnchor, worldDirection, screenPosition, isInFrontOfCamera);
        cachedVisualPoseFrame = Time.frameCount;
        cachedVisualPoseLauncher = launcher;
        cachedVisualPoseCamera = worldCamera;
        cachedVisualPose = pose;
        return true;
    }

    public static Vector3 GetLauncherCarrierVelocity(Transform launcher)
    {
        if (launcher == null) return Vector3.zero;

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        ArtificialSatellite ownerSatellite = partFocus != null
            ? partFocus.OwnerSatellite
            : launcher.GetComponentInParent<ArtificialSatellite>();

        if (ownerSatellite != null)
        {
            OrbitRevolution orbit = ownerSatellite.GetComponent<OrbitRevolution>();
            if (orbit != null)
            {
                return GetPlanarVelocity(orbit.GetCurrentOrbitalVelocity());
            }

            Rigidbody ownerRb = ownerSatellite.GetComponent<Rigidbody>();
            if (ownerRb != null)
            {
                return GetPlanarVelocity(ownerRb.linearVelocity);
            }
        }

        OrbitRevolution parentOrbit = launcher.GetComponentInParent<OrbitRevolution>();
        if (parentOrbit != null)
        {
            return GetPlanarVelocity(parentOrbit.GetCurrentOrbitalVelocity());
        }

        Rigidbody launcherRb = launcher.GetComponentInParent<Rigidbody>();
        if (launcherRb != null)
        {
            return GetPlanarVelocity(launcherRb.linearVelocity);
        }

        return Vector3.zero;
    }

    private static Vector3 GetPlanarVelocity(Vector3 velocity)
    {
        velocity.z = 0f;
        return velocity;
    }

    private static bool TryGetStableLauncherPose(Transform launcher, out Vector3 worldAnchor, out Vector3 worldDirection)
    {
        worldAnchor = Vector3.zero;
        worldDirection = Vector3.right;
        if (launcher == null) return false;
        if (!TryEnsureCachedReference(launcher)) return false;

        Transform reference = cachedReferenceTransform;
        if (reference == null) return false;

        worldDirection = GetPlanarDirection(reference.TransformDirection(cachedReferenceLocalDirection));

        if (cachedReferenceWorldPosition != null &&
            CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator) &&
            coordinator != null)
        {
            Vector3 localOffset = reference.TransformVector(cachedReferenceLocalAnchor);
            Double3 stableWorld = cachedReferenceWorldPosition.worldPosition + (Double3)localOffset;
            worldAnchor = coordinator.ToLocal(stableWorld);
            return true;
        }

        worldAnchor = reference.TransformPoint(cachedReferenceLocalAnchor);
        return true;
    }

    private static bool TryGetRenderedLauncherPose(Transform launcher, out Vector3 worldAnchor, out Vector3 worldDirection)
    {
        worldAnchor = Vector3.zero;
        worldDirection = Vector3.right;
        if (launcher == null) return false;
        if (!TryEnsureCachedReference(launcher)) return false;

        Transform basis = cachedVisualBasisTransform != null ? cachedVisualBasisTransform : launcher;
        worldAnchor = basis.TransformPoint(cachedVisualBasisLocalAnchor);
        worldDirection = GetPlanarDirection(basis.TransformDirection(cachedVisualBasisLocalDirection));
        return true;
    }

    private static bool TryEnsureCachedReference(Transform launcher)
    {
        if (launcher == null) return false;

        if (cachedLauncher == launcher &&
            cachedReferenceTransform != null &&
            cachedVisualBasisTransform != null)
        {
            return true;
        }

        cachedLauncher = launcher;
        cachedReferenceTransform = launcher;
        cachedReferenceWorldPosition = launcher.GetComponent<WorldPosition>();
        cachedReferenceLocalAnchor = Vector3.up * 0.05f;
        cachedReferenceLocalDirection = Vector3.up;
        cachedVisualBasisTransform = launcher;
        cachedVisualBasisLocalAnchor = Vector3.up * 0.05f;
        cachedVisualBasisLocalDirection = Vector3.up;
        cachedVisualPoseFrame = -1;
        cachedVisualPoseLauncher = null;
        cachedVisualPoseCamera = null;
        cachedVisualPose = default;

        Transform anchorBasis;
        Vector3 basisLocalAnchor;
        Vector3 basisLocalDirection;
        if (!TryResolveAnchorBasis(launcher, out anchorBasis, out basisLocalAnchor, out basisLocalDirection))
        {
            return false;
        }

        cachedVisualBasisTransform = anchorBasis != null ? anchorBasis : launcher;
        cachedVisualBasisLocalAnchor = basisLocalAnchor;
        cachedVisualBasisLocalDirection = basisLocalDirection.sqrMagnitude > 0.000001f
            ? basisLocalDirection.normalized
            : Vector3.up;

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            Transform owner = partFocus.OwnerSatellite.transform;
            if (owner != null)
            {
                cachedReferenceTransform = owner;
                cachedReferenceWorldPosition = owner.GetComponent<WorldPosition>();
            }
        }

        Transform reference = cachedReferenceTransform != null ? cachedReferenceTransform : launcher;
        Matrix4x4 anchorToReference = reference.worldToLocalMatrix * anchorBasis.localToWorldMatrix;
        cachedReferenceLocalAnchor = anchorToReference.MultiplyPoint3x4(basisLocalAnchor);

        Vector3 referenceLocalDirection = anchorToReference.MultiplyVector(basisLocalDirection);
        cachedReferenceLocalDirection = referenceLocalDirection.sqrMagnitude > 0.000001f
            ? referenceLocalDirection.normalized
            : Vector3.up;
        return true;
    }

    private static bool TryResolveAnchorBasis(
        Transform launcher,
        out Transform anchorBasis,
        out Vector3 basisLocalAnchor,
        out Vector3 basisLocalDirection)
    {
        anchorBasis = launcher;
        basisLocalAnchor = Vector3.up * 0.05f;
        basisLocalDirection = Vector3.up;
        if (launcher == null) return false;

        Renderer renderer = launcher.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds localBounds = meshFilter.sharedMesh.bounds;
                anchorBasis = meshFilter.transform;
                basisLocalAnchor = localBounds.center + Vector3.up * localBounds.extents.y;
                basisLocalDirection = Vector3.up;
                return true;
            }
        }

        BoxCollider boxCollider = launcher.GetComponentInChildren<BoxCollider>(true);
        if (boxCollider != null)
        {
            anchorBasis = boxCollider.transform;
            basisLocalAnchor = boxCollider.center + Vector3.up * (boxCollider.size.y * 0.5f);
            basisLocalDirection = Vector3.up;
            return true;
        }

        return true;
    }
}
