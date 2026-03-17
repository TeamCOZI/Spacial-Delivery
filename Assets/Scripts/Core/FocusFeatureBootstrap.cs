using UnityEngine;

[DefaultExecutionOrder(33980)]
[DisallowMultipleComponent]
[RequireComponent(typeof(LauncherLaunchController))]
[RequireComponent(typeof(LauncherSelectionPresenter))]
[RequireComponent(typeof(LauncherDirectionGuide))]
[RequireComponent(typeof(SpaceshipHUDPresenter))]
[RequireComponent(typeof(SpaceshipTargetPresenter))]
public class FocusFeatureBootstrap : MonoBehaviour
{
    private void Awake()
    {
        _ = ComponentUtility.GetOrAddComponent<SpaceshipTargetPresenter>(gameObject);
    }
}


