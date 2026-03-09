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

        Transform rotationSource = ResolveFocusRotationSource(focus);
        return rotationSource != null ? rotationSource.eulerAngles.z : 0f;
    }

    public float GetZoomScaleForFocus(Transform focus)
    {
        if (focus == null) return 1f;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
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

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        return partFocus != null && partFocus.OwnerSatellite != null;
    }

    public bool IsLauncherPartFocus(Transform focus)
    {
        if (focus == null) return false;

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null) return false;

        string partName = partFocus.SourcePart.partName;
        return string.Equals(partName, "Launcher", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsSpaceshipFocus(Transform focus)
    {
        if (focus == null) return false;
        return focus.GetComponent<Spaceship>() != null;
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

        AssemblyPartFocus partFocus = focus.GetComponent<AssemblyPartFocus>();
        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        return focus;
    }
}
