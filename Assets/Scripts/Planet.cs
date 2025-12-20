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
    public float unfocusedScaleMultiplier = 30f;

    void Start()
    {
        originalScale = transform.localScale;
        
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.OnFocusChanged += HandleFocusChanged;
            HandleFocusChanged(FocusManager.Instance.CurrentFocus);
        }
    }

    void OnDestroy()
    {
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.OnFocusChanged -= HandleFocusChanged;
        }
    }

    private void HandleFocusChanged(Transform newFocus)
    {
        bool isFocused = (newFocus == transform);

        if (isFocused)
        {
            transform.localScale = originalScale;
        }
        else
        {
            transform.localScale = originalScale * unfocusedScaleMultiplier;
        }

        Orbiter orbiter = GetComponent<Orbiter>();
        if (orbiter == null || orbiter.centralBody == null) return;

        Gravity gravityComponent = GetComponent<Gravity>();
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