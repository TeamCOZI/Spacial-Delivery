using Unity.VisualScripting;
using UnityEngine;

public class Icon : MonoBehaviour
{
    [Header("Icon Settings")]
    public GameObject iconCircle;
    public float screenHeightFraction = 0.01f;

    private Renderer mainRenderer;
    private Gravity gravityComponent;
    private float tanHalFov;
    private bool isVisible = true;

    void Start()
    {
        mainRenderer = GetComponent<Renderer>();
        gravityComponent = GetComponent<Gravity>();

        if (Camera.main != null)
        {
            tanHalFov = Mathf.Tan(Camera.main.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        if (iconCircle != null)
        {
            iconCircle.SetActive(false);
        }

        if (mainRenderer == null || gravityComponent == null)
        {
            Debug.LogWarning($"Icon on {gameObject.name} is missing Renderer or Gravity Component.");
            enabled = false;
        }
    }

    void Update()
    {
        if (Camera.main == null || !isVisible)
        {
            return;
        }

        HandleVisibilityByDistance();
    }

    private void HandleVisibilityByDistance()
    {
        bool isFocused = Camera.main.transform.position.z > gravityComponent.gravityRadius * -10;

        if (isFocused)
        {
            mainRenderer.enabled = true;
            if (iconCircle != null)
            {
                iconCircle.SetActive(false);
            }
        }
        else
        {
            mainRenderer.enabled = false;

            if (iconCircle != null)
            {
                iconCircle.SetActive(true);

                float zDistance = Mathf.Abs(Camera.main.transform.position.z);
                float frustumHeight = 2.0f * zDistance * tanHalFov;
                float targetWorldSize = frustumHeight * screenHeightFraction;
                iconCircle.transform.localScale = Vector3.one * targetWorldSize;
            }
        }
    }

    public void SetVisibility(bool visible)
    {
        isVisible = visible;
        mainRenderer.enabled = visible;
        if (iconCircle != null)
        {
            iconCircle.SetActive(visible);
        }

        if (!visible)
        {
            if (iconCircle != null) iconCircle.SetActive(false);
        }
    }
}