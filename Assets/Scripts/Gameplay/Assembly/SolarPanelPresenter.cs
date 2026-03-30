using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(34001)]
[DisallowMultipleComponent]
public class SolarPanelPresenter : FocusEventSubscriber
{
    private const string PanelObjectName = "SolarPanelUIPanel";
    private const int PanelSortingOrderOffset = 208;

    private static readonly Color PanelBackgroundColor = new Color(0.53f, 0.53f, 0.53f, 0.96f);
    private static readonly Color PanelBorderColor = new Color(0.92f, 0.92f, 0.92f, 0.28f);
    private static readonly Color LightCardColor = new Color(0.98f, 0.73f, 0.11f, 0.98f);
    private static readonly Color PowerCardColor = new Color(0.98f, 0.73f, 0.11f, 0.98f);
    private static readonly Color OrbitHeaderColor = new Color(0.74f, 0.90f, 0.64f, 1f);
    private static readonly Color TotalCardColor = new Color(0.98f, 0.73f, 0.11f, 0.98f);
    private static readonly Color ForecastCardColor = new Color(0.34f, 0.77f, 0.92f, 1f);
    private static readonly Color StatsCardColor = new Color(0.98f, 0.73f, 0.11f, 0.98f);
    private static readonly Color CloseButtonColor = new Color(0.97f, 0.20f, 0.13f, 1f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.09f, 0.09f, 0.09f, 1f);

    private static SolarPanelPresenter instance;
    private static Sprite cachedCircularButtonSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private Image closeButtonBackground;
    private TextMeshProUGUI lightIntensityValueLabel;
    private TextMeshProUGUI powerGenerationValueLabel;
    private TextMeshProUGUI orbitPeriodValueLabel;
    private TextMeshProUGUI cycleTotalValueLabel;
    private TextMeshProUGUI forecastValueLabel;
    private TextMeshProUGUI meanValueLabel;
    private TextMeshProUGUI maxValueLabel;
    private TextMeshProUGUI minValueLabel;
    private AssemblyPartFocus focusedPart;

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
        EnsurePanel();
    }

    protected override void OnDisable()
    {
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
        if (panelRect != null && panelRect.gameObject.activeInHierarchy)
        {
            RefreshPanelContent();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedPart = TryResolveFocusedSolarPanel(focused, out AssemblyPartFocus partFocus)
            ? partFocus
            : null;
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
        panelRect.anchorMin = new Vector2(0.52f, 0.16f);
        panelRect.anchorMax = new Vector2(0.98f, 0.94f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, PanelBorderColor, new Vector2(2f, -2f));

        panelCanvas = panelObject.GetComponent<Canvas>();
        EnsurePanelDrawOrder();

        CreateHeader(panelObject.transform);
        CreateMetrics(panelObject.transform);
    }

    private void CreateHeader(Transform parent)
    {
        titleLabel = CreateLabel(parent, "SolarPanelTitle", "SOLAR PANEL", new Vector2(0.03f, 0.91f), new Vector2(0.78f, 0.98f), 22f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        statusLabel = CreateLabel(parent, "SolarPanelStatus", string.Empty, new Vector2(0.03f, 0.85f), new Vector2(0.78f, 0.91f), 11f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        Button closeButton = CreateButton(parent, "SolarPanelClose", "X", new Vector2(0.90f, 0.89f), new Vector2(0.98f, 0.98f), 18f, out closeButtonBackground, out _, CloseButtonColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateMetrics(Transform parent)
    {
        RectTransform lightRect = CreateSection(parent, "SolarPanelLightRect", new Vector2(0.06f, 0.58f), new Vector2(0.42f, 0.84f), LightCardColor);
        CreateStatTitle(lightRect, "SolarPanelLightTitle", "LIGHT INTENSITY");
        lightIntensityValueLabel = CreateStatValue(lightRect, "SolarPanelLightValue", 28f);

        RectTransform generationRect = CreateSection(parent, "SolarPanelGenerationRect", new Vector2(0.58f, 0.58f), new Vector2(0.94f, 0.84f), PowerCardColor);
        CreateStatTitle(generationRect, "SolarPanelGenerationTitle", "POWER GENERATION");
        powerGenerationValueLabel = CreateStatValue(generationRect, "SolarPanelGenerationValue", 28f);

        RectTransform cycleRect = CreateSection(parent, "SolarPanelCycleRect", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.48f), new Color(0.14f, 0.42f, 0.58f, 0.98f));
        RectTransform cycleHeader = CreateSection(cycleRect, "SolarPanelCycleHeader", new Vector2(0.02f, 0.78f), new Vector2(0.98f, 0.96f), OrbitHeaderColor);
        orbitPeriodValueLabel = CreateLabel(cycleHeader, "SolarPanelOrbitPeriod", "ORBIT PERIOD -", new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.90f), 20f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        RectTransform totalRect = CreateSection(cycleRect, "SolarPanelCycleTotal", new Vector2(0.02f, 0.12f), new Vector2(0.44f, 0.74f), TotalCardColor);
        CreateStatTitle(totalRect, "SolarPanelTotalTitle", "TOTAL THIS CYCLE");
        cycleTotalValueLabel = CreateStatValue(totalRect, "SolarPanelTotalValue", 24f);

        RectTransform forecastRect = CreateSection(cycleRect, "SolarPanelCycleForecast", new Vector2(0.44f, 0.12f), new Vector2(0.76f, 0.74f), ForecastCardColor);
        CreateStatTitle(forecastRect, "SolarPanelForecastTitle", "FORECAST REMAINING");
        forecastValueLabel = CreateStatValue(forecastRect, "SolarPanelForecastValue", 24f);

        RectTransform statsRect = CreateSection(cycleRect, "SolarPanelCycleStats", new Vector2(0.78f, 0.12f), new Vector2(0.98f, 0.74f), StatsCardColor);
        meanValueLabel = CreateStackMetric(statsRect, "SolarPanelMeanValue", "MEAN", new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.92f));
        maxValueLabel = CreateStackMetric(statsRect, "SolarPanelMaxValue", "MAX", new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.62f));
        minValueLabel = CreateStackMetric(statsRect, "SolarPanelMinValue", "MIN", new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.32f));
    }

    private TextMeshProUGUI CreateStackMetric(RectTransform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform metricRect = CreateSection(parent, name + "Rect", anchorMin, anchorMax, new Color(1f, 0.90f, 0.54f, 1f));
        _ = CreateLabel(metricRect, name + "Title", labelText, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.90f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
        return CreateLabel(metricRect, name, "-", new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.52f), 18f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveCloseFocus();
        focusedPart = null;
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

    private Transform ResolveCloseFocus()
    {
        return focusedPart != null && focusedPart.OwnerSatellite != null
            ? focusedPart.OwnerSatellite.transform
            : null;
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        bool shouldShow = focusedPart != null;
        SetPanelVisible(shouldShow);
        if (shouldShow)
        {
            RefreshPanelContent();
        }
    }

    private void RefreshPanelContent()
    {
        if (focusedPart == null || !SolarPanelUtility.IsSolarPanelPart(focusedPart))
        {
            SetPanelVisible(false);
            return;
        }

        SolarPanelState state = ComponentUtility.GetOrAddComponent<SolarPanelState>(focusedPart.gameObject);
        string title = !string.IsNullOrWhiteSpace(focusedPart.SourcePart.partName) ? focusedPart.SourcePart.partName.ToUpperInvariant() : "SOLAR PANEL";
        if (titleLabel != null)
        {
            titleLabel.SetText(title);
        }

        if (statusLabel != null)
        {
            statusLabel.SetText(state.StatusLabel);
        }

        SetMetric(lightIntensityValueLabel, $"{state.CurrentLightIntensity:0.##}");
        SetMetric(powerGenerationValueLabel, state.CurrentGenerationAmount.ToString());
        SetMetric(orbitPeriodValueLabel, $"ORBIT PERIOD {state.OrbitPeriodSeconds:0.##}S");
        SetMetric(cycleTotalValueLabel, state.TotalGeneratedThisCycle.ToString());
        SetMetric(forecastValueLabel, state.ForecastRemainingGeneration.ToString());
        SetMetric(meanValueLabel, $"{state.MeanGenerationAmount:0.##}");
        SetMetric(maxValueLabel, state.MaximumGenerationAmount.ToString());
        SetMetric(minValueLabel, state.MinimumGenerationAmount.ToString());
    }

    private void EnsurePanelDrawOrder()
    {
        if (panelCanvas == null || hostCanvas == null)
        {
            return;
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingLayerID = hostCanvas.sortingLayerID;
        panelCanvas.sortingOrder = hostCanvas.sortingOrder + PanelSortingOrderOffset;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect == null)
        {
            return;
        }

        if (panelRect.gameObject.activeSelf != visible)
        {
            panelRect.gameObject.SetActive(visible);
        }
    }

    private bool IsPointerOverVisiblePanel()
    {
        if (panelRect == null || !panelRect.gameObject.activeInHierarchy || Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Camera uiCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? panelCanvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPosition, uiCamera);
    }

    private static bool TryResolveFocusedSolarPanel(Transform focused, out AssemblyPartFocus partFocus)
    {
        partFocus = null;
        if (focused == null)
        {
            return false;
        }

        if (focused.GetComponentInParent<AssemblyOutputPortFocus>() != null)
        {
            return false;
        }

        partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = focused.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null)
        {
            partFocus = null;
            return false;
        }

        if (!SolarPanelUtility.IsSolarPanelPart(partFocus.SourcePart))
        {
            partFocus = null;
            return false;
        }

        return true;
    }

    private static void SetMetric(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.SetText(value);
        }
    }

    private static RectTransform CreateSection(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
    {
        GameObject sectionObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        ConfigureStretchRect(sectionRect, anchorMin, anchorMax);
        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = backgroundColor;
        ApplyOutline(sectionObject, new Color(1f, 1f, 1f, 0.12f), Vector2.zero);
        return sectionRect;
    }

    private static TextMeshProUGUI CreateStatTitle(RectTransform parent, string name, string text)
    {
        return CreateLabel(parent, name, text, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.94f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
    }

    private static TextMeshProUGUI CreateStatValue(RectTransform parent, string name, float fontSize)
    {
        return CreateLabel(parent, name, "-", new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.58f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        GameObject labelObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, anchorMin, anchorMax);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(7f, fontSize * 0.55f);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, float fontSize, out Image backgroundImage, out TextMeshProUGUI label, Color backgroundColor, Color textColor, bool circular)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        if (circular)
        {
            Sprite circularSprite = ResolveCircularButtonSprite();
            if (circularSprite != null)
            {
                backgroundImage.sprite = circularSprite;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.preserveAspect = true;
            }
        }
        else
        {
            ApplyOutline(buttonObject, new Color(1f, 1f, 1f, 0.12f), Vector2.zero);
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;

        label = CreateLabel(buttonRect, "Label", labelText, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        return button;
    }

    private static Sprite ResolveCircularButtonSprite()
    {
        if (cachedCircularButtonSprite != null)
        {
            return cachedCircularButtonSprite;
        }

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.name = "SolarPanelCircularButtonTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2(31.5f, 31.5f);
        float radius = 30f;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? fill : clear);
            }
        }

        texture.Apply();
        cachedCircularButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return cachedCircularButtonSprite;
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 effectDistance)
    {
        if (target == null)
        {
            return;
        }

        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(target);
        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = true;
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

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
