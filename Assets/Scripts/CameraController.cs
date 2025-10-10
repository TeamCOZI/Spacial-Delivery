using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform SelectedPrefab { get; private set; }
    public event Action<Transform> OnSelectionChanged;

    [Header("Zoom Settings")]
    public float zoomSpeed = 100f;
    public float defaultZ = -30f;
    public float minZ = -50f;
    public float maxZ = -10f;

    [Header("Follow Settings")]
    public float followSpeed = 5.0f;
    public string prefabTag = "Prefab";
    public float spaceshipFollowDistance = 10f;
    public float spaceshipFollowHeight = 5f;

    [Header("Angled Follow Settings")]
    public Vector3 angledOffset = new Vector3(0, 0, -15);
    public Vector3 angledRotation = new Vector3(30, 0, 0);

    private bool isDragging = false;
    private Vector3 lastMouseScreenPos;
    private PlayerSpaceship targetSpaceship;

    private Vector3 preSelectionPosition;
    private Quaternion preSelectionRotation;

    public void Start()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, defaultZ);
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (SelectedPrefab != null && Mouse.current.rightButton.isPressed)
        {
            UpdateSelection(null);
        }

        if (targetSpaceship == null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag(prefabTag))
                {
                    UpdateSelection(hit.transform);
                }
            }
        }
        
        HandleZoom();

        if (SelectedPrefab == null)
        {
            HandleDrag();
        }
    }

    private void LateUpdate()
    {
        if (targetSpaceship != null)
        {
            transform.position = targetSpaceship.transform.position;
            transform.rotation = targetSpaceship.transform.rotation;

            transform.Translate(angledOffset, Space.Self);
            transform.Rotate(angledRotation, Space.Self);
        }
        else if (SelectedPrefab != null)
        {
            Vector3 desiredPosition = new Vector3(SelectedPrefab.position.x, SelectedPrefab.position.y, transform.position.z);
            transform.position = desiredPosition;
        }
    }

    public void UpdateSelection(Transform newSelection)
    {
        if (SelectedPrefab == newSelection) return;

        if (SelectedPrefab == null && newSelection != null)
        {
            preSelectionPosition = transform.position;
            preSelectionRotation = transform.rotation;
        }

        SelectedPrefab = newSelection;

        if (SelectedPrefab != null)
        {
            targetSpaceship = SelectedPrefab.GetComponent<PlayerSpaceship>();
        }
        else
        {
            targetSpaceship = null;
            transform.position = preSelectionPosition;
            transform.rotation = preSelectionRotation;
        }

        OnSelectionChanged?.Invoke(SelectedPrefab);
    }

    private void HandleDrag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastMouseScreenPos = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 currentMouseScreenPos = Mouse.current.position.ReadValue();

            if (Vector3.Distance(currentMouseScreenPos, lastMouseScreenPos) <= 0.1f)
            {
                return;
            }

            Vector3 lastWorldPos = GetWorldPosOnXYPlane(lastMouseScreenPos);
            Vector3 currentWorldPos = GetWorldPosOnXYPlane(currentMouseScreenPos);

            Vector3 worldDelta = currentWorldPos - lastWorldPos;

            transform.position -= worldDelta;

            lastMouseScreenPos = currentMouseScreenPos;
        }
    }

    private void HandleZoom()
    {
        float scrollValue = Mouse.current.scroll.ReadValue().y;

        if (scrollValue != 0f)
        {
            float scrollInput = scrollValue / 120f;
            Vector3 currentPosition = transform.position;
            float newZ = currentPosition.z + scrollInput * zoomSpeed;
            newZ = Mathf.Clamp(newZ, minZ, maxZ);
            transform.position = new Vector3(currentPosition.x, currentPosition.y, newZ);
        }
    }

    private Vector3 GetWorldPosOnXYPlane(Vector3 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);
        if (xyPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }
}