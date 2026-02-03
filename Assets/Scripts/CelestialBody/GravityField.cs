using UnityEngine;

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
        gravityField = new GameObject("Gravity Field");
        gravityField.transform.SetParent(transform);

        lineRendererComponent = gravityField.AddComponent<LineRenderer>();

        lineRendererComponent.loop = true;
        lineRendererComponent.positionCount = 360;

        lineRendererComponent.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineRendererComponent.material.color = lineColor;

        lineRendererComponent.startColor = lineColor;
        lineRendererComponent.endColor = lineColor;

        lineRendererComponent.useWorldSpace = false;
    }

    private void UpdateGravityField()
    {
        float width = 2.0f * Mathf.Abs(cameraComponent.transform.position.z) * tanFovHalf * staticLlineWidth;

        lineRendererComponent.startWidth = width;
        lineRendererComponent.endWidth = width;

        gravityField.transform.position = transform.position;

        Vector3[] vector3 = new Vector3[lineRendererComponent.positionCount];
        for (int i = 0; i < lineRendererComponent.positionCount; i++)
        {
            float angle = (float)i / (lineRendererComponent.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * gravityComponent.GravityRadius;
            float y = Mathf.Sin(angle) * gravityComponent.GravityRadius;
            vector3[i] = new Vector3(x, y, 0);
        }
        lineRendererComponent.SetPositions(vector3);
    }
}