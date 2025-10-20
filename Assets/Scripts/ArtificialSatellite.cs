using UnityEngine;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(SphereCollider))]
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

    private SphereCollider sphereCollider;
    private readonly List<LaunchData> successfulLaunchRoutes = new List<LaunchData>();
    private readonly List<LaunchData> pendingLaunchesThisPeriod = new List<LaunchData>();
    private LaunchData pendingLaunchData;

    private CameraController cameraController;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;

        if (Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }
    }

    private void Start()
    {
        UpdateRange();
        TimeManager.OnGlobalPeriodCompleted += OnPeriodCompleted;
    }

    private void OnDestroy()
    {
        TimeManager.OnGlobalPeriodCompleted -= OnPeriodCompleted;
    }

    private void Update()
    {
        if (launcher == null) return;

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
        if (sphereCollider == null)
        {
            sphereCollider = GetComponent<SphereCollider>();
        }
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
        sphereCollider.radius = captureRange;

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

    private void CapturePlayerSpaceship(GameObject packageObject)
    {
        Package package = packageObject.GetComponent<Package>();
        if (package != null && package.launchData != null)
        {
            package.launchData.pathPoints = new List<Vector3>(package.pathPoints);
            pendingLaunchData = package.launchData;

            UIManager.Instance.ShowConfirmationPanel();

            TimeManager.Instance.Pause();
        }

        Vector3 capturePosition = packageObject.transform.position;
        Destroy(packageObject);
        ShowSuccessEffect(capturePosition);
    }
    
    private void CapturePackage(GameObject packageObject)
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
}