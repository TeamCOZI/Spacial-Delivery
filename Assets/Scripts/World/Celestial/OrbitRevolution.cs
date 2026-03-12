using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class OrbitRevolution : MonoBehaviour
{
    private const float OrbitSpinVisualOffsetZ = -90f;
    private const int OriginRebaseInterpolationCooldownSteps = 2;

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
    private bool isTimeManagerRegistered;
    private RigidbodyInterpolation configuredInterpolation = RigidbodyInterpolation.Interpolate;
    private int interpolationResumeFixedStepsRemaining;

    private void Start()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
        configuredInterpolation = RigidbodyInterpolation.Interpolate;
        rigidbodyComponent.interpolation = configuredInterpolation;
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

        TryRegisterToTimeManager();
    }

    private void OnDestroy()
    {
        TryDeregisterFromTimeManager();
    }

    private void LateUpdate()
    {
        if (isTimeManagerRegistered && !CoreRuntimeAccess.TryGetTimeManager(out _))
        {
            isTimeManagerRegistered = false;
        }

        TryRegisterToTimeManager();
    }

    private void FixedUpdate()
    {
        UpdateInterpolationResetState();

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
        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        if (coordinator != null)
        {
            return coordinator.ToLocal(worldPosition);
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

    public Vector3 GetCurrentOrbitalVelocity()
    {
        if (!isInitialized || Mathf.Approximately(revolutionPeriod, 0f))
        {
            return Vector3.zero;
        }

        double clockwise = isClockwise ? -1d : 1d;
        double degPerSecond = timeMultiplier * clockwise * 20d / revolutionPeriod;
        double thetaRad = currentAnglePrecise * System.Math.PI / 180d;
        double omegaRad = degPerSecond * System.Math.PI / 180d;

        double dx = -semiMajorAxis * System.Math.Sin(thetaRad) * omegaRad;
        double dy = semiMinorAxis * System.Math.Cos(thetaRad) * omegaRad;

        double tiltRad = orbitTiltDegrees * System.Math.PI / 180d;
        double tiltDx = dx * System.Math.Cos(tiltRad) - dy * System.Math.Sin(tiltRad);
        double tiltDy = dx * System.Math.Sin(tiltRad) + dy * System.Math.Cos(tiltRad);

        Vector3 selfVelocity = new Vector3((float)tiltDx, (float)tiltDy, 0f);
        return selfVelocity + GetCenterVelocity();
    }

    private Vector3 GetCenterVelocity()
    {
        if (center == null) return Vector3.zero;

        OrbitRevolution centerOrbit = center.GetComponent<OrbitRevolution>();
        if (centerOrbit != null)
        {
            return centerOrbit.GetCurrentOrbitalVelocity();
        }

        Rigidbody centerRb = center.GetComponent<Rigidbody>();
        if (centerRb != null)
        {
            return centerRb.linearVelocity;
        }

        return Vector3.zero;
    }

    public void SuspendInterpolationForOriginRebase()
    {
        if (rigidbodyComponent == null)
        {
            rigidbodyComponent = GetComponent<Rigidbody>();
            if (rigidbodyComponent == null) return;
            configuredInterpolation = rigidbodyComponent.interpolation;
        }

        if (configuredInterpolation == RigidbodyInterpolation.None)
        {
            return;
        }

        rigidbodyComponent.interpolation = RigidbodyInterpolation.None;
        interpolationResumeFixedStepsRemaining = OriginRebaseInterpolationCooldownSteps;
    }

    private void UpdateInterpolationResetState()
    {
        if (interpolationResumeFixedStepsRemaining <= 0 || rigidbodyComponent == null)
        {
            return;
        }

        interpolationResumeFixedStepsRemaining--;
        if (interpolationResumeFixedStepsRemaining > 0)
        {
            return;
        }

        rigidbodyComponent.interpolation = configuredInterpolation;
    }
    private void TryRegisterToTimeManager()
    {
        if (isTimeManagerRegistered) return;
        if (!CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager)) return;

        timeManager.Register(this);
        isTimeManagerRegistered = true;
    }

    private void TryDeregisterFromTimeManager()
    {
        if (!isTimeManagerRegistered) return;
        if (CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager))
        {
            timeManager.Deregister(this);
        }
        isTimeManagerRegistered = false;
    }
}

