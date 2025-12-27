using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AsteroidbeltGenerator : MonoBehaviour
{
    [Header("Belt Settings")]
    public GameObject centralBody;
    public GameObject asteroidPrefab;
    public int numberOfAsteroids = 200;
    public bool clockwise = false;

    [Header("Orbit Shape")]
    public float beltSemiMajorAxis = 10f;
    public float beltSemiMinorAxis = 10f;
    public float beltTiltDegrees = 0f;
    public float beltWidth = 10f;
    public int colliderResolution = 32;

    [Header("Orbit Speed")]
    public float orbitSpeed = 10f;

    [Header("Visual Settings")]
    public Material lineMaterial;
    public float lineWidth = 0.5f;
    public Color lineColor = Color.yellow;

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        if (centralBody == null || asteroidPrefab == null)
        {
            return;
        }

        SetupVisualLines();
        SetupTriggerRing(colliderResolution);

        for (int i = 0; i < numberOfAsteroids; i++)
        {
            GameObject asteroid = Instantiate(asteroidPrefab, transform);

            Orbiter orbiter = asteroid.GetComponent<Orbiter>();
            if (orbiter == null)
            {
                Destroy(asteroid);
                continue;
            }

            float randonMultiplier = Random.Range(1.0f, 1.3f);

            orbiter.centralBody = centralBody;
            orbiter.semiMajorAxis = beltSemiMajorAxis * randonMultiplier;
            orbiter.semiMinorAxis = beltSemiMinorAxis * randonMultiplier;
            orbiter.orbitTiltDegrees = beltTiltDegrees;
            orbiter.orbitSpeed = orbitSpeed;
            orbiter.clockwise = clockwise;
            orbiter.currentAngle = Random.Range(0f, 360f);
        }
    }

    private void SetupVisualLines()
    {
        GameObject innerLineGO = new GameObject("InnerBeltLine");
        innerLineGO.transform.SetParent(transform, false);
        LineRenderer innerLine = innerLineGO.AddComponent<LineRenderer>();
        ConfigureLineRenderer(innerLine);
        float innerMajorRadius = beltSemiMajorAxis - beltWidth / 2f;
        DrawEllipse(innerLine, innerMajorRadius);

        GameObject outerLineGO = new GameObject("OuterBeltLine");
        outerLineGO.transform.SetParent(transform, false);
        LineRenderer outerLine = outerLineGO.AddComponent<LineRenderer>();
        ConfigureLineRenderer(outerLine);
        float outerMajorRadius = beltSemiMajorAxis + beltWidth / 2f;
        DrawEllipse(outerLine, outerMajorRadius);
    }

    private void ConfigureLineRenderer(LineRenderer line)
    {
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = colliderResolution + 1;

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.material = lineMaterial;
        line.startColor = lineColor;
        line.endColor = lineColor;

        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void DrawEllipse(LineRenderer line, float majorRadius)
    {
        if (majorRadius <= 0) return;

        float minorRadius = beltSemiMinorAxis * (majorRadius / beltSemiMajorAxis);

        Vector3[] points = new Vector3[colliderResolution + 1];
        Quaternion tilt = Quaternion.Euler(0, 0, beltTiltDegrees);

        for (int i = 0; i <= colliderResolution; i++)
        {
            float angle = i * (360f / colliderResolution) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * majorRadius, Mathf.Sin(angle) * minorRadius, 0);
        }
        line.SetPositions(points);
    }

    private void SetupTriggerRing(int segments)
    {
        GameObject ringContainer = new GameObject("TriggerRing");
        ringContainer.transform.SetParent(transform, false);

        float angleStep = 360f / segments;
        float circumference = 2 * Mathf.PI * beltSemiMajorAxis;
        float segmentLength = circumference / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float angleRad = angle * Mathf.Deg2Rad;

            GameObject segmentGO = new GameObject($"TriggerSegment_{i}");
            segmentGO.transform.SetParent(ringContainer.transform, false);

            float x = Mathf.Cos(angleRad) * beltSemiMajorAxis;
            float y = Mathf.Sin(angleRad) * beltSemiMinorAxis;
            segmentGO.transform.localPosition = new Vector3(x, y, 0);

            float tangentAngle = Mathf.Atan2(beltSemiMinorAxis * Mathf.Cos(angleRad), -beltSemiMajorAxis * Mathf.Sin(angleRad)) * Mathf.Rad2Deg;
            segmentGO.transform.localRotation = Quaternion.Euler(0, 0, tangentAngle);

            BoxCollider boxCollider = segmentGO.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(segmentLength, beltWidth, 1f);
        }

        ringContainer.transform.localRotation = Quaternion.Euler(0, 0, beltTiltDegrees);
    }
}