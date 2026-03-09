using UnityEngine;

[DefaultExecutionOrder(31000)]
[RequireComponent(typeof(Gravity))]
public class GravityField : MonoBehaviour
{
    [Header("Field Settings")]
    public Color lineColor = Color.white;
    public float staticLlineWidth = 0.005f;

    private Gravity gravityComponent;
    private Camera cameraComponent;

    private GameObject gravityField;
    private LineRenderer lineRendererComponent;
    private Vector3[] cachedPoints;
    private Vector3[] cachedWorldPoints;
    private float cachedRadius = -1f;

    private float tanFovHalf;

    private void Awake()
    {
        gravityComponent = GetComponent<Gravity>();
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
        Transform gravityGroup = RuntimeHierarchyOrganizer.GetOrCreateGroup("GravityFields");
        gravityField = new GameObject($"{gameObject.name} Gravity Field");
        gravityField.transform.SetParent(gravityGroup, false);

        lineRendererComponent = gravityField.AddComponent<LineRenderer>();

        lineRendererComponent.loop = true;
        lineRendererComponent.positionCount = 360;

        lineRendererComponent.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineRendererComponent.material.color = lineColor;

        lineRendererComponent.startColor = lineColor;
        lineRendererComponent.endColor = lineColor;

        lineRendererComponent.useWorldSpace = true;
        cachedPoints = new Vector3[lineRendererComponent.positionCount];
        cachedWorldPoints = new Vector3[lineRendererComponent.positionCount];
        RegenerateFieldPoints();
    }

    private void OnDestroy()
    {
        if (gravityField != null)
        {
            Destroy(gravityField);
        }
    }

    private void UpdateGravityField()
    {
        float width = 2.0f * Mathf.Abs(cameraComponent.transform.position.z) * tanFovHalf * staticLlineWidth;

        lineRendererComponent.startWidth = width;
        lineRendererComponent.endWidth = width;

        if (!Mathf.Approximately(cachedRadius, gravityComponent.GravityRadius))
        {
            RegenerateFieldPoints();
        }

        Vector3 centerPosition = transform.position;
        for (int i = 0; i < cachedPoints.Length; i++)
        {
            cachedWorldPoints[i] = centerPosition + cachedPoints[i];
        }

        lineRendererComponent.SetPositions(cachedWorldPoints);
    }

    private void RegenerateFieldPoints()
    {
        if (cachedPoints == null || lineRendererComponent == null) return;

        int count = lineRendererComponent.positionCount;
        if (cachedPoints.Length != count)
        {
            cachedPoints = new Vector3[count];
            cachedWorldPoints = new Vector3[count];
        }

        cachedRadius = gravityComponent.GravityRadius;

        for (int i = 0; i < count; i++)
        {
            float angle = (float)i / (count - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * cachedRadius;
            float y = Mathf.Sin(angle) * cachedRadius;
            cachedPoints[i] = new Vector3(x, y, 0);
        }
    }
}
