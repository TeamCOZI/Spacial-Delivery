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
        if (!CanApplyThrust()) return;
        if (!TryGetThrustDirection(out Vector3 thrustDirection)) return;

        mover.AddAcceleration(thrustDirection * thrustAcceleration, Time.deltaTime);
        fuel.Consume(fuelConsumeRate * Time.deltaTime);
    }

    public void RefillFuel()
    {
        if (fuel != null)
        {
            fuel.Refill();
        }
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

    private bool CanApplyThrust()
    {
        if (mover == null || !mover.IsLaunched) return false;
        if (IsMovementPaused()) return false;
        if (!IsFocused()) return false;
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

}

