using UnityEngine;

[DefaultExecutionOrder(34001)]
[DisallowMultipleComponent]
public class LauncherLaunchFocusHandler : MonoBehaviour
{
    private LauncherLaunchController launchController;

    private void Awake()
    {
        EnsureLaunchController();
    }

    private void OnEnable()
    {
        EnsureLaunchController();
        if (launchController != null)
        {
            launchController.launchSucceeded += HandleLaunchSucceeded;
        }
    }

    private void OnDisable()
    {
        if (launchController != null)
        {
            launchController.launchSucceeded -= HandleLaunchSucceeded;
        }
    }

    private static void HandleLaunchSucceeded(LauncherSpawner.LaunchResult launchResult)
    {
        if (launchResult.spaceship == null) return;
        if (!CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager)) return;

        Spaceship spaceship = launchResult.spaceship.GetComponent<Spaceship>();
        Transform focusTarget = SpaceshipFocusUtility.ResolveFocusTarget(spaceship);
        focusManager.SetFocus(focusTarget);
        CameraManager.Instance?.ApplyImmediateLauncherLaunchZoom(launchResult.launcher);
    }

    private void EnsureLaunchController()
    {
        if (launchController == null)
        {
            launchController = GetComponent<LauncherLaunchController>();
        }
    }
}


