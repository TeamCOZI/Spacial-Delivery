using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class Satellite : MonoBehaviour, CelestialBody
{
    [Header("Capture Settings")]
    public float captureRange = 3f;
    public string playerSpaceshipTag = "PlayerSpaceship";
    public string packageTag = "Package";

    [Header("Dependencies")]
    public Launcher launcher;

    [Header("Visual Settings")]
    public Transform rangeVisualizer;

    [Header("Effect Settings")]
    public GameObject successEffectPrefab;
    public float effectDuration = 1.5f;
    public float effectScale = 1f;
    public GameObject pathVisualizerPrefab;

    [Header("UI Settings")]
    public TextMeshProUGUI routeInfoText;

    [Header("Distance-Based Visibility")]
    public float screenHeightFraction = 0.01f;
    public float baseFocusSize = 0.0025f;

    [Header("Hover Settings")]
    public float hoverScaleMultiplier = 1.2f;
    public float hoverTransitionSpeed = 5f;
    
    private Vector3 originalScale;
    public bool isHovered = false;
    private Vector3 targetScale;
    private Vector3 currentScaleVelocity;

    private Renderer mainRenderer;
    private MaterialPropertyBlock propBlock;

    private readonly List<LaunchData> successfulLaunchRoutes = new List<LaunchData>();
    private readonly List<LaunchData> pendingLaunchesThisPeriod = new List<LaunchData>();
    private LaunchData pendingLaunchData;

    private CameraController cameraController;

    private CaptureRangeHandler captureHandler;
    private MeshCollider meshCollider;

    public int magneticField;
    public int solarWind;
    public int atm;
    public int heat;

    private void Awake()
    {
        foreach (var col in GetComponents<Collider>())
        {
            Destroy(col);
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = true;
        }

        if (rangeVisualizer != null)
        {
            captureHandler = rangeVisualizer.GetComponent<CaptureRangeHandler>();
            if (captureHandler == null)
            {
                captureHandler = rangeVisualizer.gameObject.AddComponent<CaptureRangeHandler>();
            }
        }

        if (UnityEngine.Camera.main != null)
        {
            cameraController = UnityEngine.Camera.main.GetComponent<CameraController>();
        }

        mainRenderer = GetComponent<Renderer>();
        if (mainRenderer == null)
        {
            Debug.LogError("Renderer component not found in artificial satellite.", this);
        }
        propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        UpdateRange();
        TimeManager.OnGlobalPeriodCompleted += OnPeriodCompleted;

        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void OnDestroy()
    {
        TimeManager.OnGlobalPeriodCompleted -= OnPeriodCompleted;
    }

    private void Update()
    {
        transform.localScale = Vector3.SmoothDamp(transform.localScale, targetScale, ref currentScaleVelocity, hoverTransitionSpeed);

        if (launcher == null || !mainRenderer.enabled) return;

        int currentPeriodFrame = TimeManager.Instance.GlobalFrame - TimeManager.Instance.PeriodStartFrame;
        List<LaunchData> launchedRoutes = new List<LaunchData>();

        foreach (var routeData in pendingLaunchesThisPeriod)
        {
            if (currentPeriodFrame >= routeData.RelativeLaunchFrame)
            {
                launcher.AutomatedLaunch(routeData);
                launchedRoutes.Add(routeData);
            }
        }

        foreach (var launchedRoute in launchedRoutes)
        {
            pendingLaunchesThisPeriod.Remove(launchedRoute);
        }

        if (routeInfoText != null)
        {
            routeInfoText.text = $"Current Period Frame: {currentPeriodFrame}\nPeriod Start Frame: {TimeManager.Instance.PeriodStartFrame}";
            routeInfoText.gameObject.SetActive(true);
        }
    }

    private void OnValidate()
    {
        UpdateRange();
    }

    private void OnPeriodCompleted()
    {
        pendingLaunchesThisPeriod.Clear();
        if (successfulLaunchRoutes.Count > 0)
        {
            pendingLaunchesThisPeriod.AddRange(successfulLaunchRoutes);
        }
    }

    private void UpdateRange()
    {
        if (captureHandler != null)
        {
            captureHandler.UpdateRadius();
        }

        if (rangeVisualizer != null)
        {
            rangeVisualizer.localScale = new Vector3(captureRange * 2, captureRange * 2, captureRange * 2);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerSpaceshipTag))
        {
            CapturePlayerSpaceship(other.gameObject);
        }
        if (other.CompareTag(packageTag))
        {
            CapturePackage(other.gameObject);
        }
    }

    public void CapturePlayerSpaceship(GameObject packageObject)
    {
        Package package = packageObject.GetComponent<Package>();
        if (package != null && package.launchData != null)
        {
            package.launchData.pathPoints = new List<Vector3>(package.pathPoints);
            pendingLaunchData = package.launchData;

            UIManager.Instance.ShowConfirmationPanel(this);

            TimeManager.Instance.Pause();
        }

        Vector3 capturePosition = packageObject.transform.position;

        if (cameraController != null && cameraController.SelectedPrefab == packageObject.transform)
        {
            cameraController.UpdateSelection(null);
        }
        
        Destroy(packageObject);
        ShowSuccessEffect(capturePosition);
    }
    
    public void CapturePackage(GameObject packageObject)
    {
        Vector3 capturePosition = packageObject.transform.position;
        Destroy(packageObject);
        ShowSuccessEffect(capturePosition);
    }

    public void ConfirmRoute()
    {
        if (cameraController != null)
        {
            cameraController.UpdateSelection(null);
        }

        if (pendingLaunchData != null)
        {
            successfulLaunchRoutes.Add(pendingLaunchData);

            if (pendingLaunchData.pathPoints != null)
            {
                DrawPackagePath(pendingLaunchData.pathPoints);
            }
        }
        pendingLaunchData = null;

        UIManager.Instance.HideConfirmationPanel();

        TimeManager.Instance.Play();
    }

    public void DeclineRoute()
    {
        if (cameraController != null)
        {
            cameraController.UpdateSelection(null);
        }
        
        pendingLaunchData = null;

        UIManager.Instance.HideConfirmationPanel();

        TimeManager.Instance.Play();
    }

    private void DrawPackagePath(List<Vector3> pathPoints)
    {
        if (pathVisualizerPrefab != null)
        {
            GameObject visualizerObject = Instantiate(pathVisualizerPrefab, Vector3.zero, Quaternion.identity);
            PathVisualizer visualizer = visualizerObject.GetComponent<PathVisualizer>();
            if (visualizer != null)
            {
                visualizer.DrawPath(pathPoints);
            }
        }
    }

    private void ShowSuccessEffect(Vector3 position)
    {
        if (successEffectPrefab != null)
        {
            GameObject successEffect = Instantiate(successEffectPrefab, position, Quaternion.identity);
            successEffect.transform.localScale = new Vector3(effectScale, effectScale, effectScale);

            Canvas effectCanvas = successEffect.GetComponent<Canvas>();
            if (effectCanvas != null)
            {
                if (effectCanvas.worldCamera == null)
                {
                    effectCanvas.worldCamera = UnityEngine.Camera.main;
                }
            }

            Destroy(successEffect, effectDuration);
        }
    }

    public int GetHeat()
    {
        return heat;
    }

    public void SetHeat(int heat)
    {
        this.heat = heat;
    }

    public int GetATM()
    {
        return atm;
    }

    public void SetATM(int ATM)
    {
        this.atm = ATM;
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
        Revolution revolutionComponent = GetComponent<Revolution>();
        Resource resourceComponent = GetComponent<Resource>();

        if (rigidbodyComponent == null || revolutionComponent == null || resourceComponent == null)
        {
            Debug.LogError("Planet is missing required components.");
            return null;
        }

        string satelliteName = name;

        string satelliteCelestialBodyType = "위성";

        string satelliteHeat;
        if (heat >= 300) satelliteHeat = "초고온";
        else if (heat >= 30) satelliteHeat = "고온";
        else if (heat >= -10) satelliteHeat = "평범함";
        else if (heat >= -200) satelliteHeat = "저온";
        else satelliteHeat = "초저온";

        string satelliteMass;
        int mass = Mathf.RoundToInt(rigidbodyComponent.mass);
        if (mass >= 1000000) satelliteMass = "매우 강함";
        else if (mass >= 90000) satelliteMass = "강함";
        else if (mass >= 15000) satelliteMass = "평범함";
        else if (mass >= 3000) satelliteMass = "약함";
        else satelliteMass = "매우 약함";

        string satelliteATM;
        if (atm >= 600) satelliteATM = "매우 높음";
        else if (atm >= 200) satelliteATM = "높음";
        else if (atm >= 100) satelliteATM = "평범함";
        else if (atm >= 50) satelliteATM = "낮음";
        else  satelliteATM = "매우 낮음";

        string satelliteSolarWind;
        if (solarWind >= 8000) satelliteSolarWind = "매우 강함";
        else if (solarWind >= 6000) satelliteSolarWind = "강함";
        else if (solarWind >= 4000) satelliteSolarWind = "평범함";
        else if (solarWind >= 2000) satelliteSolarWind = "약함";
        else satelliteSolarWind = "매우 약함";

        string satelliteCenter = revolutionComponent.center.name;

        string satelliteChildren = "N/A";

        string satellitePeriod = Mathf.RoundToInt(360f / revolutionComponent.revolutionSpeed).ToString();
        
        string satelliteResource = "N/A";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", satelliteName },
            { "분류", satelliteCelestialBodyType },
            { "온도", satelliteHeat },
            { "중력", satelliteMass },
            { "기압", satelliteATM },
            { "태양풍", satelliteSolarWind },
            { "질량 중심 천체", satelliteCenter },
            { "포획 천체", satelliteChildren },
            { "공전 주기", satellitePeriod },
            { "매장 자원", satelliteResource }
        };

        return focusInfo;
    }
}