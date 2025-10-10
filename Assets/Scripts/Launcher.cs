using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEditor;

public class Launcher : MonoBehaviour
{
    private enum LaunchState { Idle, Aiming }
    private LaunchState currentState = LaunchState.Idle;

    public CameraController cameraController;

    [Header("Prefabs")]
    public GameObject packagePrefab;
    public GameObject playerSpaceshipPrefab;
    public GameObject dummyPackagePrefab;
    public GameObject dotPrefab;

    [Header("Launch Settings")]
    public float launchPowerMultiplier = 0.1f;
    public float maxDragDistance = 200f;
    public float slowDown = 0.1f;

    [Header("Trajectory Dots")]
    public float trajectorySimulationDots = 50;
    public int numberOfDots = 10;
    public float dotScale = 0.1f;
    public float dotAnimationSpeed = 1f;
    public float dotSpacingMultiplier = 0.5f;

    [Header("UI")]
    public GameObject aimingCircleUI;

    private GameObject dummyInstance;
    private Vector3 launchCenter;
    private Vector3 aimingOriginScreenPos;

    private ObjectPool dotPool;
    private readonly List<GameObject> activeDots = new List<GameObject>();
    private float dotPathOffset = 0f;
    private RectTransform aimingCircleRectTransform;
    private bool isDragging = false;

    private void Start()
    {
        if (dotPrefab != null)
        {
            dotPool = new ObjectPool(dotPrefab, numberOfDots, transform);
        }

        if (aimingCircleUI != null)
        {
            aimingCircleRectTransform = aimingCircleUI.GetComponent<RectTransform>();
            aimingCircleUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (TimeManager.Instance != null && TimeManager.Instance.IspausedOrRewinding())
        {
            if (currentState == LaunchState.Aiming)
            {
                CancelAiming();
            }
            return;
        }

        if (Mouse.current == null) return;

        if (currentState == LaunchState.Aiming)
        {
            if (cameraController.SelectedPrefab == null || !cameraController.SelectedPrefab.gameObject.activeInHierarchy)
            {
                CancelAiming();
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                isDragging = true;
            }

            Vector3 finalLaunchVelocity = Vector3.zero;

            if (isDragging)
            {
                float prefabRadius = 0f;
                Renderer prefabRenderer = cameraController.SelectedPrefab.GetComponent<Renderer>();
                if (prefabRenderer != null)
                {
                    prefabRadius = prefabRenderer.bounds.extents.y;
                }
                launchCenter = cameraController.SelectedPrefab.position + new Vector3(0, 3f + prefabRadius, 0);

                if (dummyInstance != null)
                {
                    dummyInstance.transform.position = launchCenter;
                }

                Vector3 currentMouseScreenPos = Mouse.current.position.ReadValue();
                Vector3 screenDragVector = currentMouseScreenPos - aimingOriginScreenPos;
                if (screenDragVector.magnitude > maxDragDistance)
                {
                    screenDragVector = screenDragVector.normalized * maxDragDistance;
                }

                Vector3 relativeLaunchVelocity = new Vector3(-screenDragVector.x, -screenDragVector.y, 0) * launchPowerMultiplier;

                Vector3 prefabVelocity = Vector3.zero;
                Rigidbody prefabRb = cameraController.SelectedPrefab.GetComponent<Rigidbody>();
                if (prefabRb != null)
                {
                    prefabVelocity = prefabRb.linearVelocity;
                }

                finalLaunchVelocity = prefabVelocity + relativeLaunchVelocity;

                if (playerSpaceshipPrefab != null)
                {
                    UpdateTrajectoryDots(finalLaunchVelocity, playerSpaceshipPrefab.GetComponent<Rigidbody>().mass, launchCenter);
                }

                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    LaunchPlayer(finalLaunchVelocity);
                }
            }
            else
            {
                HideAllDots();
            }
        }
    }

    public void OnLaunchButtonPressed()
    {
        if (currentState != LaunchState.Idle || cameraController.SelectedPrefab == null) return;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.FastForward(slowDown);
        }

        currentState = LaunchState.Aiming;

        if (aimingCircleUI != null)
        {
            aimingCircleUI.SetActive(true);
            aimingCircleRectTransform.anchoredPosition = new Vector2(0, -150);

            aimingOriginScreenPos = aimingCircleRectTransform.position;
        }

        dummyInstance = Instantiate(dummyPackagePrefab, Vector3.zero, Quaternion.identity);
    }

    public void LaunchPlayer(Vector3 initialVelocity)
    {
        if (TimeManager.Instance != null) TimeManager.Instance.Play();
        CleanupAimingState();

        if (dummyInstance != null)
        {
            Vector3 launchPosition = dummyInstance.transform.position;
            Destroy(dummyInstance);
            dummyInstance = null;

            GameObject launchedObject = Instantiate(playerSpaceshipPrefab, launchPosition, Quaternion.identity);
            Package packageComponent = launchedObject.GetComponent<Package>();

            if (packageComponent != null && TimeManager.Instance != null)
            {
                packageComponent.launchData = new LaunchData
                {
                    LaunchPosition = launchPosition,
                    InitialVelocity = initialVelocity,
                    RelativeLaunchFrame = TimeManager.Instance.GlobalFrame - TimeManager.Instance.PeriodStartFrame
                };
            }

            Rigidbody rb = launchedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = initialVelocity;
            }
        }
    }

    public void AutomatedLaunch(LaunchData data)
    {
        if (data == null) return;

        GameObject launchedObject = Instantiate(packagePrefab, data.LaunchPosition, Quaternion.identity);
        Package packageComponent = launchedObject.GetComponent<Package>();

        if (packageComponent != null)
        {
            packageComponent.launchData = data;
        }

        Rigidbody rb = launchedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = data.InitialVelocity;
        }
    }

    private void CancelAiming()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Play();
        }

        isDragging = false;
        currentState = LaunchState.Idle;
        HideAllDots();
        if (aimingCircleUI != null)
        {
            aimingCircleUI.SetActive(false);
        }
        if (dummyInstance != null)
        {
            Destroy(dummyInstance);
            dummyInstance = null;
        }
    }

    private void CleanupAimingState()
    {
        isDragging = false;
        currentState = LaunchState.Idle;
        HideAllDots();
        if (aimingCircleUI != null) aimingCircleUI.SetActive(false);
    }

    private void UpdateTrajectoryDots(Vector3 initialVelocity, float mass, Vector3 startPos)
    {
        HideAllDots();

        Dictionary<Gravity, float> simulatedAngles = new Dictionary<Gravity, float>();
        foreach (var source in Gravity.AllSources)
        {
            Orbiter o = source.GetComponent<Orbiter>();
            if (o != null)
            {
                simulatedAngles[source] = o.currentAngle;
            }
        }

        List<Vector3> pathPoints = new List<Vector3>();
        Vector3 currentPosition = startPos;
        Vector3 currentVelocity = initialVelocity;
        float timeStep = Time.fixedDeltaTime;

        for (int i = 0; i < trajectorySimulationDots; i++)
        {
            pathPoints.Add(currentPosition);

            Vector3 gravityForce = Vector3.zero;
            foreach (var source in Gravity.AllSources)
            {
                Orbiter o = source.GetComponent<Orbiter>();
                if (o != null && simulatedAngles.ContainsKey(source))
                {
                    Vector3 sourceFuturePosition = o.GetPositionAngle(simulatedAngles[source]);

                    float dist = Vector3.Distance(currentPosition, sourceFuturePosition);
                    if (dist > 0 && dist <= source.gravityRadius)
                    {
                        Vector3 dir = (sourceFuturePosition - currentPosition).normalized;
                        gravityForce += dir * (source.gravity * mass) / (dist * dist);
                    }
                }
            }

            currentVelocity += gravityForce / mass * timeStep;
            currentPosition += currentVelocity * timeStep;

            foreach (var source in Gravity.AllSources)
            {
                Orbiter o = source.GetComponent<Orbiter>();
                if (o != null && simulatedAngles.ContainsKey(source))
                {
                    float direction = o.clockwise ? -1f : 1f;
                    simulatedAngles[source] += o.orbitSpeed * o.timeMultiplier * direction * timeStep;
                }
            }

            if (i > 0 && Physics.Linecast(pathPoints[pathPoints.Count - 1], currentPosition, out RaycastHit hit))
            {
                pathPoints.Add(hit.point);
                break;
            }
        }

        float pathLength = GetPathLength(pathPoints);
        if (pathLength < 0.1f) return;

        float dotSpacing = pathLength / numberOfDots;
        if (dotSpacing <= 0) return;

        dotPathOffset = (dotPathOffset + Time.deltaTime * dotAnimationSpeed) % dotSpacing;

        for (int i = 0; i < numberOfDots; i++)
        {
            float distance = dotPathOffset + i * dotSpacing;
            if (distance > pathLength) continue;

            Vector3 point = GetPointAtDistance(pathPoints, distance);

            GameObject dot = dotPool.Get();
            dot.transform.position = point;
            dot.transform.localScale = Vector3.one * dotScale;
            activeDots.Add(dot);
        }
    }

    private void HideAllDots()
    {
        foreach (var dot in activeDots)
        {
            dotPool.Return(dot);
        }
        activeDots.Clear();
    }

    private float GetPathLength(List<Vector3> points)
    {
        float length = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            length += Vector3.Distance(points[i], points[i + 1]);
        }
        return length;
    }

    private Vector3 GetPointAtDistance(List<Vector3> points, float distance)
    {
        if (points.Count < 2) return points.Count > 0 ? points[0] : Vector3.zero;

        float distanceCovered = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            float segmentLength = Vector3.Distance(points[i], points[i + 1]);
            if (distanceCovered + segmentLength >= distance)
            {
                float fraction = (distance - distanceCovered) / segmentLength;
                return Vector3.Lerp(points[i], points[i + 1], fraction);
            }
            distanceCovered += segmentLength;
        }
        return points[points.Count - 1];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDragDistance);
    }
}