using UnityEngine;

[DisallowMultipleComponent]
public class SolarTurbineState : MonoBehaviour
{
    private const string UnsupportedStatus = "UNSUPPORTED";
    private const string NoOrbitStatus = "NO ORBIT";
    private const string OnlineStatus = "ONLINE";
    private const float MinimumOrbitPeriodSeconds = 0.01f;
    private const float OrbitDurationScale = 18f;

    [SerializeField] private string statusLabel = UnsupportedStatus;
    [SerializeField] private int totalGeneratedThisCycle;
    [SerializeField] private float currentWindIntensity;
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
    public int TotalGeneratedThisCycle => Mathf.Max(0, totalGeneratedThisCycle);
    public float CurrentWindIntensity => Mathf.Max(0f, currentWindIntensity);
    public int CurrentGenerationAmount => Mathf.Max(0, currentGenerationAmount);
    public float MeanGenerationAmount => Mathf.Max(0f, meanGenerationAmount);
    public int MaximumGenerationAmount => Mathf.Max(0, maximumGenerationAmount);
    public int MinimumGenerationAmount => Mathf.Max(0, minimumGenerationAmount);
    public int ForecastRemainingGeneration => Mathf.Max(0, forecastRemainingGeneration);
    public float OrbitPeriodSeconds => Mathf.Max(0f, orbitPeriodSeconds);
    public float WindPowerRatio => SolarTurbineUtility.ResolveWindPowerRatio(partFocus != null ? partFocus.SourcePart : null);
    public float GenerationSeconds => SolarTurbineUtility.ResolveGenerationSeconds(partFocus != null ? partFocus.SourcePart : null);

    private void Awake()
    {
        BindReferences();
        RefreshStatistics();
    }

    private void OnEnable()
    {
        BindReferences();
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
        UpdateCycleTracking();
        RefreshStatistics();
        UpdateGeneration(Mathf.Max(0f, Time.deltaTime));
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
        if (!SolarTurbineUtility.IsSolarTurbinePart(partFocus))
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
            int generatedAmount = Mathf.Max(0, Mathf.RoundToInt(EvaluateGenerationAmount()));
            if (generatedAmount <= 0)
            {
                continue;
            }

            totalGeneratedThisCycle += generatedAmount;
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
        if (!SolarTurbineUtility.IsSolarTurbinePart(partFocus))
        {
            currentWindIntensity = 0f;
            currentGenerationAmount = 0;
            meanGenerationAmount = 0f;
            maximumGenerationAmount = 0;
            minimumGenerationAmount = 0;
            forecastRemainingGeneration = 0;
            orbitPeriodSeconds = 0f;
            statusLabel = UnsupportedStatus;
            totalGeneratedThisCycle = Mathf.Max(0, totalGeneratedThisCycle);
            return;
        }

        currentWindIntensity = SolarTurbineUtility.DefaultWindIntensity;
        currentGenerationAmount = Mathf.Max(0, Mathf.RoundToInt(EvaluateGenerationAmount()));
        meanGenerationAmount = currentGenerationAmount;
        maximumGenerationAmount = currentGenerationAmount;
        minimumGenerationAmount = currentGenerationAmount;

        if (orbitRevolution == null)
        {
            forecastRemainingGeneration = 0;
            orbitPeriodSeconds = 0f;
            statusLabel = NoOrbitStatus;
            totalGeneratedThisCycle = Mathf.Max(0, totalGeneratedThisCycle);
            return;
        }

        orbitPeriodSeconds = ResolveOrbitPeriodSeconds(orbitRevolution);
        float cyclePotential = meanGenerationAmount * ResolveGenerationStepCountPerOrbit();
        forecastRemainingGeneration = Mathf.Max(0, Mathf.RoundToInt(cyclePotential * ResolveRemainingOrbitFraction(orbitRevolution)));
        statusLabel = OnlineStatus;
        totalGeneratedThisCycle = Mathf.Max(0, totalGeneratedThisCycle);
    }

    private float EvaluateGenerationAmount()
    {
        return WindPowerRatio * SolarTurbineUtility.DefaultWindIntensity;
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
}