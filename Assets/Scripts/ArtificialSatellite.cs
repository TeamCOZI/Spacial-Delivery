using UnityEngine;
using System.Collections.Generic;

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

    private SphereCollider sphereCollider;
    private LaunchData successfulLaunchData;

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
                this.successfulLaunchData = package.launchData;
                this.waitingForLaunchWindow = false;
            }
            DrawPackagePath(package.pathPoints);
        }

        Vector3 capturePosition = packageObject.transform.position;
        Destroy(packageObject);
        ShowSuccessEffect(capturePosition);
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