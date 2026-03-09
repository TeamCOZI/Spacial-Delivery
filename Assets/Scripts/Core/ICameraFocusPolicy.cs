using UnityEngine;

public interface ICameraFocusPolicy
{
    float ResolveTargetRotationZ(Transform focus);
    float GetZoomScaleForFocus(Transform focus);
    bool IsSatelliteRelatedFocus(Transform focus);
    bool IsLauncherPartFocus(Transform focus);
    bool IsSpaceshipFocus(Transform focus);
}
