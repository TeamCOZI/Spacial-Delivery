using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(33999)]
[DisallowMultipleComponent]
public class ModuleProcessingPresenter : FocusEventSubscriber
{
    private sealed class ResourceSlotView
    {
        public Image background;
        public Image icon;
        public TextMeshProUGUI countLabel;
    }

    private const string LauncherPartName = "Launcher";
    private const string PanelObjectName = "ModuleProcessingUIPanel";
    private const string DefaultTitleText = "MODULE";
    private const string InputTitleText = "INPUT CAPACITY";
    private const string OutputTitleText = "OUTPUT CAPACITY";
    private const string RecipeTitleText = "PROCESS RECIPE";
    private const string EmptySectionText = "EMPTY";
    private const string NoRecipeText = "NO RECIPES";
    private const string SelectRecipeText = "SELECT RECIPE";
    private const int PanelSortingOrderOffset = 206;
    private const int CapacityColumnCount = 2;
    private const float CapacitySpacing = 8f;
    private const float CapacityCellSize = 62f;

    private static readonly Color PanelBackgroundColor = new Color(0.53f, 0.53f, 0.53f, 0.96f);
    private static readonly Color PanelBorderColor = new Color(0.92f, 0.92f, 0.92f, 0.28f);
    private static readonly Color InputColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color OutputColor = new Color(0.88f, 0.48f, 0.24f, 0.98f);
    private static readonly Color RecipeAreaColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color PreviewColor = new Color(0.94f, 0.95f, 0.97f, 1f);
    private static readonly Color PreviewAccentColor = new Color(0.76f, 0.88f, 0.96f, 1f);
    private static readonly Color RecipeButtonColor = new Color(0.78f, 0.92f, 0.74f, 1f);
    private static readonly Color RecipeButtonActiveColor = new Color(0.96f, 0.83f, 0.55f, 1f);
    private static readonly Color PowerButtonColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color PowerButtonActiveColor = new Color(0.28f, 0.72f, 0.50f, 1f);
    private static readonly Color PowerButtonOffActiveColor = new Color(0.84f, 0.35f, 0.32f, 1f);
    private static readonly Color CloseButtonColor = new Color(0.97f, 0.20f, 0.13f, 1f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.11f, 0.11f, 0.11f, 1f);

    private static ModuleProcessingPresenter instance;
    private static Sprite cachedCircularButtonSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private GraphicRaycaster panelRaycaster;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private Button closeButton;
    private Image closeButtonBackground;
    private TextMeshProUGUI closeButtonLabel;
    private RectTransform inputContentRect;
    private RectTransform outputContentRect;
    private GridLayoutGroup inputGridLayout;
    private GridLayoutGroup outputGridLayout;
    private TextMeshProUGUI inputPlaceholderLabel;
    private TextMeshProUGUI outputPlaceholderLabel;
    private readonly List<ResourceSlotView> inputSlotViews = new List<ResourceSlotView>();
    private readonly List<ResourceSlotView> outputSlotViews = new List<ResourceSlotView>();
    private Image previewInputIcon;
    private TextMeshProUGUI previewInputCountLabel;
    private Image previewOutputIcon;
    private TextMeshProUGUI previewOutputCountLabel;
    private TextMeshProUGUI processTimeLabel;
    private TextMeshProUGUI previewProcessTimeLabel;
    private TextMeshProUGUI previewPlaceholderLabel;
    private RectTransform recipeButtonsRowRect;
    private readonly List<Button> recipeButtons = new List<Button>();
    private readonly List<Image> recipeButtonBackgrounds = new List<Image>();
    private readonly List<TextMeshProUGUI> recipeButtonLabels = new List<TextMeshProUGUI>();
    private Button powerOnButton;
    private Image powerOnButtonBackground;
    private Button powerOffButton;
    private Image powerOffButtonBackground;
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
        focusedPart = TryResolveFocusedModulePart(focused, out AssemblyPartFocus partFocus)
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
        panelRect.anchorMin = new Vector2(0.52f, 0.14f);
        panelRect.anchorMax = new Vector2(0.98f, 0.94f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, PanelBorderColor, new Vector2(2f, -2f));

        panelCanvas = panelObject.GetComponent<Canvas>();
        panelRaycaster = panelObject.GetComponent<GraphicRaycaster>();
        EnsurePanelDrawOrder();

        CreateHeader(panelObject.transform);
        CreateCapacitySections(panelObject.transform);
        CreateRecipeSection(panelObject.transform);
        CreatePowerButtons(panelObject.transform);
    }

    private void CreateHeader(Transform parent)
    {
        titleLabel = CreateLabel(parent, "ModuleProcessTitle", DefaultTitleText, new Vector2(0.03f, 0.90f), new Vector2(0.70f, 0.98f), 22f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        statusLabel = CreateLabel(parent, "ModuleProcessStatus", string.Empty, new Vector2(0.03f, 0.84f), new Vector2(0.70f, 0.90f), 11f, FontStyles.Normal, TextAlignmentOptions.Left, WhiteText);
        closeButton = CreateButton(parent, "ModuleProcessCloseButton", "X", new Vector2(0.90f, 0.89f), new Vector2(0.98f, 0.98f), 18f, out closeButtonBackground, out closeButtonLabel, CloseButtonColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateCapacitySections(Transform parent)
    {
        RectTransform inputSectionRect = CreateSection(parent, "ModuleProcessInput", new Vector2(0.06f, 0.47f), new Vector2(0.42f, 0.84f), InputColor);
        _ = CreateLabel(inputSectionRect, "ModuleProcessInputTitle", InputTitleText, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), 13f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        CreateCapacityGrid(inputSectionRect, "ModuleProcessInputGrid", out inputContentRect, out inputGridLayout, out inputPlaceholderLabel);

        RectTransform outputSectionRect = CreateSection(parent, "ModuleProcessOutput", new Vector2(0.58f, 0.47f), new Vector2(0.94f, 0.84f), OutputColor);
        _ = CreateLabel(outputSectionRect, "ModuleProcessOutputTitle", OutputTitleText, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), 13f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        CreateCapacityGrid(outputSectionRect, "ModuleProcessOutputGrid", out outputContentRect, out outputGridLayout, out outputPlaceholderLabel);

        _ = CreateLabel(parent, "ModuleProcessArrowLabel", ">>>", new Vector2(0.43f, 0.58f), new Vector2(0.57f, 0.70f), 22f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        processTimeLabel = CreateLabel(parent, "ModuleProcessTimeLabel", string.Empty, new Vector2(0.43f, 0.53f), new Vector2(0.57f, 0.60f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
    }

    private void CreateCapacityGrid(RectTransform parent, string name, out RectTransform contentRect, out GridLayoutGroup gridLayout, out TextMeshProUGUI placeholderLabel)
    {
        GameObject contentObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(GridLayoutGroup));
        contentRect = contentObject.GetComponent<RectTransform>();
        ConfigureStretchRect(contentRect, new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.74f));

        gridLayout = contentObject.GetComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = CapacityColumnCount;
        gridLayout.spacing = new Vector2(CapacitySpacing, CapacitySpacing);
        gridLayout.padding = new RectOffset(0, 0, 0, 0);
        gridLayout.cellSize = new Vector2(CapacityCellSize, CapacityCellSize);
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        placeholderLabel = CreateLabel(parent, name + "Placeholder", EmptySectionText, new Vector2(0.12f, 0.26f), new Vector2(0.88f, 0.60f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        placeholderLabel.gameObject.SetActive(false);
    }

    private void CreateRecipeSection(Transform parent)
    {
        RectTransform recipeSectionRect = CreateSection(parent, "ModuleProcessRecipeSection", new Vector2(0.06f, 0.06f), new Vector2(0.78f, 0.44f), RecipeAreaColor);
        _ = CreateLabel(recipeSectionRect, "ModuleProcessRecipeTitle", RecipeTitleText, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), 14f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);

        RectTransform previewRect = CreateSection(recipeSectionRect, "ModuleProcessRecipePreview", new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.82f), PreviewColor);
        previewPlaceholderLabel = CreateLabel(previewRect, "ModuleProcessPreviewPlaceholder", SelectRecipeText, new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.78f), 15f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        RectTransform inputCard = CreateSection(previewRect, "ModuleProcessPreviewInputCard", new Vector2(0.08f, 0.18f), new Vector2(0.34f, 0.82f), PreviewAccentColor);
        previewInputIcon = CreateIconImage(inputCard, "ModuleProcessPreviewInputIcon", new Vector2(0.18f, 0.36f), new Vector2(0.82f, 0.88f));
        previewInputCountLabel = CreateLabel(inputCard, "ModuleProcessPreviewInputCount", string.Empty, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.28f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        _ = CreateLabel(previewRect, "ModuleProcessPreviewArrow", ">>>", new Vector2(0.38f, 0.40f), new Vector2(0.62f, 0.66f), 22f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.48f, 0.48f, 0.48f, 1f));
        previewProcessTimeLabel = CreateLabel(previewRect, "ModuleProcessPreviewTime", string.Empty, new Vector2(0.38f, 0.22f), new Vector2(0.62f, 0.40f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        RectTransform outputCard = CreateSection(previewRect, "ModuleProcessPreviewOutputCard", new Vector2(0.66f, 0.18f), new Vector2(0.92f, 0.82f), PreviewAccentColor);
        previewOutputIcon = CreateIconImage(outputCard, "ModuleProcessPreviewOutputIcon", new Vector2(0.18f, 0.36f), new Vector2(0.82f, 0.88f));
        previewOutputCountLabel = CreateLabel(outputCard, "ModuleProcessPreviewOutputCount", string.Empty, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.28f), 12f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        GameObject buttonRowObject = CreateUiObject("ModuleProcessRecipeButtons", recipeSectionRect, typeof(RectTransform));
        recipeButtonsRowRect = buttonRowObject.GetComponent<RectTransform>();
        ConfigureStretchRect(recipeButtonsRowRect, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.24f));
    }
    private void CreatePowerButtons(Transform parent)
    {
        powerOnButton = CreateButton(parent, "ModuleProcessPowerOn", "ON", new Vector2(0.82f, 0.24f), new Vector2(0.96f, 0.44f), 18f, out powerOnButtonBackground, out _, PowerButtonColor, WhiteText, false);
        powerOnButton.onClick.AddListener(() => HandlePowerClicked(true));

        powerOffButton = CreateButton(parent, "ModuleProcessPowerOff", "OFF", new Vector2(0.82f, 0.06f), new Vector2(0.96f, 0.22f), 18f, out powerOffButtonBackground, out _, PowerButtonColor, WhiteText, false);
        powerOffButton.onClick.AddListener(() => HandlePowerClicked(false));
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

    private void HandleRecipeClicked(int recipeIndex)
    {
        ModuleProcessingState processingState = ResolveProcessingState();
        if (processingState == null)
        {
            return;
        }

        processingState.SelectRecipe(recipeIndex);
        RefreshPanelContent();
    }

    private void HandlePowerClicked(bool poweredOn)
    {
        ModuleProcessingState processingState = ResolveProcessingState();
        if (processingState == null)
        {
            return;
        }

        processingState.SetPowerState(poweredOn);
        RefreshPanelContent();
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        bool visible = focusedPart != null;
        SetPanelVisible(visible);
        if (!visible)
        {
            return;
        }

        EnsurePanelDrawOrder();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        RefreshPanelContent();
    }

    private void RefreshPanelContent()
    {
        if (focusedPart == null || focusedPart.SourcePart == null)
        {
            return;
        }

        ModuleProcessingState processingState = ResolveProcessingState();
        titleLabel.SetText(focusedPart.SourcePart.partName.ToUpperInvariant());
        statusLabel.SetText(processingState != null ? processingState.StatusLabel : string.Empty);

        ModulePartInventoryUtility.TryResolveInputInventory(focusedPart, out StructureResourceInventory inputInventory);
        ModulePartInventoryUtility.TryResolveOutputInventory(focusedPart, out StructureResourceInventory outputInventory);
        RefreshCapacitySlots(inputInventory, inputContentRect, inputGridLayout, inputSlotViews, inputPlaceholderLabel);
        RefreshCapacitySlots(outputInventory, outputContentRect, outputGridLayout, outputSlotViews, outputPlaceholderLabel);
        RefreshProcessTime(processingState);
        RefreshRecipePreview(processingState);
        RefreshRecipeButtons(processingState);
        RefreshPowerButtons(processingState);
    }

    private void RefreshCapacitySlots(
        StructureResourceInventory inventory,
        RectTransform contentRect,
        GridLayoutGroup gridLayout,
        List<ResourceSlotView> slotViews,
        TextMeshProUGUI placeholderLabel)
    {
        IReadOnlyList<StructureResourceInventory.ResourceAmount> resources = inventory != null ? inventory.Resources : Array.Empty<StructureResourceInventory.ResourceAmount>();
        EnsureCapacitySlotCount(contentRect, slotViews, resources.Count);
        for (int i = 0; i < slotViews.Count; i++)
        {
            bool visible = i < resources.Count;
            ResourceSlotView view = slotViews[i];
            if (view.background != null)
            {
                view.background.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                continue;
            }

            StructureResourceInventory.ResourceAmount resourceAmount = resources[i];
            view.icon.sprite = InventoryResourceCatalog.GetSlotSprite(resourceAmount.resourceType);
            view.icon.enabled = view.icon.sprite != null;
            view.background.color = ResolveResourceSlotColor(resourceAmount.resourceType);
            view.countLabel.SetText($"x{Mathf.Max(0, resourceAmount.amount)}");
        }

        if (placeholderLabel != null)
        {
            placeholderLabel.gameObject.SetActive(resources.Count <= 0);
        }

        if (contentRect != null)
        {
            float availableWidth = contentRect.rect.width;
            if (availableWidth > 0f && gridLayout != null)
            {
                float spacing = gridLayout.spacing.x * (CapacityColumnCount - 1);
                float cellSize = Mathf.Max(44f, (availableWidth - spacing) / CapacityColumnCount);
                gridLayout.cellSize = new Vector2(cellSize, cellSize);
            }
        }
    }

    private void EnsureCapacitySlotCount(RectTransform parent, List<ResourceSlotView> slotViews, int slotCount)
    {
        while (slotViews.Count < slotCount)
        {
            ResourceSlotView view = CreateResourceSlot(parent, "ModuleProcessSlot" + slotViews.Count);
            slotViews.Add(view);
        }

        for (int i = 0; i < slotViews.Count; i++)
        {
            bool visible = i < slotCount;
            if (slotViews[i].background != null)
            {
                slotViews[i].background.gameObject.SetActive(visible);
            }
        }
    }

    private ResourceSlotView CreateResourceSlot(RectTransform parent, string name)
    {
        GameObject slotObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        Image slotBackground = slotObject.GetComponent<Image>();
        slotBackground.color = new Color(1f, 1f, 1f, 0.20f);
        ApplyOutline(slotObject, new Color(1f, 1f, 1f, 0.18f), Vector2.zero);

        Image icon = CreateIconImage(slotObject.transform, name + "Icon", new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.84f));
        TextMeshProUGUI countLabel = CreateLabel(slotObject.transform, name + "Count", string.Empty, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.26f), 11f, FontStyles.Bold, TextAlignmentOptions.Center, WhiteText);
        return new ResourceSlotView
        {
            background = slotBackground,
            icon = icon,
            countLabel = countLabel
        };
    }

    private void RefreshProcessTime(ModuleProcessingState processingState)
    {
        if (processTimeLabel == null)
        {
            return;
        }

        ModuleRecipeDefinition recipe = default;
        bool hasRecipe = processingState != null && processingState.TryGetSelectedRecipe(out recipe);
        processTimeLabel.SetText(hasRecipe ? $"{Mathf.Max(0.01f, recipe.processSeconds):0.#}s" : string.Empty);
    }

    private void RefreshRecipePreview(ModuleProcessingState processingState)
    {
        ModuleRecipeDefinition recipe = default;
        bool hasRecipe = processingState != null && processingState.TryGetSelectedRecipe(out recipe);
        if (!hasRecipe)
        {
            previewInputIcon.enabled = false;
            previewOutputIcon.enabled = false;
            previewInputCountLabel.SetText(string.Empty);
            previewOutputCountLabel.SetText(string.Empty);
            previewProcessTimeLabel.SetText(string.Empty);
            int recipeCount = ModuleRecipeCatalog.GetRecipes(focusedPart).Count;
            previewPlaceholderLabel.SetText(recipeCount > 0 ? SelectRecipeText : NoRecipeText);
            previewPlaceholderLabel.gameObject.SetActive(true);
            return;
        }

        previewPlaceholderLabel.gameObject.SetActive(false);
        previewInputIcon.sprite = InventoryResourceCatalog.GetSlotSprite(recipe.inputResourceType);
        previewInputIcon.enabled = previewInputIcon.sprite != null;
        previewOutputIcon.sprite = InventoryResourceCatalog.GetSlotSprite(recipe.outputResourceType);
        previewOutputIcon.enabled = previewOutputIcon.sprite != null;
        previewInputCountLabel.SetText($"x{recipe.inputAmount}");
        previewOutputCountLabel.SetText($"x{recipe.outputAmount}");
        previewProcessTimeLabel.SetText($"{Mathf.Max(0.01f, recipe.processSeconds):0.#}s");
    }

    private void RefreshRecipeButtons(ModuleProcessingState processingState)
    {
        IReadOnlyList<ModuleRecipeDefinition> recipes = ModuleRecipeCatalog.GetRecipes(focusedPart);
        EnsureRecipeButtonCount(recipes.Count);
        int selectedIndex = processingState != null ? processingState.SelectedRecipeIndex : -1;
        for (int i = 0; i < recipeButtons.Count; i++)
        {
            bool visible = i < recipes.Count;
            recipeButtons[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            recipeButtonLabels[i].SetText($"R{i + 1}");
            recipeButtonBackgrounds[i].color = i == selectedIndex ? RecipeButtonActiveColor : RecipeButtonColor;
            recipeButtonLabels[i].color = DarkText;
        }
    }
    private void EnsureRecipeButtonCount(int count)
    {
        while (recipeButtons.Count < count)
        {
            int buttonIndex = recipeButtons.Count;
            float width = 0.15f;
            float gap = 0.015f;
            float minX = 0.01f + buttonIndex * (width + gap);
            float maxX = Mathf.Min(0.99f, minX + width);
            Button button = CreateButton(recipeButtonsRowRect, "ModuleProcessRecipeButton" + buttonIndex, "R" + (buttonIndex + 1), new Vector2(minX, 0.10f), new Vector2(maxX, 0.90f), 11f, out Image background, out TextMeshProUGUI label, RecipeButtonColor, DarkText, false);
            int capturedIndex = buttonIndex;
            button.onClick.AddListener(() => HandleRecipeClicked(capturedIndex));
            recipeButtons.Add(button);
            recipeButtonBackgrounds.Add(background);
            recipeButtonLabels.Add(label);
        }
    }

    private void RefreshPowerButtons(ModuleProcessingState processingState)
    {
        bool isOn = processingState != null && processingState.IsPoweredOn;
        if (powerOnButtonBackground != null)
        {
            powerOnButtonBackground.color = isOn ? PowerButtonActiveColor : PowerButtonColor;
        }

        if (powerOffButtonBackground != null)
        {
            powerOffButtonBackground.color = isOn ? PowerButtonColor : PowerButtonOffActiveColor;
        }
    }

    private ModuleProcessingState ResolveProcessingState()
    {
        return focusedPart != null ? ComponentUtility.GetOrAddComponent<ModuleProcessingState>(focusedPart.gameObject) : null;
    }

    private Transform ResolveCloseFocus()
    {
        return focusedPart != null && focusedPart.OwnerSatellite != null
            ? focusedPart.OwnerSatellite.transform
            : null;
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

    private static bool TryResolveFocusedModulePart(Transform focused, out AssemblyPartFocus partFocus)
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

        if (partFocus.SourcePart.partType == PartType.Core || partFocus.SourcePart.partType == PartType.Pipe)
        {
            partFocus = null;
            return false;
        }

        if (string.Equals(partFocus.SourcePart.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase))
        {
            partFocus = null;
            return false;
        }

        return true;
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

    private static Image CreateIconImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject iconObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        ConfigureStretchRect(iconRect, anchorMin, anchorMax);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.color = Color.white;
        iconImage.enabled = false;
        return iconImage;
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
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
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
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;

        label = CreateLabel(buttonRect, name + "Label", labelText, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
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

        return new Color(1f, 1f, 1f, 0.20f);
    }

    private static Sprite ResolveCircularButtonSprite()
    {
        if (cachedCircularButtonSprite != null)
        {
            return cachedCircularButtonSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "ModuleProcessUIButtonCircle";
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
        cachedCircularButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return cachedCircularButtonSprite;
    }
}


