using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Orbiter))]
public class OrbitVisualizer : MonoBehaviour
{
    public enum VisualizationMode { Dots, Line }

    [Header("General Settings")]
    public VisualizationMode mode = VisualizationMode.Line;

    [Header("Dot Visualization Settings")]
    public GameObject dotPrefab;
    public float dotSpacing = 0.5f;
    public float dotScale = 0.1f;

    [Header("Line Visualization Settings")]
    public Color lineColor = Color.yellow;
    public float staticLineWidth = 0.1f;
    [Range(10, 200)]
    public int lineResolution = 100;

    [Header("Screen-Space Line Width")]
    public bool useScreenSpaceWidth = true;
    public float screenSpaceWidth = 1f;

    private Orbiter orbiter;
    private Transform centralBody;
    private Vector3 lastCentralBodyPosition;
    private bool isInitialized = false;
    private Camera mainCamera;
    private CameraController cameraController;

    private ObjectPool dotPool;
    private readonly List<GameObject> activeDots = new List<GameObject>();
    private GameObject orbitContainer;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        orbiter = GetComponent<Orbiter>();
        mainCamera = Camera.main;

    if (mainCamera != null)
        {
            cameraController = mainCamera.GetComponent<CameraController>();
        }

        if (mode == VisualizationMode.Line)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
        }
    }

    private void Start()
    {
        orbiter = GetComponent<Orbiter>();
        if (orbiter.centralBody == null || dotPrefab == null)
        {
            enabled = false;
            return;
        }

        centralBody = orbiter.centralBody.transform;
        lastCentralBodyPosition = centralBody.position;

        if (mode == VisualizationMode.Dots)
        {
            InitializeDots();
        }

        DrawOrbit();
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        if (centralBody.position != lastCentralBodyPosition)
        {
            DrawOrbit();
            lastCentralBodyPosition = centralBody.position;
        }

        if (mode == VisualizationMode.Line && useScreenSpaceWidth)
        {
            UpdateScreenSpaceWidth();
        }
    }

    private void InitializeDots()
    {
        if (dotPrefab == null) return;

        float a = orbiter.semiMajorAxis;
        float b = orbiter.semiMinorAxis;
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

        int calculatedNumberOfDots = (dotSpacing > 0) ? Mathf.RoundToInt(circumference / dotSpacing) : 0;
        if (calculatedNumberOfDots <= 0) return;

        orbitContainer = new GameObject(gameObject.name + " Orbit Dots");
        dotPool = new ObjectPool(dotPrefab, calculatedNumberOfDots, orbitContainer.transform);
    }

    private void DrawOrbit()
    {
        if (lineRenderer != null) lineRenderer.enabled = (mode == VisualizationMode.Line);
        if (orbitContainer != null) orbitContainer.SetActive(mode == VisualizationMode.Dots);

        switch (mode)
        {
            case VisualizationMode.Dots:
                DrawOrbitWithDots();
                break;
            case VisualizationMode.Line:
                DrawOrbitWithLine();
                break;
        }
    }

    private void DrawOrbitWithDots()
    {
        if (dotPool == null) return;

        foreach (var dot in activeDots)
        {
            dotPool.Return(dot);
        }
        activeDots.Clear();

        float a = orbiter.semiMajorAxis;
        float b = orbiter.semiMinorAxis;
        float circumference;
        if (a == b) circumference = 2 * Mathf.PI * a;
        else
        {
            float h = Mathf.Pow(a - b, 2) / Mathf.Pow(a + b, 2);
            circumference = Mathf.PI * (a + b) * (1 + (3 * h) / (10 + Mathf.Sqrt(4 - 3 * h)));
        }
        int numberOfDots = (dotSpacing > 0) ? Mathf.RoundToInt(circumference / dotSpacing) : 0;
        if (numberOfDots <= 0) return;

        float angleStep = 360f / numberOfDots;

        for (int i = 0; i < numberOfDots; i++)
        {
            float angle = i * angleStep;
            Vector3 dotPosition = orbiter.GetPositionAngle(angle);

            GameObject dot = dotPool.Get();
            dot.transform.position = dotPosition;
            dot.transform.localScale = Vector3.one * dotScale;
            activeDots.Add(dot);
        }
    }

    private void DrawOrbitWithLine()
    {
        if (lineRenderer == null) return;

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = staticLineWidth;
        lineRenderer.endWidth = staticLineWidth;

        lineRenderer.positionCount = lineResolution + 1;

        var points = new Vector3[lineResolution + 1];
        for (int i = 0; i <= lineResolution; i++)
        {
            float angle = (float)i / lineResolution * 360f;
            points[i] = orbiter.GetPositionAngle(angle);
        }
        lineRenderer.SetPositions(points);
    }
    
    private void UpdateScreenSpaceWidth()
    {
        if (lineRenderer == null || mainCamera == null || centralBody == null) return;

        float distance = Vector3.Distance(mainCamera.transform.position, centralBody.transform.position);

        float newWorldWidth = distance * screenSpaceWidth * 0.005f;

        lineRenderer.startWidth = newWorldWidth;
        lineRenderer.endWidth = newWorldWidth;
    }

    private void OnDestroy()
    {
        Destroy(orbitContainer);
    }
}