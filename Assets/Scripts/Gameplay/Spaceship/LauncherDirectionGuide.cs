using UnityEngine;

[DefaultExecutionOrder(33990)]
[DisallowMultipleComponent]
public class LauncherDirectionGuide : FocusEventSubscriber
{
    private const float MinDirectionLineLength = 5f;
    private const float MinArrowHeadLength = 1f;

    [Header("Direction Guide")]
    [SerializeField, Min(0.00001f)] private float directionLineWidth = 0.005f;
    [SerializeField, Min(0.1f)] private float directionLineLengthScale = 0.15f;
    [SerializeField] private Color directionLineColor = new Color(1f, 0.8f, 0.1f, 0.95f);
    [SerializeField, Min(0.00001f)] private float directionArrowWidth = 0.005f;
    [SerializeField, Min(0.02f)] private float directionArrowLengthScale = 0.12f;
    [SerializeField, Range(5f, 80f)] private float directionArrowAngle = 28f;

    private LineRenderer directionLine;
    private LineRenderer directionArrowHead;
    private Material directionLineMaterial;
    private Camera cameraComponent;
    private float tanFovHalf;
    private Transform focusedLauncher;

    private void Awake()
    {
        TryEnsureCamera();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HideDirectionLine();
    }

    private void OnDestroy()
    {
        if (directionLineMaterial != null)
        {
            Destroy(directionLineMaterial);
            directionLineMaterial = null;
        }

        if (directionLine != null)
        {
            Destroy(directionLine.gameObject);
            directionLine = null;
        }

        if (directionArrowHead != null)
        {
            Destroy(directionArrowHead.gameObject);
            directionArrowHead = null;
        }
    }

    protected override void LateUpdate()
    {
        if (focusedLauncher == null)
        {
            HideDirectionLine();
            return;
        }

        EnsureDirectionLine();
        if (directionLine == null) return;
        UpdateDirectionLineWidth();

        Vector3 start = LauncherLaunchUtility.GetLauncherStableWorldAnchor(focusedLauncher);
        Vector3 dir = LauncherLaunchUtility.GetPlanarDirection(LauncherLaunchUtility.GetLauncherWorldDirection(focusedLauncher));
        float lineLength = MinDirectionLineLength;
        if (TryEnsureCamera())
        {
            float distance = Vector3.Distance(cameraComponent.transform.position, start);
            lineLength = Mathf.Max(MinDirectionLineLength, distance * directionLineLengthScale);
        }

        Vector3 end = start + dir * lineLength;
        start.z = 0f;
        end.z = 0f;

        if (!directionLine.gameObject.activeSelf) directionLine.gameObject.SetActive(true);
        directionLine.SetPosition(0, start);
        directionLine.SetPosition(1, end);

        if (directionArrowHead != null)
        {
            float headLength = Mathf.Max(MinArrowHeadLength, lineLength * directionArrowLengthScale);
            Vector3 back = -dir * headLength;
            Vector3 wingA = Quaternion.Euler(0f, 0f, directionArrowAngle) * back;
            Vector3 wingB = Quaternion.Euler(0f, 0f, -directionArrowAngle) * back;
            Vector3 headA = end + wingA;
            Vector3 headB = end + wingB;
            headA.z = 0f;
            headB.z = 0f;

            if (!directionArrowHead.gameObject.activeSelf) directionArrowHead.gameObject.SetActive(true);
            directionArrowHead.SetPosition(0, headA);
            directionArrowHead.SetPosition(1, end);
            directionArrowHead.SetPosition(2, headB);
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedLauncher = LauncherLaunchUtility.ResolveFocusedLauncher(focused);
        if (focusedLauncher == null)
        {
            HideDirectionLine();
        }
    }

    private void EnsureDirectionLine()
    {
        if (directionLine != null && directionArrowHead != null) return;

        if (directionLine == null)
        {
            GameObject lineObject = new GameObject("LauncherDirectionLine", typeof(LineRenderer));
            lineObject.transform.SetParent(transform, false);
            directionLine = lineObject.GetComponent<LineRenderer>();
            directionLine.positionCount = 2;
            ConfigureLineRenderer(directionLine, directionLineWidth);
            directionLine.startColor = directionLineColor;
            directionLine.endColor = directionLineColor;
        }

        if (directionArrowHead == null)
        {
            GameObject arrowObject = new GameObject("LauncherDirectionArrowHead", typeof(LineRenderer));
            arrowObject.transform.SetParent(transform, false);
            directionArrowHead = arrowObject.GetComponent<LineRenderer>();
            directionArrowHead.positionCount = 3;
            ConfigureLineRenderer(directionArrowHead, directionArrowWidth);
            directionArrowHead.startColor = directionLineColor;
            directionArrowHead.endColor = directionLineColor;
        }

        if (directionLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                directionLineMaterial = new Material(shader);
                directionLineMaterial.color = directionLineColor;
            }
        }

        if (directionLineMaterial != null)
        {
            directionLine.sharedMaterial = directionLineMaterial;
            if (directionArrowHead != null)
            {
                directionArrowHead.sharedMaterial = directionLineMaterial;
            }
        }
    }

    private void HideDirectionLine()
    {
        if (directionLine != null && directionLine.gameObject.activeSelf)
        {
            directionLine.gameObject.SetActive(false);
        }
        if (directionArrowHead != null && directionArrowHead.gameObject.activeSelf)
        {
            directionArrowHead.gameObject.SetActive(false);
        }
    }

    private void UpdateDirectionLineWidth()
    {
        if (!TryEnsureCamera() || directionLine == null) return;

        float baseDepth = Mathf.Abs(cameraComponent.transform.position.z);
        float lineWidth = 2f * baseDepth * tanFovHalf * directionLineWidth;
        float arrowWidth = 2f * baseDepth * tanFovHalf * directionArrowWidth;

        directionLine.startWidth = lineWidth;
        directionLine.endWidth = lineWidth;

        if (directionArrowHead != null)
        {
            directionArrowHead.startWidth = arrowWidth;
            directionArrowHead.endWidth = arrowWidth;
        }
    }

    private bool TryEnsureCamera()
    {
        if (cameraComponent == null)
        {
            cameraComponent = Camera.main;
            if (cameraComponent == null) return false;
            tanFovHalf = Mathf.Tan(cameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        return true;
    }

    private static void ConfigureLineRenderer(LineRenderer lineRenderer, float width)
    {
        if (lineRenderer == null) return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
    }
}
