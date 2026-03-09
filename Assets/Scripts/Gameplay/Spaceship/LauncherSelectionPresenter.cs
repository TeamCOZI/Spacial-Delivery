using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(33995)]
[DisallowMultipleComponent]
public class LauncherSelectionPresenter : FocusEventSubscriber
{
    public event Action<Transform> launchRequested;

    private Canvas hostCanvas;
    private RectTransform canvasRect;
    private RectTransform buttonRect;
    private Button launchButton;
    private Image launchButtonBackground;
    private TextMeshProUGUI launchButtonLabel;
    private Transform focusedLauncher;
    private Camera worldCamera;

    private Vector2 buttonSize = new Vector2(150f, 44f);
    private Vector2 screenOffset = new Vector2(120f, -20f);
    private string buttonLabel = "Launcher";
    private Color buttonColor = new Color(0.18f, 0.6f, 0.95f, 0.95f);

    public void Configure(Vector2 size, Vector2 offset, string label, Color color)
    {
        buttonSize = size;
        screenOffset = offset;
        buttonLabel = label;
        buttonColor = color;
        ApplyButtonConfiguration();
    }

    private void Awake()
    {
        worldCamera = Camera.main;
        EnsureButton();
        HideButton();
    }

    protected override void LateUpdate()
    {
        UpdateButtonVisibilityAndPosition();
    }

    private void EnsureButton()
    {
        if (buttonRect != null && launchButton != null) return;

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null) return;

        canvasRect = hostCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        Transform existing = hostCanvas.transform.Find("LauncherLaunchButton");
        if (existing != null)
        {
            buttonRect = existing as RectTransform;
            launchButton = existing.GetComponent<Button>();
            if (launchButton == null) launchButton = existing.gameObject.AddComponent<Button>();
            launchButtonBackground = launchButton.targetGraphic as Image;
            if (launchButtonBackground == null)
            {
                launchButtonBackground = existing.GetComponent<Image>();
                if (launchButtonBackground != null)
                {
                    launchButton.targetGraphic = launchButtonBackground;
                }
            }
            launchButtonLabel = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            BindLaunchButtonClick();
            ApplyButtonConfiguration();
            return;
        }

        GameObject buttonObject = new GameObject("LauncherLaunchButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(hostCanvas.transform, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        launchButtonBackground = buttonObject.GetComponent<Image>();
        launchButtonBackground.color = buttonColor;

        launchButton = buttonObject.GetComponent<Button>();
        launchButton.targetGraphic = launchButtonBackground;
        BindLaunchButtonClick();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        launchButtonLabel = textObject.GetComponent<TextMeshProUGUI>();
        launchButtonLabel.SetText(buttonLabel);
        launchButtonLabel.color = Color.white;
        launchButtonLabel.alignment = TextAlignmentOptions.Center;
        launchButtonLabel.enableAutoSizing = true;
        launchButtonLabel.fontSizeMin = 12f;
        launchButtonLabel.fontSizeMax = 22f;

        FocusManager focusManager = GetFocusManager();
        if (focusManager != null && focusManager.focusInfoText != null && focusManager.focusInfoText.font != null)
        {
            launchButtonLabel.font = focusManager.focusInfoText.font;
        }

        ApplyButtonConfiguration();
    }

    private void UpdateButtonVisibilityAndPosition()
    {
        if (buttonRect == null || launchButton == null)
        {
            EnsureButton();
        }

        if (buttonRect == null || launchButton == null || canvasRect == null)
        {
            return;
        }

        if (focusedLauncher == null)
        {
            HideButton();
            return;
        }

        if (!TryEnsureWorldCamera())
        {
            HideButton();
            return;
        }

        Vector3 stableAnchor = LauncherLaunchUtility.GetLauncherStableWorldAnchor(focusedLauncher);
        Vector3 screenPos = worldCamera.WorldToScreenPoint(stableAnchor);
        if (screenPos.z <= 0f)
        {
            HideButton();
            return;
        }

        if (hostCanvas != null && hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 pixelPos = new Vector2(
                Mathf.Round(screenPos.x + screenOffset.x),
                Mathf.Round(screenPos.y + screenOffset.y));
            buttonRect.position = pixelPos;
        }
        else
        {
            Camera uiCamera = hostCanvas != null ? hostCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    uiCamera,
                    out Vector2 localPoint))
            {
                HideButton();
                return;
            }

            buttonRect.anchoredPosition = localPoint + screenOffset;
            Vector2 anchored = buttonRect.anchoredPosition;
            buttonRect.anchoredPosition = new Vector2(Mathf.Round(anchored.x), Mathf.Round(anchored.y));
        }
        if (!buttonRect.gameObject.activeSelf) buttonRect.gameObject.SetActive(true);
    }

    private void HideButton()
    {
        if (buttonRect != null && buttonRect.gameObject.activeSelf)
        {
            buttonRect.gameObject.SetActive(false);
        }
    }

    private void HandleLaunchClicked()
    {
        if (focusedLauncher == null) return;
        launchRequested?.Invoke(focusedLauncher);
    }

    private void BindLaunchButtonClick()
    {
        if (launchButton == null) return;
        launchButton.onClick.RemoveListener(HandleLaunchClicked);
        launchButton.onClick.AddListener(HandleLaunchClicked);
    }

    private void ApplyButtonConfiguration()
    {
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = buttonSize;
        }

        if (launchButton != null)
        {
            if (launchButtonBackground == null)
            {
                launchButtonBackground = launchButton.targetGraphic as Image;
                if (launchButtonBackground == null)
                {
                    launchButtonBackground = launchButton.GetComponent<Image>();
                    if (launchButtonBackground != null)
                    {
                        launchButton.targetGraphic = launchButtonBackground;
                    }
                }
            }
            if (launchButtonBackground != null)
            {
                launchButtonBackground.color = buttonColor;
            }
        }

        if (launchButtonLabel != null)
        {
            launchButtonLabel.SetText(buttonLabel);
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

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedLauncher = LauncherLaunchUtility.ResolveFocusedLauncher(focused);
        if (focusedLauncher == null)
        {
            HideButton();
        }
    }

    private static FocusManager GetFocusManager()
    {
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            return focusManager;
        }

        return null;
    }
}
