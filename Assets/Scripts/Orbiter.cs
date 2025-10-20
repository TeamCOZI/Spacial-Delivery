using UnityEngine;

public class Orbiter : MonoBehaviour
{
    [Header("Orbit Target")]
    public GameObject centralBody;

    [Header("Orbit Shape")]
    public float semiMajorAxis = 10f;
    public float semiMinorAxis = 5f;
    public float orbitTiltDegrees = 0f;

    [Header("Orbit Speed")]
    public float orbitSpeed = 30f;
    public bool clockwise = false;

    [Header("Initial Setup")]
    public float currentAngle = 0f;

    [HideInInspector]
    public float timeMultiplier = 1f;

    private float baseOrbitSpeed;
    private Rigidbody rb;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        baseOrbitSpeed = orbitSpeed;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Register(this);
        }

        if (centralBody == null)
        {
            enabled = false;
            return;
        }

        if (semiMajorAxis < semiMinorAxis)
        {
            float temp = semiMajorAxis;
            semiMajorAxis = semiMinorAxis;
            semiMinorAxis = temp;
        }

        UpdatePosition();
    }

    private void FixedUpdate()
    {
        if (centralBody != null)
        {
            float direction = clockwise ? -1f : 1f;
            currentAngle += baseOrbitSpeed * timeMultiplier * direction * Time.fixedDeltaTime;
            currentAngle %= 360f;

            UpdatePosition();
        }
    }

    private void OnEnable()
    {
        if (PeriodVisualizer.Instance != null)
        {
            PeriodVisualizer.Instance.RegisterOrbiter(this);
        }
    }

    private void OnDisable()
    {
        if (PeriodVisualizer.Instance != null)
        {
            PeriodVisualizer.Instance.DeregisterOrbiter(this);
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Deregister(this);
        }
    }

    private void UpdatePosition()
    {
        rb.MovePosition(GetPositionAngle(currentAngle));
    }

    public Vector3 GetPositionAngle(float angle)
    {
        float focusDistance = Mathf.Sqrt(Mathf.Pow(semiMajorAxis, 2) - Mathf.Pow(semiMinorAxis, 2));

        float angleInRad = angle * Mathf.Deg2Rad;

        float x = semiMajorAxis * Mathf.Cos(angleInRad);
        float y = semiMinorAxis * Mathf.Sin(angleInRad);

        x -= focusDistance;

        float tiltInRad = orbitTiltDegrees * Mathf.Deg2Rad;
        float rotatedX = x * Mathf.Cos(tiltInRad) - y * Mathf.Sin(tiltInRad);
        float rotatedY = x * Mathf.Sin(tiltInRad) + y * Mathf.Cos(tiltInRad);

        Vector3 orbitPosition = new Vector3(rotatedX, rotatedY, 0);

        return centralBody.transform.position + orbitPosition;
    }
}