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
    public float baseFocusSize = 0.005f;

    private float tanHalfFov;

    public float unfocusedScaleMultiplier = 30f;

    private Renderer mainRenderer;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        mainRenderer = GetComponent<Renderer>();
        if (mainRenderer == null)
        {
            Debug.LogError("Cannot find renderer component.", this);
        }

        propBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
        originalScale = transform.localScale;
        
        if (Camera.main != null)
        {
            tanHalfFov = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
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
        if (gravityComponent == null) return;

        bool isFocused = Camera.main.transform.position.z > gravityComponent.gravityRadius * -10;

        float isVisibleValue = isFocused ? 0f : 1f;

        mainRenderer.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat("_IsVisible", isVisibleValue);
        mainRenderer.SetPropertyBlock(propBlock, 1);

        if (isFocused)
        {
            transform.localScale = originalScale;
        }
        else
        {
            float zDistance = Mathf.Abs(Camera.main.transform.position.z);
            float frustumHeight = 2.0f * zDistance * tanHalfFov;
            float targetWorldSize = frustumHeight * screenHeightFraction * GameSettings.IconSize;
            transform.localScale = Vector3.one * baseFocusSize * targetWorldSize;
        }
        
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