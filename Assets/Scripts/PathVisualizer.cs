using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public GameObject dotPrefab;
    public float dotScale = 0.1f;

    private ObjectPool dotPool;
    private readonly List<GameObject> activeDots = new List<GameObject>();

    public void DrawPath(List<Vector3> pathPoints)
    {
        if (dotPrefab == null || pathPoints == null || pathPoints.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        GameObject pathContainer = new GameObject("CapturedPackagepath");
        dotPool = new ObjectPool(dotPrefab, pathPoints.Count, pathContainer.transform);

        foreach (Vector3 point in pathPoints)
        {
            GameObject dot = dotPool.Get();
            dot.transform.position = point;
            dot.transform.localScale = Vector3.one * dotScale;
            activeDots.Add(dot);
        }
    }
}