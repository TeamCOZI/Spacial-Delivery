using UnityEngine;

[DefaultExecutionOrder(33980)]
[DisallowMultipleComponent]
[RequireComponent(typeof(LauncherLaunchController))]
[RequireComponent(typeof(LauncherSelectionPresenter))]
[RequireComponent(typeof(LauncherDirectionGuide))]
[RequireComponent(typeof(SpaceshipHUDPresenter))]
[RequireComponent(typeof(SpaceshipTargetPresenter))]
[RequireComponent(typeof(SpaceshipOrbitCommitPresenter))]
[RequireComponent(typeof(CoreLogisticsHubPresenter))]
[RequireComponent(typeof(ModuleInventoryPresenter))]
[RequireComponent(typeof(ModuleProcessingPresenter))]
[RequireComponent(typeof(PowerGeneratorPresenter))]
[RequireComponent(typeof(ModuleOutputPortSelectorPresenter))]
[RequireComponent(typeof(AssemblyFocusedPartHighlightPresenter))]
public class FocusFeatureBootstrap : MonoBehaviour
{
    private void Awake()
    {
        _ = ComponentUtility.GetOrAddComponent<SpaceshipTargetPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<SpaceshipOrbitCommitPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<CoreLogisticsHubPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<ModuleInventoryPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<ModuleProcessingPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<PowerGeneratorPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<ModuleOutputPortSelectorPresenter>(gameObject);
        _ = ComponentUtility.GetOrAddComponent<AssemblyFocusedPartHighlightPresenter>(gameObject);

        OutputPortSelectionPresenter[] legacySelectors = GetComponents<OutputPortSelectionPresenter>();
        for (int i = 0; i < legacySelectors.Length; i++)
        {
            OutputPortSelectionPresenter legacySelector = legacySelectors[i];
            if (legacySelector == null)
            {
                continue;
            }

            legacySelector.enabled = false;
            Destroy(legacySelector);
        }
    }
}