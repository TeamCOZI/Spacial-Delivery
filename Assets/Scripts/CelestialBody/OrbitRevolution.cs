using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class OrbitRevolution : MonoBehaviour
{
    private const float OrbitSpinVisualOffsetZ = -90f;

    [Header("Orbit Revolution Settings")]
    public GameObject center;
    public float semiMajorAxis;
    public float semiMinorAxis;
    public float orbitTiltDegrees;
    public float revolutionPeriod;
    public float currentAngle;
    public bool isClockwise;
    public bool rotateAroundZWithOrbit = true;

    [HideInInspector]
    public float timeMultiplier = 1f;

    private Rigidbody rigidbodyComponent;
    private double currentAnglePrecise;
    private bool isInitialized;
    private WorldPosition worldPositionComponent;
    private int simulationStepInterval = 1;
    private int fixedStepCounter;
    private double selfRotationAnglePrecise;
    private Quaternion initialWorldRotation;

    private void Start()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
        rigidbodyComponent.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbodyComponent.collisionDetectionMode = rigidbodyComponent.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;
        worldPositionComponent = GetComponent<WorldPosition>();

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

        currentAnglePrecise = currentAngle;
        // Keep self-rotation phase aligned with orbit angle from the first frame.
        selfRotationAnglePrecise = currentAnglePrecise;
        initialWorldRotation = transform.rotation;
        isInitialized = true;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Register(this);
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Deregister(this);
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized || revolutionPeriod == 0f) return;

        fixedStepCounter++;
        if (fixedStepCounter < simulationStepInterval) return;
        int elapsedSteps = fixedStepCounter;
        fixedStepCounter = 0;

        double elapsedTime = Time.fixedDeltaTime * elapsedSteps;
        double clockwise = isClockwise ? -1d : 1d;
        double deltaDegrees = elapsedTime * timeMultiplier * clockwise * 20d / revolutionPeriod;
        currentAnglePrecise += deltaDegrees;
        currentAnglePrecise %= 360d;
        if (currentAnglePrecise < 0d) currentAnglePrecise += 360d;

        currentAngle = (float)currentAnglePrecise;
        Double3 worldPosition = CalculateWorldPosition(currentAnglePrecise);
        if (worldPositionComponent != null)
        {
            worldPositionComponent.SetWorldPosition(worldPosition);
        }

        Vector3 localPosition = LargeWorldCoordinator.Instance != null
            ? LargeWorldCoordinator.Instance.ToLocal(worldPosition)
            : worldPosition.ToVector3();

        rigidbodyComponent.MovePosition(localPosition);

        if (rotateAroundZWithOrbit)
        {
            selfRotationAnglePrecise += deltaDegrees;
            selfRotationAnglePrecise %= 360d;
            if (selfRotationAnglePrecise < 0d) selfRotationAnglePrecise += 360d;

            // Rotate around local Z while preserving the model's initial "standing" pose.
            Quaternion rotation = initialWorldRotation * Quaternion.Euler(0f, 0f, (float)selfRotationAnglePrecise + OrbitSpinVisualOffsetZ);
            rigidbodyComponent.MoveRotation(rotation);
        }
    }

    public Vector3 UpdatePosition(float currentAngle)
    {
        Double3 worldPosition = CalculateWorldPosition(currentAngle);
        if (LargeWorldCoordinator.Instance != null)
        {
            return LargeWorldCoordinator.Instance.ToLocal(worldPosition);
        }

        return worldPosition.ToVector3();
    }

    private Double3 CalculateWorldPosition(double angleDegrees)
    {
        double semiMajor = semiMajorAxis;
        double semiMinor = semiMinorAxis;
        double focusDistance = System.Math.Sqrt(System.Math.Max(0d, semiMajor * semiMajor - semiMinor * semiMinor));

        double currentAngleRad = angleDegrees * System.Math.PI / 180d;
        double x = semiMajor * System.Math.Cos(currentAngleRad);
        double y = semiMinor * System.Math.Sin(currentAngleRad);
        x -= focusDistance;

        double tiltRad = orbitTiltDegrees * System.Math.PI / 180d;
        double tiltX = x * System.Math.Cos(tiltRad) - y * System.Math.Sin(tiltRad);
        double tiltY = x * System.Math.Sin(tiltRad) + y * System.Math.Cos(tiltRad);

        Double3 orbitOffset = new Double3(tiltX, tiltY, 0d);

        Double3 parentPosition;
        OrbitRevolution parentOrbitRevolution = center.GetComponent<OrbitRevolution>();
        if (parentOrbitRevolution != null)
        {
            parentPosition = parentOrbitRevolution.CalculateWorldPosition(parentOrbitRevolution.currentAnglePrecise);
        }
        else
        {
            WorldPosition centerWorld = center.GetComponent<WorldPosition>();
            if (centerWorld != null)
            {
                parentPosition = centerWorld.worldPosition;
            }
            else
            {
                Vector3 centerPosition = center.transform.position;
                parentPosition = new Double3(centerPosition.x, centerPosition.y, centerPosition.z);
            }
        }

        return parentPosition + orbitOffset;
    }
    public void SetSimulationStepInterval(int interval)
    {
        simulationStepInterval = Mathf.Max(1, interval);
    }
}
