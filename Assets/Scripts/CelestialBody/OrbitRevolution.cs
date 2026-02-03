using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class OrbitRevolution : MonoBehaviour
{
    [Header("Orbit Revolution Settings")]
    public GameObject center;
    public float semiMajorAxis;
    public float semiMinorAxis;
    public float orbitTiltDegrees;
    public float revolutionPeriod;
    public float currentAngle;
    public bool isClockwise;

    [HideInInspector]
    public float timeMultiplier = 1f;

    private Rigidbody rigidbodyComponent;

    private void Start()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();

        if (center == null)
        {
            Debug.LogError(transform.name + "is missing center.");
            return;
        }

        if (semiMajorAxis < semiMinorAxis)
        {
            float temp = semiMajorAxis;
            semiMajorAxis = semiMinorAxis;
            semiMinorAxis = temp;
        }
    }

    private void FixedUpdate()
    {
        
        float clockwise = isClockwise ? -1f : 1f;
        currentAngle += Time.fixedDeltaTime * timeMultiplier * clockwise * 20.0f / revolutionPeriod;
        currentAngle %= 360f;

        rigidbodyComponent.MovePosition(UpdatePosition(currentAngle));
    }

    public Vector3 UpdatePosition(float currentAngle)
    {
        float focusDistance = Mathf.Sqrt(Mathf.Pow(semiMajorAxis, 2) - Mathf.Pow(semiMinorAxis, 2));

        float currentAngle2Rad = currentAngle * Mathf.Deg2Rad;
        float x = semiMajorAxis * Mathf.Cos(currentAngle2Rad);
        float y = semiMinorAxis * Mathf.Sin(currentAngle2Rad);

        x -= focusDistance;

        float orbitTiltDegrees2Rad = orbitTiltDegrees * Mathf.Deg2Rad;
        float tiltX = x * Mathf.Cos(orbitTiltDegrees2Rad) - y * Mathf.Sin(orbitTiltDegrees2Rad);
        float tiltY = x * Mathf.Sin(orbitTiltDegrees2Rad) + y * Mathf.Cos(orbitTiltDegrees2Rad);

        Vector3 position = new Vector3(tiltX, tiltY, 0);

        Vector3 parentPosition;
        OrbitRevolution parentOrbitRevolution = center.GetComponent<OrbitRevolution>();
        if (parentOrbitRevolution != null) parentPosition = parentOrbitRevolution.UpdatePosition(parentOrbitRevolution.currentAngle);
        else parentPosition = center.transform.position;

        return parentPosition + position;
    }
}