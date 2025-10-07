using UnityEngine;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(SphereCollider))]
public class ArtificialSatellite : MonoBehaviour
{
    [Header("Capture Settings")]
    public float captureRange = 3f;

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
    public GameObject confirmationPanel;
    public TextMeshProUGUI routeInfoText;

    private SphereCollider sphereCollider;
    private LaunchData successfulLaunchData;
    private LaunchData pendingLaunchData;

    private bool waitingForLaunchWindow = false;

    private void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    private void Start()
    {
        UpdateRange();
        TimeManager.OnGlobalPeriodCompleted += OnPeriodCompleted;
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        TimeManager.OnGlobalPeriodCompleted -= OnPeriodCompleted;
    }

    private void Update()
    {
        if (waitingForLaunchWindow && successfulLaunchData != null && launcher != null)
        {
            int currentPeriodFrame = TimeManager.Instance.GlobalFrame - TimeManager.Instance.PeriodStartFrame;

            if (currentPeriodFrame >= successfulLaunchData.RelativeLaunchFrame)
            {
                launcher.AutomatedLaunch(successfulLaunchData);
                waitingForLaunchWindow = false;
                if (routeInfoText != null)
                {
                    routeInfoText.text = "Waiting next period...";
                }
            }
            else
            {
                if (routeInfoText != null)
                {
                    int remainingFrames = successfulLaunchData.RelativeLaunchFrame - currentPeriodFrame;
                    float remainingSeconds = (remainingFrames * Time.fixedDeltaTime) / Time.timeScale;
                    routeInfoText.text = $"Remaining time for next automated launch: {remainingSeconds:F1}s";
                    routeInfoText.gameObject.SetActive(true);
                }
            }
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
        if (successfulLaunchData != null)
        {
            waitingForLaunchWindow = true;
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
        if (other.CompareTag("Package"))
        {
            CapturePackage(other.gameObject);
        }
    }

    private void CapturePackage(GameObject packageObject)
    {
        Package package = packageObject.GetComponent<Package>();
        if (package != null)
        {
            if (package.launchData != null)
            {
                package.launchData.pathPoints = new List<Vector3>(package.pathPoints);
                
                pendingLaunchData = package.launchData;

                if (confirmationPanel != null)
                {
                    confirmationPanel.SetActive(true);
                }

                TimeManager.Instance.Pause();
            }
        }

        Vector3 capturePosition = packageObject.transform.position;
        Destroy(packageObject);
        ShowSuccessEffect(capturePosition);
    }

    public void ConfirmRoute()
    {
        if (pendingLaunchData != null)
        {
            this.successfulLaunchData = pendingLaunchData;

            if (pendingLaunchData.pathPoints != null)
            {
                DrawPackagePath(pendingLaunchData.pathPoints);
            }

            if (routeInfoText != null)
            {
                routeInfoText.text = "Waiting next period...";
                routeInfoText.gameObject.SetActive(true);
            }
        }
        pendingLaunchData = null;

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
        TimeManager.Instance.Play();
    }

    public void DeclineRoute()
    {
        pendingLaunchData = null;

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
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