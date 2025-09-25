using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Package : MonoBehaviour
{
    public static float destroyDistance = 10000f;

    public List<Vector3> pathPoints = new List<Vector3>();
    private const float MinPathPointDistance = 0.5f;
    private Vector3 lastPathPoint;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        if (pathPoints.Count == 0)
        {
            pathPoints.Add(transform.position);
            lastPathPoint = transform.position;
        }
    }

    private void Update()
    {
        if (transform.position.magnitude > destroyDistance)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        foreach (var source in Gravity.AllSources)
        {
            Vector3 direction = source.transform.position - rb.position;
            float distance = direction.magnitude;

            if (distance == 0f || distance > source.gravityRadius)
                continue;

            float forceMagnitude = (source.gravity * rb.mass) / (distance * distance);

            Vector3 force = direction.normalized * forceMagnitude;

            rb.AddForce(force);
        }

        if (Vector3.Distance(transform.position, lastPathPoint) > MinPathPointDistance)
        {
            pathPoints.Add(transform.position);
            lastPathPoint = transform.position;
        }
    }
}