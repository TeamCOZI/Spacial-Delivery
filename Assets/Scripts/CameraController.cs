using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform SelectedPrefab { get; private set; }
    public event Action<Transform> OnSelectionChanged;

    private Planet currentlyHoveredPlanet;

    [Header("Zoom Settings")]
    public float zoomSpeed = 100f;
    public float defaultZ = -30f;
    public float minZ = -50f;
    public float maxZ = -10f;
    public float selectZoom = 100f;

    [Header("Follow Settings")]
    public float smoothTime = 0.15f;
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

    private Quaternion preSelectionRotation;

    private PlayerSpaceship _currentlyControlledSpaceship;

    private Star centralStar;

    private Vector2 targetXY;
    private float targetZ;

    private Vector3 currentOffset;
    private Vector3 positionVelocity = Vector3.zero;
    private Vector3 offsetVelocity = Vector3.zero;

    public void Start()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, defaultZ);
        targetXY = new Vector2(transform.position.x, transform.position.y);
        targetZ = defaultZ;
        preSelectionRotation = transform.rotation;
    }

    public void SetCentralStar(Star star)
    {
        centralStar = star;
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        HandleHover();

        if (targetSpaceship == null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag(prefabTag) || hit.collider.CompareTag(spaceshipTag))
                {
                    UpdateSelection(hit.transform);
                    ActivateSpecialView();
                }
            }
        }
        
        HandleZoom();
        HandleDrag();
    }

    private void HandleHover()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        float maxDistance = Mathf.Infinity;
        int layerMask = ~0;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            Planet hitPlanet = hit.transform.GetComponent<Planet>();

            if (hitPlanet != currentlyHoveredPlanet)
            {
                if (currentlyHoveredPlanet != null)
                {
                    currentlyHoveredPlanet.isHovered = false;
                }

                currentlyHoveredPlanet = hitPlanet;

                if (currentlyHoveredPlanet != null)
                {
                    currentlyHoveredPlanet.isHovered = true;
                }
            }
        }
        else
        {
            if (currentlyHoveredPlanet != null)
            {
                currentlyHoveredPlanet.isHovered = false;
                currentlyHoveredPlanet = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (SelectedPrefab != null)
        {
            Vector3 targetOffset;
            Quaternion targetRotation;

            if (targetSpaceship != null)
            {
                targetOffset = angledOffset;
                targetRotation = Quaternion.Euler(angledRotation);
            }
            else
            {
                targetOffset = new Vector3(0, 0, targetZ);
                targetRotation = preSelectionRotation;
            }

            currentOffset = Vector3.SmoothDamp(currentOffset, targetOffset, ref offsetVelocity, smoothTime);

            transform.position = SelectedPrefab.position + currentOffset;

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothTime * 10 * Time.deltaTime);
        }
        else
        {
            Vector3 finalTargetPosition = new Vector3(targetXY.x, targetXY.y, targetZ);

            transform.position = Vector3.SmoothDamp(transform.position, finalTargetPosition, ref positionVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, preSelectionRotation, smoothTime * 10 * Time.deltaTime);
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

        if (targetSpaceship != null)
        {
            targetSpaceship = null;
        }

        if (SelectedPrefab == null && newSelection != null)
        {
            preSelectionRotation = transform.rotation;

            currentOffset = transform.position - newSelection.position;
        }
        else if (SelectedPrefab != null && newSelection == null)
        {
            targetXY = new Vector2(transform.position.x, transform.position.y);
            targetZ = transform.position.z;
        }

        SelectedPrefab = newSelection;
        OnSelectionChanged?.Invoke(SelectedPrefab);
    }

    private void ActivateSpecialView(bool preserveZoom = false)
    {
        if (SelectedPrefab == null) return;

        if (FocusManager.Instance != null)
        {
            FocusManager.Instance.SetFocus(SelectedPrefab);
        }

        if (_currentlyControlledSpaceship != null)
        {
            _currentlyControlledSpaceship.ReleaseControl();
        }

        PlayerSpaceship ps = SelectedPrefab.GetComponent<PlayerSpaceship>();
        Gravity gravityComponent = SelectedPrefab.GetComponent<Gravity>();
        if (ps != null)
        {
            targetSpaceship = ps;
            _currentlyControlledSpaceship = targetSpaceship;
            _currentlyControlledSpaceship.TakeControl();

            float alpha_rad = angledRotation.x * Mathf.Deg2Rad;
            float newDistance = preserveZoom ? -currentOffset.z / Mathf.Cos(alpha_rad) : defaultSpaceshipDistance;
            newDistance = Mathf.Clamp(newDistance, minSpaceshipDistance, maxSpaceshipDistance);
        }
        else if (gravityComponent != null)
        {
            float newZ = gravityComponent.gravityRadius * -9;
            targetZ = Mathf.Clamp(newZ, minZ, maxZ);
        }
        else
        {
            float newZ = -SelectedPrefab.transform.lossyScale.x * selectZoom;
            targetZ = Mathf.Clamp(newZ, minZ, maxZ);
        }
    }

    public void SelectAndActivateSpecialView(Transform newSelection)
    {
        UpdateSelection(newSelection);
        ActivateSpecialView(true);
    }

    private void HandleDrag()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (SelectedPrefab != null)
            {
                UpdateSelection(null);
            }

            if (FocusManager.Instance != null && centralStar != null)
            {
                FocusManager.Instance.SetFocus(centralStar.transform);
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

            targetXY -= new Vector2(worldDelta.x, worldDelta.y);

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
                float baseZ = transform.position.z;
                float adaptiveZoomMultiplier = zoomSpeed / 100f;
                float zoomAmount = scrollInput * Mathf.Abs(baseZ) * adaptiveZoomMultiplier;

                float newZ = targetZ + zoomAmount;
                targetZ = Mathf.Clamp(newZ, minZ, maxZ);
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