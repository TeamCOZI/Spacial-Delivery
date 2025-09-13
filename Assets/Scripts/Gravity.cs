using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Gravity : MonoBehaviour
{
    public float gravity = 100f;
    public float gravityRadius = 10f;

    [Header("Visuals")]
    public Color lineColor = Color.yellow;
    public float lineWidth = 0.1f;
    private LineRenderer radiusLine;

    public static List<Gravity> AllSources = new List<Gravity>();

    private void OnEnable()
    {
        if (!AllSources.Contains(this))
        {
            AllSources.Add(this);
        }
    }

    private void Start()
    {
        SetupRadiusVisual();
    }

    private void Update()
    {
        UpdateRadiusVisual();
    }

    private void SetupRadiusVisual()
    {
        GameObject visualObject = new GameObject("RadiusVisual");
        visualObject.transform.parent = this.transform;
        visualObject.transform.localPosition = Vector3.zero;

        radiusLine = visualObject.AddComponent<LineRenderer>();
        radiusLine.useWorldSpace = false;
        radiusLine.loop = true;
        radiusLine.positionCount = 100;
        radiusLine.startWidth = lineWidth;
        radiusLine.endWidth = lineWidth;

        radiusLine.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        radiusLine.startColor = lineColor;
        radiusLine.endColor = lineColor;
    }

    private void UpdateRadiusVisual()
    {
        radiusLine.startWidth = lineWidth;
        radiusLine.endWidth = lineWidth;
        radiusLine.startColor = lineColor;
        radiusLine.endColor = lineColor;

        Vector3[] points = new Vector3[radiusLine.positionCount];
        for (int i = 0; i < radiusLine.positionCount; i++)
        {
            float angle = (float)i / (radiusLine.positionCount - 1) * 2f * Mathf.PI;
            float x = Mathf.Cos(angle) * gravityRadius;
            float y = Mathf.Sin(angle) * gravityRadius;
            points[i] = new Vector3(x, y, 0);
        }
        radiusLine.SetPositions(points);
    }

    private void OnDisable()
    {
        if (AllSources.Contains(this))
        {
            AllSources.Remove(this);
        }
    }

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gravityRadius);
    }
}