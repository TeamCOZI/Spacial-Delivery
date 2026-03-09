using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(32000)]
[RequireComponent(typeof(PlayerInput))]
public partial class UserInput : MonoBehaviour, IInputService
{
    public static UserInput Instance { get; private set; }
    public static event Action<UserInput> InstanceChanged;
    public event Action<Transform> focusEvent;
    public event Action<Icon> hoverEvent;
    public event Action<Vector2> dragOffsetEvent;
    public event Action<float> zoomOffsetEvent;

    private PlayerInput playerInput;
    private InputAction focusParentAction;
    private InputAction assemblyModeAction;
    private Camera mainCamera;
    private int focusRaycastMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InstanceChanged?.Invoke(this);

        playerInput = GetComponent<PlayerInput>();
        focusParentAction = playerInput.actions != null ? playerInput.actions.FindAction("FocusParent", false) : null;
        if (focusParentAction != null)
        {
            focusParentAction.performed += FocusParent;
        }
        else
        {
            Debug.LogWarning("UserInput: 'FocusParent' action was not found.");
        }

        assemblyModeAction = playerInput.actions != null ? playerInput.actions.FindAction("AssemblyMode", false) : null;
        if (assemblyModeAction != null)
        {
            assemblyModeAction.performed += AssemblyMode;
        }
        else
        {
            Debug.LogWarning("UserInput: 'AssemblyMode' action was not found.");
        }
        mainCamera = Camera.main;

        int smallScaleLayer = SmallScaleLayerUtility.GetLayer();
        focusRaycastMask = Physics.DefaultRaycastLayers;
        if (smallScaleLayer >= 0) focusRaycastMask |= 1 << smallScaleLayer;
    }

    private void OnDestroy()
    {
        if (focusParentAction != null)
        {
            focusParentAction.performed -= FocusParent;
            focusParentAction = null;
        }
        if (assemblyModeAction != null)
        {
            assemblyModeAction.performed -= AssemblyMode;
            assemblyModeAction = null;
        }

        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }

    private bool TryGetMainCamera(out Camera camera)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        camera = mainCamera;
        return camera != null;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        ClearSpaceshipFocusByKey();
        Drag();
        Zoom();
    }

    private void LateUpdate()
    {
        if (Mouse.current == null) return;

        FocusHover();
    }
}
