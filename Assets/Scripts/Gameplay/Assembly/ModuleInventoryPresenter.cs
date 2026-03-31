using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(33998)]
[DisallowMultipleComponent]
public class ModuleInventoryPresenter : FocusEventSubscriber
{
    private const string LauncherPartName = "Launcher";
    private const string PanelObjectName = "ModuleInventoryUIPanel";
    private const string CloseButtonText = "CLOSE";
    private const string DefaultTitleText = "MODULE INVENTORY";
    private const string DefaultSubtitleText = "-";
    private const string EmptyIconText = "MODULE";
    private const string OutputIconText = "PORT";
    private const string ItemListTitleText = "RESOURCE INVENTORY";
    private const string OutputListTitleText = "OUTPUT RESOURCES";
    private const string EmptyGridText = "EMPTY";
    private const string EmptyOutputGridText = "NO RESOURCES";
    private const string CapacityTitleText = "CAPACITY";
    private const string StoredTitleText = "STORED";
    private const string FreeTitleText = "FREE";
    private const string CategoryTitleText = "CATEGORY";
    private const string FootprintTitleText = "FOOTPRINT";
    private const string OwnerTitleText = "OWNER";
    private const int ItemGridColumnCount = 5;
    private const float ItemGridSpacing = 10f;
    private const float ItemGridPadding = 12f;
    private const float MinItemGridCellSize = 24f;
    private const int PanelSortingOrderOffset = 205;

    private static readonly Color PanelBackgroundColor = new Color(0.97f, 0.98f, 0.99f, 0.985f);
    private static readonly Color BorderColor = new Color(0.11f, 0.20f, 0.28f, 1f);
    private static readonly Color PrimaryBlue = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color SecondaryBlue = new Color(0.24f, 0.47f, 0.63f, 0.98f);
    private static readonly Color Orange = new Color(0.92f, 0.70f, 0.56f, 0.98f);
    private static readonly Color OrangeAlt = new Color(0.95f, 0.75f, 0.61f, 0.98f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);
    private static readonly Color PlaceholderTextColor = new Color(0.92f, 0.96f, 1f, 0.96f);
    private static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.20f);
    private static readonly Color FilledSlotOutlineColor = new Color(0.10f, 0.16f, 0.21f, 0.84f);
    private static readonly Color EmptySlotOutlineColor = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color SelectedSlotOutlineColor = new Color(1f, 0.95f, 0.74f, 0.98f);
    private static readonly Color DisabledButtonColor = new Color(0.42f, 0.48f, 0.54f, 0.92f);
    private static readonly Color DisabledButtonTextColor = new Color(0.90f, 0.94f, 0.97f, 0.92f);

    private static ModuleInventoryPresenter instance;

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
    private Image moduleIconImage;
    private TextMeshProUGUI moduleIconFallbackLabel;
    private TextMeshProUGUI capacityValueLabel;
    private TextMeshProUGUI storedValueLabel;
    private TextMeshProUGUI freeValueLabel;
    private TextMeshProUGUI categoryValueLabel;
    private TextMeshProUGUI footprintValueLabel;
    private TextMeshProUGUI ownerValueLabel;
    private RectTransform listSectionRect;
    private ScrollRect itemListScrollRect;
    private RectTransform itemListContentRect;
    private GridLayoutGroup itemListGridLayout;
    private TextMeshProUGUI itemListTitleLabel;
    private TextMeshProUGUI itemListPlaceholderLabel;
    private readonly List<Image> itemSlotImages = new List<Image>();
    private readonly List<Image> itemSlotIconImages = new List<Image>();
    private readonly List<TextMeshProUGUI> itemSlotNameLabels = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> itemSlotCountLabels = new List<TextMeshProUGUI>();
    private readonly List<Button> itemSlotButtons = new List<Button>();
    private readonly List<InventoryResourceType?> itemSlotResourceTypes = new List<InventoryResourceType?>();
    private readonly List<StructureResourceInventory.ResourceAmount> outputPortResources = new List<StructureResourceInventory.ResourceAmount>();
    private Button runButton;
    private Image runButtonBackground;
    private TextMeshProUGUI runButtonLabel;
    private int displayedSlotCount = -1;
    private int selectedResourceSlotIndex = -1;
    private AssemblyPartFocus focusedPart;
    private StructureFocus focusedStructure;
    private AssemblyOutputPortFocus focusedOutputPort;

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
        focusedOutputPort = focused != null ? focused.GetComponent<AssemblyOutputPortFocus>() : null;
        if (focusedOutputPort == null && focused != null)
        {
            focusedOutputPort = focused.GetComponentInParent<AssemblyOutputPortFocus>();
        }

        if (focusedOutputPort != null)
        {
            focusedPart = focusedOutputPort.OwnerPartFocus;
            focusedStructure = null;
        }
        else
        {
            focusedPart = null;
            focusedStructure = null;
        }

        selectedResourceSlotIndex = -1;
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
        panelRect.anchorMin = new Vector2(0.68f, 0.46f);
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
        CreateItemListSection(panelObject.transform);
    }

    private void CreateHeader(Transform parent)
    {
        titleLabel = CreateLabel(parent, "ModuleInventoryTitle", DefaultTitleText, new Vector2(0.04f, 0.86f), new Vector2(0.78f, 0.96f), 20f, FontStyles.Bold, TextAlignmentOptions.Left, BorderColor);
        subtitleLabel = CreateLabel(parent, "ModuleInventorySubtitle", DefaultSubtitleText, new Vector2(0.04f, 0.81f), new Vector2(0.78f, 0.87f), 11f, FontStyles.Normal, TextAlignmentOptions.Left, BorderColor);

        runButton = CreateButton(
            parent,
            "ModuleInventoryRunButton",
            "RUN",
            new Vector2(0.69f, 0.87f),
            new Vector2(0.83f, 0.98f),
            13f,
            out runButtonBackground,
            out runButtonLabel,
            Orange,
            DarkText);
        runButton.onClick.AddListener(HandleRunClicked);

        closeButton = CreateButton(
            parent,
            "ModuleInventoryCloseButton",
            CloseButtonText,
            new Vector2(0.84f, 0.87f),
            new Vector2(0.97f, 0.98f),
            13f,
            out closeButtonBackground,
            out closeButtonLabel,
            PrimaryBlue,
            WhiteText);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateIconSection(Transform parent)
    {
        iconSectionRect = CreateSection(parent, "ModuleInventoryIconSection", new Vector2(0.04f, 0.56f), new Vector2(0.28f, 0.79f), PrimaryBlue);

        GameObject iconImageObject = CreateUiObject("ModuleInventoryIconImage", iconSectionRect, typeof(RectTransform), typeof(Image));
        RectTransform iconImageRect = iconImageObject.GetComponent<RectTransform>();
        ConfigureStretchRect(iconImageRect, new Vector2(0.12f, 0.20f), new Vector2(0.88f, 0.88f));
        moduleIconImage = iconImageObject.GetComponent<Image>();
        moduleIconImage.preserveAspect = true;
        moduleIconImage.color = Color.white;

        moduleIconFallbackLabel = CreateLabel(iconSectionRect, "ModuleInventoryIconFallback", EmptyIconText, new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.84f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
    }

    private void CreateMetricSections(Transform parent)
    {
        RectTransform capacityRect = CreateSection(parent, "ModuleInventoryCapacity", new Vector2(0.33f, 0.72f), new Vector2(0.67f, 0.79f), PrimaryBlue);
        CreateStatTitle(capacityRect, "ModuleInventoryCapacityTitle", CapacityTitleText, WhiteText, 14f);
        capacityValueLabel = CreateStatValue(capacityRect, "ModuleInventoryCapacityValue", WhiteText, 16f);

        RectTransform storedRect = CreateSection(parent, "ModuleInventoryStored", new Vector2(0.67f, 0.72f), new Vector2(0.96f, 0.79f), Orange);
        CreateStatTitle(storedRect, "ModuleInventoryStoredTitle", StoredTitleText, DarkText, 14f);
        storedValueLabel = CreateStatValue(storedRect, "ModuleInventoryStoredValue", DarkText, 16f);

        RectTransform freeRect = CreateSection(parent, "ModuleInventoryFree", new Vector2(0.33f, 0.56f), new Vector2(0.55f, 0.72f), SecondaryBlue);
        CreateStatTitle(freeRect, "ModuleInventoryFreeTitle", FreeTitleText, WhiteText, 12f);
        freeValueLabel = CreateStatValue(freeRect, "ModuleInventoryFreeValue", WhiteText, 18f);

        RectTransform categoryRect = CreateSection(parent, "ModuleInventoryCategory", new Vector2(0.55f, 0.56f), new Vector2(0.67f, 0.72f), SecondaryBlue);
        CreateStatTitle(categoryRect, "ModuleInventoryCategoryTitle", CategoryTitleText, WhiteText, 11f);
        categoryValueLabel = CreateStatValue(categoryRect, "ModuleInventoryCategoryValue", WhiteText, 12f);

        RectTransform footprintRect = CreateSection(parent, "ModuleInventoryFootprint", new Vector2(0.67f, 0.56f), new Vector2(0.84f, 0.72f), OrangeAlt);
        CreateStatTitle(footprintRect, "ModuleInventoryFootprintTitle", FootprintTitleText, DarkText, 11f);
        footprintValueLabel = CreateStatValue(footprintRect, "ModuleInventoryFootprintValue", DarkText, 14f);

        RectTransform ownerRect = CreateSection(parent, "ModuleInventoryOwner", new Vector2(0.84f, 0.56f), new Vector2(0.96f, 0.72f), OrangeAlt);
        CreateStatTitle(ownerRect, "ModuleInventoryOwnerTitle", OwnerTitleText, DarkText, 11f);
        ownerValueLabel = CreateStatValue(ownerRect, "ModuleInventoryOwnerValue", DarkText, 12f);
    }
    private void CreateItemListSection(Transform parent)
    {
        listSectionRect = CreateSection(parent, "ModuleInventoryListSection", new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.53f), PrimaryBlue);
        itemListTitleLabel = CreateLabel(listSectionRect, "ModuleInventoryListTitle", ItemListTitleText, new Vector2(0.03f, 0.87f), new Vector2(0.97f, 0.98f), 15f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);

        GameObject scrollObject = CreateUiObject("ModuleInventoryListViewport", listSectionRect, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        ConfigureStretchRect(scrollRectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.84f));

        Image scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0.12f);
        scrollObject.GetComponent<Mask>().showMaskGraphic = false;
        ApplyOutline(scrollObject, new Color(0.88f, 0.94f, 0.99f, 0.55f), Vector2.zero);

        GameObject contentObject = CreateUiObject("ModuleInventoryListContent", scrollRectTransform, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        itemListContentRect = contentObject.GetComponent<RectTransform>();
        itemListContentRect.anchorMin = new Vector2(0f, 1f);
        itemListContentRect.anchorMax = new Vector2(1f, 1f);
        itemListContentRect.pivot = new Vector2(0.5f, 1f);
        itemListContentRect.anchoredPosition = Vector2.zero;
        itemListContentRect.offsetMin = new Vector2(ItemGridPadding, 0f);
        itemListContentRect.offsetMax = new Vector2(-ItemGridPadding, 0f);

        itemListGridLayout = contentObject.GetComponent<GridLayoutGroup>();
        itemListGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        itemListGridLayout.constraintCount = ItemGridColumnCount;
        itemListGridLayout.spacing = new Vector2(ItemGridSpacing, ItemGridSpacing);
        itemListGridLayout.padding = new RectOffset(0, 0, 0, 0);
        itemListGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        itemListGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        itemListGridLayout.childAlignment = TextAnchor.UpperLeft;
        itemListGridLayout.cellSize = new Vector2(72f, 72f);

        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        itemListScrollRect = scrollObject.GetComponent<ScrollRect>();
        itemListScrollRect.viewport = scrollRectTransform;
        itemListScrollRect.content = itemListContentRect;
        itemListScrollRect.horizontal = false;
        itemListScrollRect.vertical = true;
        itemListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        itemListScrollRect.scrollSensitivity = 24f;

        itemListPlaceholderLabel = CreateLabel(scrollRectTransform, "ModuleInventoryListPlaceholder", string.Empty, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f), 12f, FontStyles.Normal, TextAlignmentOptions.Center, PlaceholderTextColor);
        itemListPlaceholderLabel.gameObject.SetActive(false);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveCloseFocus();
        focusedPart = null;
        focusedStructure = null;
        focusedOutputPort = null;
        selectedResourceSlotIndex = -1;
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

    private void HandleRunClicked()
    {
        if (!TryResolveProductionState(out OutputPortProductionState productionState))
        {
            return;
        }

        productionState.ToggleRunning();
        RefreshPanelContent();
    }

    private void HandleItemSlotClicked(int slotIndex)
    {
        if (focusedOutputPort == null)
        {
            return;
        }
        if (slotIndex < 0 || slotIndex >= itemSlotResourceTypes.Count)
        {
            return;
        }
        InventoryResourceType? selectedType = itemSlotResourceTypes[slotIndex];
        if (!selectedType.HasValue)
        {
            return;
        }
        if (TryResolveProductionState(out OutputPortProductionState productionState))
        {
            productionState.SetAssignedResource(selectedType.Value);
            SyncSelectedSlotFromAssignedResource();
        }
        else
        {
            selectedResourceSlotIndex = -1;
        }
        UpdateSlotSelectionVisuals();
        RefreshPanelContent();
    }
    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        if (!HasFocusedModule())
        {
            SetPanelVisible(false);
            return;
        }

        EnsurePanelDrawOrder();
        SetPanelVisible(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        RefreshPanelContent();
    }

    private void RefreshPanelContent()
    {
        if (!HasFocusedModule())
        {
            return;
        }

        OutputPortProductionState productionState = TryResolveProductionState(out OutputPortProductionState resolvedProductionState)
            ? resolvedProductionState
            : null;

        int capacity;
        int stored;
        int free;
        if (focusedOutputPort != null)
        {
            RefreshOutputResourceSnapshot(out capacity, out stored);
            free = Mathf.Max(0, capacity - stored);
        }
        else
        {
            StructureResourceInventory resourceInventory = ResolveFocusedInventory();
            capacity = resourceInventory != null ? resourceInventory.Capacity : 0;
            stored = resourceInventory != null ? resourceInventory.TotalAmount : 0;
            free = Mathf.Max(0, capacity - stored);
        }

        titleLabel.SetText(ResolveModuleTitle());
        subtitleLabel.SetText(ResolveSubtitle(productionState));

        Sprite displayIcon = ResolveDisplayIcon(productionState);
        bool hasIcon = displayIcon != null;
        moduleIconImage.enabled = hasIcon;
        moduleIconImage.sprite = displayIcon;
        moduleIconFallbackLabel.gameObject.SetActive(!hasIcon);
        if (!hasIcon)
        {
            moduleIconFallbackLabel.SetText(ResolveDisplayFallbackText(productionState));
        }

        SetMetricText(capacityValueLabel, capacity.ToString());
        SetMetricText(storedValueLabel, stored.ToString());
        SetMetricText(freeValueLabel, free.ToString());
        SetMetricText(categoryValueLabel, ResolveCategoryLabel());
        SetMetricText(footprintValueLabel, ResolveFootprintLabel());
        SetMetricText(ownerValueLabel, ResolveOwnerLabel());
        RefreshRunButton(productionState);

        if (itemListTitleLabel != null)
        {
            itemListTitleLabel.SetText(focusedOutputPort != null ? OutputListTitleText : ItemListTitleText);
        }

        RefreshItemGrid(capacity);
    }

    private void RefreshOutputResourceSnapshot(out int totalCapacity, out int totalStored)
    {
        outputPortResources.Clear();
        totalCapacity = 0;
        totalStored = 0;
        if (focusedOutputPort == null)
        {
            return;
        }
        OutputPortTransferUtility.BuildOutputResourceAmounts(focusedOutputPort, outputPortResources, out totalCapacity);
        for (int i = 0; i < outputPortResources.Count; i++)
        {
            totalStored += Mathf.Max(0, outputPortResources[i].amount);
        }
    }
    private void RefreshItemGrid(int capacity)
    {
        StructureResourceInventory resourceInventory = focusedOutputPort == null ? ResolveFocusedInventory() : null;
        int slotCount = focusedOutputPort != null
            ? ResolveOutputDisplaySlotCount()
            : ResolveDisplaySlotCount(resourceInventory, capacity);

        EnsureItemSlots(slotCount);
        if (focusedOutputPort != null)
        {
            BindOutputPortSlots(slotCount);
        }
        else
        {
            BindItemSlots(resourceInventory, slotCount);
        }

        UpdateGridCellSize();

        bool hasItems = slotCount > 0;
        itemListPlaceholderLabel.gameObject.SetActive(!hasItems);
        itemListPlaceholderLabel.SetText(focusedOutputPort != null ? EmptyOutputGridText : EmptyGridText);
        itemListContentRect.gameObject.SetActive(hasItems);
    }
    private void EnsureItemSlots(int slotCount)
    {
        if (displayedSlotCount == slotCount)
        {
            return;
        }

        while (itemSlotImages.Count < slotCount)
        {
            CreateItemSlot(itemSlotImages.Count);
        }

        for (int i = 0; i < itemSlotImages.Count; i++)
        {
            bool shouldShow = i < slotCount;
            if (itemSlotImages[i] != null)
            {
                itemSlotImages[i].gameObject.SetActive(shouldShow);
            }
        }

        displayedSlotCount = slotCount;
    }

    private void CreateItemSlot(int slotIndex)
    {
        GameObject slotObject = CreateUiObject($"ModuleInventorySlot{slotIndex}", itemListContentRect, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.localScale = Vector3.one;

        Image slotImage = slotObject.GetComponent<Image>();
        slotImage.color = EmptySlotColor;
        ApplyOutline(slotObject, EmptySlotOutlineColor, Vector2.zero);

        Button slotButton = slotObject.GetComponent<Button>();
        slotButton.targetGraphic = slotImage;
        int capturedIndex = slotIndex;
        slotButton.onClick.AddListener(() => HandleItemSlotClicked(capturedIndex));

        GameObject iconObject = CreateUiObject("Icon", slotRect, typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        ConfigureStretchRect(iconRect, new Vector2(0.16f, 0.36f), new Vector2(0.84f, 0.88f));
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.enabled = false;

        TextMeshProUGUI nameLabel = CreateLabel(slotRect, "Name", string.Empty, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.34f), 8f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        TextMeshProUGUI countLabel = CreateLabel(slotRect, "Count", string.Empty, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.96f), 9f, FontStyles.Bold, TextAlignmentOptions.TopRight, WhiteText);

        itemSlotImages.Add(slotImage);
        itemSlotIconImages.Add(iconImage);
        itemSlotNameLabels.Add(nameLabel);
        itemSlotCountLabels.Add(countLabel);
        itemSlotButtons.Add(slotButton);
        itemSlotResourceTypes.Add(null);
    }

    private void BindItemSlots(StructureResourceInventory resourceInventory, int slotCount)
    {
        for (int i = 0; i < itemSlotImages.Count; i++)
        {
            bool isVisible = i < slotCount;
            if (!isVisible)
            {
                if (i < itemSlotResourceTypes.Count)
                {
                    itemSlotResourceTypes[i] = null;
                }

                if (i < itemSlotButtons.Count && itemSlotButtons[i] != null)
                {
                    itemSlotButtons[i].interactable = false;
                }

                continue;
            }

            if (resourceInventory != null && resourceInventory.TryGetResourceAtSlot(i, out StructureResourceInventory.ResourceAmount resourceAmount))
            {
                itemSlotResourceTypes[i] = resourceAmount.resourceType;
                ApplyFilledSlotVisuals(i, resourceAmount, false, false);
                continue;
            }

            itemSlotResourceTypes[i] = null;
            ApplyEmptySlotVisuals(i);
        }

        selectedResourceSlotIndex = -1;
        UpdateSlotSelectionVisuals();
    }

    private void BindOutputPortSlots(int slotCount)
    {
        for (int i = 0; i < itemSlotImages.Count; i++)
        {
            bool isVisible = i < slotCount;
            if (!isVisible)
            {
                if (i < itemSlotResourceTypes.Count)
                {
                    itemSlotResourceTypes[i] = null;
                }

                if (i < itemSlotButtons.Count && itemSlotButtons[i] != null)
                {
                    itemSlotButtons[i].interactable = false;
                }

                continue;
            }

            if (i < outputPortResources.Count)
            {
                StructureResourceInventory.ResourceAmount resourceAmount = outputPortResources[i];
                itemSlotResourceTypes[i] = resourceAmount.resourceType;
                ApplyFilledSlotVisuals(i, resourceAmount, false, true);
                continue;
            }

            itemSlotResourceTypes[i] = null;
            ApplyEmptySlotVisuals(i);
        }

        SyncSelectedSlotFromAssignedResource();
        UpdateSlotSelectionVisuals();
    }

    private void ApplyFilledSlotVisuals(int slotIndex, StructureResourceInventory.ResourceAmount resourceAmount, bool hideCount, bool isSelectable)
    {
        if (slotIndex < 0 || slotIndex >= itemSlotImages.Count)
        {
            return;
        }

        Image slotImage = itemSlotImages[slotIndex];
        Image iconImage = itemSlotIconImages[slotIndex];
        TextMeshProUGUI nameLabel = itemSlotNameLabels[slotIndex];
        TextMeshProUGUI countLabel = itemSlotCountLabels[slotIndex];

        if (slotImage != null)
        {
            slotImage.color = ResolveResourceSlotColor(resourceAmount.resourceType);
        }

        if (iconImage != null)
        {
            iconImage.sprite = InventoryResourceCatalog.GetSlotSprite(resourceAmount.resourceType);
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameLabel != null)
        {
            nameLabel.SetText(InventoryResourceCatalog.GetCompactLabel(resourceAmount.resourceType));
            nameLabel.color = WhiteText;
        }

        if (countLabel != null)
        {
            countLabel.SetText(hideCount ? string.Empty : $"x{Mathf.Max(0, resourceAmount.amount)}");
            countLabel.color = WhiteText;
        }

        if (slotIndex < itemSlotButtons.Count && itemSlotButtons[slotIndex] != null)
        {
            itemSlotButtons[slotIndex].interactable = isSelectable;
        }

        Outline slotOutline = slotImage != null ? slotImage.GetComponent<Outline>() : null;
        if (slotOutline != null)
        {
            slotOutline.effectColor = FilledSlotOutlineColor;
        }
    }

    private void ApplyEmptySlotVisuals(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= itemSlotImages.Count)
        {
            return;
        }

        Image slotImage = itemSlotImages[slotIndex];
        Image iconImage = itemSlotIconImages[slotIndex];
        TextMeshProUGUI nameLabel = itemSlotNameLabels[slotIndex];
        TextMeshProUGUI countLabel = itemSlotCountLabels[slotIndex];

        if (slotImage != null)
        {
            slotImage.color = EmptySlotColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameLabel != null)
        {
            nameLabel.SetText(string.Empty);
        }

        if (countLabel != null)
        {
            countLabel.SetText(string.Empty);
        }

        if (slotIndex < itemSlotButtons.Count && itemSlotButtons[slotIndex] != null)
        {
            itemSlotButtons[slotIndex].interactable = false;
        }

        Outline slotOutline = slotImage != null ? slotImage.GetComponent<Outline>() : null;
        if (slotOutline != null)
        {
            slotOutline.effectColor = EmptySlotOutlineColor;
        }
    }

    private void UpdateGridCellSize()
    {
        if (itemListGridLayout == null || itemListScrollRect == null)
        {
            return;
        }

        RectTransform viewport = itemListScrollRect.viewport;
        float viewportWidth = viewport != null ? viewport.rect.width : 0f;
        if (viewportWidth <= 0f && listSectionRect != null)
        {
            viewportWidth = listSectionRect.rect.width * 0.94f;
        }

        float usableWidth = Mathf.Max(0f, viewportWidth - (ItemGridPadding * 2f) - (ItemGridSpacing * (ItemGridColumnCount - 1)));
        float cellSize = usableWidth > 0f ? usableWidth / ItemGridColumnCount : MinItemGridCellSize;
        cellSize = Mathf.Max(MinItemGridCellSize, cellSize);
        itemListGridLayout.cellSize = new Vector2(cellSize, cellSize);
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemListContentRect);
    }
    private StructureResourceInventory ResolveFocusedInventory()
    {
        if (focusedOutputPort != null)
        {
            if (ModulePartInventoryUtility.TryResolveOutputInventory(focusedOutputPort, out StructureResourceInventory outputInventory) && outputInventory != null)
            {
                return outputInventory;
            }
        }
        if (focusedPart != null)
        {
            if (ModulePartInventoryUtility.TryResolveDisplayedInventory(focusedPart, out StructureResourceInventory inventory) && inventory != null)
            {
                return inventory;
            }
        }
        if (focusedStructure != null)
        {
            Structure sourceStructure = focusedStructure.SourceStructure;
            StructureResourceInventory inventory = focusedStructure.GetComponent<StructureResourceInventory>();
            if (inventory == null)
            {
                inventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(focusedStructure.gameObject);
                inventory.InitializeForStructure(sourceStructure);
            }
            return inventory;
        }
        return null;
    }
    private string ResolveModuleTitle()
    {
        if (focusedOutputPort != null && !string.IsNullOrWhiteSpace(focusedOutputPort.DisplayName))
        {
            return focusedOutputPort.DisplayName.ToUpperInvariant();
        }

        if (focusedPart != null && focusedPart.SourcePart != null && !string.IsNullOrWhiteSpace(focusedPart.SourcePart.partName))
        {
            return focusedPart.SourcePart.partName.ToUpperInvariant();
        }

        if (focusedStructure != null && focusedStructure.SourceStructure != null && !string.IsNullOrWhiteSpace(focusedStructure.SourceStructure.structureName))
        {
            return focusedStructure.SourceStructure.structureName.ToUpperInvariant();
        }

        return DefaultTitleText;
    }

    private Sprite ResolveDisplayIcon(OutputPortProductionState productionState)
    {
        if (focusedOutputPort != null)
        {
            if (TryResolveSelectedResource(out _, out Sprite selectedSprite))
            {
                return selectedSprite;
            }

            if (productionState != null && productionState.HasProducedResource)
            {
                return InventoryResourceCatalog.GetSlotSprite(productionState.ProducedResourceType);
            }
        }

        return ResolveModuleIcon();
    }

    private string ResolveDisplayFallbackText(OutputPortProductionState productionState)
    {
        if (focusedOutputPort != null)
        {
            if (TryResolveSelectedResource(out InventoryResourceType selectedResourceType, out _))
            {
                return InventoryResourceCatalog.GetCompactLabel(selectedResourceType);
            }

            if (productionState != null && productionState.HasProducedResource)
            {
                return InventoryResourceCatalog.GetCompactLabel(productionState.ProducedResourceType);
            }

            return OutputIconText;
        }

        return EmptyIconText;
    }

    private Sprite ResolveModuleIcon()
    {
        if (focusedOutputPort != null)
        {
            return focusedOutputPort.DisplayIcon;
        }

        if (focusedPart != null && focusedPart.SourcePart != null)
        {
            return focusedPart.SourcePart.partIcon;
        }

        if (focusedStructure != null && focusedStructure.SourceStructure != null)
        {
            return focusedStructure.SourceStructure.structureIcon;
        }

        return null;
    }

    private string ResolveSubtitle(OutputPortProductionState productionState)
    {
        if (focusedOutputPort != null)
        {
            if (productionState == null)
            {
                return "Output selection unavailable.";
            }

            return productionState.GetDetailedStatusDescription();
        }

        StructureResourceInventory resourceInventory = ResolveFocusedInventory();
        if (resourceInventory == null)
        {
            return DefaultSubtitleText;
        }

        return $"{resourceInventory.TotalAmount} stored / {resourceInventory.FreeCapacity} free";
    }

    private string ResolveCategoryLabel()
    {
        if (focusedOutputPort != null)
        {
            return "OUTPUT PORT";
        }

        if (focusedPart != null && focusedPart.SourcePart != null)
        {
            return focusedPart.SourcePart.partType.ToString().ToUpperInvariant();
        }

        if (focusedStructure != null && focusedStructure.SourceStructure != null)
        {
            StructureFacilityKind facilityKind = focusedStructure.SourceStructure.facilityKind;
            return facilityKind == StructureFacilityKind.None
                ? "STRUCTURE"
                : facilityKind.ToString().ToUpperInvariant();
        }

        return "-";
    }

    private string ResolveFootprintLabel()
    {
        if (focusedPart != null && focusedPart.SourcePart != null)
        {
            return $"{focusedPart.SourcePart.gridWidth} x {focusedPart.SourcePart.gridHeight}";
        }

        if (focusedStructure != null && focusedStructure.SourceStructure != null)
        {
            return $"{focusedStructure.SourceStructure.gridWidth} x {focusedStructure.SourceStructure.gridHeight}";
        }

        if (focusedOutputPort != null && !string.IsNullOrWhiteSpace(focusedOutputPort.SideLabel))
        {
            return focusedOutputPort.SideLabel.ToUpperInvariant();
        }

        return "-";
    }

    private string ResolveOwnerLabel()
    {
        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        return ownerSatellite != null ? ownerSatellite.name : "-";
    }

    private Transform ResolveCloseFocus()
    {
        if (focusedOutputPort != null)
        {
            return focusedOutputPort.ResolveCloseFocus();
        }

        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        return ownerSatellite != null ? ownerSatellite.transform : null;
    }

    private ArtificialSatellite ResolveOwnerSatellite()
    {
        if (focusedOutputPort != null)
        {
            return focusedOutputPort.OwnerSatellite;
        }

        if (focusedPart != null)
        {
            return focusedPart.OwnerSatellite;
        }

        if (focusedStructure != null)
        {
            return focusedStructure.OwnerSatellite;
        }

        return null;
    }

    private bool HasFocusedModule()
    {
        return focusedOutputPort != null || focusedPart != null || focusedStructure != null;
    }
    private void RefreshRunButton(OutputPortProductionState productionState)
    {
        if (runButton == null)
        {
            return;
        }

        bool isOutputPort = focusedOutputPort != null;
        runButton.gameObject.SetActive(isOutputPort);
        if (!isOutputPort)
        {
            return;
        }

        bool isRunning = productionState != null && productionState.IsRunning;
        string disabledReason = string.Empty;
        bool canStart = productionState != null && productionState.CanStartProduction(out disabledReason);
        bool interactable = productionState != null && (isRunning || canStart);

        runButton.interactable = interactable;
        if (runButtonBackground != null)
        {
            runButtonBackground.color = isRunning
                ? Orange
                : interactable
                    ? SecondaryBlue
                    : DisabledButtonColor;
        }

        if (runButtonLabel != null)
        {
            runButtonLabel.SetText(isRunning ? "STOP" : "RUN");
            runButtonLabel.color = isRunning
                ? DarkText
                : interactable
                    ? WhiteText
                    : DisabledButtonTextColor;
        }
    }

    private bool TryResolveProductionState(out OutputPortProductionState productionState)
    {
        productionState = null;
        if (focusedOutputPort == null)
        {
            return false;
        }

        productionState = ComponentUtility.GetOrAddComponent<OutputPortProductionState>(focusedOutputPort.gameObject);
        return productionState != null;
    }

    private void SyncSelectedSlotFromAssignedResource()
    {
        selectedResourceSlotIndex = -1;
        if (!TryResolveProductionState(out OutputPortProductionState productionState) ||
            !productionState.TryGetAssignedResource(out InventoryResourceType assignedResourceType))
        {
            return;
        }
        for (int i = 0; i < itemSlotResourceTypes.Count; i++)
        {
            if (!itemSlotResourceTypes[i].HasValue || itemSlotResourceTypes[i].Value != assignedResourceType)
            {
                continue;
            }
            selectedResourceSlotIndex = i;
            break;
        }
    }
    private bool TryResolveSelectedResource(out InventoryResourceType resourceType, out Sprite sprite)
    {
        resourceType = default;
        sprite = null;

        if (TryResolveProductionState(out OutputPortProductionState productionState) &&
            productionState.TryGetAssignedResource(out InventoryResourceType assignedResourceType))
        {
            resourceType = assignedResourceType;
            sprite = InventoryResourceCatalog.GetSlotSprite(resourceType);
            return true;
        }

        if (selectedResourceSlotIndex < 0 || selectedResourceSlotIndex >= itemSlotResourceTypes.Count)
        {
            return false;
        }

        InventoryResourceType? selectedType = itemSlotResourceTypes[selectedResourceSlotIndex];
        if (!selectedType.HasValue)
        {
            return false;
        }

        resourceType = selectedType.Value;
        sprite = InventoryResourceCatalog.GetSlotSprite(resourceType);
        return true;
    }

    private void UpdateSlotSelectionVisuals()
    {
        for (int i = 0; i < itemSlotImages.Count; i++)
        {
            Image slotImage = itemSlotImages[i];
            if (slotImage == null)
            {
                continue;
            }

            bool hasResource = i < itemSlotResourceTypes.Count && itemSlotResourceTypes[i].HasValue;
            bool isSelected = focusedOutputPort != null && i == selectedResourceSlotIndex && hasResource;
            slotImage.transform.localScale = isSelected ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;

            Outline slotOutline = slotImage.GetComponent<Outline>();
            if (slotOutline != null)
            {
                slotOutline.effectColor = isSelected
                    ? SelectedSlotOutlineColor
                    : hasResource
                        ? FilledSlotOutlineColor
                        : EmptySlotOutlineColor;
            }
        }
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

    private static int ResolveDisplaySlotCount(StructureResourceInventory resourceInventory, int capacity)
    {
        int resourceCount = resourceInventory != null ? resourceInventory.ResourceTypeCount : 0;
        int logicalSlotCount = Mathf.Max(Mathf.Max(0, capacity), resourceCount);
        if (logicalSlotCount <= 0)
        {
            return 0;
        }

        int rowCount = Mathf.CeilToInt(logicalSlotCount / (float)ItemGridColumnCount);
        return Mathf.Max(ItemGridColumnCount, rowCount * ItemGridColumnCount);
    }

    private int ResolveOutputDisplaySlotCount()
    {
        int logicalSlotCount = outputPortResources.Count;
        if (logicalSlotCount <= 0)
        {
            return 0;
        }

        int rowCount = Mathf.CeilToInt(logicalSlotCount / (float)ItemGridColumnCount);
        return Mathf.Max(ItemGridColumnCount, rowCount * ItemGridColumnCount);
    }

    private static bool TryResolveFocusedModule(Transform focused, out AssemblyPartFocus partFocus, out StructureFocus structureFocus)
    {
        partFocus = null;
        structureFocus = null;
        if (focused == null)
        {
            return false;
        }

        if (focused.GetComponentInParent<AssemblyOutputPortFocus>() != null)
        {
            return false;
        }

        StructureFocus directStructure = focused.GetComponent<StructureFocus>();
        if (directStructure == null)
        {
            directStructure = focused.GetComponentInParent<StructureFocus>();
        }

        if (directStructure != null)
        {
            if (directStructure.UsesLogisticsHubUi || directStructure.UsesFabricatorUi || CorePowerControlUtility.IsCorePowerControlFocus(directStructure))
            {
                return false;
            }

            if (directStructure.OwnerSatellite == null)
            {
                return false;
            }

            structureFocus = directStructure;
            return true;
        }

        AssemblyPartFocus directPart = focused.GetComponent<AssemblyPartFocus>();
        if (directPart == null)
        {
            directPart = focused.GetComponentInParent<AssemblyPartFocus>();
        }

        if (directPart == null || directPart.SourcePart == null || directPart.OwnerSatellite == null)
        {
            return false;
        }

        if (directPart.SourcePart.partType == PartType.Pipe || directPart.SourcePart.partType == PartType.Core)
        {
            return false;
        }

        if (string.Equals(directPart.SourcePart.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SolarPanelUtility.IsSolarPanelPart(directPart.SourcePart) || SolarTurbineUtility.IsSolarTurbinePart(directPart.SourcePart))
        {
            return false;
        }

        partFocus = directPart;
        return true;
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

        return EmptySlotColor;
    }

    private static void SetMetricText(TextMeshProUGUI label, string value)
    {
        if (label != null)
        {
            label.SetText(value);
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

    private static RectTransform CreateSection(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor)
    {
        GameObject sectionObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        ConfigureStretchRect(sectionRect, anchorMin, anchorMax);

        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = backgroundColor;
        ApplyOutline(sectionObject, new Color(1f, 1f, 1f, 0.10f), Vector2.zero);
        return sectionRect;
    }

    private static TextMeshProUGUI CreateStatTitle(RectTransform parent, string name, string text, Color color, float fontSize)
    {
        return CreateLabel(parent, name, text, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.96f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, color);
    }

    private static TextMeshProUGUI CreateStatValue(RectTransform parent, string name, Color color, float fontSize)
    {
        return CreateLabel(parent, name, "-", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.58f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, color);
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
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
        label.enableWordWrapping = true;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(7f, fontSize * 0.55f);
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string labelText,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        out Image backgroundImage,
        out TextMeshProUGUI label,
        Color backgroundColor,
        Color textColor)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        ApplyOutline(buttonObject, new Color(1f, 1f, 1f, 0.14f), Vector2.zero);

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

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}

