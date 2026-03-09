using UnityEngine;

[DefaultExecutionOrder(31000)]
[RequireComponent(typeof(OrbitRevolution))]
public class OrbitVisualizer : MonoBehaviour
{
    [Header("Orbit Visualization Settings")]
    public Color lineColor = new Color(255, 128, 0);
    public float staticLlineWidth = 0.005f;

    private OrbitRevolution orbitRevolutionComponent;
    private Camera cameraComponent;

    private GameObject revolutionOrbit;
    private LineRenderer lineRendererComponent;
    private Vector3[] cachedPoints;
    private Vector3[] cachedWorldPoints;
    private float cachedSemiMajor = -1f;
    private float cachedSemiMinor = -1f;
    
    private float tanFovHalf;

    private void Awake()
    {
        orbitRevolutionComponent = GetComponent<OrbitRevolution>();
        if (Camera.main == null)
        {
            Debug.LogError("Camera is missing.");
            return;
        }
        cameraComponent = Camera.main;
        tanFovHalf = Mathf.Tan(cameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    private void Start()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        UpdateRevolutionOrbit();
    }

    private void Initialize()
    {
        Transform orbitGroup = RuntimeHierarchyOrganizer.GetOrCreateGroup("RevolutionOrbits");
        revolutionOrbit = new GameObject($"{gameObject.name} Revolution Orbit");
        revolutionOrbit.transform.SetParent(orbitGroup, false);

        lineRendererComponent = revolutionOrbit.AddComponent<LineRenderer>();

        lineRendererComponent.loop = true;
        lineRendererComponent.positionCount = 3600;

        lineRendererComponent.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineRendererComponent.material.color = lineColor;

        lineRendererComponent.startColor = lineColor;
        lineRendererComponent.endColor = lineColor;

        lineRendererComponent.useWorldSpace = true;
        cachedPoints = new Vector3[lineRendererComponent.positionCount];
        cachedWorldPoints = new Vector3[lineRendererComponent.positionCount];
        RegenerateOrbitPoints();
    }

    private void OnDestroy()
    {
        if (revolutionOrbit != null)
        {
            Destroy(revolutionOrbit);
        }
    }

    private void UpdateRevolutionOrbit()
    {
        float width = 2.0f * Mathf.Abs(cameraComponent.transform.position.z) * tanFovHalf * staticLlineWidth;

        lineRendererComponent.startWidth = width;
        lineRendererComponent.endWidth = width;
        
        if (orbitRevolutionComponent.center == null)
        {
            Debug.LogError("Orbit center is missing.");
            return;
        }

        if (!Mathf.Approximately(cachedSemiMajor, orbitRevolutionComponent.semiMajorAxis) ||
            !Mathf.Approximately(cachedSemiMinor, orbitRevolutionComponent.semiMinorAxis))
        {
            RegenerateOrbitPoints();
        }

        Vector3 centerPosition = orbitRevolutionComponent.center.transform.position;
        for (int i = 0; i < cachedPoints.Length; i++)
        {
            cachedWorldPoints[i] = centerPosition + cachedPoints[i];
        }

        lineRendererComponent.SetPositions(cachedWorldPoints);
    }

    private void RegenerateOrbitPoints()
    {
        if (cachedPoints == null || lineRendererComponent == null) return;

        int count = lineRendererComponent.positionCount;
        if (cachedPoints.Length != count)
        {
            cachedPoints = new Vector3[count];
            cachedWorldPoints = new Vector3[count];
        }

        cachedSemiMajor = orbitRevolutionComponent.semiMajorAxis;
        cachedSemiMinor = orbitRevolutionComponent.semiMinorAxis;

        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / (count - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * cachedSemiMajor;
            float y = Mathf.Sin(angle) * cachedSemiMinor;
            cachedPoints[i] = new Vector3(x, y, 0);
        }
    }
}
