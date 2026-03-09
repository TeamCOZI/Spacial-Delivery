using UnityEngine;

public static class LauncherLaunchUtility
{
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
        if (launcher == null) return Vector3.zero;

        Vector3 fallback = GetLauncherWorldAnchor(launcher);
        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        if (coordinator == null) return fallback;

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.OwnerSatellite == null) return fallback;

        WorldPosition ownerWp = partFocus.OwnerSatellite.GetComponent<WorldPosition>();
        if (ownerWp == null) return fallback;

        Transform owner = partFocus.OwnerSatellite.transform;
        Vector3 localOffset = owner.InverseTransformPoint(fallback);
        Vector3 rotatedOffset = owner.TransformVector(localOffset);
        Double3 stableWorld = ownerWp.worldPosition + (Double3)rotatedOffset;
        return coordinator.ToLocal(stableWorld);
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
}
