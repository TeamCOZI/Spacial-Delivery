using System;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    public Camera cameraComponent;

    public Action<Vector2> dragOffsetEvent;

    [Header("Default Settings")]
    public float minZ;
    public float maxZ;
    public float defaultZ;
    public float smoothTime;
    
    [Header("Follow Settings")]
    public float followZ;

    [Header("Zoom Settings")]
    public float zoomSpeed;

    private Transform oldFocus;
    private Vector3 target;
    private Vector3 offset;
    private Vector2 dragOffset;
    private float zoomOffset;

    private Vector3 currentVelocity;

    private void Awake()
    {
        cameraComponent = Camera.main;
        target = Vector3.zero;

        dragOffset = Vector2.zero;
        zoomOffset = defaultZ;
    }

    private void Start()
    {
        UserInput.Instance.dragOffsetEvent += UpdateDragOffset;
        UserInput.Instance.zoomOffsetEvent += UpdateZoomOffset;

        FocusManager.Instance.focusEvent += UpdateFocus;
    }

    private void Update()
    {
        UpdateCameraPos();
    }

    private void UpdateCameraPos()
    {
        if (oldFocus != null) target = oldFocus.transform.position - new Vector3(0, 0, oldFocus.transform.lossyScale.z);

        offset = Vector3.SmoothDamp(offset, new Vector3(0, 0, zoomOffset), ref currentVelocity, smoothTime);

        cameraComponent.transform.position = target + offset + new Vector3(dragOffset.x, dragOffset.y, 0);
    }

    private void UpdateFocus(Transform transform)
    {
        // New Focus.
        if (oldFocus == null && transform != null)
        {
            target = transform.position + new Vector3(0, 0, followZ);
            offset = cameraComponent.transform.position - target;
            dragOffset = Vector2.zero;
            zoomOffset = followZ;
        }
        // Focus changed.
        if (oldFocus != null && transform != null)
        {
            target = transform.position + new Vector3(0, 0, followZ);
            offset = cameraComponent.transform.position - target;
            dragOffset = Vector2.zero;
            zoomOffset = followZ;
        }
        // Unfocus.
        else if (oldFocus != null && transform == null)
        {
            target = new Vector3(cameraComponent.transform.position.x, cameraComponent.transform.position.y, 0);
            dragOffset = Vector2.zero;
            zoomOffset = cameraComponent.transform.position.z;
        }

        oldFocus = transform;
    }

    private void UpdateDragOffset(Vector2 dragOffset)
    {
        this.dragOffset -= dragOffset;
        dragOffsetEvent?.Invoke(this.dragOffset);

        if (oldFocus != null)
        {
            // Unfocus by XY.
            if (Vector3.Distance(this.dragOffset, Vector3.zero) > oldFocus.GetComponent<Gravity>().GravityRadius)
            {
                FocusManager.Instance.SetFocus(null);
                this.dragOffset = Vector2.zero;
            }
        }
    }

    private void UpdateZoomOffset(float zoomOffset)
    {
        this.zoomOffset = Mathf.Clamp(this.zoomOffset + zoomOffset * Mathf.Abs(this.zoomOffset) * zoomSpeed / 100f, minZ, maxZ);
    }
}