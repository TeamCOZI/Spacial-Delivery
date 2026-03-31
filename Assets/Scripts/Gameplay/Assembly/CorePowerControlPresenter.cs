using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

[DefaultExecutionOrder(34008)]
[DisallowMultipleComponent]
public class CorePowerControlPresenter : FocusEventSubscriber
{
    private const string PanelObjectName = "CorePowerControlUIPanel";
    private const int PanelSortingOrderOffset = 209;
    private const string TitleText = "\uCF54\uC5B4 \uC804\uB825\uC18C";
    private const string ReserveText = "\uC608\uBE44\uC804\uB825";
    private const string ProductionText = "\uC0DD\uC0B0\uC804\uB825";
    private const string ConsumptionText = "\uC18C\uBAA8\uC804\uB825";

    private static readonly string[] KoreanFontCandidates = { "Malgun Gothic", "\uB9D1\uC740 \uACE0\uB515", "Noto Sans KR", "Noto Sans CJK KR", "Arial Unicode MS" };
    private static readonly Color PanelColor = new Color(0.34f, 0.34f, 0.34f, 0.96f);
    private static readonly Color CardColor = new Color(0.56f, 0.56f, 0.56f, 1f);
    private static readonly Color WhiteCard = new Color(0.98f, 0.98f, 0.98f, 1f);
    private static readonly Color ReserveColor = new Color(0.96f, 0.73f, 0.30f, 1f);
    private static readonly Color ProductionColor = new Color(0.35f, 0.66f, 0.87f, 1f);
    private static readonly Color ConsumptionColor = new Color(0.96f, 0.73f, 0.30f, 1f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);
    private static readonly Color CloseColor = new Color(0.78f, 0.23f, 0.22f, 1f);

    private static CorePowerControlPresenter instance;
    private static TMP_FontAsset cachedFont;
    private static Sprite cachedCircleSprite;
    private static Sprite cachedRingSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private GraphicRaycaster panelRaycaster;
    private RectTransform panelRect;
    private Image reserveFillImage;
    private TextMeshProUGUI reserveValueLabel;
    private TextMeshProUGUI reserveCapacityLabel;
    private RectTransform productionBarRect;
    private RectTransform consumptionBarRect;
    private RectTransform dividerRect;
    private RectTransform chartRect;
    private Image chartImage;
    private TextMeshProUGUI productionLegendLabel;
    private TextMeshProUGUI consumptionLegendLabel;
    private StructureFocus focusedStructure;
    private CorePowerControlState focusedState;
    private Texture2D chartTexture;
    private Sprite chartSprite;
    private int lastChartRevision = -1;
    private Vector2 lastChartSize = Vector2.zero;

    private void Awake()
    {
        if (instance == null || instance == this) instance = this;
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
        if (instance == this) instance = null;
        if (chartSprite != null) Destroy(chartSprite);
        if (chartTexture != null) Destroy(chartTexture);
        if (panelRect != null) Destroy(panelRect.gameObject);
    }

    protected override void LateUpdate()
    {
        if (panelRect != null && panelRect.gameObject.activeInHierarchy) RefreshPanelContent();
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedStructure = TryResolveFocusedStructure(focused, out StructureFocus structureFocus) ? structureFocus : null;
        focusedState = focusedStructure != null ? CorePowerControlUtility.ResolveState(focusedStructure.gameObject) : null;
        UpdatePanelState();
    }

    private void EnsurePanel()
    {
        if (panelRect != null) return;
        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null) return;

        GameObject panelObject = CreateUiObject(PanelObjectName, hostCanvas.transform, typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.28f, 0.05f);
        panelRect.anchorMax = new Vector2(0.98f, 0.96f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = PanelColor;
        panelCanvas = panelObject.GetComponent<Canvas>();
        panelRaycaster = panelObject.GetComponent<GraphicRaycaster>();
        EnsurePanelDrawOrder();

        _ = CreateLabel(panelRect, "Title", TitleText, new Vector2(0.03f, 0.90f), new Vector2(0.50f, 0.99f), 32f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        Button closeButton = CreateButton(panelRect, "Close", "X", new Vector2(0.90f, 0.89f), new Vector2(0.985f, 0.995f), 30f, CloseColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);

        RectTransform reserveCard = CreateCard(panelRect, "ReserveCard", new Vector2(0.02f, 0.48f), new Vector2(0.43f, 0.86f), CardColor);
        RectTransform reserveInner = CreateCard(reserveCard, "ReserveInner", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), WhiteCard);
        _ = CreateImage(reserveInner, "ReserveBg", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), ResolveRingSprite(), Color.white);
        reserveFillImage = CreateImage(reserveInner, "ReserveFill", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), ResolveRingSprite(), ReserveColor);
        reserveFillImage.type = Image.Type.Filled;
        reserveFillImage.fillMethod = Image.FillMethod.Radial360;
        reserveFillImage.fillOrigin = (int)Image.Origin360.Top;
        reserveFillImage.fillClockwise = false;
        _ = CreateImage(reserveInner, "Center", new Vector2(0.29f, 0.29f), new Vector2(0.71f, 0.71f), ResolveCircleSprite(), Color.white);
        _ = CreateImage(reserveInner, "Dot", new Vector2(0.86f, 0.84f), new Vector2(0.92f, 0.90f), ResolveCircleSprite(), ReserveColor);
        _ = CreateLabel(reserveInner, "ReserveTitle", ReserveText, new Vector2(0.34f, 0.46f), new Vector2(0.66f, 0.58f), 14f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
        reserveValueLabel = CreateLabel(reserveInner, "ReserveValue", "0", new Vector2(0.25f, 0.34f), new Vector2(0.75f, 0.52f), 34f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
        reserveCapacityLabel = CreateLabel(reserveInner, "ReserveCapacity", "/0", new Vector2(0.30f, 0.24f), new Vector2(0.70f, 0.34f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.18f, 0.18f, 0.18f, 0.65f));

        RectTransform balanceCard = CreateCard(panelRect, "BalanceCard", new Vector2(0.45f, 0.48f), new Vector2(0.98f, 0.86f), CardColor);
        RectTransform balanceInner = CreateCard(balanceCard, "BalanceInner", new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.88f), WhiteCard);
        productionBarRect = CreateImage(balanceInner, "ProductionBar", new Vector2(0f, 0f), new Vector2(0.5f, 1f), null, ProductionColor).rectTransform;
        consumptionBarRect = CreateImage(balanceInner, "ConsumptionBar", new Vector2(0.5f, 0f), new Vector2(1f, 1f), null, ConsumptionColor).rectTransform;
        dividerRect = CreateImage(balanceInner, "Divider", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), null, new Color(1f, 1f, 1f, 0.55f)).rectTransform;
        dividerRect.sizeDelta = new Vector2(2f, 0f);

        RectTransform chartCard = CreateCard(panelRect, "ChartCard", new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.46f), CardColor);
        RectTransform chartInner = CreateCard(chartCard, "ChartInner", new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.94f), WhiteCard);
        chartRect = CreateImage(chartInner, "Chart", new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.92f), null, Color.white).rectTransform;
        chartImage = chartRect.GetComponent<Image>();
        RectTransform productionLegend = CreateUiObject("ProductionLegend", chartInner, typeof(RectTransform)).GetComponent<RectTransform>();
        ConfigureStretchRect(productionLegend, new Vector2(0.36f, 0.04f), new Vector2(0.58f, 0.16f));
        _ = CreateImage(productionLegend, "ProductionSwatch", new Vector2(0.00f, 0.24f), new Vector2(0.10f, 0.76f), null, ProductionColor);
        productionLegendLabel = CreateLabel(productionLegend, "ProductionLabel", ProductionText + " 0", new Vector2(0.14f, 0.10f), new Vector2(1f, 0.90f), 14f, FontStyles.Bold, TextAlignmentOptions.Left, DarkText);
        RectTransform consumptionLegend = CreateUiObject("ConsumptionLegend", chartInner, typeof(RectTransform)).GetComponent<RectTransform>();
        ConfigureStretchRect(consumptionLegend, new Vector2(0.60f, 0.04f), new Vector2(0.84f, 0.16f));
        _ = CreateImage(consumptionLegend, "ConsumptionSwatch", new Vector2(0.00f, 0.24f), new Vector2(0.10f, 0.76f), null, ConsumptionColor);
        consumptionLegendLabel = CreateLabel(consumptionLegend, "ConsumptionLabel", ConsumptionText + " 0", new Vector2(0.14f, 0.10f), new Vector2(1f, 0.90f), 14f, FontStyles.Bold, TextAlignmentOptions.Left, DarkText);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = focusedStructure != null && focusedStructure.OwnerSatellite != null ? focusedStructure.OwnerSatellite.transform : null;
        focusedStructure = null;
        focusedState = null;
        SetPanelVisible(false);
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (nextFocus != null) CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            focusManager.SetFocus(nextFocus);
        }
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        bool shouldShow = focusedStructure != null && focusedState != null;
        SetPanelVisible(shouldShow);
        if (shouldShow) RefreshPanelContent();
    }

    private void RefreshPanelContent()
    {
        if (focusedStructure == null || !CorePowerControlUtility.IsCorePowerControlFocus(focusedStructure))
        {
            SetPanelVisible(false);
            return;
        }

        focusedState = CorePowerControlUtility.ResolveState(focusedStructure.gameObject);
        if (focusedState == null)
        {
            SetPanelVisible(false);
            return;
        }

        int reservePower = focusedState.ReservePower;
        int reserveCapacity = focusedState.ReserveCapacity;
        reserveFillImage.fillAmount = reserveCapacity > 0 ? Mathf.Clamp01(reservePower / (float)reserveCapacity) : 0f;
        reserveValueLabel.SetText(FormatCompact(reservePower));
        reserveCapacityLabel.SetText("/" + FormatCompact(reserveCapacity));

        float production = focusedState.CurrentProduction;
        float consumption = focusedState.CurrentConsumption;
        float ratio = production + consumption > 0.0001f ? production / (production + consumption) : 0.5f;
        ratio = Mathf.Clamp01(ratio);
        productionBarRect.anchorMin = new Vector2(0f, 0f);
        productionBarRect.anchorMax = new Vector2(ratio, 1f);
        productionBarRect.offsetMin = Vector2.zero;
        productionBarRect.offsetMax = Vector2.zero;
        consumptionBarRect.anchorMin = new Vector2(ratio, 0f);
        consumptionBarRect.anchorMax = new Vector2(1f, 1f);
        consumptionBarRect.offsetMin = Vector2.zero;
        consumptionBarRect.offsetMax = Vector2.zero;
        dividerRect.anchorMin = new Vector2(ratio, 0f);
        dividerRect.anchorMax = new Vector2(ratio, 1f);
        dividerRect.anchoredPosition = Vector2.zero;

        productionLegendLabel.SetText(ProductionText + " " + FormatValue(production));
        consumptionLegendLabel.SetText(ConsumptionText + " " + FormatValue(consumption));
        RefreshChart();
    }

    private void RefreshChart()
    {
        if (focusedState == null || chartRect == null || chartImage == null) return;
        Vector2 size = chartRect.rect.size;
        if (size.x <= 4f || size.y <= 4f) return;
        bool sizeChanged = (size - lastChartSize).sqrMagnitude > 1f;
        if (!sizeChanged && lastChartRevision == focusedState.Revision) return;

        int width = Mathf.Clamp(Mathf.RoundToInt(size.x), 128, 1024);
        int height = Mathf.Clamp(Mathf.RoundToInt(size.y), 96, 512);
        EnsureChartTexture(width, height);
        DrawChart(focusedState.ProductionHistory, focusedState.ConsumptionHistory, width, height);
        chartImage.sprite = chartSprite;
        lastChartRevision = focusedState.Revision;
        lastChartSize = size;
    }

    private void EnsureChartTexture(int width, int height)
    {
        if (chartTexture != null && chartTexture.width == width && chartTexture.height == height) return;
        if (chartSprite != null) Destroy(chartSprite);
        if (chartTexture != null) Destroy(chartTexture);
        chartTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        chartSprite = Sprite.Create(chartTexture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void DrawChart(IReadOnlyList<float> productionHistory, IReadOnlyList<float> consumptionHistory, int width, int height)
    {
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        float maxValue = Mathf.Max(1f, FindMax(productionHistory), FindMax(consumptionHistory)) * 1.1f;
        DrawSeries(pixels, width, height, productionHistory, maxValue, ProductionColor);
        DrawSeries(pixels, width, height, consumptionHistory, maxValue, ConsumptionColor);
        chartTexture.SetPixels32(pixels);
        chartTexture.Apply(false, false);
    }

    private static float FindMax(IReadOnlyList<float> values)
    {
        float maxValue = 0f;
        if (values == null) return maxValue;
        for (int i = 0; i < values.Count; i++) maxValue = Mathf.Max(maxValue, Mathf.Max(0f, values[i]));
        return maxValue;
    }

    private static void DrawSeries(Color32[] pixels, int width, int height, IReadOnlyList<float> history, float maxValue, Color color)
    {
        if (history == null || history.Count <= 0) return;
        int padX = 8;
        int padY = 10;
        int graphWidth = Mathf.Max(1, width - (padX * 2));
        int graphHeight = Mathf.Max(1, height - (padY * 2));
        Color32 drawColor = color;
        Vector2Int? prev = null;
        for (int i = 0; i < history.Count; i++)
        {
            float t = history.Count > 1 ? i / (float)(history.Count - 1) : 1f;
            int x = padX + Mathf.RoundToInt(graphWidth * t);
            int y = padY + Mathf.RoundToInt(graphHeight * Mathf.Clamp01(Mathf.Max(0f, history[i]) / maxValue));
            Vector2Int current = new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
            if (prev.HasValue) DrawLine(pixels, width, height, prev.Value, current, drawColor); else DrawPoint(pixels, width, height, current.x, current.y, drawColor);
            prev = current;
        }
    }

    private static void DrawLine(Color32[] pixels, int width, int height, Vector2Int start, Vector2Int end, Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
        for (int i = 0; i <= Mathf.Max(1, steps); i++)
        {
            float t = steps > 0 ? i / (float)steps : 0f;
            DrawPoint(pixels, width, height, Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t)), Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t)), color);
        }
    }

    private static void DrawPoint(Color32[] pixels, int width, int height, int x, int y, Color32 color)
    {
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int px = x + ox;
                int py = y + oy;
                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                pixels[py * width + px] = color;
            }
        }
    }

    private bool IsPointerOverVisiblePanel()
    {
        if (panelRect == null || !panelRect.gameObject.activeInHierarchy || Mouse.current == null) return false;
        Vector2 pointer = Mouse.current.position.ReadValue();
        Camera uiCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? panelCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointer, uiCamera);
    }

    private void EnsurePanelDrawOrder()
    {
        if (panelCanvas == null || hostCanvas == null) return;
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingLayerID = hostCanvas.sortingLayerID;
        panelCanvas.sortingOrder = hostCanvas.sortingOrder + PanelSortingOrderOffset;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect == null) return;
        if (panelRect.gameObject.activeSelf != visible) panelRect.gameObject.SetActive(visible);
        if (panelRaycaster != null) panelRaycaster.enabled = visible;
    }

    private static bool TryResolveFocusedStructure(Transform focused, out StructureFocus structureFocus)
    {
        structureFocus = null;
        if (focused == null || focused.GetComponentInParent<AssemblyOutputPortFocus>() != null) return false;
        structureFocus = focused.GetComponent<StructureFocus>() ?? focused.GetComponentInParent<StructureFocus>();
        return structureFocus != null && structureFocus.OwnerSatellite != null && CorePowerControlUtility.IsCorePowerControlFocus(structureFocus);
    }

    private static string FormatCompact(int value)
    {
        int sanitized = Mathf.Max(0, value);
        if (sanitized >= 1000)
        {
            float compact = sanitized / 1000f;
            return compact >= 10f ? $"{compact:0}K" : $"{compact:0.#}K";
        }
        return sanitized.ToString();
    }

    private static string FormatValue(float value)
    {
        float sanitized = Mathf.Max(0f, value);
        if (sanitized >= 1000f)
        {
            float compact = sanitized / 1000f;
            return compact >= 10f ? $"{compact:0}K" : $"{compact:0.#}K";
        }
        return sanitized >= 100f ? sanitized.ToString("0") : sanitized.ToString("0.#");
    }

    private static RectTransform CreateCard(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject obj = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        ConfigureStretchRect(rect, anchorMin, anchorMax);
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, Color color)
    {
        GameObject obj = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        ConfigureStretchRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float size, FontStyles style, TextAlignmentOptions align, Color color)
    {
        GameObject obj = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = obj.GetComponent<RectTransform>();
        ConfigureStretchRect(rect, anchorMin, anchorMax);
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = align;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8f, size * 0.45f);
        label.fontSizeMax = size;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        ApplyFont(label);
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float size, Color bg, Color fg, bool circular)
    {
        GameObject obj = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = obj.GetComponent<RectTransform>();
        ConfigureStretchRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.color = bg;
        image.sprite = circular ? ResolveCircleSprite() : null;
        image.preserveAspect = circular;
        Button button = obj.GetComponent<Button>();
        _ = CreateLabel(rect, name + "Label", text, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f), size, FontStyles.Bold, TextAlignmentOptions.Center, fg);
        return button;
    }

    private static void ApplyFont(TextMeshProUGUI label)
    {
        if (label == null) return;
        TMP_FontAsset font = ResolveFont();
        if (font != null) label.font = font;
    }

    private static TMP_FontAsset ResolveFont()
    {
        if (cachedFont != null) return cachedFont;
        Font osFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
        if (osFont != null)
        {
            cachedFont = TMP_FontAsset.CreateFontAsset(osFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (cachedFont != null) return cachedFont;
        }
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) && focusManager.focusInfoText != null && focusManager.focusInfoText.font != null)
        {
            cachedFont = focusManager.focusInfoText.font;
            return cachedFont;
        }
        cachedFont = TMP_Settings.defaultFontAsset;
        if (cachedFont == null) cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return cachedFont;
    }

    private static Sprite ResolveCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) texture.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= radius ? Color.white : new Color(0f, 0f, 0f, 0f));
        texture.Apply();
        cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircleSprite;
    }

    private static Sprite ResolveRingSprite()
    {
        if (cachedRingSprite != null) return cachedRingSprite;
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = 118f;
        float innerRadius = 58f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inside = distance <= outerRadius && distance >= innerRadius;
                texture.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }
        texture.Apply();
        cachedRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedRingSprite;
    }

    private static void ConfigureStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
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
        GameObject obj = new GameObject(name, components);
        obj.transform.SetParent(parent, false);
        return obj;
    }
}
