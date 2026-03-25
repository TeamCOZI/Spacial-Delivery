using UnityEngine;
using UnityEngine.InputSystem;

public partial class UserInput
{
    private bool isDrag = false;
    private Vector3 oldMousePos;
    private const float DragMoveThreshold = 0.1f;
    private const float DragMoveThresholdSqr = DragMoveThreshold * DragMoveThreshold;

    public bool IsCameraDragging => isDrag;

    private void Drag()
    {
        bool isPointerOverBlockingUi = IsPointerOverBlockingUi();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (isPointerOverBlockingUi)
            {
                isDrag = false;
                return;
            }

            isDrag = true;
            oldMousePos = Mouse.current.position.ReadValue();
            return;
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDrag = false;
        }

        if (!isDrag) return;

        Vector3 currentMousePos = Mouse.current.position.ReadValue();
        if (isPointerOverBlockingUi)
        {
            oldMousePos = currentMousePos;
            return;
        }

        if ((currentMousePos - oldMousePos).sqrMagnitude <= DragMoveThresholdSqr) return;
        if (!TryGetMainCamera(out Camera camera)) return;

        float dragPlaneZ = CameraManager.Instance != null ? CameraManager.Instance.GetDragPlaneZ() : 0f;
        Vector3 oldScreenMousePos = Utility.screenMousePos(camera, oldMousePos, dragPlaneZ);
        Vector3 currentScreenMousePos = Utility.screenMousePos(camera, currentMousePos, dragPlaneZ);
        Vector3 screenDelta = currentScreenMousePos - oldScreenMousePos;

        dragOffsetEvent?.Invoke(screenDelta);
        oldMousePos = currentMousePos;
    }

    private void Zoom()
    {
        if (IsPointerOverBlockingUi()) return;

        float mouseScroll = Mouse.current.scroll.ReadValue().y;
        if (mouseScroll == 0f) return;

        mouseScroll /= 120f;
        zoomOffsetEvent?.Invoke(mouseScroll);
    }
}

