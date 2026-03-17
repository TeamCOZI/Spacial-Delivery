using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(34110)]
[DisallowMultipleComponent]
public class SpaceshipTargetPresenter : FocusEventSubscriber
{
    [Header("Label")]
    [SerializeField] private Vector2 labelSize = new Vector2(220f, 52f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 28f);
    [SerializeField] private Color backgroundColor = new Color(0.92f, 0.34f, 0.18f, 0.92f);
    [SerializeField] private Color textColor = Color.white;

    private Canvas hostCanvas;
    private RectTransform canvasRect;
    private RectTransform labelRect;
    private Image labelBackground;
    private TextMeshProUGUI labelText;
    private Camera worldCamera;
    private Spaceship focusedSpaceship;
    private Transform displayedTarget;

    private void Awake()
    {
        worldCamera = Camera.main;
        EnsureLabel();
        HideLabel();
    }

    protected override void LateUpdate()
    {
        UpdateTargetLabel();
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        if (SpaceshipFocusUtility.TryResolveSpaceship(focused, out Spaceship spaceship))
        {
            focusedSpaceship = spaceship;
        }
        else
        {
            focusedSpaceship = null;
        }

        displayedTarget = null;
        UpdateTargetLabel();
    }

    private void EnsureLabel()
    {
        if (labelRect != null && labelText != null) return;

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null) return;

        canvasRect = hostCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        Transform existing = hostCanvas.transform.Find("SpaceshipTargetLabel");
        if (existing != null)
        {
            labelRect = existing as RectTransform;
            labelBackground = existing.GetComponent<Image>();
            labelText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            ApplyVisualConfig();
            return;
        }

        GameObject labelObject = new GameObject("SpaceshipTargetLabel", typeof(RectTransform), typeof(Image));
        labelObject.transform.SetParent(hostCanvas.transform, false);

        labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.sizeDelta = labelSize;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);

        labelBackground = labelObject.GetComponent<Image>();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(labelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        labelText = textObject.GetComponent<TextMeshProUGUI>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 12f;
        labelText.fontSizeMax = 24f;
        ApplyVisualConfig();
    }

    private void ApplyVisualConfig()
    {
        if (labelRect != null)
        {
            labelRect.sizeDelta = labelSize;
        }

        if (labelBackground != null)
        {
            labelBackground.color = backgroundColor;
        }

        if (labelText != null)
        {
            labelText.color = textColor;
            TMP_FontAsset font = ResolveReferenceFont();
            if (font != null)
            {
                labelText.font = font;
            }
        }
    }

    private void UpdateTargetLabel()
    {
        EnsureLabel();
        if (labelRect == null || labelText == null || canvasRect == null)
        {
            return;
        }

        if (focusedSpaceship == null)
        {
            HideLabel();
            return;
        }

        Transform target = focusedSpaceship.CurrentTarget;
        if (target == null)
        {
            HideLabel();
            return;
        }

        if (!TryEnsureWorldCamera())
        {
            HideLabel();
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(ResolveTargetAnchor(target));
        if (screenPosition.z <= 0f)
        {
            HideLabel();
            return;
        }

        if (displayedTarget != target)
        {
            displayedTarget = target;
            labelText.SetText($"TARGET\n{target.name}");
        }

        if (hostCanvas != null && hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            labelRect.position = new Vector3(
                screenPosition.x + screenOffset.x,
                screenPosition.y + screenOffset.y,
                0f);
        }
        else
        {
            Camera uiCamera = hostCanvas != null ? hostCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            {
                HideLabel();
                return;
            }

            labelRect.anchoredPosition = localPoint + screenOffset;
        }

        if (!labelRect.gameObject.activeSelf)
        {
            labelRect.gameObject.SetActive(true);
        }
    }

    private void HideLabel()
    {
        displayedTarget = null;
        if (labelRect != null && labelRect.gameObject.activeSelf)
        {
            labelRect.gameObject.SetActive(false);
        }
    }

    private bool TryEnsureWorldCamera()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        return worldCamera != null;
    }

    private static Vector3 ResolveTargetAnchor(Transform target)
    {
        if (target == null) return Vector3.zero;

        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            collider = target.GetComponentInChildren<Collider>(true);
        }
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = target.GetComponentInChildren<Renderer>(true);
        }
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        float scale = Mathf.Max(
            Mathf.Abs(target.lossyScale.x),
            Mathf.Abs(target.lossyScale.y),
            Mathf.Abs(target.lossyScale.z),
            0.5f);
        return target.position + Vector3.up * scale;
    }

    private static TMP_FontAsset ResolveReferenceFont()
    {
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
        {
            return focusManager.focusInfoText.font;
        }

        return null;
    }
}

