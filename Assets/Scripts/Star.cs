using UnityEngine;

public class Star : MonoBehaviour
{
    [Header("Light Settings")]
    public Color lightColor = Color.red;
    [Range(0, 10)]
    public float lightIntensity = 1f;
    [Min(0)]
    public float lightRadius = 20f;

    private Light starLight;

    private void Awake()
    {
        if (!TryGetComponent(out starLight))
        {
            starLight = gameObject.AddComponent<Light>();
        }

        starLight.type = LightType.Point;
        starLight.shadows = LightShadows.Soft;

        ApplyLightProperties();
    }

    private void Update()
    {
        ApplyLightProperties();
    }

#if UNITY_EDITOR
    private void Onalidate()
    {
        if (starLight == null)
        {
            starLight = GetComponent<Light>();
        }

        if (starLight != null)
        {
            ApplyLightProperties();
        }
    }
#endif

    private void ApplyLightProperties()
    {
        if (starLight != null)
        {
            starLight.color = lightColor;
            starLight.intensity = lightIntensity;
            starLight.range = lightRadius;
        }
    }
}