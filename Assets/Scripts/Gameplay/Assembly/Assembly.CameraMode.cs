using UnityEngine;

public partial class Assembly
{
    private bool TryEnsureMainCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        return mainCamera != null;
    }

    private static void EnterAssemblyCameraMode()
    {
        if (CoreRuntimeAccess.TryGetCameraManager(out CameraManager cameraManager))
        {
            cameraManager.EnterAssemblyMode();
        }
        else
        {
            Debug.LogWarning("Assembly: CameraManager instance is missing. Assembly camera mode was not entered.");
        }
    }

    private static void ExitAssemblyCameraMode()
    {
        if (CoreRuntimeAccess.TryGetCameraManager(out CameraManager cameraManager))
        {
            cameraManager.ExitAssemblyMode();
        }
        else
        {
            Debug.LogWarning("Assembly: CameraManager instance is missing. Assembly camera mode was not exited.");
        }
    }
}
