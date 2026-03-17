using UnityEngine;

public static class CameraFocusPolicy
{
    private static readonly ICameraFocusPolicy DefaultPolicy = new DefaultCameraFocusPolicy();
    private static ICameraFocusPolicy current = DefaultPolicy;

    public static ICameraFocusPolicy Current
    {
        get => current ?? DefaultPolicy;
        set => current = value ?? DefaultPolicy;
    }

    public static float ResolveTargetRotationZ(Transform focus)
    {
        return Current.ResolveTargetRotationZ(focus);
    }

    public static float GetZoomScaleForFocus(Transform focus)
    {
        return Current.GetZoomScaleForFocus(focus);
    }

    public static bool IsSatelliteRelatedFocus(Transform focus)
    {
        return Current.IsSatelliteRelatedFocus(focus);       
    }

    public static bool IsLauncherPartFocus(Transform focus)
    {
        return Current.IsLauncherPartFocus(focus);
    }

    public static bool IsSpaceshipFocus(Transform focus)
    {
        return Current.IsSpaceshipFocus(focus);
    }
}
