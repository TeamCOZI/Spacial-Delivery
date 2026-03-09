using UnityEngine;

public static class LauncherSpawner
{
    private const float LaunchClearanceMargin = 0.15f;
    private const float LaunchSeparationEpsilon = 0.01f;
    private const int MaxInitialSeparationPasses = 8;

    public readonly struct LaunchResult
    {
        public readonly GameObject spaceship;
        public readonly Vector3 spawnPosition;
        public readonly Vector3 launchDirection;
        public readonly Vector3 launchVelocity;

        public LaunchResult(GameObject spaceship, Vector3 spawnPosition, Vector3 launchDirection, Vector3 launchVelocity)
        {
            this.spaceship = spaceship;
            this.spawnPosition = spawnPosition;
            this.launchDirection = launchDirection;
            this.launchVelocity = launchVelocity;
        }
    }

    public static GameObject ResolveSpaceshipPrefab(GameObject current)
    {
        if (current != null) return current;

        GameObject resolved = Resources.Load<GameObject>("SpaceshipPrefab");

#if UNITY_EDITOR
        if (resolved == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("SpaceshipPrefab t:Prefab");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                resolved = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
#endif

        return resolved;
    }

    public static bool TryLaunch(
        Transform launcher,
        GameObject spaceshipPrefab,
        float launchSpeed,
        out LaunchResult result)
    {
        result = default;
        if (launcher == null || spaceshipPrefab == null) return false;

        Vector3 planarLaunchDir = LauncherLaunchUtility.GetPlanarDirection(
            LauncherLaunchUtility.GetLauncherWorldDirection(launcher));
        Vector3 anchorPosition = LauncherLaunchUtility.GetLauncherStableWorldAnchor(launcher);
        anchorPosition.z = 0f;
        Quaternion spawnRotation = GetLaunchRotation(planarLaunchDir);

        GameObject spaceship = Object.Instantiate(spaceshipPrefab, anchorPosition, spawnRotation);
        SmallScaleLayerUtility.ApplyRecursively(spaceship.transform);
        Vector3 spawnPosition = PositionSpaceshipForClearLaunch(launcher, spaceship, anchorPosition, planarLaunchDir);
        IgnoreCollisionsWithLaunchHost(launcher, spaceship);

        Vector3 carrierVelocity = LauncherLaunchUtility.GetLauncherCarrierVelocity(launcher);
        Vector3 launchVelocity = (planarLaunchDir * launchSpeed) + carrierVelocity;
        launchVelocity.z = 0f;
        Spaceship spaceshipController = spaceship.GetComponent<Spaceship>();
        if (spaceshipController == null)
        {
            Debug.LogError("LauncherSpawner: Spaceship prefab is missing Spaceship component.");
            Object.Destroy(spaceship);
            return false;
        }

        spaceshipController.Launch(launchVelocity);

        result = new LaunchResult(spaceship, spawnPosition, planarLaunchDir, launchVelocity);
        return true;
    }

    private static Quaternion GetLaunchRotation(Vector3 launchDir)
    {
        Vector2 dir2 = new Vector2(launchDir.x, launchDir.y);
        if (dir2.sqrMagnitude < 0.000001f)
        {
            return Quaternion.Euler(0f, 0f, 0f);
        }

        // Spaceship visuals are authored with +Y as forward.
        float z = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, z);
    }

    private static void IgnoreCollisionsWithLaunchHost(Transform launcher, GameObject spaceship)
    {
        if (spaceship == null) return;
        Transform hostRoot = ResolveLaunchHostRoot(launcher);
        if (hostRoot == null) return;

        Collider[] launcherColliders = hostRoot.GetComponentsInChildren<Collider>(true);
        Collider[] shipColliders = spaceship.GetComponentsInChildren<Collider>(true);
        if (launcherColliders == null || shipColliders == null) return;

        for (int i = 0; i < launcherColliders.Length; i++)
        {
            Collider a = launcherColliders[i];
            if (a == null) continue;

            for (int j = 0; j < shipColliders.Length; j++)
            {
                Collider b = shipColliders[j];
                if (b == null) continue;
                Physics.IgnoreCollision(a, b, true);
            }
        }
    }

    private static Vector3 PositionSpaceshipForClearLaunch(
        Transform launcher,
        GameObject spaceship,
        Vector3 anchorPosition,
        Vector3 launchDirection)
    {
        if (spaceship == null) return anchorPosition;

        Vector3 dir = LauncherLaunchUtility.GetPlanarDirection(launchDirection);
        Transform hostRoot = ResolveLaunchHostRoot(launcher);

        Collider[] shipColliders = spaceship.GetComponentsInChildren<Collider>(true);
        Collider[] hostColliders = hostRoot != null ? hostRoot.GetComponentsInChildren<Collider>(true) : null;

        float anchorProjection = Vector3.Dot(anchorPosition, dir);
        float hostForwardDistance = ComputeForwardDistanceFromAnchor(hostColliders, anchorProjection, dir, null);
        float shipBackwardDistance = ComputeBackwardDistanceFromPivot(shipColliders, Vector3.Dot(spaceship.transform.position, dir), dir);
        float offset = hostForwardDistance + shipBackwardDistance + LaunchClearanceMargin;

        Vector3 positioned = anchorPosition + (dir * offset);
        positioned.z = 0f;
        spaceship.transform.position = positioned;

        ResolveInitialOverlap(spaceship, shipColliders, hostColliders, dir);

        Vector3 finalPosition = spaceship.transform.position;
        finalPosition.z = 0f;
        spaceship.transform.position = finalPosition;
        return finalPosition;
    }

    private static void ResolveInitialOverlap(
        GameObject spaceship,
        Collider[] shipColliders,
        Collider[] hostColliders,
        Vector3 launchDirection)
    {
        if (spaceship == null || shipColliders == null || hostColliders == null) return;

        Vector3 dir = LauncherLaunchUtility.GetPlanarDirection(launchDirection);
        for (int pass = 0; pass < MaxInitialSeparationPasses; pass++)
        {
            bool separatedAny = false;
            for (int i = 0; i < shipColliders.Length; i++)
            {
                Collider ship = shipColliders[i];
                if (!IsSolidCollider(ship)) continue;

                for (int j = 0; j < hostColliders.Length; j++)
                {
                    Collider host = hostColliders[j];
                    if (!IsSolidCollider(host)) continue;
                    if (host.transform.IsChildOf(spaceship.transform)) continue;

                    bool overlapped = Physics.ComputePenetration(
                        ship, ship.transform.position, ship.transform.rotation,
                        host, host.transform.position, host.transform.rotation,
                        out Vector3 separationDirection, out float separationDistance);

                    if (!overlapped || separationDistance <= 0f) continue;

                    float alongLaunch = Vector3.Dot(separationDirection, dir);
                    Vector3 push = alongLaunch > 0.01f
                        ? dir * (separationDistance + LaunchSeparationEpsilon)
                        : separationDirection * (separationDistance + LaunchSeparationEpsilon);

                    push.z = 0f;
                    spaceship.transform.position += push;
                    separatedAny = true;
                }
            }

            if (!separatedAny) break;
        }
    }

    private static float ComputeForwardDistanceFromAnchor(
        Collider[] colliders,
        float anchorProjection,
        Vector3 direction,
        Transform ignoreRoot)
    {
        if (colliders == null || colliders.Length == 0) return 0f;

        float maxForwardProjection = float.NegativeInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (!IsSolidCollider(col)) continue;
            if (ignoreRoot != null && col.transform.IsChildOf(ignoreRoot)) continue;

            Bounds bounds = col.bounds;
            float half = ProjectAabbHalfExtent(bounds.extents, direction);
            float forward = Vector3.Dot(bounds.center, direction) + half;
            if (forward > maxForwardProjection) maxForwardProjection = forward;
        }

        if (float.IsNegativeInfinity(maxForwardProjection)) return 0f;
        return Mathf.Max(0f, maxForwardProjection - anchorProjection);
    }

    private static float ComputeBackwardDistanceFromPivot(
        Collider[] colliders,
        float pivotProjection,
        Vector3 direction)
    {
        if (colliders == null || colliders.Length == 0) return 0f;

        float minProjection = float.PositiveInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (!IsSolidCollider(col)) continue;

            Bounds bounds = col.bounds;
            float half = ProjectAabbHalfExtent(bounds.extents, direction);
            float backward = Vector3.Dot(bounds.center, direction) - half;
            if (backward < minProjection) minProjection = backward;
        }

        if (float.IsPositiveInfinity(minProjection)) return 0f;
        return Mathf.Max(0f, pivotProjection - minProjection);
    }

    private static float ProjectAabbHalfExtent(Vector3 extents, Vector3 direction)
    {
        Vector3 absDir = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        return (extents.x * absDir.x) + (extents.y * absDir.y) + (extents.z * absDir.z);
    }

    private static bool IsSolidCollider(Collider collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger;
    }

    private static Transform ResolveLaunchHostRoot(Transform launcher)
    {
        if (launcher == null) return null;

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus == null) partFocus = launcher.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        return launcher.root != null ? launcher.root : launcher;
    }

}
