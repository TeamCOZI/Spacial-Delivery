using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject confirmationPanel;
    public GameObject focusInfoPanel;

    [Header("UI Controls")]
    public Slider iconSizeSlider;

    [Header("Button Control")]
    public GameObject launchButton;
    public CameraController cameraController;
    
    [Header("Confirmation Panel Buttons")]
    [SerializeField] private Button confirmRouteButton;
    [SerializeField] private Button declineRouteButton;

    private ArtificialSatellite _targetSatellite;

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

        if (focusInfoPanel != null)
        {
            focusInfoPanel.SetActive(false);
        }

        if (confirmRouteButton != null)
        {
            confirmRouteButton.onClick.AddListener(OnConfirmRoute);
        }
        if (declineRouteButton != null)
        {
            declineRouteButton.onClick.AddListener(OnDeclineRoute);
        }

        if (iconSizeSlider != null)
        {
            iconSizeSlider.value = GameSettings.IconSize;
            iconSizeSlider.onValueChanged.AddListener(OnIconSizeChanged);
        }
    }

    public void ShowConfirmationPanel(ArtificialSatellite satelliteToConfirm)
    {
        if (confirmationPanel != null)
        {
            _targetSatellite = satelliteToConfirm;
            confirmationPanel.SetActive(true);
        }
    }

    public void HideConfirmationPanel()
    {
        if (confirmationPanel != null)
        {
            _targetSatellite = null;
            confirmationPanel.SetActive(false);
        }
    }

    public void OnConfirmRoute()
    {
        if (_targetSatellite != null)
        {
            _targetSatellite.ConfirmRoute();
        }
        HideConfirmationPanel();
    }

    public void OnDeclineRoute()
    {
        if (_targetSatellite != null)
        {
            _targetSatellite.DeclineRoute();
        }
        HideConfirmationPanel();
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

    public void OnIconSizeChanged(float value)
    {
        GameSettings.IconSize = value;
    }
}