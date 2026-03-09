public static class CoreRuntimeAccess
{
    public static bool TryGetFocusService(out IFocusService focusService)
    {
        focusService = FocusManager.Instance;
        return focusService != null;
    }

    public static bool TryGetFocusManager(out FocusManager focusManager)
    {
        focusManager = FocusManager.Instance;
        return focusManager != null;
    }

    public static bool TryGetCameraManager(out CameraManager cameraManager)
    {
        cameraManager = CameraManager.Instance;
        return cameraManager != null;
    }

    public static bool TryGetUserInput(out UserInput userInput)
    {
        userInput = UserInput.Instance;
        return userInput != null;
    }

    public static bool TryGetInputService(out IInputService inputService)
    {
        inputService = UserInput.Instance;
        return inputService != null;
    }

    public static bool TryGetCameraFocusPolicy(out ICameraFocusPolicy cameraFocusPolicy)
    {
        cameraFocusPolicy = CameraFocusPolicy.Current;
        return cameraFocusPolicy != null;
    }

    public static bool TryGetTimeManager(out TimeManager timeManager)
    {
        timeManager = TimeManager.Instance;
        return timeManager != null;
    }

    public static bool TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator)
    {
        coordinator = LargeWorldCoordinator.Instance;
        return coordinator != null;
    }
}
