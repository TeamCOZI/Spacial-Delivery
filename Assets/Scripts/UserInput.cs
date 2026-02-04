using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class UserInput : MonoBehaviour
{
    public static UserInput Instance { get; private set; }
    public event Action<Transform> focusEvent;
    public event Action<Icon> hoverEvent;
    public event Action<Vector2> dragOffsetEvent;
    public event Action<float> zoomOffsetEvent;

    private bool isDrag = false;
    private Vector3 oldMousePos;

    private PlayerInput playerInput;

    // Temp
    public GameObject artificialSatellitePrefab;
    public event Action<bool> destroyEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        playerInput = GetComponent<PlayerInput>();
        playerInput.actions["FocusParent"].performed += FocusParent;
        playerInput.actions["GenerateArtificialSatellite"].performed += GenerateArtificialSatellite;
        playerInput.actions["DestroyArtificialSatellite"].performed += DestroyArtificialSatellite;
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        Drag();
        Zoom();
        FocusHover();
    }

    private void Drag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDrag = true;
            oldMousePos = Mouse.current.position.ReadValue();
            return;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDrag = false;
        }
        if (isDrag)
        {
            Vector3 currentMousePos = Mouse.current.position.ReadValue();

            if (Vector3.Distance(currentMousePos, oldMousePos) <= 0.1f) return;

            Vector3 oldScreenMousePos = Utility.screenMousePos(oldMousePos);
            Vector3 currentScreenMousePos = Utility.screenMousePos(currentMousePos);
            Vector3 screenDelta = currentScreenMousePos - oldScreenMousePos;

            dragOffsetEvent?.Invoke(screenDelta);

            oldMousePos = currentMousePos;
        }
    }

    private void Zoom()
    {
        float mouseScroll = Mouse.current.scroll.ReadValue().y;

        if (mouseScroll != 0f)
        {
            mouseScroll /= 120f;

            zoomOffsetEvent?.Invoke(mouseScroll);
        }
    }

    private void FocusHover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Icon"))
            {
                hoverEvent?.Invoke(hit.transform.GetComponent<Icon>());
            }
            else
            {
                hoverEvent?.Invoke(null);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (hit.collider.CompareTag("Icon") || hit.collider.CompareTag("Prefab") || hit.collider.CompareTag("Spaceship"))
                {
                    focusEvent?.Invoke(hit.transform);
                }
            }
        }
        else
        {
            hoverEvent?.Invoke(null);
        }
    }

    private void FocusParent(InputAction.CallbackContext context)
    {
        if (FocusManager.currentFocus != null && FocusManager.currentFocus.GetComponent<OrbitRevolution>() != null) focusEvent?.Invoke(FocusManager.currentFocus.parent);
    }

    private void GenerateArtificialSatellite(InputAction.CallbackContext context)
    {
        Transform transform = FocusManager.currentFocus;
        if (transform.GetComponent<Planet>() != null || transform.GetComponent<Satellite>() != null)
        {
            GameObject artificialSatellite = Instantiate(artificialSatellitePrefab, transform);
            artificialSatellite.name = transform.name + " - Artificial Satellite";

            ArtificialSatellite artificialSatelliteComponent = artificialSatellite.GetComponent<ArtificialSatellite>();
            OrbitRevolution orbitRevolutionComponent = artificialSatellite.GetComponent<OrbitRevolution>();

            if (artificialSatelliteComponent == null || orbitRevolutionComponent == null)
            {
                Debug.LogError("Artificial satellite is missing required components.");
                return;
            }

            artificialSatellite.transform.localScale = Vector3.one * Mathf.RoundToInt(artificialSatelliteComponent.scale / transform.localScale.x) * 0.01f;

            orbitRevolutionComponent.center = transform.gameObject;
            orbitRevolutionComponent.semiMajorAxis = Mathf.RoundToInt((transform.lossyScale.x + artificialSatelliteComponent.altitude) * 10) / 10;
            orbitRevolutionComponent.semiMinorAxis = Mathf.RoundToInt((transform.lossyScale.x + artificialSatelliteComponent.altitude) * 10) / 10;

            if (transform.GetComponent<OrbitRevolution>() == null)
            {
                Debug.LogError("Parent is missing required component.");
                return;
            }

            orbitRevolutionComponent.revolutionPeriod = transform.GetComponent<OrbitRevolution>().revolutionPeriod * 0.5f;
            orbitRevolutionComponent.currentAngle = 0f;
        }
    }

    private void DestroyArtificialSatellite(InputAction.CallbackContext context)
    {
        ArtificialSatellite artificialSatellite = FocusManager.currentFocus.GetComponent<ArtificialSatellite>();
        if (artificialSatellite!= null) artificialSatellite.Destroy();
    }
}