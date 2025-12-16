using UnityEngine;
using TMPro;
using System;

public class FocusInfoUI : MonoBehaviour
{
    [Header("Dependencies")]
    public CameraController cameraController;

    [Header("UI Elements")]
    public GameObject focusPanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI classificationText;
    public TextMeshProUGUI temperatureText;
    public TextMeshProUGUI gravityText;
    public TextMeshProUGUI atmText;
    public TextMeshProUGUI satellitesText;
    public TextMeshProUGUI centralBodyText;
    public TextMeshProUGUI periodText;
    public TextMeshProUGUI resourcesText;

    private void Start()
    {
        if (cameraController == null)
        {
            return;
        }

        cameraController.OnSelectionChanged += HandleSelectionChanged;

        if (focusPanel != null)
        {
            focusPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (cameraController != null)
        {
            cameraController.OnSelectionChanged -= HandleSelectionChanged;
        }
    }

    private void HandleSelectionChanged(Transform newSelection)
    {
        if (newSelection != null && IsCelestialBody(newSelection))
        {
            UpdateInfo(newSelection);
            if (focusPanel != null)
            {
                focusPanel.SetActive(true);
            }
        }
        else
        {
            if (focusPanel != null)
            {
                focusPanel.SetActive(false);
            }
        }
    }

    private bool IsCelestialBody(Transform target)
    {
        return target.GetComponent<PlayerSpaceship>() == null;
    }

    public void UpdateInfo(Transform target)
    {
        // Name
        nameText.text = $"이름 : {target.name}";

        Star star = target.GetComponent<Star>();
        Planet planet = target.GetComponent<Planet>();
        ArtificialSatellite satellite = target.GetComponent<ArtificialSatellite>();
        Orbiter orbiter = target.GetComponent<Orbiter>();
        Gravity gravity = target.GetComponent<Gravity>();

        // Classification
        if (star != null)
        {
            classificationText.text = "분류 : 항성";
        }
        else if (planet != null)
        {
            if (planet.planetClass_ == planetClass.terrestrial) classificationText.text = "분류 : 암석형 행성";
            else classificationText.text = "분류 : 가스형 행성";
        }
        else if (satellite != null)
        {
            classificationText.text = "분류 : 인공위성";
        }
        else
        {
            classificationText.text = "분류 : N/A";
        }

        // Temperature
        if (planet != null)
        {
            if (planet.heat >= 100)
            {
                temperatureText.text = "온도 : 초고온";
            }
            else if (planet.heat >= 50)
            {
                temperatureText.text = "온도 : 고온";
            }
            else if (planet.heat >= 0)
            {
                temperatureText.text = "온도 : 평범함";
            }
            else if (planet.heat >= -100)
            {
                temperatureText.text = "온도 : 저온";
            }
            else
            {
                temperatureText.text = "온도 : 초저온";
            }
        }
        else if (satellite != null)
        {
            if (satellite.heat >= 100)
            {
                temperatureText.text = "온도 : 초고온";
            }
            else if (satellite.heat >= 50)
            {
                temperatureText.text = "온도 : 고온";
            }
            else if (satellite.heat >= 0)
            {
                temperatureText.text = "온도 : 평범함";
            }
            else if (satellite.heat >= -100)
            {
                temperatureText.text = "온도 : 저온";
            }
            else
            {
                temperatureText.text = "온도 : 초저온";
            }
        }
        else
        {
            temperatureText.text = "온도 : N/A";
        }

        // Gravity
        if (gravity != null)
        {
            if (gravity.gravity >= 5000)
            {
                gravityText.text = "중력 : 매우 강함";
            }
            else if (gravity.gravity >= 301)
            {
                gravityText.text = "중력 : 강함";
            }
            else if (gravity.gravity >= 150)
            {
                gravityText.text = "중력 : 평범함";
            }
            else if (gravity.gravity >= 30)
            {
                gravityText.text = "중력 : 약함";
            }
            else
            {
                gravityText.text = "중력 : 매우 약함";
            }
        }
        else
        {
            gravityText.text = "중력 : N/A";
        }

        // ATM
        if (planet != null)
        {
            if (planet.atm >= 2401)
            {
                atmText.text = "기압 : 매우 높음";
            }
            else if (planet.atm >= 1201)
            {
                atmText.text = "기압 : 높음";
            }
            else if (planet.atm >= 300)
            {
                atmText.text = "기압 : 평범함";
            }
            else if (planet.atm >= 60)
            {
                atmText.text = "기압 : 낮음";
            }
            else
            {
                atmText.text = "기압 : 매우 낮음";
            }
        }
        else if (satellite != null)
        {
            if (satellite.atm >= 2401)
            {
                atmText.text = "기압 : 매우 높음";
            }
            else if (satellite.atm >= 1201)
            {
                atmText.text = "기압 : 높음";
            }
            else if (satellite.atm >= 300)
            {
                atmText.text = "기압 : 평범함";
            }
            else if (satellite.atm >= 60)
            {
                atmText.text = "기압 : 낮음";
            }
            else
            {
                atmText.text = "기압 : 매우 낮음";
            }
        }
        else
        {
            atmText.text = "기압 : N/A";
        }

        // Satellites
        if (star != null)
        {
            String planetsToPrint = "행성 : ";
            for (int i = 0; i < star.planets.Count; i++)
            {
                if (i == 0) planetsToPrint += $"{star.planets[i].name}";
                else planetsToPrint += $" ,{star.planets[i].name}";
            }
            satellitesText.text = planetsToPrint;
        }
        else if (planet != null)
        {
            String satellitesToPrint = "위성 : ";
            for (int i = 0; i < planet.satellites.Count; i++)
            {
                if (i == 0) satellitesToPrint += $"{planet.satellites[i].name}";
                else satellitesToPrint += $" ,{planet.satellites[i].name}";
            }
            satellitesText.text = satellitesToPrint;
        }
        else
        {
            satellitesText.text = "위성 : N/A";
        }

        // Central Body
        if (orbiter != null)
        {
            centralBodyText.text = $"공전 모체 : {orbiter.centralBody.name}";
        }
        else
        {
            centralBodyText.text = "공전 모체 : N/A";
        }

        // Period
        if (orbiter != null)
        {
            periodText.text = $"공전 주기 : {(int)(360f / orbiter.orbitSpeed)}";
        }

        // Resources
        resourcesText.text = "획득 가능 자원 : N/A";
    }
}