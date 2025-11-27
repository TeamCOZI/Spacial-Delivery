using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Package : MonoBehaviour
{
    [Header("Movement")]
    public float thrustForce = 10f;
    public static float destroyDistance = 10000f;

    public List<Vector3> pathPoints = new List<Vector3>();
    protected const float MinPathPointDistance = 0.5f;
    protected Vector3 lastPathPoint;

    public LaunchData launchData;
    protected Rigidbody rb;

    private int automatedLaunchFrame;
    private int thrustIndex = 0;

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

    public void SetLaunchFrame(int frame)
    {
        automatedLaunchFrame = frame;
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
        ApplyRecordedThrust();

        if (Vector3.Distance(transform.position, lastPathPoint) > MinPathPointDistance)
        {
            pathPoints.Add(transform.position);
            lastPathPoint = transform.position;
        }
    }

    private void ApplyRecordedThrust()
    {
        if (launchData == null || launchData.thrusts == null || thrustIndex >= launchData.thrusts.Count)
        {
            return;
        }

        int currentRelativeFrame = TimeManager.Instance.GlobalFrame - automatedLaunchFrame;

        while (thrustIndex < launchData.thrusts.Count && currentRelativeFrame >= launchData.thrusts[thrustIndex].frame)
        {
            ThrustData currentThrust = launchData.thrusts[thrustIndex];
            Vector3 thrustDirection = new Vector3(currentThrust.direction.x, currentThrust.direction.y, 0).normalized;
            rb.AddForce(thrustDirection * thrustForce);
            thrustIndex++;
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

            Rigidbody sourceRb = source.GetComponent<Rigidbody>();
            if (sourceRb == null)
            {
                Debug.Log(sourceRb);
                continue;
            }

            float forceMagnitude = source.gravity * sourceRb.mass * rb.mass / (distance * distance);

            Vector3 force = direction.normalized * forceMagnitude;

            rb.AddForce(force);
        }
    }
}