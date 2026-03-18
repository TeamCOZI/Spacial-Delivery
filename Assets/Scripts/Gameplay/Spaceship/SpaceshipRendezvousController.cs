using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(28850)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Spaceship))]
[RequireComponent(typeof(SpaceshipFlightController))]
[RequireComponent(typeof(GravityAffectedMover))]
[RequireComponent(typeof(WorldPosition))]
public class SpaceshipRendezvousController : MonoBehaviour
{
    private const float OrbitDirectionSpeedEpsilon = 0.05f;
    private const float OrbitDistanceEpsilon = 0.001f;
    private const float OrbitGravityEpsilon = 0.0001f;
    private const float OrbitSpeedEpsilon = 0.01f;
    private const float MinimumRevolutionPeriod = 0.01f;

    private struct OrbitAssistSnapshot
    {
        public Transform target;
        public Vector3 relativePosition;
        public Vector3 relativeVelocity;
        public float currentDistance;
        public float desiredOrbitRadius;
        public float orbitDirectionSign;
        public float desiredOrbitalSpeed;
        public float currentRadialSpeed;
        public float currentTangentialSpeed;
    }

    private struct OrbitCommitState
    {
        public Transform target;
        public float orbitRadius;
        public float orbitDirectionSign;
        public float tangentialSpeed;
        public float currentAngleDegrees;
        public float revolutionPeriod;
    }

    [Header("Formation")]
    [SerializeField, Min(0f)] private float targetOffsetPadding = 2f;
    [SerializeField, Min(0.5f)] private float minimumTargetOffset = 6f;

    [Header("Assist")]
    [SerializeField, Min(0f)] private float positionGain = 0.8f;
    [SerializeField, Min(0f)] private float velocityGain = 1.6f;
    [SerializeField, Min(0f)] private float maxPositionError = 200f;

    [Header("Stability")]
    [SerializeField, Min(0.05f)] private float stableHoldSeconds = 0.75f;
    [SerializeField, Min(0f)] private float stableRadiusTolerance = 0.5f;
    [SerializeField, Min(0f)] private float stableRadialSpeedTolerance = 0.35f;
    [SerializeField, Min(0f)] private float stableTangentialSpeedTolerance = 0.5f;

    [Header("Orbit Control")]
    [SerializeField, Min(0f)] private float orbitRadiusAdjustRate = 12f;
    [SerializeField, Min(0f)] private float orbitSpeedAdjustRate = 8f;
    [SerializeField, Min(0.01f)] private float minimumCommittedOrbitSpeed = 0.1f;

    private Spaceship spaceship;
    private SpaceshipFlightController flightController;
    private GravityAffectedMover mover;
    private WorldPosition worldPosition;
    private Rigidbody rigidbodyComponent;
    private Transform lockedOrbitTarget;
    private float lockedOrbitRadius;
    private float lockedOrbitDirectionSign = 1f;
    private bool hasLockedOrbitRadius;
    private bool hasLockedOrbitDirection;
    private float stableOrbitTimer;
    private OrbitCommitState stableOrbitCandidate;
    private bool hasStableOrbitCandidate;
    private bool orbitCommitted;
    private bool activationToggleEnabled;
    private PlayerInput sharedPlayerInput;
    private InputAction moveAction;
    private float committedOrbitTangentialSpeed;

    public bool IsActive { get; private set; }
    public bool IsOrbitCommitted => orbitCommitted;
    public bool HasStableOrbitCandidate => IsStableOrbitCandidateAvailable();
    public Transform StableOrbitTarget => IsStableOrbitCandidateAvailable() ? stableOrbitCandidate.target : null;

    private void Awake()
    {
        CacheComponents();
    }

    private void Update()
    {
        CacheComponents();
        RefreshStableOrbitCandidateTarget();
        HandleActivationToggleInput();

        if (orbitCommitted)
        {
            IsActive = false;
            return;
        }

        if (!activationToggleEnabled)
        {
            IsActive = false;
            return;
        }

        if (!TryBuildContext(out SpaceshipRendezvousUtility.Context context))
        {
            DisableActivationToggle();
            return;
        }

        Vector3 requestedAcceleration = ComputeRequestedAcceleration(context, out OrbitAssistSnapshot snapshot);
        IsActive = true;

        if (snapshot.target != null)
        {
            UpdateStableOrbitCandidate(context, snapshot);
        }
        else
        {
            ResetStabilityTimer();
        }

        if (requestedAcceleration.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        flightController.RequestAutomatedAcceleration(requestedAcceleration);
    }

    private void FixedUpdate()
    {
        if (!orbitCommitted)
        {
            return;
        }

        UpdateCommittedOrbitControls();
        SyncCommittedOrbitRotation();
    }

    public bool ShouldReserveActivationInput()
    {
        CacheComponents();
        if (orbitCommitted) return false;
        if (activationToggleEnabled || IsActive) return true;
        if (!SpaceshipRendezvousUtility.TryResolveContext(spaceship, out SpaceshipRendezvousUtility.Context context)) return false;
        return context.flightController != null && context.flightController.CanApplyAutomatedThrust();
    }

    public bool TryGetCommittedOrbitCameraRotationZ(out float rotationZ)
    {
        rotationZ = 0f;
        if (!orbitCommitted) return false;

        OrbitRevolution orbitRevolution = GetComponent<OrbitRevolution>();
        Vector3 centerToShip = orbitRevolution != null
            ? orbitRevolution.GetInterpolatedCenterToOrbitVector()
            : Vector3.zero;

        if (centerToShip.sqrMagnitude <= OrbitDistanceEpsilon * OrbitDistanceEpsilon &&
            !TryGetCommittedOrbitCenterToShipVector(out centerToShip))
        {
            return false;
        }

        rotationZ = Mathf.Atan2(centerToShip.y, centerToShip.x) * Mathf.Rad2Deg - 90f;
        return true;
    }

    public bool TryCommitStableOrbit()
    {
        CacheComponents();

        if (orbitCommitted)
        {
            LogCommitFailure("already_committed");
            return false;
        }

        if (mover == null || worldPosition == null)
        {
            LogCommitFailure("missing_dependencies");
            return false;
        }

        if (!TryResolveCommitState(out OrbitCommitState commitState))
        {
            LogCommitFailure("no_valid_commit_state");
            return false;
        }

        OrbitRevolution orbitRevolution = ComponentUtility.GetOrAddComponent<OrbitRevolution>(gameObject);
        if (orbitRevolution == null)
        {
            LogCommitFailure("orbit_revolution_component_missing");
            return false;
        }

        string targetName = commitState.target != null ? commitState.target.name : "<null>";
        Debug.Log(
            $"[OrbitCommit] applying ship={name} target={targetName} radius={commitState.orbitRadius:0.###} " +
            $"tangentialSpeed={commitState.tangentialSpeed:0.###} period={commitState.revolutionPeriod:0.###} " +
            $"angle={commitState.currentAngleDegrees:0.###} clockwise={(commitState.orbitDirectionSign < 0f)}");

        orbitRevolution.center = commitState.target != null ? commitState.target.gameObject : null;
        orbitRevolution.semiMajorAxis = commitState.orbitRadius;
        orbitRevolution.semiMinorAxis = commitState.orbitRadius;
        orbitRevolution.orbitTiltDegrees = 0f;
        orbitRevolution.revolutionPeriod = commitState.revolutionPeriod;
        orbitRevolution.currentAngle = commitState.currentAngleDegrees;
        orbitRevolution.isClockwise = commitState.orbitDirectionSign < 0f;
        orbitRevolution.rotateAroundZWithOrbit = false;
        orbitRevolution.timeMultiplier = 1f;
        committedOrbitTangentialSpeed = Mathf.Max(minimumCommittedOrbitSpeed, commitState.tangentialSpeed);

        OrbitVisualizer orbitVisualizer = ComponentUtility.GetOrAddComponent<OrbitVisualizer>(gameObject);
        if (orbitVisualizer != null)
        {
            orbitVisualizer.enabled = true;
        }
        else
        {
            Debug.LogWarning($"[OrbitCommit] orbit_visualizer_missing ship={name} target={targetName}");
        }

        ComponentUtility.GetOrAddComponent<SpaceshipCommittedOrbitRenderSync>(gameObject);
        mover.EnterOrbitDrivenState();
        orbitRevolution.ForceInitializeNow();
        SyncCommittedOrbitRotation(true);

        orbitCommitted = true;
        activationToggleEnabled = false;
        IsActive = false;
        ResetOrbitLock();
        ResetStabilityTimer();
        ClearStableOrbitCandidate();
        spaceship?.RefreshFocusPresentation();
        Debug.Log($"[OrbitCommit] success ship={name} target={targetName}");
        return true;
    }

    private void LogCommitFailure(string reason)
    {
        string currentTargetName = spaceship != null && spaceship.CurrentTarget != null
            ? spaceship.CurrentTarget.name
            : "<null>";
        string stableTargetName = stableOrbitCandidate.target != null
            ? stableOrbitCandidate.target.name
            : "<null>";

        Debug.LogWarning(
            $"[OrbitCommit] failed ship={name} reason={reason} currentTarget={currentTargetName} " +
            $"stableCandidate={hasStableOrbitCandidate} stableTarget={stableTargetName} " +
            $"toggle={activationToggleEnabled} active={IsActive} committed={orbitCommitted}");
    }

    private void CacheComponents()
    {
        if (spaceship == null) spaceship = GetComponent<Spaceship>();
        if (flightController == null) flightController = GetComponent<SpaceshipFlightController>();
        if (mover == null) mover = GetComponent<GravityAffectedMover>();
        if (worldPosition == null) worldPosition = GetComponent<WorldPosition>();
        if (rigidbodyComponent == null) rigidbodyComponent = GetComponent<Rigidbody>();
    }

    private void UpdateCommittedOrbitControls()
    {
        if (!IsFocusedSpaceship()) return;
        if (IsMovementPaused()) return;

        OrbitRevolution orbitRevolution = GetComponent<OrbitRevolution>();
        if (orbitRevolution == null || orbitRevolution.center == null) return;

        Vector2 moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude <= 0.000001f) return;
        if (!TryResolveCommittedOrbitRadiusBounds(orbitRevolution, out float minimumOrbitRadius, out float maximumOrbitRadius)) return;

        EnsureCommittedOrbitSpeedInitialized(orbitRevolution);

        float deltaTime = Time.fixedDeltaTime;
        if (deltaTime <= 0f) return;

        float currentRadius = Mathf.Max(OrbitDistanceEpsilon, orbitRevolution.semiMajorAxis);
        float nextRadius = Mathf.Clamp(currentRadius + (moveInput.y * orbitRadiusAdjustRate * deltaTime), minimumOrbitRadius, maximumOrbitRadius);
        float speedInput = orbitRevolution.isClockwise ? moveInput.x : -moveInput.x;
        float maximumOrbitSpeed = ComputeMaximumCommittedOrbitSpeed(nextRadius, orbitRevolution.timeMultiplier);
        float nextSpeed = Mathf.Clamp(
            committedOrbitTangentialSpeed + (speedInput * orbitSpeedAdjustRate * deltaTime),
            minimumCommittedOrbitSpeed,
            maximumOrbitSpeed);

        if (Mathf.Abs(nextRadius - currentRadius) <= OrbitDistanceEpsilon &&
            Mathf.Abs(nextSpeed - committedOrbitTangentialSpeed) <= OrbitSpeedEpsilon)
        {
            return;
        }

        ApplyCommittedOrbitState(orbitRevolution, nextRadius, nextSpeed);
    }

    private bool TryResolveCommittedOrbitRadiusBounds(
        OrbitRevolution orbitRevolution,
        out float minimumOrbitRadius,
        out float maximumOrbitRadius)
    {
        minimumOrbitRadius = 0f;
        maximumOrbitRadius = 0f;
        if (orbitRevolution == null || orbitRevolution.center == null) return false;

        Gravity centerGravity = orbitRevolution.center.GetComponent<Gravity>();
        if (centerGravity == null || centerGravity.GravityRadius <= OrbitDistanceEpsilon) return false;

        minimumOrbitRadius = ComputeMinimumOrbitRadius(orbitRevolution.center.transform);
        maximumOrbitRadius = Mathf.Max(OrbitDistanceEpsilon, centerGravity.GravityRadius - 0.01f);
        return minimumOrbitRadius <= maximumOrbitRadius;
    }

    private void EnsureCommittedOrbitSpeedInitialized(OrbitRevolution orbitRevolution)
    {
        if (committedOrbitTangentialSpeed > OrbitSpeedEpsilon) return;

        float orbitRadius = orbitRevolution != null
            ? Mathf.Max(OrbitDistanceEpsilon, orbitRevolution.semiMajorAxis)
            : OrbitDistanceEpsilon;
        committedOrbitTangentialSpeed = Mathf.Max(
            minimumCommittedOrbitSpeed,
            ComputeTangentialSpeedFromRevolution(orbitRevolution, orbitRadius));
    }

    private void ApplyCommittedOrbitState(OrbitRevolution orbitRevolution, float orbitRadius, float tangentialSpeed)
    {
        if (orbitRevolution == null) return;

        float clampedRadius = Mathf.Max(OrbitDistanceEpsilon, orbitRadius);
        float clampedSpeed = Mathf.Max(minimumCommittedOrbitSpeed, tangentialSpeed);
        float timeMultiplierMagnitude = Mathf.Max(Mathf.Abs(orbitRevolution.timeMultiplier), OrbitSpeedEpsilon);
        float degPerSecond = clampedSpeed / clampedRadius * Mathf.Rad2Deg;
        float revolutionPeriod = Mathf.Max(
            MinimumRevolutionPeriod,
            (20f * timeMultiplierMagnitude) / Mathf.Max(OrbitSpeedEpsilon, degPerSecond));

        orbitRevolution.semiMajorAxis = clampedRadius;
        orbitRevolution.semiMinorAxis = clampedRadius;
        orbitRevolution.revolutionPeriod = revolutionPeriod;
        committedOrbitTangentialSpeed = clampedSpeed;
        orbitRevolution.RefreshCurrentWorldPosition();
    }

    private static float ComputeTangentialSpeedFromRevolution(OrbitRevolution orbitRevolution, float orbitRadius)
    {
        if (orbitRevolution == null || orbitRadius <= OrbitDistanceEpsilon) return 0f;

        float timeMultiplierMagnitude = Mathf.Max(Mathf.Abs(orbitRevolution.timeMultiplier), OrbitSpeedEpsilon);
        float period = Mathf.Max(MinimumRevolutionPeriod, orbitRevolution.revolutionPeriod);
        float degPerSecond = (20f * timeMultiplierMagnitude) / period;
        return orbitRadius * degPerSecond * Mathf.Deg2Rad;
    }

    private static float ComputeMaximumCommittedOrbitSpeed(float orbitRadius, float timeMultiplier)
    {
        float timeMultiplierMagnitude = Mathf.Max(Mathf.Abs(timeMultiplier), OrbitSpeedEpsilon);
        float degPerSecond = (20f * timeMultiplierMagnitude) / MinimumRevolutionPeriod;
        return Mathf.Max(OrbitSpeedEpsilon, orbitRadius * degPerSecond * Mathf.Deg2Rad);
    }

    private Vector2 ReadMoveInput()
    {
        InputAction action = EnsureMoveAction();
        if (action != null)
        {
            return action.ReadValue<Vector2>();
        }

        Vector2 move = Vector2.zero;
        if (Keyboard.current == null) return move;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
        return Vector2.ClampMagnitude(move, 1f);
    }

    private InputAction EnsureMoveAction()
    {
        if (moveAction != null) return moveAction;

        if (sharedPlayerInput == null && CoreRuntimeAccess.TryGetUserInput(out UserInput userInput))
        {
            sharedPlayerInput = userInput.GetComponent<PlayerInput>();
        }

        if (sharedPlayerInput == null || sharedPlayerInput.actions == null) return null;
        moveAction = sharedPlayerInput.actions.FindAction("Move", false);
        return moveAction;
    }

    private static bool IsMovementPaused()
    {
        return CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager) && timeManager.IsPaused;
    }

    private void HandleActivationToggleInput()
    {
        if (!WasActivationInputPressedThisFrame()) return;
        if (orbitCommitted || !IsFocusedSpaceship()) return;

        if (activationToggleEnabled)
        {
            DisableActivationToggle();
            return;
        }

        if (!CanEnableActivationToggle()) return;
        EnableActivationToggle();
    }

    private bool CanEnableActivationToggle()
    {
        if (spaceship == null) return false;
        if (!SpaceshipRendezvousUtility.TryResolveContext(spaceship, out SpaceshipRendezvousUtility.Context context)) return false;
        return context.flightController != null && context.flightController.CanApplyAutomatedThrust();
    }

    private void EnableActivationToggle()
    {
        activationToggleEnabled = true;
        IsActive = false;
        ResetOrbitLock();
        ResetStabilityTimer();
    }

    private void DisableActivationToggle()
    {
        activationToggleEnabled = false;
        IsActive = false;
        ResetOrbitLock();
        ResetStabilityTimer();
    }

    private bool TryBuildContext(out SpaceshipRendezvousUtility.Context context)
    {
        context = default;
        if (spaceship == null || flightController == null || mover == null || worldPosition == null) return false;
        if (!SpaceshipRendezvousUtility.TryResolveContext(spaceship, out context)) return false;
        if (!IsFocusedSpaceship()) return false;
        if (!flightController.CanApplyAutomatedThrust()) return false;
        return true;
    }

    private bool IsFocusedSpaceship()
    {
        return SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out Spaceship focusedSpaceship)
            && focusedSpaceship == spaceship;
    }

    private static bool WasActivationInputPressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private Vector3 ComputeRequestedAcceleration(SpaceshipRendezvousUtility.Context context, out OrbitAssistSnapshot snapshot)
    {
        snapshot = default;
        if (context.target == null || context.targetGravity == null) return Vector3.zero;

        Double3 shipWorld = worldPosition.worldPosition;
        Double3 targetWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(context.target);
        Vector3 targetToShip = (shipWorld - targetWorld).ToVector3();
        targetToShip.z = 0f;

        float currentDistance = targetToShip.magnitude;
        if (currentDistance <= OrbitDistanceEpsilon) return Vector3.zero;

        float gravityRadius = Mathf.Max(0f, context.targetGravity.GravityRadius);
        if (gravityRadius <= OrbitDistanceEpsilon || currentDistance > gravityRadius) return Vector3.zero;

        Vector3 radialDirection = targetToShip / currentDistance;
        Vector3 shipVelocity = mover != null ? mover.CurrentVelocity : Vector3.zero;
        Vector3 targetVelocity = SpaceshipRendezvousUtility.ResolvePlanarVelocity(context.target);
        Vector3 targetAcceleration = SpaceshipRendezvousUtility.ResolvePlanarAcceleration(context.target);
        Vector3 currentGravityAcceleration = mover != null ? mover.EvaluateCurrentGravityAcceleration() : Vector3.zero;
        currentGravityAcceleration.z = 0f;

        Vector3 relativeVelocity = shipVelocity - targetVelocity;
        relativeVelocity.z = 0f;

        if (!TryEnsureOrbitLock(context, targetToShip, relativeVelocity, currentDistance, gravityRadius, out float desiredOrbitRadius, out float orbitDirectionSign))
        {
            return Vector3.zero;
        }

        Vector3 tangentialDirection = orbitDirectionSign >= 0f
            ? new Vector3(-radialDirection.y, radialDirection.x, 0f)
            : new Vector3(radialDirection.y, -radialDirection.x, 0f);

        float desiredGravityAcceleration = ComputeTargetGravityAcceleration(gravityRadius, desiredOrbitRadius);
        if (desiredGravityAcceleration <= OrbitGravityEpsilon) return Vector3.zero;

        float desiredOrbitalSpeed = Mathf.Sqrt(desiredGravityAcceleration * desiredOrbitRadius);
        float currentRadialSpeed = Vector3.Dot(relativeVelocity, radialDirection);
        float currentTangentialSpeed = Vector3.Dot(relativeVelocity, tangentialDirection);
        float tangentialSpeedError = desiredOrbitalSpeed - currentTangentialSpeed;
        float radialDistanceError = desiredOrbitRadius - currentDistance;

        Vector3 positionError = radialDirection * radialDistanceError;
        positionError.z = 0f;
        if (maxPositionError > 0f && positionError.sqrMagnitude > maxPositionError * maxPositionError)
        {
            positionError = positionError.normalized * maxPositionError;
        }

        Vector3 radialCorrection = (positionError * positionGain) - (radialDirection * currentRadialSpeed * velocityGain);
        Vector3 tangentialCorrection = tangentialDirection * tangentialSpeedError * velocityGain;
        Vector3 orbitalFeedforward = targetAcceleration - currentGravityAcceleration - (radialDirection * desiredGravityAcceleration);
        Vector3 requestedAcceleration = orbitalFeedforward + radialCorrection + tangentialCorrection;
        requestedAcceleration.z = 0f;

        snapshot.target = context.target;
        snapshot.relativePosition = targetToShip;
        snapshot.relativeVelocity = relativeVelocity;
        snapshot.currentDistance = currentDistance;
        snapshot.desiredOrbitRadius = desiredOrbitRadius;
        snapshot.orbitDirectionSign = orbitDirectionSign;
        snapshot.desiredOrbitalSpeed = desiredOrbitalSpeed;
        snapshot.currentRadialSpeed = currentRadialSpeed;
        snapshot.currentTangentialSpeed = currentTangentialSpeed;
        return requestedAcceleration;
    }

    private bool TryEnsureOrbitLock(
        SpaceshipRendezvousUtility.Context context,
        Vector3 relativePosition,
        Vector3 relativeVelocity,
        float currentDistance,
        float gravityRadius,
        out float desiredOrbitRadius,
        out float orbitDirectionSign)
    {
        desiredOrbitRadius = 0f;
        orbitDirectionSign = 1f;

        if (context.target != lockedOrbitTarget)
        {
            ResetOrbitLock();
            lockedOrbitTarget = context.target;
        }

        float minimumOrbitRadius = ComputeMinimumOrbitRadius(context.target);
        float maximumOrbitRadius = Mathf.Max(OrbitDistanceEpsilon, gravityRadius - 0.01f);
        if (minimumOrbitRadius > maximumOrbitRadius)
        {
            ResetOrbitLock();
            return false;
        }

        if (!hasLockedOrbitRadius)
        {
            lockedOrbitRadius = Mathf.Clamp(currentDistance, minimumOrbitRadius, maximumOrbitRadius);
            hasLockedOrbitRadius = true;
        }
        else
        {
            lockedOrbitRadius = Mathf.Clamp(lockedOrbitRadius, minimumOrbitRadius, maximumOrbitRadius);
        }

        float angularMomentumZ = (relativePosition.x * relativeVelocity.y) - (relativePosition.y * relativeVelocity.x);
        float angularThreshold = OrbitDirectionSpeedEpsilon * Mathf.Max(currentDistance, 1f);
        if (Mathf.Abs(angularMomentumZ) > angularThreshold)
        {
            lockedOrbitDirectionSign = Mathf.Sign(angularMomentumZ);
            hasLockedOrbitDirection = true;
        }
        else if (!hasLockedOrbitDirection)
        {
            lockedOrbitDirectionSign = ResolveDefaultOrbitDirection(context);
            hasLockedOrbitDirection = true;
        }

        desiredOrbitRadius = lockedOrbitRadius;
        orbitDirectionSign = lockedOrbitDirectionSign;
        return true;
    }

    private static float ResolveDefaultOrbitDirection(SpaceshipRendezvousUtility.Context context)
    {
        if (context.targetOrbit != null)
        {
            return context.targetOrbit.isClockwise ? -1f : 1f;
        }

        return 1f;
    }

    private void UpdateStableOrbitCandidate(SpaceshipRendezvousUtility.Context context, OrbitAssistSnapshot snapshot)
    {
        bool isStable = Mathf.Abs(snapshot.currentDistance - snapshot.desiredOrbitRadius) <= stableRadiusTolerance
            && Mathf.Abs(snapshot.currentRadialSpeed) <= stableRadialSpeedTolerance
            && Mathf.Abs(snapshot.desiredOrbitalSpeed - snapshot.currentTangentialSpeed) <= stableTangentialSpeedTolerance;

        if (!isStable)
        {
            ResetStabilityTimer();
            return;
        }

        stableOrbitTimer += Time.deltaTime;
        if (stableOrbitTimer < stableHoldSeconds)
        {
            return;
        }

        float fallbackSpeed = Mathf.Max(Mathf.Abs(snapshot.currentTangentialSpeed), snapshot.desiredOrbitalSpeed, OrbitSpeedEpsilon);
        if (!TryBuildCommitState(context.target, snapshot.orbitDirectionSign, fallbackSpeed, out OrbitCommitState commitState))
        {
            return;
        }

        stableOrbitCandidate = commitState;
        hasStableOrbitCandidate = true;
    }

    private bool TryResolveCommitState(out OrbitCommitState commitState)
    {
        commitState = default;
        if (!IsStableOrbitCandidateAvailable()) return false;

        float fallbackDirectionSign = Mathf.Abs(stableOrbitCandidate.orbitDirectionSign) > 0f
            ? stableOrbitCandidate.orbitDirectionSign
            : 1f;
        float fallbackTangentialSpeed = Mathf.Max(stableOrbitCandidate.tangentialSpeed, OrbitSpeedEpsilon);

        if (TryBuildCommitState(stableOrbitCandidate.target, fallbackDirectionSign, fallbackTangentialSpeed, out commitState))
        {
            return true;
        }

        commitState = stableOrbitCandidate;
        return commitState.target != null;
    }

    private bool TryBuildCommitState(
        Transform target,
        float fallbackDirectionSign,
        float fallbackTangentialSpeed,
        out OrbitCommitState commitState)
    {
        commitState = default;
        CacheComponents();
        if (target == null || mover == null || worldPosition == null) return false;

        Gravity targetGravity = target.GetComponent<Gravity>();
        if (targetGravity == null) return false;

        Double3 shipWorld = worldPosition.worldPosition;
        Double3 targetWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(target);
        Vector3 relativePosition = (shipWorld - targetWorld).ToVector3();
        relativePosition.z = 0f;

        float orbitRadius = relativePosition.magnitude;
        float gravityRadius = Mathf.Max(0f, targetGravity.GravityRadius);
        if (orbitRadius <= OrbitDistanceEpsilon) return false;
        if (gravityRadius <= OrbitDistanceEpsilon || orbitRadius > gravityRadius) return false;

        Vector3 shipVelocity = mover.CurrentVelocity;
        Vector3 targetVelocity = SpaceshipRendezvousUtility.ResolvePlanarVelocity(target);
        Vector3 relativeVelocity = shipVelocity - targetVelocity;
        relativeVelocity.z = 0f;

        float orbitDirectionSign = ResolveCommitDirectionSign(target, relativePosition, relativeVelocity, fallbackDirectionSign);
        Vector3 radialDirection = relativePosition / orbitRadius;
        Vector3 tangentialDirection = orbitDirectionSign >= 0f
            ? new Vector3(-radialDirection.y, radialDirection.x, 0f)
            : new Vector3(radialDirection.y, -radialDirection.x, 0f);

        float tangentialSpeed = Mathf.Abs(Vector3.Dot(relativeVelocity, tangentialDirection));
        if (tangentialSpeed <= OrbitSpeedEpsilon)
        {
            tangentialSpeed = Mathf.Max(fallbackTangentialSpeed, OrbitSpeedEpsilon);
        }

        if (tangentialSpeed <= OrbitSpeedEpsilon)
        {
            float desiredGravityAcceleration = ComputeTargetGravityAcceleration(gravityRadius, orbitRadius);
            if (desiredGravityAcceleration <= OrbitGravityEpsilon) return false;
            tangentialSpeed = Mathf.Sqrt(desiredGravityAcceleration * orbitRadius);
        }

        float currentAngleDegrees = Mathf.Atan2(relativePosition.y, relativePosition.x) * Mathf.Rad2Deg;
        if (currentAngleDegrees < 0f)
        {
            currentAngleDegrees += 360f;
        }

        float degPerSecond = tangentialSpeed / orbitRadius * Mathf.Rad2Deg;
        if (degPerSecond <= OrbitSpeedEpsilon) return false;

        float revolutionPeriod = Mathf.Max(MinimumRevolutionPeriod, 20f / degPerSecond);
        commitState.target = target;
        commitState.orbitRadius = orbitRadius;
        commitState.orbitDirectionSign = orbitDirectionSign;
        commitState.tangentialSpeed = tangentialSpeed;
        commitState.currentAngleDegrees = currentAngleDegrees;
        commitState.revolutionPeriod = revolutionPeriod;
        return true;
    }

    private float ResolveCommitDirectionSign(
        Transform target,
        Vector3 relativePosition,
        Vector3 relativeVelocity,
        float fallbackDirectionSign)
    {
        float angularMomentumZ = (relativePosition.x * relativeVelocity.y) - (relativePosition.y * relativeVelocity.x);
        float angularThreshold = OrbitDirectionSpeedEpsilon * Mathf.Max(relativePosition.magnitude, 1f);
        if (Mathf.Abs(angularMomentumZ) > angularThreshold)
        {
            return Mathf.Sign(angularMomentumZ);
        }

        if (Mathf.Abs(fallbackDirectionSign) > 0f)
        {
            return Mathf.Sign(fallbackDirectionSign);
        }

        OrbitRevolution targetOrbit = target != null ? target.GetComponent<OrbitRevolution>() : null;
        if (targetOrbit != null)
        {
            return targetOrbit.isClockwise ? -1f : 1f;
        }

        return 1f;
    }

    private bool IsStableOrbitCandidateAvailable()
    {
        CacheComponents();
        if (!hasStableOrbitCandidate || orbitCommitted) return false;
        if (stableOrbitCandidate.target == null || spaceship == null || worldPosition == null) return false;
        if (spaceship.CurrentTarget != stableOrbitCandidate.target) return false;

        Gravity targetGravity = stableOrbitCandidate.target.GetComponent<Gravity>();
        if (targetGravity == null || targetGravity.GravityRadius <= 0) return false;

        Double3 targetWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(stableOrbitCandidate.target);
        Vector3 relativePosition = (worldPosition.worldPosition - targetWorld).ToVector3();
        relativePosition.z = 0f;
        return relativePosition.magnitude <= targetGravity.GravityRadius;
    }

    private void RefreshStableOrbitCandidateTarget()
    {
        if (!hasStableOrbitCandidate || orbitCommitted || spaceship == null) return;
        if (spaceship.CurrentTarget == stableOrbitCandidate.target) return;
        ClearStableOrbitCandidate();
    }

    private void ResetOrbitLock()
    {
        lockedOrbitTarget = null;
        lockedOrbitRadius = 0f;
        lockedOrbitDirectionSign = 1f;
        hasLockedOrbitRadius = false;
        hasLockedOrbitDirection = false;
    }

    private void ResetStabilityTimer()
    {
        stableOrbitTimer = 0f;
    }

    private void ClearStableOrbitCandidate()
    {
        stableOrbitCandidate = default;
        hasStableOrbitCandidate = false;
    }

    private void SyncCommittedOrbitRotation(bool snapImmediately = false)
    {
        if (!TryGetCommittedOrbitCenterToShipVector(out Vector3 centerToShip))
        {
            return;
        }

        OrbitRevolution orbitRevolution = GetComponent<OrbitRevolution>();
        if (orbitRevolution == null)
        {
            return;
        }

        Vector3 radialDirection = centerToShip.normalized;
        Vector3 tangentialDirection = orbitRevolution.isClockwise
            ? new Vector3(radialDirection.y, -radialDirection.x, 0f)
            : new Vector3(-radialDirection.y, radialDirection.x, 0f);
        float z = Mathf.Atan2(tangentialDirection.y, tangentialDirection.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, z);

        if (rigidbodyComponent != null)
        {
            if (snapImmediately)
            {
                rigidbodyComponent.rotation = rotation;
                transform.rotation = rotation;
            }
            else
            {
                rigidbodyComponent.MoveRotation(rotation);
            }

            return;
        }

        transform.rotation = rotation;
    }

    private bool TryGetCommittedOrbitCenterToShipVector(out Vector3 centerToShip)
    {
        centerToShip = Vector3.zero;
        CacheComponents();

        OrbitRevolution orbitRevolution = GetComponent<OrbitRevolution>();
        if (orbitRevolution == null || orbitRevolution.center == null || worldPosition == null)
        {
            return false;
        }

        Double3 centerWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(orbitRevolution.center.transform);
        centerToShip = (worldPosition.worldPosition - centerWorld).ToVector3();
        centerToShip.z = 0f;
        return centerToShip.sqrMagnitude > OrbitDistanceEpsilon * OrbitDistanceEpsilon;
    }

    private float ComputeTargetGravityAcceleration(float gravityRadius, float distance)
    {
        if (mover == null) return 0f;
        if (gravityRadius <= OrbitDistanceEpsilon || distance >= gravityRadius) return 0f;

        float falloff = 1f - Mathf.Clamp01(distance / gravityRadius);
        return mover.BaseGravityAcceleration * falloff;
    }

    private float ComputeMinimumOrbitRadius(Transform target)
    {
        float shipRadius = EstimatePlanarRadius(transform);
        float targetRadius = EstimatePlanarRadius(target);
        float offset = shipRadius + targetRadius + targetOffsetPadding;
        return Mathf.Max(minimumTargetOffset, offset);
    }

    private static float EstimatePlanarRadius(Transform root)
    {
        if (root == null) return 0.5f;

        if (TryGetCombinedBounds(root.GetComponentsInChildren<Collider>(true), out Bounds colliderBounds))
        {
            return Mathf.Max(0.5f, new Vector2(colliderBounds.extents.x, colliderBounds.extents.y).magnitude);
        }

        if (TryGetCombinedBounds(root.GetComponentsInChildren<Renderer>(true), out Bounds rendererBounds))
        {
            return Mathf.Max(0.5f, new Vector2(rendererBounds.extents.x, rendererBounds.extents.y).magnitude);
        }

        return 0.5f;
    }

    private static bool TryGetCombinedBounds<T>(T[] components, out Bounds bounds) where T : Component
    {
        bounds = default;
        if (components == null || components.Length == 0) return false;

        bool foundAny = false;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null) continue;

            Bounds candidateBounds;
            if (component is Collider collider)
            {
                if (!collider.enabled) continue;
                candidateBounds = collider.bounds;
            }
            else if (component is Renderer renderer)
            {
                if (!renderer.enabled) continue;
                candidateBounds = renderer.bounds;
            }
            else
            {
                continue;
            }

            if (!foundAny)
            {
                bounds = candidateBounds;
                foundAny = true;
            }
            else
            {
                bounds.Encapsulate(candidateBounds);
            }
        }

        return foundAny;
    }
}




















