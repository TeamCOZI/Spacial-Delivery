using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class Launcher : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject packagePrefab;
    public GameObject dotPrefab;

    [Header("Launch Settings")]
    public float launchPowerMultiplier = 10f;
    public float maxDragDistance = 1.5f;

    [Header("Trajectory Dots")]
    public float trajectorySimulationDots = 50;
    public int numberOfDots = 10;
    public float dotScale = 0.1f;
    public float dotAnimationSpeed = 1f;
    public float dotSpacingMultiplier = 0.5f;

    private GameObject currentPackage;
    private Vector3 launchCenter;
    private bool isDragging = false;
    private SphereCollider launchCollider;

    private ObjectPool dotPool;
    private readonly List<GameObject> activeDots = new List<GameObject>();
    private float dotPathOffset = 0f;

    private void Start()
    {
        launchCollider = GetComponent<SphereCollider>();
        if (launchCollider != null)
        {
            launchCollider.radius = maxDragDistance;
        }

        launchCenter = transform.position;

        if (dotPrefab != null)
        {
            dotPool = new ObjectPool(dotPrefab, numberOfDots, transform);
        }

        DrawLaunchAreaCircle(maxDragDistance);
    }

    private void Update()
    {
        if (TimeManager.Instance != null && TimeManager.Instance.IspausedOrRewinding())
        {
            if (isDragging)
            {
                isDragging = false;
                HideAllDots();
                if (currentPackage != null)
                {
                    Destroy(currentPackage);
                    currentPackage = null;
                }
            }
            return;
        }
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == launchCollider)
            {
                isDragging = true;
                currentPackage = Instantiate(packagePrefab, launchCenter, Quaternion.identity);

                Collider packageCollider = currentPackage.GetComponent<Collider>();
                if (packageCollider != null)
                {
                    Physics.IgnoreCollision(packageCollider, launchCollider, true);
                }

                Rigidbody rb = currentPackage.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                Package package = currentPackage.GetComponent<Package>();
                if (package != null)
                {
                    package.enabled = false;
                }
            }
        }

        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector3 currentPosition = GetMouseWorldPosition();
            Vector3 dragVector = currentPosition - launchCenter;
            dragVector.z = 0;

            if (dragVector.magnitude > maxDragDistance)
            {
                dragVector = dragVector.normalized * maxDragDistance;
            }

            currentPackage.transform.position = launchCenter + dragVector;

            Vector3 launchForce = (launchCenter - currentPackage.transform.position) * launchPowerMultiplier;
            UpdateTrajectoryDots(launchForce, currentPackage.GetComponent<Rigidbody>().mass, currentPackage.transform.position, dragVector.magnitude);
        }

        if (isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            HideAllDots();
            dotPathOffset = 0f;

            Rigidbody rb = currentPackage.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 launchForce = (launchCenter - currentPackage.transform.position) * launchPowerMultiplier;
                rb.AddForce(launchForce, ForceMode.Impulse);
            }

            Package package = currentPackage.GetComponent<Package>();
            if (package != null)
            {
                package.enabled = true;
            }

            currentPackage = null;
        }
    }

    private void UpdateTrajectoryDots(Vector3 launchForce, float mass, Vector3 startPos, float dragMagnitude)
    {
        HideAllDots();

        Collider packageCollider = currentPackage.GetComponent<Collider>();

        List<Vector3> pathPoints = new List<Vector3>();
        Vector3 currentPosition = startPos;
        Vector3 previousPosition = startPos;
        Vector3 currentVelocity = launchForce / mass;
        float timeStep = Time.fixedDeltaTime;

        for (int i = 0; i < trajectorySimulationDots; i++)
        {
            pathPoints.Add(currentPosition);
            previousPosition = currentPosition;

            Vector3 gravityForce = Vector3.zero;
            foreach (var source in Gravity.AllSources)
            {
                float dist = Vector3.Distance(currentPosition, source.transform.position);
                if (dist > 0 && dist <= source.gravityRadius)
                {
                    Vector3 dir = (source.transform.position - currentPosition).normalized;
                    gravityForce += dir * (source.gravity * mass) / (dist * dist);
                }
            }

            currentVelocity += (gravityForce / mass) * timeStep;
            currentPosition += currentVelocity * timeStep;

            if (Physics.Linecast(previousPosition, currentPosition, out RaycastHit hit))
            {
                if (hit.collider != launchCollider && hit.collider != packageCollider)
                {
                    pathPoints.Add(hit.point);
                    break;
                }
            }
        }

        float pathLength = GetPathLength(pathPoints);
        if (pathLength < 0.1f) return;

        float dotSpacing = dragMagnitude * dotSpacingMultiplier;
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

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);
        if (xyPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }

    private void DrawLaunchAreaCircle(float radius)
    {
        GameObject circleObj = new GameObject("LaunchAreaCircle");
        circleObj.transform.parent = transform;
        circleObj.transform.position = launchCenter;

        LineRenderer lineRenderer = circleObj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.loop = true;

        lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        int segments = 100;
        lineRenderer.positionCount = segments;

        Vector3[] points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            points[i] = new Vector3(x, y, 0);
        }
        lineRenderer.SetPositions(points);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxDragDistance);
    }
}