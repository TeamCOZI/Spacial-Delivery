using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

[DefaultExecutionOrder(34007)]
[DisallowMultipleComponent]
public class WarehousePresenter : FocusEventSubscriber
{
    private sealed class SlotView
    {
        public Image background;
        public Outline outline;
        public Image icon;
        public TextMeshProUGUI placeholderLabel;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI countLabel;
    }

    private const string PanelObjectName = "WarehouseUIPanel";
    private const int PanelSortingOrderOffset = 208;
    private const int SlotCount = 9;
    private const int SlotColumnCount = 3;
    private const float SlotSpacing = 12f;
    private const float SlotPadding = 16f;
    private const float MinSlotSize = 56f;
    private const string TitleText = "\uC800\uC7A5\uACE0";
    private const string CloseText = "X";
    private const string TransportText = "\uC6B4\uC1A1";
    private const string StoreText = "\uC800\uC7A5";
    private const string StoredStatusFormat = "Stored {0} / {1}";
    private const string OverflowStatusFormat = "Stored {0} / {1}  (+{2} types)";
    private const string SlotPlaceholderFormat = "Capacity {0}";

    private static readonly string[] KoreanFontCandidates =
    {
        "Malgun Gothic",
        "\uB9D1\uC740 \uACE0\uB515",
        "Noto Sans KR",
        "Noto Sans CJK KR",
        "Arial Unicode MS"
    };

    private static readonly Color PanelBackgroundColor = new Color(0.53f, 0.53f, 0.53f, 0.96f);
    private static readonly Color PanelBorderColor = new Color(0.92f, 0.92f, 0.92f, 0.28f);
    private static readonly Color SlotBackgroundColor = new Color(0.97f, 0.97f, 0.97f, 1f);
    private static readonly Color EmptySlotColor = new Color(0.97f, 0.97f, 0.97f, 1f);
    private static readonly Color SlotOutlineColor = new Color(0.12f, 0.12f, 0.12f, 0.35f);
    private static readonly Color FilledSlotOutlineColor = new Color(0.10f, 0.16f, 0.21f, 0.84f);
    private static readonly Color ButtonColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color ButtonActiveColor = new Color(0.16f, 0.60f, 0.48f, 0.98f);
    private static readonly Color CloseButtonColor = new Color(0.97f, 0.20f, 0.13f, 1f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);

    private static WarehousePresenter instance;
    private static Sprite cachedCircularSprite;
    private static TMP_FontAsset cachedDisplayFont;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private GraphicRaycaster panelRaycaster;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private RectTransform slotGridRect;
    private GridLayoutGroup slotGridLayout;
    private readonly List<SlotView> slotViews = new List<SlotView>();
    private Button transportButton;
    private Image transportButtonBackground;
    private Button storeButton;
    private Image storeButtonBackground;
    private AssemblyPartFocus focusedWarehousePart;
    private WarehouseState focusedWarehouseState;
    private StructureResourceInventory focusedInventory;

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
        focusedWarehousePart = TryResolveFocusedWarehousePart(focused, out AssemblyPartFocus partFocus)
            ? partFocus
            : null;
        focusedWarehouseState = focusedWarehousePart != null ? WarehouseUtility.ResolveState(focusedWarehousePart.gameObject) : null;
        focusedInventory = ResolveInventory(focusedWarehousePart);
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
        panelRect.anchorMin = new Vector2(0.43f, 0.05f);
        panelRect.anchorMax = new Vector2(0.98f, 0.96f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, PanelBorderColor, new Vector2(2f, -2f));

        panelCanvas = panelObject.GetComponent<Canvas>();
        panelRaycaster = panelObject.GetComponent<GraphicRaycaster>();
        EnsurePanelDrawOrder();

        titleLabel = CreateLabel(panelRect, "WarehouseTitle", TitleText, new Vector2(0.04f, 0.92f), new Vector2(0.56f, 0.99f), 28f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        statusLabel = CreateLabel(panelRect, "WarehouseStatus", string.Empty, new Vector2(0.04f, 0.87f), new Vector2(0.72f, 0.92f), 13f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);

        Button closeButton = CreateButton(panelRect, "WarehouseCloseButton", CloseText, new Vector2(0.90f, 0.90f), new Vector2(0.985f, 0.995f), 22f, out _, CloseButtonColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);

        CreateSlotGrid(panelRect);

        transportButton = CreateButton(panelRect, "WarehouseTransportButton", TransportText, new Vector2(0.85f, 0.38f), new Vector2(0.97f, 0.58f), 24f, out transportButtonBackground, ButtonColor, WhiteText, false);
        transportButton.onClick.AddListener(HandleTransportClicked);

        storeButton = CreateButton(panelRect, "WarehouseStoreButton", StoreText, new Vector2(0.85f, 0.18f), new Vector2(0.97f, 0.38f), 24f, out storeButtonBackground, ButtonColor, WhiteText, false);
        storeButton.onClick.AddListener(HandleStoreClicked);
    }

    private void CreateSlotGrid(Transform parent)
    {
        GameObject gridObject = CreateUiObject("WarehouseSlotGrid", parent, typeof(RectTransform), typeof(GridLayoutGroup));
        slotGridRect = gridObject.GetComponent<RectTransform>();
        ConfigureStretchRect(slotGridRect, new Vector2(0.05f, 0.08f), new Vector2(0.82f, 0.84f));

        slotGridLayout = gridObject.GetComponent<GridLayoutGroup>();
        slotGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotGridLayout.constraintCount = SlotColumnCount;
        slotGridLayout.spacing = new Vector2(SlotSpacing, SlotSpacing);
        slotGridLayout.padding = new RectOffset(
            Mathf.RoundToInt(SlotPadding),
            Mathf.RoundToInt(SlotPadding),
            Mathf.RoundToInt(SlotPadding),
            Mathf.RoundToInt(SlotPadding));
        slotGridLayout.childAlignment = TextAnchor.MiddleCenter;
        slotGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        slotGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        slotGridLayout.cellSize = new Vector2(120f, 120f);

        EnsureSlotViews();
    }

    private void EnsureSlotViews()
    {
        if (slotGridRect == null)
        {
            return;
        }

        while (slotViews.Count < SlotCount)
        {
            int slotIndex = slotViews.Count;
            GameObject slotObject = CreateUiObject("WarehouseSlot_" + slotIndex, slotGridRect, typeof(RectTransform), typeof(Image));
            Image backgroundImage = slotObject.GetComponent<Image>();
            backgroundImage.color = SlotBackgroundColor;
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;
            Outline outline = ComponentUtility.GetOrAddComponent<Outline>(slotObject);
            outline.effectColor = SlotOutlineColor;
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.useGraphicAlpha = true;

            Image iconImage = CreateImage(slotObject.transform, "WarehouseSlotIcon_" + slotIndex, new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.84f));
            iconImage.enabled = false;

            TextMeshProUGUI placeholderLabel = CreateLabel(slotObject.transform, "WarehouseSlotPlaceholder_" + slotIndex, string.Format(SlotPlaceholderFormat, slotIndex + 1), new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.84f), 20f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
            TextMeshProUGUI nameLabel = CreateLabel(slotObject.transform, "WarehouseSlotName_" + slotIndex, string.Empty, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.42f), 18f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
            TextMeshProUGUI countLabel = CreateLabel(slotObject.transform, "WarehouseSlotCount_" + slotIndex, string.Empty, new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.18f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

            slotViews.Add(new SlotView
            {
                background = backgroundImage,
                outline = outline,
                icon = iconImage,
                placeholderLabel = placeholderLabel,
                nameLabel = nameLabel,
                countLabel = countLabel
            });
        }
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveCloseFocus();
        focusedWarehousePart = null;
        focusedWarehouseState = null;
        focusedInventory = null;
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

    private void HandleTransportClicked()
    {
        if (focusedWarehouseState == null)
        {
            return;
        }

        focusedWarehouseState.SetTransportToCoreEnabled(true);
        RefreshPanelContent();
    }

    private void HandleStoreClicked()
    {
        if (focusedWarehouseState == null)
        {
            return;
        }

        focusedWarehouseState.SetTransportToCoreEnabled(false);
        RefreshPanelContent();
    }

    private Transform ResolveCloseFocus()
    {
        return focusedWarehousePart != null && focusedWarehousePart.OwnerSatellite != null
            ? focusedWarehousePart.OwnerSatellite.transform
            : null;
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        bool shouldShow = focusedWarehousePart != null && focusedWarehouseState != null;
        SetPanelVisible(shouldShow);
        if (shouldShow)
        {
            RefreshPanelContent();
        }
    }

    private void RefreshPanelContent()
    {
        if (focusedWarehousePart == null || focusedWarehouseState == null || !WarehouseUtility.IsWarehousePart(focusedWarehousePart.SourcePart))
        {
            SetPanelVisible(false);
            return;
        }

        focusedInventory = ResolveInventory(focusedWarehousePart);
        if (focusedInventory == null)
        {
            SetPanelVisible(false);
            return;
        }

        titleLabel.SetText(TitleText);
        statusLabel.SetText(BuildStatusText(focusedInventory));
        RefreshSlotViews();
        UpdateButtons();
        UpdateGridCellSize();
    }

    private void RefreshSlotViews()
    {
        EnsureSlotViews();
        for (int i = 0; i < slotViews.Count; i++)
        {
            SlotView slotView = slotViews[i];
            if (slotView == null)
            {
                continue;
            }

            if (focusedInventory != null && focusedInventory.TryGetResourceAtSlot(i, out StructureResourceInventory.ResourceAmount resourceAmount))
            {
                ApplyItemSlotVisuals(slotView, resourceAmount);
            }
            else
            {
                ClearItemSlotVisuals(slotView, i);
            }
        }
    }

    private void ApplyItemSlotVisuals(SlotView slotView, StructureResourceInventory.ResourceAmount resourceAmount)
    {
        if (slotView == null)
        {
            return;
        }

        Sprite resourceSprite = InventoryResourceCatalog.GetSlotSprite(resourceAmount.resourceType);
        if (slotView.background != null)
        {
            slotView.background.color = resourceSprite != null ? SlotBackgroundColor : ResolveResourceSlotColor(resourceAmount.resourceType);
        }

        if (slotView.outline != null)
        {
            slotView.outline.effectColor = FilledSlotOutlineColor;
        }

        if (slotView.icon != null)
        {
            slotView.icon.sprite = resourceSprite;
            slotView.icon.color = Color.white;
            slotView.icon.enabled = resourceSprite != null;
        }

        SetLabel(slotView.placeholderLabel, string.Empty, false);
        SetLabel(slotView.nameLabel, InventoryResourceCatalog.GetCompactLabel(resourceAmount.resourceType), true);
        SetLabel(slotView.countLabel, $"x{Mathf.Max(0, resourceAmount.amount)}", true);
    }

    private void ClearItemSlotVisuals(SlotView slotView, int slotIndex)
    {
        if (slotView == null)
        {
            return;
        }

        if (slotView.background != null)
        {
            slotView.background.color = EmptySlotColor;
        }

        if (slotView.outline != null)
        {
            slotView.outline.effectColor = SlotOutlineColor;
        }

        if (slotView.icon != null)
        {
            slotView.icon.sprite = null;
            slotView.icon.enabled = false;
        }

        SetLabel(slotView.placeholderLabel, string.Format(SlotPlaceholderFormat, slotIndex + 1), true);
        SetLabel(slotView.nameLabel, string.Empty, false);
        SetLabel(slotView.countLabel, string.Empty, false);
    }

    private void UpdateButtons()
    {
        bool hasState = focusedWarehouseState != null;
        bool transportEnabled = hasState && focusedWarehouseState.IsTransportToCoreEnabled;

        if (transportButton != null)
        {
            transportButton.interactable = hasState;
        }

        if (storeButton != null)
        {
            storeButton.interactable = hasState;
        }

        if (transportButtonBackground != null)
        {
            transportButtonBackground.color = transportEnabled ? ButtonActiveColor : ButtonColor;
        }

        if (storeButtonBackground != null)
        {
            storeButtonBackground.color = !transportEnabled ? ButtonActiveColor : ButtonColor;
        }
    }

    private void UpdateGridCellSize()
    {
        if (slotGridLayout == null || slotGridRect == null)
        {
            return;
        }

        float width = slotGridRect.rect.width;
        float height = slotGridRect.rect.height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        float usableWidth = width - slotGridLayout.padding.left - slotGridLayout.padding.right - (SlotSpacing * (SlotColumnCount - 1));
        float usableHeight = height - slotGridLayout.padding.top - slotGridLayout.padding.bottom - (SlotSpacing * (SlotColumnCount - 1));
        float cellSize = Mathf.Max(MinSlotSize, Mathf.Min(usableWidth / SlotColumnCount, usableHeight / SlotColumnCount));
        if (!Mathf.Approximately(slotGridLayout.cellSize.x, cellSize) || !Mathf.Approximately(slotGridLayout.cellSize.y, cellSize))
        {
            slotGridLayout.cellSize = new Vector2(cellSize, cellSize);
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotGridRect);
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

        if (panelRaycaster != null)
        {
            panelRaycaster.enabled = visible;
        }
    }

    private static StructureResourceInventory ResolveInventory(AssemblyPartFocus partFocus)
    {
        if (partFocus == null)
        {
            return null;
        }

        ModulePartInventoryUtility.TryResolvePartInventory(partFocus, StructureResourceInventoryKind.General, out StructureResourceInventory inventory);
        return inventory;
    }

    private static bool TryResolveFocusedWarehousePart(Transform focused, out AssemblyPartFocus partFocus)
    {
        partFocus = null;
        if (focused == null || focused.GetComponentInParent<AssemblyOutputPortFocus>() != null)
        {
            return false;
        }

        partFocus = focused.GetComponent<AssemblyPartFocus>() ?? focused.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null)
        {
            partFocus = null;
            return false;
        }

        if (!WarehouseUtility.IsWarehousePart(partFocus.SourcePart))
        {
            partFocus = null;
            return false;
        }

        return true;
    }

    private static string BuildStatusText(StructureResourceInventory inventory)
    {
        if (inventory == null)
        {
            return string.Format(StoredStatusFormat, 0, 0);
        }

        int hiddenTypeCount = Mathf.Max(0, inventory.ResourceTypeCount - SlotCount);
        if (hiddenTypeCount > 0)
        {
            return string.Format(OverflowStatusFormat, inventory.TotalAmount, inventory.Capacity, hiddenTypeCount);
        }

        return string.Format(StoredStatusFormat, inventory.TotalAmount, inventory.Capacity);
    }

    private static void SetLabel(TextMeshProUGUI label, string text, bool visible)
    {
        if (label == null)
        {
            return;
        }

        label.gameObject.SetActive(visible);
        label.SetText(visible ? text : string.Empty);
    }

    private static Color ResolveResourceSlotColor(InventoryResourceType resourceType)
    {
        string resourceName = resourceType.ToString();
        if (resourceName.EndsWith("_Crystal", StringComparison.Ordinal))
        {
            return new Color(0.29f, 0.63f, 0.95f, 0.94f);
        }

        if (resourceName.EndsWith("_Gas", StringComparison.Ordinal))
        {
            return new Color(0.92f, 0.72f, 0.45f, 0.94f);
        }

        if (resourceName.EndsWith("_Liquid", StringComparison.Ordinal))
        {
            return new Color(0.21f, 0.73f, 0.76f, 0.94f);
        }

        return SlotBackgroundColor;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        ConfigureStretchRect(imageRect, anchorMin, anchorMax);
        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
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
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.5f);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        ApplyFont(label);
        return label;
    }

    private static void ApplyFont(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        TMP_FontAsset font = ResolveDisplayFont();
        if (font != null)
        {
            label.font = font;
        }
    }

    private static TMP_FontAsset ResolveDisplayFont()
    {
        if (cachedDisplayFont != null)
        {
            return cachedDisplayFont;
        }

        Font osFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
        if (osFont != null)
        {
            cachedDisplayFont = TMP_FontAsset.CreateFontAsset(
                osFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (cachedDisplayFont != null)
            {
                return cachedDisplayFont;
            }
        }

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
        {
            cachedDisplayFont = focusManager.focusInfoText.font;
            return cachedDisplayFont;
        }

        cachedDisplayFont = TMP_Settings.defaultFontAsset;
        if (cachedDisplayFont != null)
        {
            return cachedDisplayFont;
        }

        cachedDisplayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return cachedDisplayFont;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, float fontSize, out Image backgroundImage, Color backgroundColor, Color textColor, bool circular)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);
        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        if (circular)
        {
            backgroundImage.sprite = ResolveCircularSprite();
            backgroundImage.preserveAspect = true;
        }
        else
        {
            ApplyOutline(buttonObject, PanelBorderColor, Vector2.zero);
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        _ = CreateLabel(buttonRect, name + "Label", labelText, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        return button;
    }

    private static Sprite ResolveCircularSprite()
    {
        if (cachedCircularSprite != null)
        {
            return cachedCircularSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }

        texture.Apply();
        cachedCircularSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircularSprite;
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 effectDistance)
    {
        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(target);
        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = true;
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
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
