using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject confirmationPanel;

    [Header("Button Control")]
    public GameObject launchButton;
    public CameraController cameraController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (launchButton != null)
        {
            launchButton.SetActive(false);
        }
    }

    public void ShowConfirmationPanel()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }
    }

    public void HideConfirmationPanel()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }

    public void ShowLaunchButton()
    {
        if (launchButton != null)
        {
            launchButton.SetActive(true);
        }
    }

    public void HideLaunchButton()
    {
        if (launchButton != null)
        {
            launchButton.SetActive(false);
        }
    }
}