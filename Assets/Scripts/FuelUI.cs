using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;

public class FuelUI : MonoBehaviour
{
    public Slider fuelSlider;
    public TextMeshProUGUI fuelText;
    public CameraController cameraController;

    private void Start()
    {
        if (cameraController == null)
        {
            return;
        }

        cameraController.OnSelectionChanged += HandleSelectionChanged;
        PlayerSpaceship.OnFuelUpdated += UpdateFuelUI;

        gameObject.SetActive(false);
    }

    private void Awake()
    {
        PlayerSpaceship.OnFuelUpdated += UpdateFuelUI;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cameraController != null)
        {
            cameraController.OnSelectionChanged -= HandleSelectionChanged;
        }
        PlayerSpaceship.OnFuelUpdated -= UpdateFuelUI;
    }

    private void HandleSelectionChanged(Transform newSelection)
    {
        if (newSelection == null || newSelection.GetComponent<PlayerSpaceship>() == null)
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateFuelUI(float currentFuel, float maxFuel)
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }

        float fuelPercentage = currentFuel / maxFuel;
        if (fuelSlider != null)
        {
            fuelSlider.value = fuelPercentage;
        }

        if (fuelText != null)
        {
            fuelText.text = $"Fuel: {Mathf.CeilToInt(currentFuel)} / {Mathf.CeilToInt(maxFuel)}";
        }

        if (currentFuel <= 0)
        {
            
        }
    }
}