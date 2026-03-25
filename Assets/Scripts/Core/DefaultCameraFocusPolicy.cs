using System;
using UnityEngine;

public sealed class DefaultCameraFocusPolicy : ICameraFocusPolicy
{
    public float ResolveTargetRotationZ(Transform focus)
    {
        if (IsLauncherPartFocus(focus))
        {
            return 0f;
        }

        if (TryResolveCommittedSpaceshipCameraRotationZ(focus, out float committedSpaceshipCameraRotationZ))
        {
            return committedSpaceshipCameraRotationZ;
        }

        Transform rotationSource = ResolveFocusRotationSource(focus);
        return rotationSource != null ? rotationSource.eulerAngles.z : 0f;
    }

    public float GetZoomScaleForFocus(Transform focus)
    {
        if (focus == null) return 1f;

        AssemblyPartFocus partFocus = ResolvePartFocus(focus);
        if (partFocus != null &&
            IsLauncherPartFocus(focus) &&
            partFocus.OwnerSatellite != null)
        {
            return GetFocusLossyScale(partFocus.OwnerSatellite.transform);
        }

        return GetFocusLossyScale(focus);
    }
    public bool IsSatelliteRelatedFocus(Transform focus)
    {
        if (focus == null) return false;

        if (focus.GetComponent<ArtificialSatellite>() != null) return true;

        if (TryResolveOutputPortOwnerSatellite(focus, out ArtificialSatellite outputPortOwner) && outputPortOwner != null)
        {
            return true;
        }

        AssemblyPartFocus partFocus = ResolvePartFocus(focus);
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return true;
        }

        if (TryResolveStructureOwnerSatellite(focus, out ArtificialSatellite structureOwner) && structureOwner != null)
        {
            return true;
        }

        if (SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship) && spaceship != null)
        {
            SpaceshipRendezvousController rendezvousController = spaceship.GetComponent<SpaceshipRendezvousController>();
            return rendezvousController != null && rendezvousController.IsOrbitCommitted;
        }

        return false;
    }

    public bool IsLauncherPartFocus(Transform focus)
    {
        if (focus == null) return false;

        AssemblyPartFocus partFocus = ResolvePartFocus(focus);
        if (partFocus == null || partFocus.SourcePart == null) return false;

        string partName = partFocus.SourcePart.partName;
        return string.Equals(partName, "Launcher", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsSpaceshipFocus(Transform focus)
    {
        return SpaceshipFocusUtility.TryResolveSpaceship(focus, out _);
    }

    private static bool TryResolveCommittedSpaceshipCameraRotationZ(Transform focus, out float rotationZ)
    {
        rotationZ = 0f;

        if (!SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship) || spaceship == null)
        {
            return false;
        }

        SpaceshipRendezvousController rendezvousController = spaceship.GetComponent<SpaceshipRendezvousController>();
        return rendezvousController != null && rendezvousController.TryGetCommittedOrbitCameraRotationZ(out rotationZ);
    }

    private static bool TryResolveOutputPortOwnerSatellite(Transform focus, out ArtificialSatellite ownerSatellite)
    {
        ownerSatellite = null;
        if (focus == null) return false;

        AssemblyOutputPortFocus outputPortFocus = focus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus == null)
        {
            outputPortFocus = focus.GetComponentInParent<AssemblyOutputPortFocus>();
        }

        if (outputPortFocus == null)
        {
            return false;
        }

        ownerSatellite = outputPortFocus.OwnerSatellite;
        return ownerSatellite != null;
    }

    private static bool TryResolveStructureOwnerSatellite(Transform focus, out ArtificialSatellite ownerSatellite)
    {
        ownerSatellite = null;
        if (focus == null) return false;

        StructureFocus structureFocus = focus.GetComponent<StructureFocus>();
        if (structureFocus == null)
        {
            structureFocus = focus.GetComponentInParent<StructureFocus>();
        }

        if (structureFocus == null)
        {
            return false;
        }

        ownerSatellite = structureFocus.OwnerSatellite;
        return ownerSatellite != null;
    }

    private static float GetFocusLossyScale(Transform focus)
    {
        if (focus == null) return 1f;

        Vector3 scale = focus.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.01f);
    }

    private static Transform ResolveFocusRotationSource(Transform focus)
    {
        if (focus == null) return null;

        if (TryResolveOutputPortOwnerSatellite(focus, out ArtificialSatellite outputPortOwner) && outputPortOwner != null)
        {
            return outputPortOwner.transform;
        }

        AssemblyPartFocus partFocus = ResolvePartFocus(focus);
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        if (TryResolveStructureOwnerSatellite(focus, out ArtificialSatellite structureOwner) && structureOwner != null)
        {
            return structureOwner.transform;
        }

        if (SpaceshipFocusUtility.TryResolveSpaceship(focus, out Spaceship spaceship))
        {
            return spaceship.transform;
        }

        return focus;
    }

    private static AssemblyPartFocus ResolvePartFocus(Transform focus)
    {
        if (focus == null)
        {
            return null;
        }

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            return partFocus;
        }

        AssemblyOutputPortFocus outputPortFocus = focus.GetComponent<AssemblyOutputPortFocus>();
        if (outputPortFocus == null)
        {
            outputPortFocus = focus.GetComponentInParent<AssemblyOutputPortFocus>();
        }

        return outputPortFocus != null ? outputPortFocus.ResolveOwnerModuleFocus() : null;
    }
}

