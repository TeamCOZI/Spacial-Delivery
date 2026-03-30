using UnityEngine;

[DisallowMultipleComponent]
public class SolarPanelState : MonoBehaviour
{
    private const string UnsupportedStatus = "UNSUPPORTED";
    private const string NoOrbitStatus = "NO ORBIT";
    private const string SunlitStatus = "SUNLIT";
    private const string EclipseStatus = "ECLIPSED";
    private const string PowerFullStatus = "POWER FULL";
    private const float MinimumOrbitPeriodSeconds = 0.01f;
    private const float OrbitDurationScale = 18f;
    private const int OrbitSampleCount = 96;

    [SerializeField] private string statusLabel = UnsupportedStatus;
    [SerializeField] private int currentPower;
    [SerializeField] private int totalGeneratedThisCycle;
    [SerializeField] private float currentLightIntensity;
    [SerializeField] private int currentGenerationAmount;
    [SerializeField] private float meanGenerationAmount;
    [SerializeField] private int maximumGenerationAmount;
    [SerializeField] private int minimumGenerationAmount;
    [SerializeField] private int forecastRemainingGeneration;
    [SerializeField] private float orbitPeriodSeconds;

    private AssemblyPartFocus partFocus;
    private ArtificialSatellite ownerSatellite;
    private OrbitRevolution orbitRevolution;
    private float generationTimer;
    private bool hasLastOrbitAngle;
    private float lastOrbitAngle;

    public string StatusLabel => statusLabel;
    public int CurrentPower => Mathf.Max(0, currentPower);
    public int PowerCapacity => ResolvePowerCapacity();
    public int FreePowerCapacity => Mathf.Max(0, PowerCapacity - CurrentPower);
    public int TotalGeneratedThisCycle => Mathf.Max(0, totalGeneratedThisCycle);
    public float CurrentLightIntensity => Mathf.Max(0f, currentLightIntensity);
    public int CurrentGenerationAmount => Mathf.Max(0, currentGenerationAmount);
    public float MeanGenerationAmount => Mathf.Max(0f, meanGenerationAmount);
    public int MaximumGenerationAmount => Mathf.Max(0, maximumGenerationAmount);
    public int MinimumGenerationAmount => Mathf.Max(0, minimumGenerationAmount);
    public int ForecastRemainingGeneration => Mathf.Max(0, forecastRemainingGeneration);
    public float OrbitPeriodSeconds => Mathf.Max(0f, orbitPeriodSeconds);
    public float LightPowerRatio => SolarPanelUtility.ResolveLightPowerRatio(partFocus != null ? partFocus.SourcePart : null);
    public float BasePowerGeneration => partFocus != null && partFocus.SourcePart != null ? Mathf.Max(0f, partFocus.SourcePart.powerGeneration) : 0f;
    public float GenerationSeconds => SolarPanelUtility.ResolveGenerationSeconds(partFocus != null ? partFocus.SourcePart : null);

    private void Awake()
    {
        BindReferences();
        ClampPower();
        RefreshStatistics();
    }

    private void OnEnable()
    {
        BindReferences();
        ClampPower();
        RefreshStatistics();
    }

    private void OnDisable()
    {
        generationTimer = 0f;
        hasLastOrbitAngle = false;
    }

    private void Update()
    {
        BindReferences();
        ClampPower();
        UpdateCycleTracking();
        RefreshStatistics();
        UpdateGeneration(Mathf.Max(0f, Time.deltaTime));
    }

    public bool TryConsumePower(int amount)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return true;
        }

        if (CurrentPower < sanitizedAmount)
        {
            return false;
        }

        currentPower -= sanitizedAmount;
        ClampPower();
        return true;
    }

    private void BindReferences()
    {
        if (partFocus == null)
        {
            partFocus = GetComponent<AssemblyPartFocus>();
        }

        if (partFocus == null)
        {
            partFocus = GetComponentInParent<AssemblyPartFocus>();
        }

        ownerSatellite = partFocus != null ? partFocus.OwnerSatellite : null;
        orbitRevolution = ownerSatellite != null ? ownerSatellite.GetComponent<OrbitRevolution>() : null;
    }

    private void UpdateGeneration(float deltaTime)
    {
        if (!SolarPanelUtility.IsSolarPanelPart(partFocus))
        {
            statusLabel = UnsupportedStatus;
            generationTimer = 0f;
            return;
        }

        if (orbitRevolution == null)
        {
            statusLabel = NoOrbitStatus;
            generationTimer = 0f;
            return;
        }

        generationTimer += deltaTime;
        float generationSeconds = Mathf.Max(0.01f, GenerationSeconds);
        while (generationTimer >= generationSeconds)
        {
            generationTimer -= generationSeconds;
            int generatedAmount = Mathf.Max(0, Mathf.RoundToInt(EvaluateGenerationAmount(orbitRevolution.currentAngle)));
            if (generatedAmount <= 0)
            {
                continue;
            }

            if (CurrentPower + generatedAmount > PowerCapacity)
            {
                generationTimer = 0f;
                statusLabel = PowerFullStatus;
                return;
            }

            currentPower += generatedAmount;
            totalGeneratedThisCycle += generatedAmount;
            ClampPower();
        }
    }

    private void UpdateCycleTracking()
    {
        if (orbitRevolution == null)
        {
            hasLastOrbitAngle = false;
            return;
        }

        float currentAngle = Mathf.Repeat(orbitRevolution.currentAngle, 360f);
        if (hasLastOrbitAngle)
        {
            bool wrapped = !orbitRevolution.isClockwise
                ? currentAngle + 180f < lastOrbitAngle
                : currentAngle - 180f > lastOrbitAngle;
            if (wrapped)
            {
                totalGeneratedThisCycle = 0;
            }
        }

        lastOrbitAngle = currentAngle;
        hasLastOrbitAngle = true;
    }

    private void RefreshStatistics()
    {
        if (!SolarPanelUtility.IsSolarPanelPart(partFocus))
        {
            currentLightIntensity = 0f;
            currentGenerationAmount = 0;
            meanGenerationAmount = 0f;
            maximumGenerationAmount = 0;
            minimumGenerationAmount = 0;
            forecastRemainingGeneration = 0;
            orbitPeriodSeconds = 0f;
            statusLabel = UnsupportedStatus;
            return;
        }

        currentLightIntensity = EvaluateLightIntensity(orbitRevolution != null ? orbitRevolution.currentAngle : 0f);
        currentGenerationAmount = Mathf.Max(0, Mathf.RoundToInt(EvaluateGenerationAmount(orbitRevolution != null ? orbitRevolution.currentAngle : 0f)));

        if (orbitRevolution == null)
        {
            meanGenerationAmount = currentGenerationAmount;
            maximumGenerationAmount = currentGenerationAmount;
            minimumGenerationAmount = currentGenerationAmount;
            forecastRemainingGeneration = 0;
            orbitPeriodSeconds = 0f;
            statusLabel = NoOrbitStatus;
            return;
        }

        float totalSample = 0f;
        int maxSample = int.MinValue;
        int minSample = int.MaxValue;
        for (int i = 0; i < OrbitSampleCount; i++)
        {
            float sampleAngle = (360f * i) / OrbitSampleCount;
            int sampleValue = Mathf.Max(0, Mathf.RoundToInt(EvaluateGenerationAmount(sampleAngle)));
            totalSample += sampleValue;
            if (sampleValue > maxSample) maxSample = sampleValue;
            if (sampleValue < minSample) minSample = sampleValue;
        }

        meanGenerationAmount = OrbitSampleCount > 0 ? totalSample / OrbitSampleCount : 0f;
        maximumGenerationAmount = maxSample == int.MinValue ? 0 : maxSample;
        minimumGenerationAmount = minSample == int.MaxValue ? 0 : minSample;
        orbitPeriodSeconds = ResolveOrbitPeriodSeconds(orbitRevolution);

        float remainingFraction = ResolveRemainingOrbitFraction(orbitRevolution);
        float cyclePotential = meanGenerationAmount * ResolveGenerationStepCountPerOrbit();
        forecastRemainingGeneration = Mathf.Max(0, Mathf.RoundToInt(cyclePotential * remainingFraction));
        forecastRemainingGeneration = Mathf.Min(forecastRemainingGeneration, FreePowerCapacity);

        if (CurrentPower >= PowerCapacity && currentGenerationAmount > 0)
        {
            statusLabel = PowerFullStatus;
        }
        else
        {
            statusLabel = currentGenerationAmount > 0 ? SunlitStatus : EclipseStatus;
        }
    }

    private float EvaluateGenerationAmount(float orbitAngle)
    {
        return BasePowerGeneration * EvaluateLightIntensity(orbitAngle);
    }

    private float EvaluateLightIntensity(float orbitAngle)
    {
        float baseLight = LightPowerRatio;
        if (baseLight <= 0f)
        {
            return 0f;
        }

        if (orbitRevolution == null)
        {
            return baseLight;
        }

        if (IsEclipsedAtAngle(orbitAngle))
        {
            return 0f;
        }

        return baseLight;
    }

    private bool IsEclipsedAtAngle(float orbitAngle)
    {
        if (orbitRevolution == null || orbitRevolution.center == null)
        {
            return false;
        }

        Transform starTransform = ResolveStarTransform(orbitRevolution.center.transform);
        if (starTransform == null)
        {
            return false;
        }

        Double3 centerWorld = ResolveWorldPosition(orbitRevolution.center.transform);
        Double3 starWorld = ResolveWorldPosition(starTransform);
        Double3 satelliteWorld = centerWorld + (Double3)orbitRevolution.GetOrbitOffset(orbitAngle);

        Vector3 centerToStar = (starWorld - centerWorld).ToVector3();
        Vector3 centerToSatellite = (satelliteWorld - centerWorld).ToVector3();
        if (centerToStar.sqrMagnitude <= 0.000001f || centerToSatellite.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 starDirection = centerToStar.normalized;
        float parallelDistance = Vector3.Dot(centerToSatellite, starDirection);
        if (parallelDistance >= 0f)
        {
            return false;
        }

        Vector3 perpendicular = centerToSatellite - (starDirection * parallelDistance);
        float occluderRadius = ResolveOccluderRadius(orbitRevolution.center.transform);
        return perpendicular.magnitude <= occluderRadius;
    }

    private float ResolveGenerationStepCountPerOrbit()
    {
        float generationSeconds = Mathf.Max(0.01f, GenerationSeconds);
        float periodSeconds = Mathf.Max(0f, orbitPeriodSeconds);
        if (periodSeconds <= 0f)
        {
            return 0f;
        }

        return periodSeconds / generationSeconds;
    }

    private static float ResolveRemainingOrbitFraction(OrbitRevolution orbit)
    {
        if (orbit == null)
        {
            return 0f;
        }

        float angle = Mathf.Repeat(orbit.currentAngle, 360f);
        float remainingDegrees = orbit.isClockwise ? angle : 360f - angle;
        return Mathf.Clamp01(remainingDegrees / 360f);
    }

    private static float ResolveOrbitPeriodSeconds(OrbitRevolution orbit)
    {
        if (orbit == null)
        {
            return 0f;
        }

        return Mathf.Max(MinimumOrbitPeriodSeconds, orbit.revolutionPeriod * OrbitDurationScale);
    }

    private int ResolvePowerCapacity()
    {
        return partFocus != null && partFocus.SourcePart != null
            ? Mathf.Max(0, Mathf.RoundToInt(partFocus.SourcePart.powerCapacity))
            : 0;
    }

    private void ClampPower()
    {
        currentPower = Mathf.Clamp(currentPower, 0, ResolvePowerCapacity());
        totalGeneratedThisCycle = Mathf.Max(0, totalGeneratedThisCycle);
    }

    private static Transform ResolveStarTransform(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.GetComponent<Star>() != null)
            {
                return current;
            }

            OrbitRevolution orbit = current.GetComponent<OrbitRevolution>();
            current = orbit != null && orbit.center != null ? orbit.center.transform : null;
        }

        return null;
    }

    private static Double3 ResolveWorldPosition(Transform target)
    {
        if (target == null)
        {
            return Double3.Zero;
        }

        WorldPosition worldPosition = target.GetComponent<WorldPosition>();
        if (worldPosition != null)
        {
            return worldPosition.worldPosition;
        }

        return target.position;
    }

    private static float ResolveOccluderRadius(Transform target)
    {
        if (target == null)
        {
            return 0f;
        }

        if (target.TryGetComponent(out Planet planet))
        {
            return Mathf.Max(0.05f, planet.scale * 0.5f);
        }

        if (target.TryGetComponent(out Satellite satellite))
        {
            return Mathf.Max(0.05f, satellite.scale * 0.5f);
        }

        Renderer renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            return Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y, 0.05f);
        }

        return Mathf.Max(target.lossyScale.x, target.lossyScale.y) * 0.5f;
    }
}
