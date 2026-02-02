using UnityEngine;

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

    void FixedUpdate()
    {
        UpdateRevolutionOrbit();
    }

    private void Initialize()
    {
        revolutionOrbit = new GameObject("Revolution Orbit");
        revolutionOrbit.transform.SetParent(transform);

        lineRendererComponent = revolutionOrbit.AddComponent<LineRenderer>();

        lineRendererComponent.loop = true;
        lineRendererComponent.positionCount = 360;

        lineRendererComponent.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineRendererComponent.material.color = lineColor;

        lineRendererComponent.startColor = lineColor;
        lineRendererComponent.endColor = lineColor;

        lineRendererComponent.useWorldSpace = false;
    }

    private void UpdateRevolutionOrbit()
    {
        float width = 2.0f * Mathf.Abs(cameraComponent.transform.position.z) * tanFovHalf * staticLlineWidth;

        lineRendererComponent.startWidth = width;
        lineRendererComponent.endWidth = width;
        
        if (transform.parent.position == null)
        {
            Debug.LogError("Parent is missing.");
            return;
        }
        revolutionOrbit.transform.position = transform.parent.position;

        Vector3[] vector3 = new Vector3[lineRendererComponent.positionCount];
        for (int i = 0; i < lineRendererComponent.positionCount; i++)
        {
            float angle = (float)i / (lineRendererComponent.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * orbitRevolutionComponent.semiMajorAxis;
            float y = Mathf.Sin(angle) * orbitRevolutionComponent.semiMinorAxis;
            vector3[i] = new Vector3(x, y, 0);
        }
        lineRendererComponent.SetPositions(vector3);
    }
}