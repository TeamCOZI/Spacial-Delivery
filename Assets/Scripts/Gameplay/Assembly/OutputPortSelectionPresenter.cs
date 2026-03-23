using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(33996)]
[DisallowMultipleComponent]
public class OutputPortSelectionPresenter : FocusEventSubscriber
{
    private const string PanelObjectName = "OutputPortUIPanel";
    private const string CloseButtonText = "\uB2EB\uAE30";
    private const string DefaultTitleText = "OUTPUT PORT";
    private const string DefaultSubtitleText = "-";
    private const string EmptyIconText = "PORT";
    private const string ItemListTitleText = "\uC544\uC774\uD15C \uBAA9\uB85D";
    private const string TotalProductionTitleText = "\uC0DD\uC0B0\uB7C9";
    private const string TotalConsumptionTitleText = "\uC18C\uBAA8\uB7C9";
    private const string SelfProductionTitleText = "\uC790\uCCB4 \uC0DD\uC0B0";
    private const string LogisticsSupportTitleText = "\uBB3C\uB958 \uC9C0\uC6D0";
    private const string SelfConsumptionTitleText = "\uC790\uCCB4 \uC18C\uBAA8";
    private const string LogisticsConsumptionTitleText = "\uBB3C\uB958 \uC18C\uBAA8";
    private const int PanelSortingOrderOffset = 200;

    private static readonly string[] FilterLabels =
    {
        "\uC804\uCCB4",
        "\uC7AC\uB8CC",
        "\uBD80\uD488",
        "\uC5F0\uB8CC",
        "\uAE30\uD0C0"
    };

    private static readonly Color PanelBackgroundColor = new Color(0.97f, 0.98f, 0.99f, 0.985f);
    private static readonly Color BorderColor = new Color(0.11f, 0.20f, 0.28f, 1f);
    private static readonly Color PrimaryBlue = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color SecondaryBlue = new Color(0.24f, 0.47f, 0.63f, 0.98f);
    private static readonly Color Orange = new Color(0.92f, 0.70f, 0.56f, 0.98f);
    private static readonly Color OrangeAlt = new Color(0.95f, 0.75f, 0.61f, 0.98f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);
    private static readonly Color FilterIdleColor = new Color(0.18f, 0.36f, 0.50f, 1f);
    private static readonly Color FilterActiveColor = new Color(0.92f, 0.70f, 0.56f, 1f);
    private static readonly Color FilterIdleText = Color.white;
    private static readonly Color FilterActiveText = new Color(0.09f, 0.07f, 0.07f, 1f);
    private static readonly Color PlaceholderTextColor = new Color(0.92f, 0.96f, 1f, 0.96f);

    private static OutputPortSelectionPresenter instance;
    private static Sprite cachedCircularButtonSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private GraphicRaycaster panelRaycaster;
    private RectTransform panelRect;
    private Image panelImage;
    private Button closeButton;
    private Image closeButtonBackground;
    private TextMeshProUGUI closeButtonLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private RectTransform iconSectionRect;
    private Image itemIconImage;
    private TextMeshProUGUI itemIconFallbackLabel;
    private TextMeshProUGUI totalProductionValueLabel;
    private TextMeshProUGUI totalConsumptionValueLabel;
    private TextMeshProUGUI selfProductionValueLabel;
    private TextMeshProUGUI logisticsSupportValueLabel;
    private TextMeshProUGUI selfConsumptionValueLabel;
    private TextMeshProUGUI logisticsConsumptionValueLabel;
    private readonly List<Button> filterButtons = new List<Button>();
    private readonly List<Image> filterButtonBackgrounds = new List<Image>();
    private readonly List<TextMeshProUGUI> filterButtonLabels = new List<TextMeshProUGUI>();
    private int selectedFilterIndex;
    private RectTransform listSectionRect;
    private ScrollRect itemListScrollRect;
    private RectTransform itemListContentRect;
    private TextMeshProUGUI itemListTitleLabel;
    private TextMeshProUGUI itemListPlaceholderLabel;
    private AssemblyOutputPortFocus focusedOutputPort;
    private int lastCanvasUpdateFrame = -1;

    private void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
        }

        EnsurePanel();
        SetPanelVisible(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
        EnsurePanel();
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        SetPanelVisible(false);
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
        if (focusedOutputPort != null)
        {
            UpdatePanelState();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedOutputPort = focused != null ? focused.GetComponent<AssemblyOutputPortFocus>() : null;
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

        GameObject panelObject = CreateUiObject(PanelObjectName, hostCanvas.transform, typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.40f, 0.08f);
        panelRect.anchorMax = new Vector2(0.98f, 0.96f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        panelCanvas = panelObject.GetComponent<Canvas>();
        panelRaycaster = panelObject.GetComponent<GraphicRaycaster>();
        EnsurePanelDrawOrder();

        panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, BorderColor, new Vector2(2f, -2f));

        CreateHeader(panelObject.transform);
        CreateIconSection(panelObject.transform);
        CreateMetricSections(panelObject.transform);
        CreateFilterRow(panelObject.transform);
        CreateItemListSection(panelObject.transform);
    }

    private void CreateHeader(Transform parent)
    {
        titleLabel = CreateLabel(parent, "OutputPortTitle", DefaultTitleText, new Vector2(0.04f, 0.86f), new Vector2(0.78f, 0.96f), 30f, FontStyles.Bold, TextAlignmentOptions.Left, BorderColor);
        subtitleLabel = CreateLabel(parent, "OutputPortSubtitle", DefaultSubtitleText, new Vector2(0.04f, 0.81f), new Vector2(0.78f, 0.87f), 16f, FontStyles.Normal, TextAlignmentOptions.Left, BorderColor);

        closeButton = CreateButton(
            parent,
            "OutputPortCloseButton",
            CloseButtonText,
            new Vector2(0.86f, 0.855f),
            new Vector2(0.97f, 0.98f),
            24f,
            out closeButtonBackground,
            out closeButtonLabel,
            useCircularSprite: true,
            backgroundColor: PrimaryBlue,
            textColor: WhiteText,
            outlined: false);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateIconSection(Transform parent)
    {
        iconSectionRect = CreateSection(parent, "OutputPortIconSection", new Vector2(0.04f, 0.56f), new Vector2(0.28f, 0.79f), PrimaryBlue);

        GameObject iconImageObject = CreateUiObject("OutputPortIconImage", iconSectionRect, typeof(RectTransform), typeof(Image));
        RectTransform iconImageRect = iconImageObject.GetComponent<RectTransform>();
        ConfigureStretchRect(iconImageRect, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));
        itemIconImage = iconImageObject.GetComponent<Image>();
        itemIconImage.preserveAspect = true;
        itemIconImage.color = Color.white;

        itemIconFallbackLabel = CreateLabel(iconSectionRect, "OutputPortIconFallback", EmptyIconText, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f), 28f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
    }

    private void CreateMetricSections(Transform parent)
    {
        RectTransform totalProductionRect = CreateSection(parent, "OutputPortTotalProduction", new Vector2(0.33f, 0.72f), new Vector2(0.67f, 0.79f), PrimaryBlue);
        CreateStatTitle(totalProductionRect, "TotalProductionTitle", TotalProductionTitleText, WhiteText, 22f);
        totalProductionValueLabel = CreateStatValue(totalProductionRect, "TotalProductionValue", WhiteText, 24f);

        RectTransform totalConsumptionRect = CreateSection(parent, "OutputPortTotalConsumption", new Vector2(0.67f, 0.72f), new Vector2(0.96f, 0.79f), Orange);
        CreateStatTitle(totalConsumptionRect, "TotalConsumptionTitle", TotalConsumptionTitleText, DarkText, 22f);
        totalConsumptionValueLabel = CreateStatValue(totalConsumptionRect, "TotalConsumptionValue", DarkText, 24f);

        RectTransform selfProductionRect = CreateSection(parent, "OutputPortSelfProduction", new Vector2(0.33f, 0.56f), new Vector2(0.55f, 0.72f), SecondaryBlue);
        CreateStatTitle(selfProductionRect, "SelfProductionTitle", SelfProductionTitleText, WhiteText, 18f);
        selfProductionValueLabel = CreateStatValue(selfProductionRect, "SelfProductionValue", WhiteText, 26f);

        RectTransform logisticsSupportRect = CreateSection(parent, "OutputPortLogisticsSupport", new Vector2(0.55f, 0.56f), new Vector2(0.67f, 0.72f), SecondaryBlue);
        CreateStatTitle(logisticsSupportRect, "LogisticsSupportTitle", LogisticsSupportTitleText, WhiteText, 16f);
        logisticsSupportValueLabel = CreateStatValue(logisticsSupportRect, "LogisticsSupportValue", WhiteText, 24f);

        RectTransform selfConsumptionRect = CreateSection(parent, "OutputPortSelfConsumption", new Vector2(0.67f, 0.56f), new Vector2(0.84f, 0.72f), OrangeAlt);
        CreateStatTitle(selfConsumptionRect, "SelfConsumptionTitle", SelfConsumptionTitleText, DarkText, 17f);
        selfConsumptionValueLabel = CreateStatValue(selfConsumptionRect, "SelfConsumptionValue", DarkText, 24f);

        RectTransform logisticsConsumptionRect = CreateSection(parent, "OutputPortLogisticsConsumption", new Vector2(0.84f, 0.56f), new Vector2(0.96f, 0.72f), OrangeAlt);
        CreateStatTitle(logisticsConsumptionRect, "LogisticsConsumptionTitle", LogisticsConsumptionTitleText, DarkText, 16f);
        logisticsConsumptionValueLabel = CreateStatValue(logisticsConsumptionRect, "LogisticsConsumptionValue", DarkText, 22f);
    }

    private void CreateFilterRow(Transform parent)
    {
        RectTransform filterSectionRect = CreateSection(parent, "OutputPortFilterSection", new Vector2(0.04f, 0.46f), new Vector2(0.96f, 0.53f), PrimaryBlue);

        filterButtons.Clear();
        filterButtonBackgrounds.Clear();
        filterButtonLabels.Clear();

        const float buttonWidth = 0.11f;
        const float gap = 0.015f;
        float currentMinX = 0.03f;
        for (int i = 0; i < FilterLabels.Length; i++)
        {
            float minX = currentMinX;
            float maxX = Mathf.Min(0.97f, minX + buttonWidth);
            int filterIndex = i;
            Button filterButton = CreateButton(
                filterSectionRect,
                "OutputPortFilter_" + i,
                FilterLabels[i],
                new Vector2(minX, 0.18f),
                new Vector2(maxX, 0.82f),
                15f,
                out Image background,
                out TextMeshProUGUI label,
                useCircularSprite: true,
                backgroundColor: FilterIdleColor,
                textColor: FilterIdleText,
                outlined: false);
            filterButton.onClick.AddListener(() => HandleFilterClicked(filterIndex));
            filterButtons.Add(filterButton);
            filterButtonBackgrounds.Add(background);
            filterButtonLabels.Add(label);
            currentMinX = maxX + gap;
        }

        UpdateFilterVisuals();
    }

    private void CreateItemListSection(Transform parent)
    {
        listSectionRect = CreateSection(parent, "OutputPortListSection", new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.42f), PrimaryBlue);
        itemListTitleLabel = CreateLabel(listSectionRect, "OutputPortListTitle", ItemListTitleText, new Vector2(0.26f, 0.70f), new Vector2(0.74f, 0.90f), 28f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);

        GameObject scrollObject = CreateUiObject("OutputPortItemScroll", listSectionRect, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        ConfigureStretchRect(scrollRectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.62f));
        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(1f, 1f, 1f, 0.06f);
        Mask scrollMask = scrollObject.GetComponent<Mask>();
        scrollMask.showMaskGraphic = true;

        GameObject contentObject = CreateUiObject("OutputPortItemContent", scrollRectTransform, typeof(RectTransform));
        itemListContentRect = contentObject.GetComponent<RectTransform>();
        itemListContentRect.anchorMin = Vector2.zero;
        itemListContentRect.anchorMax = Vector2.one;
        itemListContentRect.offsetMin = Vector2.zero;
        itemListContentRect.offsetMax = Vector2.zero;
        itemListContentRect.localScale = Vector3.one;

        itemListScrollRect = scrollObject.GetComponent<ScrollRect>();
        itemListScrollRect.viewport = scrollRectTransform;
        itemListScrollRect.content = itemListContentRect;
        itemListScrollRect.horizontal = false;
        itemListScrollRect.vertical = true;
        itemListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        itemListScrollRect.scrollSensitivity = 24f;

        itemListPlaceholderLabel = CreateLabel(itemListContentRect, "OutputPortListPlaceholder", string.Empty, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f), 18f, FontStyles.Normal, TextAlignmentOptions.Center, PlaceholderTextColor);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = focusedOutputPort != null ? focusedOutputPort.ResolveCloseFocus() : null;
        focusedOutputPort = null;
        SetPanelVisible(false);

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (nextFocus != null)
            {
                CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            }

            focusManager.SetFocus(nextFocus);
        }
    }

    private void HandleFilterClicked(int filterIndex)
    {
        selectedFilterIndex = Mathf.Clamp(filterIndex, 0, FilterLabels.Length - 1);
        UpdateFilterVisuals();
        RefreshPanelContent();
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        if (focusedOutputPort == null)
        {
            SetPanelVisible(false);
            return;
        }

        EnsurePanelDrawOrder();
        SetPanelVisible(true);
        RefreshPanelContent();
    }

    private void RefreshPanelContent()
    {
        if (focusedOutputPort == null)
        {
            return;
        }

        string title = focusedOutputPort.DisplayName;
        if (titleLabel != null)
        {
            titleLabel.SetText(string.IsNullOrWhiteSpace(title) ? DefaultTitleText : title.ToUpperInvariant());
        }

        if (subtitleLabel != null)
        {
            string satelliteName = focusedOutputPort.OwnerSatellite != null ? focusedOutputPort.OwnerSatellite.name : "-";
            string occupiedText = focusedOutputPort.IsOccupied ? "\uC5F0\uACB0 \uC911" : "\uB300\uAE30 \uC911";
            string side = focusedOutputPort.SideLabel;
            string mappingLabel = focusedOutputPort.HasCellMapping
                ? $"source {focusedOutputPort.SourceCell} / boundary {focusedOutputPort.MappedCell}"
                : "mapping unavailable";
            subtitleLabel.SetText($"{focusedOutputPort.OwnerLabel} / {satelliteName} / {side} / {occupiedText} / {mappingLabel}");
        }

        Sprite iconSprite = focusedOutputPort.DisplayIcon;
        bool hasSprite = iconSprite != null;
        if (itemIconImage != null)
        {
            itemIconImage.sprite = iconSprite;
            itemIconImage.enabled = hasSprite;
        }
        if (itemIconFallbackLabel != null)
        {
            itemIconFallbackLabel.gameObject.SetActive(!hasSprite);
            if (!hasSprite)
            {
                itemIconFallbackLabel.SetText(EmptyIconText);
            }
        }

        SetMetricValuesUnavailable();

        if (itemListTitleLabel != null)
        {
            itemListTitleLabel.SetText(ItemListTitleText);
        }

        if (itemListPlaceholderLabel != null)
        {
            itemListPlaceholderLabel.SetText(ResolvePlaceholderText());
        }
    }

    private string ResolvePlaceholderText()
    {
        if (focusedOutputPort == null)
        {
            return string.Empty;
        }

        string filterName = FilterLabels[Mathf.Clamp(selectedFilterIndex, 0, FilterLabels.Length - 1)];
        string occupiedText = focusedOutputPort.IsOccupied ? "\uC608" : "\uC544\uB2C8\uC624";
        return
            $"{filterName} \uD544\uD130\n\n" +
            $"Owner : {focusedOutputPort.OwnerLabel}\n" +
            $"Side : {focusedOutputPort.SideLabel}\n" +
            $"Occupied : {occupiedText}\n\n" +
            "\uC544\uC9C1 \uC544\uC774\uD15C / \uBB3C\uB958 \uC218\uCE58 \uB7F0\uD0C0\uC784 \uB370\uC774\uD130\uB294 \uC5F0\uACB0\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
    }

    private void SetMetricValuesUnavailable()
    {
        SetMetricText(totalProductionValueLabel, "-");
        SetMetricText(totalConsumptionValueLabel, "-");
        SetMetricText(selfProductionValueLabel, "-");
        SetMetricText(logisticsSupportValueLabel, "-");
        SetMetricText(selfConsumptionValueLabel, "-");
        SetMetricText(logisticsConsumptionValueLabel, "-");
    }

    private static void SetMetricText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.SetText(value);
        }
    }

    private void UpdateFilterVisuals()
    {
        for (int i = 0; i < filterButtons.Count; i++)
        {
            bool isSelected = i == selectedFilterIndex;
            if (filterButtonBackgrounds.Count > i && filterButtonBackgrounds[i] != null)
            {
                filterButtonBackgrounds[i].color = isSelected ? FilterActiveColor : FilterIdleColor;
            }

            if (filterButtonLabels.Count > i && filterButtonLabels[i] != null)
            {
                filterButtonLabels[i].color = isSelected ? FilterActiveText : FilterIdleText;
            }
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
        Camera uiCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? panelCanvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPosition, uiCamera);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf != visible)
        {
            panelRect.gameObject.SetActive(visible);
        }
    }

    private void EnsurePanelDrawOrder()
    {
        if (panelCanvas == null)
        {
            return;
        }

        if (hostCanvas == null)
        {
            hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = ResolvePanelSortingOrder();

        if (hostCanvas != null)
        {
            panelCanvas.sortingLayerID = hostCanvas.sortingLayerID;
            panelCanvas.renderMode = hostCanvas.renderMode;
            panelCanvas.worldCamera = hostCanvas.worldCamera;
            panelCanvas.planeDistance = hostCanvas.planeDistance;
            panelCanvas.additionalShaderChannels = hostCanvas.additionalShaderChannels;
        }

        if (panelRaycaster != null)
        {
            panelRaycaster.ignoreReversedGraphics = true;
        }
    }

    private int ResolvePanelSortingOrder()
    {
        if (hostCanvas == null)
        {
            return PanelSortingOrderOffset;
        }

        return hostCanvas.sortingOrder + PanelSortingOrderOffset;
    }

    private RectTransform CreateSection(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
    {
        GameObject sectionObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        ConfigureStretchRect(sectionRect, anchorMin, anchorMax);

        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = backgroundColor;
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
        bool useCircularSprite,
        Color backgroundColor,
        Color textColor,
        bool outlined)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);

        background = buttonObject.GetComponent<Image>();
        background.color = backgroundColor;
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
        else if (outlined)
        {
            ApplyOutline(buttonObject, BorderColor, new Vector2(1.5f, -1.5f));
        }

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject labelObject = CreateUiObject(name + "Label", buttonObject.transform, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, Vector2.zero, Vector2.one);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.SetText(labelText);
        label.color = textColor;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(12f, fontSize * 0.5f);
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        ApplyFont(label);
        return button;
    }

    private TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color textColor)
    {
        GameObject labelObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, anchorMin, anchorMax);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.SetText(text);
        label.color = textColor;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(12f, fontSize * 0.45f);
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        ApplyFont(label);
        return label;
    }

    private TextMeshProUGUI CreateStatTitle(RectTransform parent, string name, string text, Color textColor, float fontSize)
    {
        return CreateLabel(parent, name, text, new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.94f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
    }

    private TextMeshProUGUI CreateStatValue(RectTransform parent, string name, Color textColor, float fontSize)
    {
        return CreateLabel(parent, name, "-", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.56f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
    }

    private void ApplyFont(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
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

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "OutputPortUIButtonCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[textureSize * textureSize];
        float radius = (textureSize - 2f) * 0.5f;
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                int index = y * textureSize + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = distance <= radius ? (byte)255 : (byte)0;
                pixels[index] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        cachedCircularButtonSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        return cachedCircularButtonSprite;
    }
}
