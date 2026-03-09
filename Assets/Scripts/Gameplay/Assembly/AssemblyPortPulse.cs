using UnityEngine;

[DisallowMultipleComponent]
public class AssemblyPortPulse : MonoBehaviour
{
    [SerializeField] private float minBrightness = 0.35f;
    [SerializeField] private float maxBrightness = 3.0f;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float minEmission = 0.2f;
    [SerializeField] private float maxEmission = 6.0f;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool isPulsing;
    private Color baseColor = Color.white;
    private Color baseEmissionColor = Color.black;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        CacheBaseColor();
        EnableEmissionKeyword();
    }

    private void OnEnable()
    {
        if (!isPulsing) ApplyBrightnessAndEmission(1f, 0f);
    }

    private void OnDisable()
    {
        ApplyBrightnessAndEmission(1f, 0f);
    }

    private void Update()
    {
        if (!isPulsing || targetRenderer == null) return;

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float brightness = Mathf.Lerp(minBrightness, maxBrightness, pulse);
        float emission = Mathf.Lerp(minEmission, maxEmission, pulse);
        ApplyBrightnessAndEmission(brightness, emission);
    }

    public void SetPulsing(bool pulsing)
    {
        isPulsing = pulsing;
        if (!isPulsing) ApplyBrightnessAndEmission(1f, 0f);
    }

    private void CacheBaseColor()
    {
        if (!TryGetSharedMaterial(out Material material)) return;
        if (material.HasProperty(BaseColorId))
        {
            baseColor = material.GetColor(BaseColorId);
        }
        else if (material.HasProperty(ColorId))
        {
            baseColor = material.GetColor(ColorId);
        }

        if (material.HasProperty(EmissionColorId))
        {
            baseEmissionColor = material.GetColor(EmissionColorId);
            if (baseEmissionColor.maxColorComponent <= 0.001f)
            {
                baseEmissionColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            }
        }
    }

    private void EnableEmissionKeyword()
    {
        if (targetRenderer == null) return;
        Material mat = targetRenderer.material;
        if (mat == null) return;
        mat.EnableKeyword("_EMISSION");
    }

    private void ApplyBrightnessAndEmission(float brightness, float emissionStrength)
    {
        if (!TryGetSharedMaterial(out Material material)) return;
        bool hasBaseColor = material.HasProperty(BaseColorId);
        bool hasColor = material.HasProperty(ColorId);
        bool hasEmissionColor = material.HasProperty(EmissionColorId);
        if (!hasBaseColor && !hasColor && !hasEmissionColor) return;

        Color color = new Color(
            Mathf.Clamp01(baseColor.r * brightness),
            Mathf.Clamp01(baseColor.g * brightness),
            Mathf.Clamp01(baseColor.b * brightness),
            baseColor.a
        );

        targetRenderer.GetPropertyBlock(propertyBlock);
        if (hasBaseColor) propertyBlock.SetColor(BaseColorId, color);
        if (hasColor) propertyBlock.SetColor(ColorId, color);
        if (hasEmissionColor)
        {
            Color emissionColor = new Color(
                baseEmissionColor.r * emissionStrength,
                baseEmissionColor.g * emissionStrength,
                baseEmissionColor.b * emissionStrength,
                1f
            );
            propertyBlock.SetColor(EmissionColorId, emissionColor);
        }
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private bool TryGetSharedMaterial(out Material material)
    {
        material = null;
        if (targetRenderer == null) return false;

        material = targetRenderer.sharedMaterial;
        return material != null;
    }
}
