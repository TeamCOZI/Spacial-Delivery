using System;
using System.Collections.Generic;
using UnityEngine;

public class FocusManager : MonoBehaviour
{
    public static FocusManager Instance { get; private set; }

    public Transform CurrentFocus { get; private set; }
    public event Action<Transform> OnFocusChanged;

    private readonly List<Planet> allPlanets = new List<Planet>();
    private readonly List<ArtificialSatellite> allSatellites = new List<ArtificialSatellite>();

    private Camera mainCamera;
    private float tanHalfFov;
    private Component hoveredComponent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                tanHalfFov = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
        }
    }

    private void OnEnable()
    {
        OnFocusChanged += HandleFocusChange;
    }

    private void OnDisable()
    {
        OnFocusChanged -= HandleFocusChange;
    }

    private void LateUpdate()
    {
        UpdateAllVisuals();
    }

    private void HandleFocusChange(Transform newFocus)
    {
        UpdateAllVisuals();
    }

    public void SetFocus(Transform newFocus)
    {
        if (CurrentFocus == newFocus) return;

        CurrentFocus = newFocus;
        OnFocusChanged?.Invoke(CurrentFocus);
        Debug.Log($"Focus changed to: {(newFocus != null ? newFocus.name : "None")}");
    }

    private void UpdateAllVisuals()
    {
        if (mainCamera == null) return;

        float zDistance = Mathf.Abs(mainCamera.transform.position.z);
        float frustumHeight = 2.0f * zDistance * tanHalfFov;

        if (CurrentFocus == null || CurrentFocus.GetComponent<Star>() != null)
        {
            foreach (var planet in allPlanets)
            {
                planet.ShowAsIcon(frustumHeight);

            }
            foreach (var satellite in allSatellites)
            {
                satellite.Hide();
            }
        }
        else
        {
            Planet focusedPlanet = CurrentFocus.GetComponent<Planet>();
            ArtificialSatellite focusedSatellite = CurrentFocus.GetComponent<ArtificialSatellite>();

            if (focusedPlanet != null)
            {
                foreach (var planet in allPlanets)
                {
                    if (planet == focusedPlanet)
                    {
                        planet.ShowDetailView();
                    }
                    else
                    {
                        planet.ShowAsIcon(frustumHeight);
                    }
                }
                foreach (var satellite in allSatellites)
                {
                    Orbiter orbiter = satellite.GetComponent<Orbiter>();
                    if (orbiter != null && orbiter.centralBody.transform == focusedPlanet.transform)
                    {
                        satellite.ShowAsIcon(frustumHeight);
                    }
                    else
                    {
                        satellite.Hide();
                    }
                }
            }
            else if (focusedSatellite != null)
            {
                Planet parentPlanet = focusedSatellite.GetComponentInParent<Planet>();

                foreach (var planet in allPlanets)
                {
                    if (planet == parentPlanet)
                    {
                        planet.ShowDetailView();
                    }
                    else
                    {
                        planet.ShowAsIcon(frustumHeight);
                    }
                }
                foreach (var satellite in allSatellites)
                {
                    if (satellite == focusedSatellite)
                    {
                        satellite.ShowDetailView();
                    }
                    else
                    {
                        satellite.Hide();
                    }
                }
            }
        }
    }

    public void RegisterPlanet(Planet planet)
    {
        if (!allPlanets.Contains(planet))
        {
            allPlanets.Add(planet);
        }
    }

    public void DeregisterPlanet(Planet planet)
    {
        if (allPlanets.Contains(planet))
        {
            allPlanets.Remove(planet);
        }
    }

    public void RegisterSatellite(ArtificialSatellite satellite)
    {
        if (!allSatellites.Contains(satellite))
        {
            allSatellites.Add(satellite);
        }
    }

    public void DeregisterSatellite(ArtificialSatellite satellite)
    {
        if (allSatellites.Contains(satellite))
        {
            allSatellites.Remove(satellite);
        }
    }

    public void SetHoveredObject(Transform hoveredTransform)
    {
        Component newHoveredComponent = null;
        if (hoveredTransform != null)
        {
            newHoveredComponent = (Component)hoveredTransform.GetComponent<Planet>() ?? (Component)hoveredTransform.GetComponent<ArtificialSatellite>();
        }

        if (newHoveredComponent != hoveredComponent)
        {
            if (hoveredComponent is Planet oldPlanet)
            {
                oldPlanet.SetHover(false);
            }
            else if (hoveredComponent is ArtificialSatellite oldSatellite)
            {
                oldSatellite.SetHover(false);
            }

            hoveredComponent = newHoveredComponent;
            if (hoveredComponent is Planet newPlanet)
            {
                newPlanet.SetHover(true);
            }
            else if (hoveredComponent is ArtificialSatellite newSatellite)
            {
                newSatellite.SetHover(true);
            }

            UpdateAllVisuals();
        }
    }
}