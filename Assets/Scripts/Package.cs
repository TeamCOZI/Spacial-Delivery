using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Package : MonoBehaviour
{
    public static float destroyDistance = 10000f;

    public List<Vector3> pathPoints = new List<Vector3>();
    protected const float MinPathPointDistance = 0.5f;
    protected Vector3 lastPathPoint;

    public LaunchData launchData;
    protected Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        if (pathPoints.Count == 0)
        {
            pathPoints.Add(transform.position);
            lastPathPoint = transform.position;
        }
    }

    protected virtual void Update()
    {
        if (transform.position.magnitude > destroyDistance)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void FixedUpdate()
    {
        ApplyGravity();

        if (Vector3.Distance(transform.position, lastPathPoint) > MinPathPointDistance)
        {
            pathPoints.Add(transform.position);
            lastPathPoint = transform.position;
        }
    }

    protected void ApplyGravity()
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
    }
}