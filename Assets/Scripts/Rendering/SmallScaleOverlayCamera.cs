using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class SmallScaleOverlayCamera : MonoBehaviour
{
    [Header("Small Scale Camera")]
    [Min(0.0001f)] public float nearClip = 0.001f;
    [Min(1f)] public float minimumFarClip = 20000f;
    public bool useDynamicClipRange = true;
    [Min(0.001f)] public float dynamicNearClipOrtho = 0.01f;
    [Min(0.001f)] public float dynamicNearClipPerspective = 0.05f;
    [Min(1f)] public float dynamicFarPaddingOrtho = 50f;
    [Min(1f)] public float dynamicFarPaddingPerspective = 1000f;
    public bool excludeSmallScaleFromMainCamera = true;
    public string overlayCameraName = "SmallScaleOverlayCamera";

    private Camera mainCamera;
    private Camera overlayCamera;
    private Transform overlayTransform;
    private int smallScaleLayer = -1;
    private int originalMainCullingMask;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        originalMainCullingMask = mainCamera.cullingMask;
        smallScaleLayer = SmallScaleLayerUtility.GetLayer();

        if (smallScaleLayer < 0)
        {
            Debug.LogWarning($"Layer '{SmallScaleLayerUtility.LayerName}' not found. Overlay camera disabled.");
            enabled = false;
            return;
        }

        EnsureOverlayCamera();
        ConfigureMasks();
        SyncOverlayCamera();
    }

    private void LateUpdate()
    {
        if (overlayCamera == null) return;
        SyncOverlayCamera();
    }

    private void OnDisable()
    {
        if (mainCamera != null)
        {
            mainCamera.cullingMask = originalMainCullingMask;
        }
    }

    private void EnsureOverlayCamera()
    {
        Transform found = transform.Find(overlayCameraName);
        if (found != null)
        {
            overlayTransform = found;
            overlayCamera = found.GetComponent<Camera>();
        }

        if (overlayCamera == null)
        {
            GameObject go = new GameObject(overlayCameraName);
            overlayTransform = go.transform;
            overlayTransform.SetParent(transform, false);
            overlayTransform.localPosition = Vector3.zero;
            overlayTransform.localRotation = Quaternion.identity;

            overlayCamera = go.AddComponent<Camera>();
        }

        AudioListener listener = overlayCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            Destroy(listener);
        }
    }

    private void ConfigureMasks()
    {
        int smallMask = 1 << smallScaleLayer;

        if (excludeSmallScaleFromMainCamera)
        {
            mainCamera.cullingMask &= ~smallMask;
        }
        else
        {
            mainCamera.cullingMask |= smallMask;
        }

        overlayCamera.cullingMask = smallMask;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.depth = mainCamera.depth + 1f;
        overlayCamera.useOcclusionCulling = false;

        UniversalAdditionalCameraData mainData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        UniversalAdditionalCameraData overlayData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();
        if (overlayData == null)
        {
            overlayData = overlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        if (mainData != null)
        {
            overlayData.renderType = CameraRenderType.Overlay;
            if (!mainData.cameraStack.Contains(overlayCamera))
            {
                mainData.cameraStack.Add(overlayCamera);
            }
        }
    }

    private void SyncOverlayCamera()
    {
        overlayTransform.position = transform.position;
        overlayTransform.rotation = transform.rotation;

        overlayCamera.orthographic = mainCamera.orthographic;
        overlayCamera.orthographicSize = mainCamera.orthographicSize;
        overlayCamera.fieldOfView = mainCamera.fieldOfView;

        if (useDynamicClipRange)
        {
            float cameraDistance = Mathf.Abs(mainCamera.transform.position.z);
            if (overlayCamera.orthographic)
            {
                overlayCamera.nearClipPlane = dynamicNearClipOrtho;
                overlayCamera.farClipPlane = Mathf.Max(100f, cameraDistance + dynamicFarPaddingOrtho);
            }
            else
            {
                overlayCamera.nearClipPlane = dynamicNearClipPerspective;
                overlayCamera.farClipPlane = Mathf.Max(dynamicFarPaddingPerspective, cameraDistance + dynamicFarPaddingPerspective);
            }
        }
        else
        {
            overlayCamera.nearClipPlane = nearClip;
            overlayCamera.farClipPlane = Mathf.Max(minimumFarClip, mainCamera.farClipPlane);
        }

        overlayCamera.allowHDR = mainCamera.allowHDR;
        overlayCamera.allowMSAA = mainCamera.allowMSAA;
    }

    private void OnDestroy()
    {
        if (mainCamera != null)
        {
            mainCamera.cullingMask = originalMainCullingMask;
        }
    }
}
