using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(33995)]
[DisallowMultipleComponent]
public class LauncherSelectionPresenter : FocusEventSubscriber
{
    private const string PanelObjectName = "LauncherUIPanel";
    private const string CloseButtonText = "\uB2EB\uAE30";
    private const string LaunchActionText = "\uBC1C\uC0AC";
    private const string HangarTitleText = "\uACA9\uB0A9\uCE78";
    private const string InventoryTitleText = "\uC6B0\uC8FC\uC120\n\uC778\uBCA4\uD1A0\uB9AC";
    private const string UnassignedShipNameText = "\uBBF8\uC9C0\uC815";
    private const string OwnedShipPrefixText = "\uBCF4\uC720";
    private const string FuelTitleText = "\uC5F0\uB8CC\uB7C9";
    private const int InventoryColumns = 6;
    private const int InventoryRows = 8;

    private static readonly Color PanelBackgroundColor = new Color(0.97f, 0.98f, 0.99f, 0.98f);
    private static readonly Color BorderColor = new Color(0.12f, 0.22f, 0.31f, 1f);
    private static readonly Color SectionColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color SectionCellColor = new Color(0.23f, 0.43f, 0.58f, 0.78f);
    private static readonly Color SectionTextColor = Color.white;
    private static readonly Color DisabledButtonColor = new Color(0.36f, 0.41f, 0.46f, 0.92f);
    private static readonly Color ArrowButtonColor = new Color(0.18f, 0.33f, 0.46f, 0.92f);

    private static LauncherSelectionPresenter instance;
    private static Sprite cachedCircularButtonSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    public event Action<Transform> launchRequested;

    private Canvas hostCanvas;
    private RectTransform panelRect;
    private RectTransform hangarRect;
    private RectTransform inventoryRect;
    private RectTransform inventoryGridRect;
    private RectTransform fuelRect;
    private Button closeButton;
    private Button previousShipButton;
    private Button nextShipButton;
    private Button launchButton;
    private Image closeButtonBackground;
    private Image previousShipButtonBackground;
    private Image nextShipButtonBackground;
    private Image launchButtonBackground;
    private TextMeshProUGUI closeButtonLabel;
    private TextMeshProUGUI previousShipButtonLabel;
    private TextMeshProUGUI nextShipButtonLabel;
    private TextMeshProUGUI launchButtonLabel;
    private TextMeshProUGUI hangarTitleLabel;
    private TextMeshProUGUI hangarShipNameLabel;
    private TextMeshProUGUI hangarCountLabel;
    private TextMeshProUGUI inventoryTitleLabel;
    private TextMeshProUGUI inventoryCapacityLabel;
    private TextMeshProUGUI fuelLabel;
    private readonly List<Image> inventoryCells = new List<Image>();
    private Transform focusedLauncher;
    private int lastCanvasUpdateFrame = -1;
    private Color accentColor = new Color(0.18f, 0.6f, 0.95f, 0.95f);
    private LauncherLaunchController launchController;

    public void Configure(Vector2 _, Vector2 __, string ___, Color color)
    {
        accentColor = color;
        ApplyVisualConfiguration();
        UpdatePanelState();
    }

    private void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
        }

        launchController = GetComponent<LauncherLaunchController>();
        EnsurePanel();
        HidePanel();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        HidePanel();
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (panelRect != null)
        {
            Destroy(panelRect.gameObject);
            panelRect = null;
        }
    }

    protected override void LateUpdate()
    {
        if (panelRect == null)
        {
            EnsurePanel();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedLauncher = LauncherLaunchUtility.ResolveFocusedLauncher(focused);
        UpdatePanelState();
    }

    private void HandleWillRenderCanvases()
    {
        if (lastCanvasUpdateFrame == Time.frameCount)
        {
            return;
        }

        lastCanvasUpdateFrame = Time.frameCount;
        UpdatePanelState();
    }

    private void EnsurePanel()
    {
        if (panelRect != null)
        {
            return;
        }

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        GameObject panelObject = CreateUiObject(PanelObjectName, hostCanvas.transform, typeof(RectTransform), typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(panelRect, new Vector2(0.69f, 0.03f), new Vector2(0.99f, 0.97f));
        panelRect.SetAsLastSibling();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, BorderColor, new Vector2(2f, -2f));

        CreateCloseButton(panelObject.transform);
        CreateHangarSection(panelObject.transform);
        CreateShipCycleButtons(panelObject.transform);
        CreateInventorySection(panelObject.transform);
        CreateFuelSection(panelObject.transform);
        CreateLaunchButton(panelObject.transform);

        ApplyVisualConfiguration();
    }

    private void CreateCloseButton(Transform parent)
    {
        closeButton = CreateButton(
            parent,
            "LauncherUICloseButton",
            CloseButtonText,
            new Vector2(0.82f, 0.89f),
            new Vector2(0.97f, 0.99f),
            28f,
            out closeButtonBackground,
            out closeButtonLabel,
            useCircularSprite: true);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateHangarSection(Transform parent)
    {
        hangarRect = CreateSection(parent, "LauncherUIHangarSection", new Vector2(0.05f, 0.12f), new Vector2(0.23f, 0.86f));
        hangarShipNameLabel = CreateLabel(hangarRect, "HangarShipName", UnassignedShipNameText, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.76f), 24f, FontStyles.Bold, TextAlignmentOptions.Center);
        hangarCountLabel = CreateLabel(hangarRect, "HangarCount", OwnedShipPrefixText + " 0", new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.56f), 20f, FontStyles.Normal, TextAlignmentOptions.Center);
        hangarTitleLabel = CreateLabel(hangarRect, "HangarTitle", HangarTitleText, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.22f), 30f, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void CreateShipCycleButtons(Transform parent)
    {
        previousShipButton = CreateButton(
            parent,
            "LauncherUIPreviousShipButton",
            "<",
            new Vector2(0.005f, 0.45f),
            new Vector2(0.045f, 0.55f),
            34f,
            out previousShipButtonBackground,
            out previousShipButtonLabel,
            useCircularSprite: false);
        previousShipButton.onClick.AddListener(HandlePreviousShipClicked);

        nextShipButton = CreateButton(
            parent,
            "LauncherUINextShipButton",
            ">",
            new Vector2(0.245f, 0.45f),
            new Vector2(0.295f, 0.55f),
            34f,
            out nextShipButtonBackground,
            out nextShipButtonLabel,
            useCircularSprite: false);
        nextShipButton.onClick.AddListener(HandleNextShipClicked);
    }

    private void CreateInventorySection(Transform parent)
    {
        inventoryRect = CreateSection(parent, "LauncherUIInventorySection", new Vector2(0.34f, 0.12f), new Vector2(0.72f, 0.86f));

        inventoryGridRect = new GameObject("InventoryGrid", typeof(RectTransform)).GetComponent<RectTransform>();
        inventoryGridRect.SetParent(inventoryRect, false);
        ConfigureStretchRect(inventoryGridRect, Vector2.zero, Vector2.one);

        CreateInventoryCells();
        inventoryTitleLabel = CreateLabel(inventoryGridRect, "InventoryTitle", InventoryTitleText, new Vector2(0.18f, 0.40f), new Vector2(0.82f, 0.60f), 28f, FontStyles.Bold, TextAlignmentOptions.Center);
        inventoryCapacityLabel = CreateLabel(inventoryGridRect, "InventoryCapacity", "Capacity 0", new Vector2(0.18f, 0.31f), new Vector2(0.82f, 0.39f), 18f, FontStyles.Normal, TextAlignmentOptions.Center);
    }

    private void CreateFuelSection(Transform parent)
    {
        fuelRect = CreateSection(parent, "LauncherUIFuelSection", new Vector2(0.81f, 0.46f), new Vector2(0.97f, 0.80f));
        fuelLabel = CreateLabel(fuelRect, "FuelLabel", FuelTitleText + "\n0/0", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), 28f, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void CreateLaunchButton(Transform parent)
    {
        launchButton = CreateButton(
            parent,
            "LauncherUILaunchButton",
            LaunchActionText,
            new Vector2(0.80f, 0.07f),
            new Vector2(0.97f, 0.29f),
            38f,
            out launchButtonBackground,
            out launchButtonLabel,
            useCircularSprite: true);
        launchButton.onClick.AddListener(HandleLaunchClicked);
    }

    private void CreateInventoryCells()
    {
        inventoryCells.Clear();
        if (inventoryGridRect == null)
        {
            return;
        }

        for (int row = 0; row < InventoryRows; row++)
        {
            for (int column = 0; column < InventoryColumns; column++)
            {
                GameObject cellObject = CreateUiObject($"InventoryCell_{row}_{column}", inventoryGridRect, typeof(RectTransform), typeof(Image));
                RectTransform cellRect = cellObject.GetComponent<RectTransform>();
                float minX = column / (float)InventoryColumns;
                float maxX = (column + 1f) / InventoryColumns;
                float maxY = 1f - row / (float)InventoryRows;
                float minY = 1f - (row + 1f) / InventoryRows;
                ConfigureStretchRect(cellRect, new Vector2(minX, minY), new Vector2(maxX, maxY));

                Image cellImage = cellObject.GetComponent<Image>();
                cellImage.color = SectionCellColor;
                cellImage.raycastTarget = false;
                ApplyOutline(cellObject, BorderColor, new Vector2(1.5f, -1.5f));
                inventoryCells.Add(cellImage);
            }
        }
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveLauncherCloseFocus(focusedLauncher);
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (nextFocus != null)
            {
                CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            }

            focusManager.SetFocus(nextFocus);
            return;
        }

        focusedLauncher = null;
        HidePanel();
    }

    private void HandlePreviousShipClicked()
    {
        Debug.Log("LauncherSelectionPresenter: only one launchable spaceship type is currently available.");
    }

    private void HandleNextShipClicked()
    {
        Debug.Log("LauncherSelectionPresenter: only one launchable spaceship type is currently available.");
    }

    private void HandleLaunchClicked()
    {
        if (focusedLauncher == null) return;
        if (LauncherSpaceshipStock.GetAvailableSpaceshipCount(focusedLauncher) <= 0) return;
        launchRequested?.Invoke(focusedLauncher);
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        if (focusedLauncher == null)
        {
            HidePanel();
            return;
        }

        GameObject spaceshipPrefab = ResolveSelectedSpaceshipPrefab();
        string shipDisplayName = ResolveShipDisplayName(spaceshipPrefab);
        int availableSpaceships = LauncherSpaceshipStock.GetAvailableSpaceshipCount(focusedLauncher);
        int inventoryCapacity = ResolveInventoryCapacity(spaceshipPrefab);
        int fuelCapacity = ResolveFuelCapacity(spaceshipPrefab);
        bool canLaunch = availableSpaceships > 0;

        if (hangarTitleLabel != null) hangarTitleLabel.SetText(HangarTitleText);
        if (hangarShipNameLabel != null) hangarShipNameLabel.SetText(shipDisplayName);
        if (hangarCountLabel != null) hangarCountLabel.SetText(OwnedShipPrefixText + $" {Mathf.Max(0, availableSpaceships)}");
        if (inventoryTitleLabel != null) inventoryTitleLabel.SetText(InventoryTitleText);
        if (inventoryCapacityLabel != null) inventoryCapacityLabel.SetText($"Capacity {Mathf.Max(0, inventoryCapacity)}");
        if (fuelLabel != null) fuelLabel.SetText(FuelTitleText + $"\n{fuelCapacity}/{fuelCapacity}");
        if (launchButtonLabel != null) launchButtonLabel.SetText(LaunchActionText);

        if (launchButton != null)
        {
            launchButton.interactable = canLaunch;
        }

        if (launchButtonBackground != null)
        {
            launchButtonBackground.color = canLaunch ? accentColor : DisabledButtonColor;
        }

        if (previousShipButton != null)
        {
            previousShipButton.interactable = false;
        }

        if (nextShipButton != null)
        {
            nextShipButton.interactable = false;
        }

        if (previousShipButtonBackground != null)
        {
            previousShipButtonBackground.color = DisabledButtonColor;
        }

        if (nextShipButtonBackground != null)
        {
            nextShipButtonBackground.color = DisabledButtonColor;
        }

        panelRect.SetAsLastSibling();
        if (!panelRect.gameObject.activeSelf)
        {
            panelRect.gameObject.SetActive(true);
        }
    }

    private void HidePanel()
    {
        if (panelRect != null && panelRect.gameObject.activeSelf)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void ApplyVisualConfiguration()
    {
        ApplyFont(closeButtonLabel);
        ApplyFont(previousShipButtonLabel);
        ApplyFont(nextShipButtonLabel);
        ApplyFont(launchButtonLabel);
        ApplyFont(hangarTitleLabel);
        ApplyFont(hangarShipNameLabel);
        ApplyFont(hangarCountLabel);
        ApplyFont(inventoryTitleLabel);
        ApplyFont(inventoryCapacityLabel);
        ApplyFont(fuelLabel);

        if (closeButtonBackground != null)
        {
            closeButtonBackground.color = accentColor;
        }

        if (previousShipButtonBackground != null)
        {
            previousShipButtonBackground.color = DisabledButtonColor;
        }

        if (nextShipButtonBackground != null)
        {
            nextShipButtonBackground.color = DisabledButtonColor;
        }

        if (launchButtonBackground != null)
        {
            bool canLaunch = launchButton != null && launchButton.interactable;
            launchButtonBackground.color = canLaunch ? accentColor : DisabledButtonColor;
        }
    }

    private bool IsPointerOverVisiblePanel()
    {
        if (panelRect == null || !panelRect.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Camera uiCamera = hostCanvas != null && hostCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? hostCanvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPosition, uiCamera);
    }

    private GameObject ResolveSelectedSpaceshipPrefab()
    {
        if (launchController == null)
        {
            launchController = GetComponent<LauncherLaunchController>();
        }

        if (launchController != null)
        {
            return launchController.GetConfiguredSpaceshipPrefab();
        }

        return LauncherSpawner.ResolveSpaceshipPrefab(null);
    }

    private static string ResolveShipDisplayName(GameObject spaceshipPrefab)
    {
        if (spaceshipPrefab == null)
        {
            return UnassignedShipNameText;
        }

        string displayName = spaceshipPrefab.name ?? string.Empty;
        displayName = displayName.Replace("(Clone)", string.Empty).Trim();
        if (displayName.EndsWith("Prefab", StringComparison.OrdinalIgnoreCase))
        {
            displayName = displayName.Substring(0, displayName.Length - "Prefab".Length).Trim();
        }

        return string.IsNullOrWhiteSpace(displayName) ? UnassignedShipNameText : displayName;
    }

    private static int ResolveInventoryCapacity(GameObject spaceshipPrefab)
    {
        if (spaceshipPrefab == null)
        {
            return 0;
        }

        Inventory inventory = spaceshipPrefab.GetComponent<Inventory>();
        return inventory != null ? Mathf.Max(0, inventory.capacity) : 0;
    }

    private static int ResolveFuelCapacity(GameObject spaceshipPrefab)
    {
        if (spaceshipPrefab == null)
        {
            return 0;
        }

        SpaceshipFuel fuel = spaceshipPrefab.GetComponent<SpaceshipFuel>();
        if (fuel == null)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.CeilToInt(fuel.MaxFuel));
    }

    private static Transform ResolveLauncherCloseFocus(Transform launcher)
    {
        if (launcher == null)
        {
            return null;
        }

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = launcher.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite.transform;
        }

        return null;
    }

    private RectTransform CreateSection(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject sectionObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        ConfigureStretchRect(sectionRect, anchorMin, anchorMax);

        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = SectionColor;
        ApplyOutline(sectionObject, BorderColor, new Vector2(2f, -2f));
        return sectionRect;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string labelText,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        out Image background,
        out TextMeshProUGUI label,
        bool useCircularSprite)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);

        background = buttonObject.GetComponent<Image>();
        background.color = useCircularSprite ? accentColor : ArrowButtonColor;
        if (useCircularSprite)
        {
            Sprite circularSprite = ResolveCircularButtonSprite();
            if (circularSprite != null)
            {
                background.sprite = circularSprite;
                background.type = Image.Type.Simple;
                background.preserveAspect = true;
            }
        }
        else
        {
            ApplyOutline(buttonObject, BorderColor, new Vector2(2f, -2f));
        }

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject labelObject = CreateUiObject(name + "Label", buttonObject.transform, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, Vector2.zero, Vector2.one);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.SetText(labelText);
        label.color = SectionTextColor;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(16f, fontSize * 0.45f);
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        ApplyFont(label);
        return button;
    }

    private TextMeshProUGUI CreateLabel(
        RectTransform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, anchorMin, anchorMax);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.SetText(text);
        label.color = SectionTextColor;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(14f, fontSize * 0.45f);
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        ApplyFont(label);
        return label;
    }

    private void ApplyFont(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        FocusManager focusManager = GetFocusManager();
        if (focusManager != null && focusManager.focusInfoText != null && focusManager.focusInfoText.font != null)
        {
            label.font = focusManager.focusInfoText.font;
        }
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 effectDistance)
    {
        if (target == null)
        {
            return;
        }

        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(target);
        if (outline == null)
        {
            return;
        }

        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = false;
    }

    private static void ConfigureStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Sprite ResolveCircularButtonSprite()
    {
        if (cachedCircularButtonSprite != null)
        {
            return cachedCircularButtonSprite;
        }

        const int TextureSize = 128;
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "LauncherUIButtonCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[TextureSize * TextureSize];
        float radius = (TextureSize - 2f) * 0.5f;
        Vector2 center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                int index = y * TextureSize + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = distance <= radius ? (byte)255 : (byte)0;
                pixels[index] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        cachedCircularButtonSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        return cachedCircularButtonSprite;
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


