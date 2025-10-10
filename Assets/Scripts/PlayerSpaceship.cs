using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSpaceship : Package
{
    [Header("Movement")]
    public float thrustForce = 10f;
    public float maxFuel = 100f;
    public float fuelConsumeRate = 10f;
    public float currentFuel;

    private PlayerInput playerInput;
    private Vector2 moveInput;

    private CameraController mainCameraController;

    public static event System.Action<float, float> OnFuelUpdated;

    protected override void Awake()
    {
        base.Awake();
        playerInput = GetComponent<PlayerInput>();
        currentFuel = maxFuel;
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCameraController = Camera.main.GetComponent<CameraController>();
        }
    }

    private void OnEnable()
    {
        if (playerInput == null) return;
        playerInput.actions["Move"].performed += HandleMove;
        playerInput.actions["Move"].canceled += HandleMove;
        playerInput.actions["Interact"].performed += ToggleControl;
    }

    private void OnDisable()
    {
        if (playerInput == null) return;
        playerInput.actions["Move"].performed -= HandleMove;
        playerInput.actions["Move"].canceled -= HandleMove;
        playerInput.actions["Interact"].performed -= ToggleControl;
    }

    public void ToggleControl(InputAction.CallbackContext context)
    {
        if (mainCameraController == null) return;

        if (mainCameraController.SelectedPrefab == transform)
        {
            mainCameraController.UpdateSelection(null);
        }
        else
        {
            mainCameraController.UpdateSelection(transform);
            OnFuelUpdated?.Invoke(currentFuel, maxFuel);
        }
    }

    public void HandleMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        ApplyPlayerThrust();
    }

    private void ApplyPlayerThrust()
    {
        if (mainCameraController == null || mainCameraController.SelectedPrefab != transform)
        {
            return;
        }

        if (moveInput != Vector2.zero && currentFuel > 0)
        {
            Vector3 thrustDirection = new Vector3(moveInput.x, moveInput.y, 0).normalized;
            rb.AddForce(thrustDirection * thrustForce);

            currentFuel -= fuelConsumeRate * Time.fixedDeltaTime;
            OnFuelUpdated?.Invoke(currentFuel, maxFuel);

            if (launchData != null)
            {
                launchData.thrusts.Add(new ThrustData { frame = TimeManager.Instance.GlobalFrame, direction = moveInput });
            }
        }
    }
}