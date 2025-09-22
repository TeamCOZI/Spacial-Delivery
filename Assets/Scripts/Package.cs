using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Package : MonoBehaviour
{
    public static float destroyDistance = 10000f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
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
    }
}