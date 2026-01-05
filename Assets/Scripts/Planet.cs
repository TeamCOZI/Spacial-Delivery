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
    public float baseFocusSize = 0.0025f;

    [Header("Hover Settings")]
    public float hoverScaleMultiplier = 1.2f;
    public float hoverTransitionSpeed = 5f;

    private float tanHalfFov;
    public bool isHovered = false;
    private Vector3 targetScale;
    private Vector3 currentScaleVelocity;

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

    private void OnEnable()
    {
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.RegisterPlanet(this);
        }
    }

    private void OnDisable()
    {
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.DeregisterPlanet(this);
        }
    }

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        
        if (Camera.main != null)
        {
            tanHalfFov = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
    }

    void Update()
    {
        if (Camera.main != null)
        {
            transform.localScale = Vector3.SmoothDamp(transform.localScale, targetScale, ref currentScaleVelocity, hoverTransitionSpeed);
        }
    }

    public void ShowDetailView()
    {
        mainRenderer.enabled = true;

        mainRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_IsVisible", 0f);
        mainRenderer.SetPropertyBlock(propBlock);

        targetScale = originalScale;

        if(GetComponent<Gravity>() != null)
        {
            GetComponent<Gravity>().SetRadiusVisualVisibility(true);
        }
    }

    public void ShowAsIcon(float frustumHeight)
    {
        mainRenderer.enabled = true;

        mainRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_IsVisible", 1f);
        mainRenderer.SetPropertyBlock(propBlock);

        float targetWorldSize = frustumHeight * screenHeightFraction * GameSettings.IconSize;
        float finalSize = isHovered
        ? baseFocusSize * targetWorldSize * hoverScaleMultiplier
        : baseFocusSize * targetWorldSize;
        targetScale = Vector3.one * finalSize;

        if(GetComponent<Gravity>() != null)
        {
            GetComponent<Gravity>().SetRadiusVisualVisibility(false);
        }
    }

    public void SetHover(bool hover)
    {
        isHovered = hover;
    }
}