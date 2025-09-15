using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Orbiter))]
public class OrbitVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public GameObject dotPrefab;
    public float dotSpacing = 0.5f;
    public float dotScale = 0.1f;

    private Orbiter orbiter;
    private Transform centralBody;
    private float semiMajorAxis;
    private float semiMinorAxis;
    private float orbitTiltDegrees;

    private ObjectPool dotPool;
    private readonly List<GameObject> activeDots = new List<GameObject>();
    private int calculatedNumberOfDots;

    private Vector3 lastCentralBodyPosition;
    private bool isInitialized = false;

    private GameObject orbitContainer;

    private void Start()
    {
        orbiter = GetComponent<Orbiter>();
        if (orbiter.centralBody == null || dotPrefab == null)
        {
            enabled = false;
            return;
        }

        centralBody = orbiter.centralBody.transform;
        semiMajorAxis = orbiter.semiMajorAxis;
        semiMinorAxis = orbiter.semiMinorAxis;
        orbitTiltDegrees = orbiter.orbitTiltDegrees;
        lastCentralBodyPosition = centralBody.position;

        if (semiMajorAxis > 0 && semiMinorAxis > 0 && dotSpacing > 0)
        {
            float a = semiMajorAxis;
            float b = semiMinorAxis;
            float circumference;

            if (a == b)
            {
                circumference = 2 * Mathf.PI * a;
            }
            else
            {
                float h = Mathf.Pow(a - b, 2) / Mathf.Pow(a + b, 2);
                circumference = Mathf.PI * (a + b) * (1 + (3 * h) / (10 + Mathf.Sqrt(4 - 3 * h)));
            }

            calculatedNumberOfDots = Mathf.RoundToInt(circumference / dotSpacing);
        }
        else
        {
            calculatedNumberOfDots = 0;
        }

        if (calculatedNumberOfDots <= 0)
        {
            isInitialized = false;
            return;
        }

        orbitContainer = new GameObject(gameObject.name + "Orbit");
        dotPool = new ObjectPool(dotPrefab, calculatedNumberOfDots, orbitContainer.transform);

        DrawOrbit();
        isInitialized = true;
    }

    void Update()
    {
        if (isInitialized && centralBody.position != lastCentralBodyPosition)
        {
            DrawOrbit();
            lastCentralBodyPosition = centralBody.position;
        }
    }

    private void DrawOrbit()
    {
        foreach (var dot in activeDots)
        {
            dotPool.Return(dot);
        }
        activeDots.Clear();

        if (calculatedNumberOfDots <= 0) return;

        float angleStep = 360f / calculatedNumberOfDots;

        float focusDistance = Mathf.Sqrt(Mathf.Pow(semiMajorAxis, 2) - Mathf.Pow(semiMinorAxis, 2));
        float tiltInRad = orbitTiltDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < calculatedNumberOfDots; i++)
        {
            float angle = i * angleStep;
            float angleInRad = angle * Mathf.Deg2Rad;

            float x = semiMajorAxis * Mathf.Cos(angleInRad);
            float y = semiMinorAxis * Mathf.Sin(angleInRad);

            x -= focusDistance;

            float rotatedX = x * Mathf.Cos(tiltInRad) - y * Mathf.Sin(tiltInRad);
            float rotatedY = x * Mathf.Sin(tiltInRad) + y * Mathf.Cos(tiltInRad);

            Vector3 dotLocalPosition = new Vector3(rotatedX, rotatedY, 0);

            GameObject dot = dotPool.Get();
            dot.transform.position = centralBody.position + dotLocalPosition;
            dot.transform.localScale = Vector3.one * dotScale;
            activeDots.Add(dot);
        }
    }

    private void OnDestroy()
    {
        Destroy(orbitContainer);
    }
}