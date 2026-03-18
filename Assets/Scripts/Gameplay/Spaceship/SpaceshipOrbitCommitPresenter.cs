using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(34120)]
[DisallowMultipleComponent]
public class SpaceshipOrbitCommitPresenter : FocusEventSubscriber
{
    [Header("Button")]
    [SerializeField] private Vector2 buttonSize = new Vector2(180f, 44f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, -24f);
    [SerializeField] private Color buttonColor = new Color(0.15f, 0.7f, 0.38f, 0.95f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private string buttonLabel = "Orbit";

    private Canvas hostCanvas;
    private RectTransform canvasRect;
    private RectTransform buttonRect;
    private Button orbitButton;
    private Image buttonBackground;
    private TextMeshProUGUI buttonText;
    private Camera worldCamera;
    private Spaceship focusedSpaceship;
    private SpaceshipRendezvousController rendezvousController;
    private int lastCanvasUpdateFrame = -1;

    private void Awake()
    {
        worldCamera = Camera.main;
        EnsureButton();
        HideButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        base.OnDisable();
    }

    protected override void LateUpdate()
    {
        if (buttonRect == null || orbitButton == null || canvasRect == null)
        {
            EnsureButton();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        if (SpaceshipFocusUtility.TryResolveSpaceship(focused, out Spaceship spaceship))
        {
            focusedSpaceship = spaceship;
            rendezvousController = spaceship != null ? spaceship.GetComponent<SpaceshipRendezvousController>() : null;
        }
        else
        {
            focusedSpaceship = null;
            rendezvousController = null;
        }

        UpdateButtonVisibilityAndPosition();
    }

    private void EnsureButton()
    {
        if (buttonRect != null && orbitButton != null) return;

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null) return;

        canvasRect = hostCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        Transform existing = hostCanvas.transform.Find("SpaceshipOrbitCommitButton");
        if (existing != null)
        {
            buttonRect = existing as RectTransform;
            orbitButton = existing.GetComponent<Button>();
            if (orbitButton == null) orbitButton = existing.gameObject.AddComponent<Button>();

            buttonBackground = existing.GetComponent<Image>();
            if (buttonBackground != null)
            {
                orbitButton.targetGraphic = buttonBackground;
            }

            buttonText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            BindButtonClick();
            ApplyVisualConfig();
            return;
        }

        GameObject buttonObject = new GameObject("SpaceshipOrbitCommitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(hostCanvas.transform, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        buttonBackground = buttonObject.GetComponent<Image>();
        orbitButton = buttonObject.GetComponent<Button>();
        orbitButton.targetGraphic = buttonBackground;
        BindButtonClick();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.enableAutoSizing = true;
        buttonText.fontSizeMin = 12f;
        buttonText.fontSizeMax = 24f;
        ApplyVisualConfig();
    }

    private void UpdateButtonVisibilityAndPosition()
    {
        EnsureButton();
        if (buttonRect == null || orbitButton == null || canvasRect == null)
        {
            return;
        }

        if (focusedSpaceship == null || rendezvousController == null || !rendezvousController.HasStableOrbitCandidate)
        {
            HideButton();
            return;
        }

        Transform anchor = rendezvousController.StableOrbitTarget;
        if (anchor == null)
        {
            HideButton();
            return;
        }

        if (!TryEnsureWorldCamera())
        {
            HideButton();
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(anchor.position);
        if (screenPosition.z <= 0f)
        {
            HideButton();
            return;
        }

        if (hostCanvas != null && hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            buttonRect.position = new Vector3(
                screenPosition.x + screenOffset.x,
                screenPosition.y + screenOffset.y,
                0f);
        }
        else
        {
            Camera uiCamera = hostCanvas != null ? hostCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            {
                HideButton();
                return;
            }

            buttonRect.anchoredPosition = localPoint + screenOffset;
        }

        if (!buttonRect.gameObject.activeSelf)
        {
            buttonRect.gameObject.SetActive(true);
        }
    }

    private void HandleOrbitClicked()
    {
        string shipName = focusedSpaceship != null ? focusedSpaceship.name : "<null>";
        string targetName = rendezvousController != null && rendezvousController.StableOrbitTarget != null
            ? rendezvousController.StableOrbitTarget.name
            : (focusedSpaceship != null && focusedSpaceship.CurrentTarget != null ? focusedSpaceship.CurrentTarget.name : "<null>");

        Debug.Log($"[OrbitButton] clicked ship={shipName} target={targetName}");

        if (rendezvousController == null)
        {
            Debug.LogWarning($"[OrbitButton] failed reason=rendezvous_controller_missing ship={shipName} target={targetName}");
            return;
        }

        bool committed = rendezvousController.TryCommitStableOrbit();
        Debug.Log(committed
            ? $"[OrbitButton] commit_succeeded ship={shipName} target={targetName}"
            : $"[OrbitButton] commit_failed ship={shipName} target={targetName}");
        if (!committed) return;

        HideButton();
    }

    private void BindButtonClick()
    {
        if (orbitButton == null) return;
        orbitButton.onClick.RemoveListener(HandleOrbitClicked);
        orbitButton.onClick.AddListener(HandleOrbitClicked);
    }

    private void ApplyVisualConfig()
    {
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = buttonSize;
        }

        if (buttonBackground != null)
        {
            buttonBackground.color = buttonColor;
        }

        if (buttonText != null)
        {
            buttonText.SetText(buttonLabel);
            buttonText.color = textColor;

            if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
                focusManager.focusInfoText != null &&
                focusManager.focusInfoText.font != null)
            {
                buttonText.font = focusManager.focusInfoText.font;
            }
        }
    }

    private void HideButton()
    {
        if (buttonRect != null && buttonRect.gameObject.activeSelf)
        {
            buttonRect.gameObject.SetActive(false);
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

    private void HandleWillRenderCanvases()
    {
        if (lastCanvasUpdateFrame == Time.frameCount)
        {
            return;
        }

        lastCanvasUpdateFrame = Time.frameCount;
        UpdateButtonVisibilityAndPosition();
    }
}

