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
    public float selectZoom = 5f;

    [Header("Follow Settings")]
    public float followSpeed = 5.0f;
    public string prefabTag = "Prefab";
    public string spaceshipTag = "PlayerSpaceship";
    public float spaceshipFollowDistance = 10f;
    public float spaceshipFollowHeight = 5f;

    [Header("Angled Follow Settings")]
    public Vector3 angledOffset = new Vector3(0, 0, -15);
    public Vector3 angledRotation = new Vector3(30, 0, 0);

    [Header("Spaceship Zoom Settings")]
    public float defaultSpaceshipDistance = 17.5f;
    public float minSpaceshipDistance = 8f;
    public float maxSpaceshipDistance = 40f;
    public float spaceshipZoomSpeed = 20f;

    private bool isDragging = false;
    private Vector3 lastMouseScreenPos;
    private PlayerSpaceship targetSpaceship;

    private Vector3 preSelectionPosition;
    private Quaternion preSelectionRotation;

    private PlayerSpaceship _currentlyControlledSpaceship;

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

        if (targetSpaceship == null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag(prefabTag) || hit.collider.CompareTag(spaceshipTag))
                {
                    UpdateSelection(hit.transform);
                }
            }
        }
        
        HandleZoom();
        HandleDrag();
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

        if (_currentlyControlledSpaceship != null)
        {
            _currentlyControlledSpaceship.ReleaseControl();
            _currentlyControlledSpaceship = null;
        }

        if (SelectedPrefab == null && newSelection != null)
        {
            preSelectionPosition = transform.position;
            preSelectionRotation = transform.rotation;
        }

        SelectedPrefab = newSelection;

        if (SelectedPrefab != null)
        {
            targetSpaceship = SelectedPrefab.GetComponent<PlayerSpaceship>();

            if (targetSpaceship != null)
            {
                _currentlyControlledSpaceship = targetSpaceship;
                _currentlyControlledSpaceship.TakeControl();

                float alpha_rad = angledRotation.x * Mathf.Deg2Rad;
                angledOffset.y = defaultSpaceshipDistance * Mathf.Sin(alpha_rad);
                angledOffset.z = -defaultSpaceshipDistance * Mathf.Cos(alpha_rad);
            }
            else
            {
                float newZ = -SelectedPrefab.transform.localScale.x * selectZoom;
                transform.position = new Vector3(SelectedPrefab.position.x, SelectedPrefab.position.y, Mathf.Clamp(newZ, minZ, maxZ));
            }
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
            if (SelectedPrefab != null)
            {
                transform.rotation = preSelectionRotation;
                SelectedPrefab = null;
                targetSpaceship = null;
                OnSelectionChanged?.Invoke(SelectedPrefab);
            }

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

            if (targetSpaceship != null)
            {
                float alpha_rad = angledRotation.x * Mathf.Deg2Rad;

                float currentDistance = -angledOffset.z / Mathf.Cos(alpha_rad);

                float newDistance = currentDistance - scrollInput * spaceshipZoomSpeed;
                newDistance = Mathf.Clamp(newDistance, minSpaceshipDistance, maxSpaceshipDistance);

                angledOffset.y = newDistance * Mathf.Sin(alpha_rad);
                angledOffset.z = -newDistance * Mathf.Cos(alpha_rad);
            }
            else
            {
                Vector3 currentPosition = transform.position;

                float adaptiveZoomMultiplier = zoomSpeed / 100f;
                float zoomAmount = scrollInput * Mathf.Abs(currentPosition.z) * adaptiveZoomMultiplier;

                float newZ = currentPosition.z + zoomAmount;
                newZ = Mathf.Clamp(newZ, minZ, maxZ);
                transform.position = new Vector3(currentPosition.x, currentPosition.y, newZ);
            }
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