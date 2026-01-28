using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        if (UnityEngine.Camera.main != null)
        {
            mainCameraTransform = UnityEngine.Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            transform.rotation = mainCameraTransform.rotation;
        }
    }
}