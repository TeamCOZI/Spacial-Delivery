using System.Collections.Generic;
using UnityEngine;

public enum planetClass { terrestrial, jovian }
public class Planet : MonoBehaviour
{
    public planetClass planetClass_ { get; set; }
    public float heat { get; set; }
    public float atm { get; set; }
    public List<GameObject> satellites { get; set; }

    private Vector3 originalScale;

    [Header("Distance-Based Visibility")]
    public float visibilityDistance = 500f;
    public float screenHeightFraction = 0.01f;

    private float tanHalFov;

    public float unfocusedScaleMultiplier = 30f;

    void Start()
    {
        originalScale = transform.localScale;
        
        if (Camera.main != null)
        {
            tanHalFov = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
    }

    void Update()
    {
        if (Camera.main != null)
        {
            HandleVisibilityByDistance();
        }
    }

    private void HandleVisibilityByDistance()
    {
        Gravity gravityComponent = GetComponent<Gravity>();

        bool isFocused = Camera.main.transform.position.z > gravityComponent.gravityRadius * -10;

        if (isFocused)
        {
            transform.localScale = originalScale;
        }
        else
        {
            float zDistance = Mathf.Abs(Camera.main.transform.position.z);
            
            float frustumHeight = 2.0f * zDistance * tanHalFov;

            float targetWorldSize = frustumHeight * screenHeightFraction;

            transform.localScale = originalScale * targetWorldSize;
        }

        Orbiter orbiter = GetComponent<Orbiter>();
        if (orbiter == null || orbiter.centralBody == null) return;

        if (gravityComponent != null)
        {
            gravityComponent.SetRadiusVisualVisibility(isFocused);
        }

        SetSatelliteVisibility(isFocused);
    }

    public void SetSatelliteVisibility(bool isVisible)
    {
        if (satellites == null) return;

        foreach (var satelliteGO in satellites)
        {
            if (satelliteGO != null)
            {
                ArtificialSatellite satellite = satelliteGO.GetComponent<ArtificialSatellite>();
                if (satellite != null)
                {
                    satellite.SetVisibility(isVisible);
                }
            }
        }
    }
}