using UnityEngine;

public class AsteroidBelt : MonoBehaviour
{
    [Header("Asteroid Belt Settings")]
    public Color lineColor = Color.green;
    public float staticLlineWidth = 0.005f;

    public int asteroidBeltOrbit;
    public int asteroidBeltWidth;

    private Camera cameraComponent;
    private GameObject start;
    private GameObject end;
    private LineRenderer startLineRenderer;
    private LineRenderer endLineRenderer;

    private float tanFovHalf;

    private void Awake()
    {
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
        UpdateGravityField();
    }

    private void Initialize()
    {
        start = new GameObject("Start");
        start.transform.SetParent(transform);

        startLineRenderer = start.AddComponent<LineRenderer>();

        startLineRenderer.loop = true;
        startLineRenderer.positionCount = 360;

        startLineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        startLineRenderer.material.color = lineColor;

        startLineRenderer.startColor = lineColor;
        startLineRenderer.endColor = lineColor;

        startLineRenderer.useWorldSpace = false;

        end = new GameObject("End");
        end.transform.SetParent(transform);

        endLineRenderer = end.AddComponent<LineRenderer>();

        endLineRenderer.loop = true;
        endLineRenderer.positionCount = 360;

        endLineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        endLineRenderer.material.color = lineColor;

        endLineRenderer.startColor = lineColor;
        endLineRenderer.endColor = lineColor;

        endLineRenderer.useWorldSpace = false;
    }

    private void UpdateGravityField()
    {
        float width = 2.0f * Mathf.Abs(cameraComponent.transform.position.z) * tanFovHalf * staticLlineWidth;

        startLineRenderer.startWidth = width;
        startLineRenderer.endWidth = width;
        endLineRenderer.startWidth = width;
        endLineRenderer.endWidth = width;

        start.transform.position = transform.position;
        end.transform.position = transform.position;

        Vector3[] vector3 = new Vector3[startLineRenderer.positionCount];
        for (int i = 0; i < startLineRenderer.positionCount; i++)
        {
            float angle = (float)i / (startLineRenderer.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * (asteroidBeltOrbit - asteroidBeltWidth);
            float y = Mathf.Sin(angle) * (asteroidBeltOrbit - asteroidBeltWidth);
            vector3[i] = new Vector3(x, y, 0);
        }
        startLineRenderer.SetPositions(vector3);

        for (int i = 0; i < startLineRenderer.positionCount; i++)
        {
            float angle = (float)i / (startLineRenderer.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * (asteroidBeltOrbit + asteroidBeltWidth);
            float y = Mathf.Sin(angle) * (asteroidBeltOrbit + asteroidBeltWidth);
            vector3[i] = new Vector3(x, y, 0);
        }
        endLineRenderer.SetPositions(vector3);
    }
}