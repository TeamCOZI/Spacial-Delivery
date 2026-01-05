using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ArtificialSatellite : MonoBehaviour
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
    private OrbitVisualizer orbitVisualizer;
    private Gravity gravityComponent;

    private CaptureRangeHandler captureHandler;
    private MeshCollider meshCollider;

    public float heat { get; set; }
    public float atm { get; set; }

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

        if (Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }
        orbitVisualizer = GetComponent<OrbitVisualizer>();
        gravityComponent = GetComponent<Gravity>();

        mainRenderer = GetComponent<Renderer>();
        if (mainRenderer == null)
        {
            Debug.LogError("Renderer component not found in artificial satellite.", this);
        }
        propBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.RegisterSatellite(this);
        }
    }

    private void OnDisable()
    {
        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.DeregisterSatellite(this);
        }
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

    public void ShowDetailView()
    {
        if (meshCollider != null) meshCollider.enabled = true;

        mainRenderer.enabled = true;
        foreach(var r in GetComponentsInChildren<Renderer>()) { r.enabled = true; }

        mainRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_IsVisible", 0f);
        mainRenderer.SetPropertyBlock(propBlock);

        targetScale = originalScale;

        if (gravityComponent != null) gravityComponent.SetRadiusVisualVisibility(true);
        if (orbitVisualizer != null) orbitVisualizer.SetVisibility(true);
        if (rangeVisualizer != null) rangeVisualizer.gameObject.SetActive(true);
    }

    public void ShowAsIcon(float frustumHeight)
    {
        if (meshCollider != null) meshCollider.enabled = true;

        mainRenderer.enabled = true;
        foreach(var r in GetComponentsInChildren<Renderer>()) { r.enabled = true; }

        mainRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_IsVisible", 1f);
        mainRenderer.SetPropertyBlock(propBlock);

        float targetWorldSize = frustumHeight * screenHeightFraction * GameSettings.IconSize;
        float finalSize = isHovered
        ? baseFocusSize * targetWorldSize * hoverScaleMultiplier
        : baseFocusSize * targetWorldSize;

        if (transform.parent != null)
        {
            Vector3 parentScale = transform.parent.lossyScale;
            if (parentScale.x != 0 && parentScale.y != 0 && parentScale.z != 0)
            {
                targetScale = new Vector3(
                    finalSize / parentScale.x,
                    finalSize / parentScale.y,
                    finalSize / parentScale.z
                );
            }
        }

        targetScale = Vector3.one * finalSize;

        if (gravityComponent != null) gravityComponent.SetRadiusVisualVisibility(false);
        if (orbitVisualizer != null) orbitVisualizer.SetVisibility(false);
        if (rangeVisualizer != null) rangeVisualizer.gameObject.SetActive(false);
    }

    public void Hide()
    {
        if (meshCollider != null) meshCollider.enabled = false;

        mainRenderer.enabled = false;
        foreach(var r in GetComponentsInChildren<Renderer>()) { r.enabled = false; }

        if (gravityComponent != null) gravityComponent.SetRadiusVisualVisibility(false);
        if (orbitVisualizer != null) orbitVisualizer.SetVisibility(false);
        if (rangeVisualizer != null) rangeVisualizer.gameObject.SetActive(false);
    }

    public void SetHover(bool hover)
    {
        isHovered = hover;
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
                    effectCanvas.worldCamera = Camera.main;
                }
            }

            Destroy(successEffect, effectDuration);
        }
    }

    public void SetVisibility(bool isVisible)
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = isVisible;
        }

        foreach(Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = isVisible;
        }

        if (orbitVisualizer != null)
        {
            orbitVisualizer.SetVisibility(isVisible);
        }

        Gravity gravityComponent = GetComponent<Gravity>();
        if (gravityComponent != null)
        {
            gravityComponent.SetRadiusVisualVisibility(isVisible);
        }
    }
}