using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LaunchButtonController : MonoBehaviour
{
    public CameraController cameraController;
    private bool isButtonShown = false;

    private void Update()
    {
        if (cameraController == null || UIManager.Instance == null) return;

        bool shouldShow = false;
        if (cameraController.SelectedPrefab != null && cameraController.SelectedPrefab.gameObject.activeInHierarchy)
        {
            if (cameraController.SelectedPrefab.GetComponent<ArtificialSatellite>() != null)
            {
                shouldShow = true;
            }
        }

        if (isButtonShown != shouldShow)
        {
            if (shouldShow)
            {
                UIManager.Instance.ShowLaunchButton();
            }
            else
            {
                UIManager.Instance.HideLaunchButton();
            }

            isButtonShown = shouldShow;
        }
    }
}