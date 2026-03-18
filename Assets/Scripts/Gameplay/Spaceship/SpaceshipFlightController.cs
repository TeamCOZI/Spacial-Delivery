using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(28900)]
[DisallowMultipleComponent]
[RequireComponent(typeof(GravityAffectedMover))]
[RequireComponent(typeof(SpaceshipFuel))]
public class SpaceshipFlightController : MonoBehaviour
{
    private const float MoveInputEpsilonSqr = 0.000001f;

    [Header("Fuel")]
    [SerializeField, Min(0f)] private float fuelConsumeRate = 10f;

    [Header("Thrust")]
    [SerializeField, Min(0f)] private float thrustAcceleration = 240f;

    private GravityAffectedMover mover;
    private SpaceshipFuel fuel;
    private PlayerInput sharedPlayerInput;
    private InputAction moveAction;
    private Vector3 requestedAutomatedAcceleration;
    private bool hasAutomatedAccelerationRequest;

    public float MaxThrustAcceleration => thrustAcceleration;

    private void Awake()
    {
        mover = GetComponent<GravityAffectedMover>();
        fuel = GetComponent<SpaceshipFuel>();
    }

    private void OnEnable()
    {
        EnsureMoveAction();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 requestedAcceleration = ConsumeAutomatedAccelerationRequest();

        if (CanApplyManualThrust() && TryGetThrustDirection(out Vector3 thrustDirection))
        {
            requestedAcceleration += thrustDirection * thrustAcceleration;
        }

        if (requestedAcceleration.sqrMagnitude <= MoveInputEpsilonSqr) return;
        TryApplyAcceleration(requestedAcceleration, deltaTime);
    }

    public void RefillFuel()
    {
        if (fuel != null)
        {
            fuel.Refill();
        }
    }

    public void RequestAutomatedAcceleration(Vector3 acceleration)
    {
        requestedAutomatedAcceleration = new Vector3(acceleration.x, acceleration.y, 0f);
        hasAutomatedAccelerationRequest = true;
    }

    public bool CanApplyAutomatedThrust()
    {
        return CanApplySharedThrust();
    }

    public bool TryApplyAcceleration(Vector3 requestedAcceleration, float deltaTime)
    {
        if (!CanApplySharedThrust()) return false;
        if (deltaTime <= 0f) return false;

        float maxAcceleration = Mathf.Max(0f, thrustAcceleration);
        if (maxAcceleration <= 0f) return false;

        Vector3 planarAcceleration = new Vector3(requestedAcceleration.x, requestedAcceleration.y, 0f);
        float requestedMagnitude = planarAcceleration.magnitude;
        if (requestedMagnitude * requestedMagnitude <= MoveInputEpsilonSqr) return false;

        float appliedMagnitude = Mathf.Min(requestedMagnitude, maxAcceleration);
        Vector3 appliedAcceleration = planarAcceleration / requestedMagnitude * appliedMagnitude;
        mover.AddAcceleration(appliedAcceleration, deltaTime);

        float throttle = appliedMagnitude / maxAcceleration;
        fuel.Consume(fuelConsumeRate * throttle * deltaTime);
        return true;
    }

    private bool IsFocused()
    {
        Transform focused = GetCurrentFocus();
        return SpaceshipFocusUtility.TryResolveSpaceship(focused, out Spaceship focusedSpaceship)
            && focusedSpaceship == GetComponent<Spaceship>();
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

    private bool CanApplyManualThrust()
    {
        if (!CanApplySharedThrust()) return false;
        if (!IsFocused()) return false;
        return true;
    }

    private bool CanApplySharedThrust()
    {
        if (mover == null || !mover.IsLaunched) return false;
        if (IsMovementPaused()) return false;
        if (fuel == null || !fuel.HasFuel()) return false;
        return true;
    }

    private bool TryGetThrustDirection(out Vector3 thrustDirection)
    {
        thrustDirection = Vector3.zero;

        Vector2 moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude <= MoveInputEpsilonSqr) return false;

        thrustDirection = new Vector3(moveInput.x, moveInput.y, 0f).normalized;
        return true;
    }

    private static Transform GetCurrentFocus()
    {
        return FocusManager.currentFocus;
    }

    private static bool IsMovementPaused()
    {
        return CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager) && timeManager.IsPaused;
    }

    private Vector3 ConsumeAutomatedAccelerationRequest()
    {
        if (!hasAutomatedAccelerationRequest) return Vector3.zero;

        Vector3 requestedAcceleration = requestedAutomatedAcceleration;
        requestedAutomatedAcceleration = Vector3.zero;
        hasAutomatedAccelerationRequest = false;
        return requestedAcceleration;
    }
}