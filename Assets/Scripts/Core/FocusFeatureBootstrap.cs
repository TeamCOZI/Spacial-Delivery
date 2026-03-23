using UnityEngine;

[DefaultExecutionOrder(33980)]
[DisallowMultipleComponent]
[RequireComponent(typeof(LauncherLaunchController))]
[RequireComponent(typeof(LauncherSelectionPresenter))]
[RequireComponent(typeof(LauncherDirectionGuide))]
[RequireComponent(typeof(SpaceshipHUDPresenter))]
[RequireComponent(typeof(SpaceshipTargetPresenter))]
[RequireComponent(typeof(SpaceshipOrbitCommitPresenter))]
[RequireComponent(typeof(OutputPortSelectionPresenter))]
public class FocusFeatureBootstrap : MonoBehaviour
{
    private void Awake()
    {
        _ = ComponentUtility.GetOrAddComponent<SpaceshipTargetPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<SpaceshipOrbitCommitPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<OutputPortSelectionPresenter>(gameObject);
    }
}
