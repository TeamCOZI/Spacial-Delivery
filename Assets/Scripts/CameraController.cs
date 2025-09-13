using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 100f;
    public float defaultZ = -30f;
    public float minZ = -50f;
    public float maxZ = -10f;

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
}