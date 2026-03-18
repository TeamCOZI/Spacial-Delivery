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
    private double previousAnglePrecise;
    private double lastDeltaDegrees;
    private bool isInitialized;
    private WorldPosition worldPositionComponent;
    private float previousSemiMajorAxis;
    private float previousSemiMinorAxis;
    private float previousOrbitTiltDegrees;
    private int simulationStepInterval = 1;
    private int fixedStepCounter;
    private double selfRotationAnglePrecise;
    private Quaternion initialWorldRotation;
    private bool isTimeManagerRegistered;
    private RigidbodyInterpolation configuredInterpolation = RigidbodyInterpolation.Interpolate;
    private int interpolationResumeFixedStepsRemaining;

    private void Start()
    {
        if (!isInitialized)
        {
            InitializeRuntimeState(true);
        }
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

        previousAnglePrecise = currentAnglePrecise;
        previousSemiMajorAxis = semiMajorAxis;
        previousSemiMinorAxis = semiMinorAxis;
        previousOrbitTiltDegrees = orbitTiltDegrees;

        double elapsedTime = Time.fixedDeltaTime * elapsedSteps;
        double clockwise = isClockwise ? -1d : 1d;
        double deltaDegrees = elapsedTime * timeMultiplier * clockwise * 20d / revolutionPeriod;
        lastDeltaDegrees = deltaDegrees;
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

    public void ForceInitializeNow()
    {
        InitializeRuntimeState(true);
    }

    public void RefreshCurrentPose()
    {
        if (!isInitialized)
        {
            return;
        }

        SnapToCurrentOrbitPose();
    }

    public void RefreshCurrentWorldPosition()
    {
        if (!isInitialized)
        {
            return;
        }

        currentAngle = (float)currentAnglePrecise;
        if (worldPositionComponent != null)
        {
            worldPositionComponent.SetWorldPosition(CalculateWorldPosition(currentAnglePrecise));
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

    public Vector3 GetOrbitOffset(float angleDegrees)
    {
        return CalculateOrbitOffset(angleDegrees).ToVector3();
    }

    public Vector3 GetInterpolatedOrbitOffset(float angleDegrees)
    {
        float alpha = GetRenderInterpolationAlpha();
        return CalculateInterpolatedOrbitOffset(angleDegrees, alpha).ToVector3();
    }

    public Vector3 GetInterpolatedCenterToOrbitVector()
    {
        float alpha = GetRenderInterpolationAlpha();
        double angleDegrees = previousAnglePrecise + (lastDeltaDegrees * alpha);
        return CalculateInterpolatedOrbitOffset(angleDegrees, alpha).ToVector3();
    }

    public Vector3 GetInterpolatedLocalPosition()
    {
        Vector3 centerLocalPosition = center != null ? center.transform.position : Vector3.zero;
        return centerLocalPosition + GetInterpolatedCenterToOrbitVector();
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

    public Vector3 GetCurrentOrbitalAcceleration()
    {
        if (!isInitialized || Mathf.Approximately(revolutionPeriod, 0f))
        {
            return Vector3.zero;
        }

        double clockwise = isClockwise ? -1d : 1d;
        double degPerSecond = timeMultiplier * clockwise * 20d / revolutionPeriod;
        double thetaRad = currentAnglePrecise * System.Math.PI / 180d;
        double omegaRad = degPerSecond * System.Math.PI / 180d;
        double omegaSq = omegaRad * omegaRad;

        double ddx = -semiMajorAxis * System.Math.Cos(thetaRad) * omegaSq;
        double ddy = -semiMinorAxis * System.Math.Sin(thetaRad) * omegaSq;

        double tiltRad = orbitTiltDegrees * System.Math.PI / 180d;
        double tiltDdx = ddx * System.Math.Cos(tiltRad) - ddy * System.Math.Sin(tiltRad);
        double tiltDdy = ddx * System.Math.Sin(tiltRad) + ddy * System.Math.Cos(tiltRad);

        Vector3 selfAcceleration = new Vector3((float)tiltDdx, (float)tiltDdy, 0f);
        return selfAcceleration + GetCenterAcceleration();
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

    private void InitializeRuntimeState(bool snapImmediately)
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent == null)
        {
            isInitialized = false;
            return;
        }

        configuredInterpolation = RigidbodyInterpolation.Interpolate;
        rigidbodyComponent.interpolation = configuredInterpolation;
        rigidbodyComponent.collisionDetectionMode = rigidbodyComponent.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative
            : CollisionDetectionMode.ContinuousDynamic;
        worldPositionComponent = GetComponent<WorldPosition>();

        if (center == null)
        {
            Debug.LogError(transform.name + "is missing center.");
            isInitialized = false;
            return;
        }

        if (semiMajorAxis < semiMinorAxis)
        {
            float temp = semiMajorAxis;
            semiMajorAxis = semiMinorAxis;
            semiMinorAxis = temp;
        }

        currentAnglePrecise = currentAngle;
        previousAnglePrecise = currentAnglePrecise;
        lastDeltaDegrees = 0d;
        selfRotationAnglePrecise = currentAnglePrecise;
        previousSemiMajorAxis = semiMajorAxis;
        previousSemiMinorAxis = semiMinorAxis;
        previousOrbitTiltDegrees = orbitTiltDegrees;
        initialWorldRotation = transform.rotation;
        fixedStepCounter = 0;
        isInitialized = true;

        if (snapImmediately)
        {
            SnapToCurrentOrbitPose();
        }

        TryRegisterToTimeManager();
    }

    private void SnapToCurrentOrbitPose()
    {
        if (!isInitialized) return;

        Double3 worldPosition = CalculateWorldPosition(currentAnglePrecise);
        if (worldPositionComponent != null)
        {
            worldPositionComponent.SetWorldPosition(worldPosition);
        }

        Vector3 localPosition = UpdatePosition((float)currentAnglePrecise);
        if (rigidbodyComponent != null)
        {
            rigidbodyComponent.position = localPosition;
        }
        transform.position = localPosition;

        if (!rotateAroundZWithOrbit || rigidbodyComponent == null)
        {
            return;
        }

        Quaternion rotation = initialWorldRotation * Quaternion.Euler(0f, 0f, (float)selfRotationAnglePrecise + OrbitSpinVisualOffsetZ);
        rigidbodyComponent.rotation = rotation;
        transform.rotation = rotation;
    }

    private Double3 CalculateWorldPosition(double angleDegrees)
    {
        return ResolveCenterWorldPosition() + CalculateOrbitOffset(angleDegrees);
    }

    private Double3 CalculateOrbitOffset(double angleDegrees)
    {
        return CalculateOrbitOffset(angleDegrees, semiMajorAxis, semiMinorAxis, orbitTiltDegrees);
    }

    private Double3 CalculateInterpolatedOrbitOffset(double angleDegrees, float alpha)
    {
        float interpolatedSemiMajor = Mathf.Lerp(previousSemiMajorAxis, semiMajorAxis, alpha);
        float interpolatedSemiMinor = Mathf.Lerp(previousSemiMinorAxis, semiMinorAxis, alpha);
        float interpolatedOrbitTilt = Mathf.Lerp(previousOrbitTiltDegrees, orbitTiltDegrees, alpha);
        return CalculateOrbitOffset(angleDegrees, interpolatedSemiMajor, interpolatedSemiMinor, interpolatedOrbitTilt);
    }

    private static Double3 CalculateOrbitOffset(double angleDegrees, double semiMajor, double semiMinor, double tiltDegrees)
    {
        double focusDistance = System.Math.Sqrt(System.Math.Max(0d, semiMajor * semiMajor - semiMinor * semiMinor));

        double currentAngleRad = angleDegrees * System.Math.PI / 180d;
        double x = semiMajor * System.Math.Cos(currentAngleRad);
        double y = semiMinor * System.Math.Sin(currentAngleRad);
        x -= focusDistance;

        double tiltRad = tiltDegrees * System.Math.PI / 180d;
        double tiltX = x * System.Math.Cos(tiltRad) - y * System.Math.Sin(tiltRad);
        double tiltY = x * System.Math.Sin(tiltRad) + y * System.Math.Cos(tiltRad);

        return new Double3(tiltX, tiltY, 0d);
    }

    private static float GetRenderInterpolationAlpha()
    {
        if (Time.fixedDeltaTime <= 0f)
        {
            return 1f;
        }

        float alpha = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        return Mathf.Clamp01(alpha);
    }

    private Double3 ResolveCenterWorldPosition()
    {
        OrbitRevolution parentOrbitRevolution = center != null ? center.GetComponent<OrbitRevolution>() : null;
        if (parentOrbitRevolution != null)
        {
            return parentOrbitRevolution.CalculateWorldPosition(parentOrbitRevolution.currentAnglePrecise);
        }

        WorldPosition centerWorld = center != null ? center.GetComponent<WorldPosition>() : null;
        if (centerWorld != null)
        {
            return centerWorld.worldPosition;
        }

        if (center == null)
        {
            return Double3.Zero;
        }

        Vector3 centerPosition = center.transform.position;
        return new Double3(centerPosition.x, centerPosition.y, centerPosition.z);
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

    private Vector3 GetCenterAcceleration()
    {
        if (center == null) return Vector3.zero;

        OrbitRevolution centerOrbit = center.GetComponent<OrbitRevolution>();
        if (centerOrbit != null)
        {
            return centerOrbit.GetCurrentOrbitalAcceleration();
        }

        return Vector3.zero;
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










