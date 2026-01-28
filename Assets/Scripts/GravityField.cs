using UnityEngine;

[RequireComponent(typeof(Gravity))]
public class GravityField : MonoBehaviour
{
    [Header("Field Settings")]
    public Color lineColor = Color.white;
    public float lineWidth = 1f;

    private Gravity gravity;
    private GameObject gravityField;
    private LineRenderer lineRenderer;

    private void Awake()
    {
        gravity = GetComponent<Gravity>();
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

        lineRenderer = gravityField.AddComponent<LineRenderer>();

        lineRenderer.loop = true;
        lineRenderer.positionCount = 360;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        lineRenderer.useWorldSpace = false;

        lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    }

    private void UpdateGravityField()
    {
        gravityField.transform.position = transform.position;

        Vector3[] vector3 = new Vector3[lineRenderer.positionCount];
        for (int i = 0; i < lineRenderer.positionCount; i++)
        {
            float angle = (float)i / (lineRenderer.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * gravity.GravityRadius;
            float y = Mathf.Sin(angle) * gravity.GravityRadius;
            vector3[i] = new Vector3(x, y, 0);
        }
        lineRenderer.SetPositions(vector3);
    }
}