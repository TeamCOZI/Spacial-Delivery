using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public partial class AssemblyUI : MonoBehaviour
{
    private const string CorePartName = "Core";
    private const string LauncherPartName = "Launcher";
    private const string RootElementName = "Root";
    private const string CloseButtonName = "Close";
    private const string OpenPartsButtonName = "OpenParts";
    private const string OpenStructuresButtonName = "OpenStructures";
    private const string ToggleSelectionButtonName = "ToggleSelection";
    private const string RemoveSelectedButtonName = "RemoveSelected";
    private const string PopupElementName = "PopUp";
    private const string StructuresPopupElementName = "StructuresPopUp";
    private const string ClosePartsButtonName = "CloseParts";
    private const string CloseStructuresButtonName = "CloseStructures";
    private const string PartsListName = "PartsList";
    private const string StructuresListName = "StructuresList";
    private const string PartNameLabelName = "PartName";
    private const string PartIconElementName = "PartIcon";

    public static AssemblyUI Instance { get; private set; }

    public VisualTreeAsset partPrefab;

    private UIDocument uiDocument;
    private VisualElement root;
    private Button close;
    private Button openParts;
    private Button openStructures;
    private Button toggleSelection;
    private Button removeSelected;
    private Button closeParts;
    private Button closeStructures;
    private VisualElement parts;
    private VisualElement structuresPopup;
    private ScrollView partsList;
    private ScrollView structuresList;
    private bool lastKnownSelectionMode;
    private bool lastHasSelection;
    private bool assemblyUiVisibleRequested;

    public bool IsWorldInputBlockedByUi => TryGetHoveredAssemblyUiElement(out _);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (!TryGetRootVisualElement(out VisualElement visualElement))
        {
            return;
        }

        if (!TryResolveUiElements(visualElement))
        {
            return;
        }

        RegisterCallbacks();
        ConfigurePartsGrid();
        ConfigureStructuresGrid();
        ConfigureFabricatorUi();
        Hide();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void Update()
    {
        RefreshUiState();

        if (!TryGetAssembly(out Assembly assembly))
        {
            UpdateSelectionButtonsState(false, false);
            return;
        }

        bool isSelectionMode = assembly.IsSelectionModeActive;
        bool hasSelection = assembly.HasSelection;
        if (isSelectionMode == lastKnownSelectionMode && hasSelection == lastHasSelection) return;

        UpdateSelectionButtonsState(isSelectionMode, hasSelection);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show()
    {
        assemblyUiVisibleRequested = true;
        RefreshUiState();
        SyncSelectionButtonsStateFromAssembly();
    }

    public void Hide()
    {
        assemblyUiVisibleRequested = false;
        ClosePartsPopup();
        CloseStructuresPopup();
        HideFabricatorPopup();
        RefreshUiState();
    }

    private void OpenPartsPopup()
    {
        if (!IsAssemblyControlsVisible()) return;

        CloseStructuresPopup();
        if (!SetPartsPopupVisible(true)) return;
        PopulatePartsList();
    }

    private void ClosePartsPopup()
    {
        if (!SetPartsPopupVisible(false)) return;
        ClearPartsList();
    }

    private void OpenStructuresPopup()
    {
        if (!ShouldShowStructuresForFocus(GetCurrentFocus())) return;

        StopSelectionModeIfActive();
        ClosePartsPopup();
        if (!SetStructuresPopupVisible(true)) return;
        PopulateStructuresList();
    }

    private void CloseStructuresPopup()
    {
        if (!SetStructuresPopupVisible(false)) return;
        ClearStructuresList();
    }

    private void PopulatePartsList()
    {
        if (partPrefab == null || partsList == null)
        {
            Debug.LogError("Part prefab or parts list is missing.");
            return;
        }

        if (!TryGetPartDb(out PartDB partDb))
        {
            Debug.LogError("PartDB instance is missing.");
            return;
        }

        ClearPartsList();

        List<Part> availableParts = partDb.GetAllParts();

        foreach (Part part in availableParts)
        {
            if (!ShouldShowInAssemblyList(part)) continue;
            AddPartItemToList(part);
        }
    }

    private void PopulateStructuresList()
    {
        if (partPrefab == null || structuresList == null)
        {
            Debug.LogError("Structure prefab or structures list is missing.");
            return;
        }

        ClearStructuresList();

        List<Structure> availableStructures = StructureCatalog.GetAllStructures();
        foreach (Structure structure in availableStructures)
        {
            AddStructureItemToList(structure);
        }
    }

    private static bool ShouldShowInAssemblyList(Part part)
    {
        if (part == null) return false;
        if (string.Equals(part.partName, CorePartName, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(part.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private void AddPartItemToList(Part part)
    {
        if (part == null || partsList == null || partPrefab == null) return;

        TemplateContainer item = partPrefab.Instantiate();
        ApplyPartItemGridStyle(item);

        item.userData = part;

        Label partName = item.Q<Label>(PartNameLabelName);
        if (partName != null)
        {
            partName.text = part.partName;
        }

        VisualElement partIcon = item.Q<VisualElement>(PartIconElementName);
        if (partIcon != null)
        {
            partIcon.style.backgroundImage = new StyleBackground(part.partIcon);
            partIcon.style.unityBackgroundImageTintColor = new StyleColor(GetPartRepresentativeColor(part));
        }

        partsList.Add(item);
    }

    private void AddStructureItemToList(Structure structure)
    {
        if (structure == null || structuresList == null || partPrefab == null) return;

        TemplateContainer item = partPrefab.Instantiate();
        ApplyPartItemGridStyle(item);

        item.userData = structure;
        item.tooltip = BuildStructureTooltip(structure);

        Label structureName = item.Q<Label>(PartNameLabelName);
        if (structureName != null)
        {
            structureName.text = structure.structureName;
        }

        VisualElement structureIcon = item.Q<VisualElement>(PartIconElementName);
        if (structureIcon != null)
        {
            structureIcon.style.backgroundImage = new StyleBackground(structure.structureIcon);
            structureIcon.style.backgroundColor = new StyleColor(structure.iconTint);
            structureIcon.style.unityBackgroundImageTintColor = new StyleColor(structure.iconTint);
        }

        structuresList.Add(item);
    }

    private void ConfigurePartsGrid()
    {
        if (partsList == null) return;

        VisualElement container = partsList.contentContainer;
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.justifyContent = Justify.SpaceEvenly;
        container.style.alignContent = Align.FlexStart;
        container.style.alignItems = Align.FlexStart;
    }

    private void ConfigureStructuresGrid()
    {
        if (structuresList == null) return;

        VisualElement container = structuresList.contentContainer;
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.justifyContent = Justify.SpaceEvenly;
        container.style.alignContent = Align.FlexStart;
        container.style.alignItems = Align.FlexStart;
    }

    private static void ApplyPartItemGridStyle(TemplateContainer item)
    {
        item.style.flexGrow = 0;
        item.style.flexShrink = 0;
        item.style.flexBasis = Length.Percent(30f);
        item.style.maxWidth = Length.Percent(30f);
        item.style.marginBottom = 8f;
    }

    private void ClearPartsList() => partsList?.Clear();

    private void ClearStructuresList() => structuresList?.Clear();

    private static string BuildStructureTooltip(Structure structure)
    {
        if (structure == null) return string.Empty;

        List<string> lines = new List<string>
        {
            $"Name: {structure.structureName}",
            $"Scale: {structure.gridWidth} x {structure.gridHeight}",
            $"Mass: {structure.mass}",
            $"Durability: {structure.durability:0.###}",
            $"Capacity: {structure.capacity:0.###}",
            $"Power Generation: {structure.powerGeneration:0.###}",
            $"Power Consumption: {structure.powerConsumption:0.###}",
            $"Power Capacity: {structure.powerCapacity:0.###}"
        };

        if (structure.craftRecipe != null && !structure.craftRecipe.IsEmpty)
        {
            lines.Add("Craft Recipe:");
            for (int i = 0; i < structure.craftRecipe.ingredients.Count; i++)
            {
                CraftRecipe.IngredientEntry ingredient = structure.craftRecipe.ingredients[i];
                if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.itemName)) continue;
                lines.Add($"- {ingredient.itemName} x{Mathf.Max(1, ingredient.amount)}");
            }
        }
        else
        {
            lines.Add("Craft Recipe: None");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static Color GetPartRepresentativeColor(Part part)
    {
        if (part == null)
        {
            return Color.white;
        }

        GameObject prefabRoot;
        try
        {
            prefabRoot = part.partPrefab;
        }
        catch (MissingReferenceException)
        {
            return Color.white;
        }

        if (prefabRoot == null)
        {
            return Color.white;
        }

        Renderer renderer = prefabRoot.GetComponentInChildren<Renderer>(true);
        if (renderer == null || renderer.sharedMaterial == null) return Color.white;

        Material shared = renderer.sharedMaterial;
        if (shared.HasProperty("_BaseColor")) return shared.GetColor("_BaseColor");
        if (shared.HasProperty("_Color")) return shared.GetColor("_Color");
        return shared.color;
    }

    private void OnCloseClicked(ClickEvent _)
    {
        if (!TryGetAssemblyManager(out AssemblyManager assemblyManager)) return;

        Transform exitFocus = ResolveAssemblyExitFocus(assemblyManager.SourceSatellite);
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (exitFocus != null)
            {
                CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            }

            focusManager.SetFocus(exitFocus);
            return;
        }

        assemblyManager.EndAssemblyMode();
    }
    private void OnOpenPartsClicked(ClickEvent _)
    {
        StopSelectionModeIfActive();
        OpenPartsPopup();
    }

    private void OnOpenStructuresClicked(ClickEvent _)
    {
        OpenStructuresPopup();
    }

    private void OnClosePartsClicked(ClickEvent _)
    {
        ClosePartsPopup();
    }

    private void OnCloseStructuresClicked(ClickEvent _)
    {
        CloseStructuresPopup();
    }

    private void OnPartItemSelected(ClickEvent clickEvent)
    {
        if (!TryResolveClickedPart(clickEvent, out Part part)) return;

        Debug.Log("Selected part: " + part.partName);

        ClosePartsPopup();

        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.SelectPart(part);
        UpdateSelectionButtonsState(false, false);
    }

    private void OnStructureItemSelected(ClickEvent clickEvent)
    {
        if (!TryResolveClickedStructure(clickEvent, out Structure structure)) return;

        CloseStructuresPopup();

        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.SelectStructure(structure);
        UpdateSelectionButtonsState(false, false);
    }

    private void OnToggleSelectionClicked(ClickEvent _)
    {
        if (!TryGetAssembly(out Assembly assembly)) return;

        ClosePartsPopup();
        CloseStructuresPopup();
        bool isSelectionMode = assembly.ToggleSelectionMode();
        bool hasSelection = assembly.HasSelection;
        UpdateSelectionButtonsState(isSelectionMode, hasSelection);
    }

    private void OnRemoveSelectedClicked(ClickEvent _)
    {
        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.RemoveSelectedParts();
        UpdateSelectionButtonsState(assembly.IsSelectionModeActive, assembly.HasSelection);
    }

    private void OnRemoveSelectedMouseEnter(MouseEnterEvent _)
    {
        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.SetRemoveSelectionPreviewActive(true);
    }

    private void OnRemoveSelectedMouseLeave(MouseLeaveEvent _)
    {
        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.SetRemoveSelectionPreviewActive(false);
    }

    private void SetRootDisplay(DisplayStyle display)
    {
        if (root == null) return;
        root.style.display = display;
    }

    private static bool TryGetPartDb(out PartDB partDb)
    {
        return GameplayRuntimeAccess.TryGetPartDb(out partDb);
    }

    private static bool TryGetAssembly(out Assembly assembly)
    {
        return GameplayRuntimeAccess.TryGetAssembly(out assembly);
    }

    private static bool TryGetAssemblyManager(out AssemblyManager assemblyManager)
    {
        return GameplayRuntimeAccess.TryGetAssemblyManager(out assemblyManager);
    }

    private static Transform ResolveAssemblyExitFocus(ArtificialSatellite satellite)
    {
        if (satellite == null) return null;

        OrbitRevolution orbit = satellite.GetComponent<OrbitRevolution>();
        if (orbit != null && orbit.center != null)
        {
            return orbit.center.transform;
        }

        return null;
    }
    private static bool TryResolveClickedPart(ClickEvent clickEvent, out Part part)
    {
        part = null;
        if (clickEvent == null) return false;

        VisualElement target = clickEvent.target as VisualElement;
        if (target == null) return false;

        TemplateContainer itemRoot = target.GetFirstAncestorOfType<TemplateContainer>();
        if (itemRoot == null) return false;

        part = itemRoot.userData as Part;
        return part != null;
    }

    private static bool TryResolveClickedStructure(ClickEvent clickEvent, out Structure structure)
    {
        structure = null;
        if (clickEvent == null) return false;

        VisualElement target = clickEvent.target as VisualElement;
        if (target == null) return false;

        TemplateContainer itemRoot = target.GetFirstAncestorOfType<TemplateContainer>();
        if (itemRoot == null) return false;

        structure = itemRoot.userData as Structure;
        return structure != null;
    }

    private void RegisterCallbacks()
    {
        RegisterClickCallback(close, OnCloseClicked);
        RegisterClickCallback(openParts, OnOpenPartsClicked);
        RegisterClickCallback(openStructures, OnOpenStructuresClicked);
        RegisterClickCallback(toggleSelection, OnToggleSelectionClicked);
        RegisterClickCallback(removeSelected, OnRemoveSelectedClicked);
        RegisterMouseEnterCallback(removeSelected, OnRemoveSelectedMouseEnter);
        RegisterMouseLeaveCallback(removeSelected, OnRemoveSelectedMouseLeave);
        RegisterClickCallback(closeParts, OnClosePartsClicked);
        RegisterClickCallback(closeStructures, OnCloseStructuresClicked);
        RegisterClickCallback(partsList, OnPartItemSelected);
        RegisterClickCallback(structuresList, OnStructureItemSelected);
        RegisterFabricatorCallbacks();
    }

    private void UnregisterCallbacks()
    {
        UnregisterClickCallback(close, OnCloseClicked);
        UnregisterClickCallback(openParts, OnOpenPartsClicked);
        UnregisterClickCallback(openStructures, OnOpenStructuresClicked);
        UnregisterClickCallback(toggleSelection, OnToggleSelectionClicked);
        UnregisterClickCallback(removeSelected, OnRemoveSelectedClicked);
        UnregisterMouseEnterCallback(removeSelected, OnRemoveSelectedMouseEnter);
        UnregisterMouseLeaveCallback(removeSelected, OnRemoveSelectedMouseLeave);
        UnregisterClickCallback(closeParts, OnClosePartsClicked);
        UnregisterClickCallback(closeStructures, OnCloseStructuresClicked);
        UnregisterClickCallback(partsList, OnPartItemSelected);
        UnregisterClickCallback(structuresList, OnStructureItemSelected);
        UnregisterFabricatorCallbacks();
    }


    private static void RegisterClickCallback(VisualElement element, EventCallback<ClickEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
        element.RegisterCallback(callback);
    }

    private static void UnregisterClickCallback(VisualElement element, EventCallback<ClickEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
    }

    private static void RegisterMouseEnterCallback(VisualElement element, EventCallback<MouseEnterEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
        element.RegisterCallback(callback);
    }

    private static void UnregisterMouseEnterCallback(VisualElement element, EventCallback<MouseEnterEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
    }

    private static void RegisterMouseLeaveCallback(VisualElement element, EventCallback<MouseLeaveEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
        element.RegisterCallback(callback);
    }

    private static void UnregisterMouseLeaveCallback(VisualElement element, EventCallback<MouseLeaveEvent> callback)
    {
        if (element == null || callback == null) return;
        element.UnregisterCallback(callback);
    }

    private bool SetPartsPopupVisible(bool visible)
    {
        if (parts == null) return false;
        parts.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        return true;
    }

    private bool SetStructuresPopupVisible(bool visible)
    {
        if (structuresPopup == null) return false;
        structuresPopup.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        return true;
    }

    private bool TryGetHoveredAssemblyUiElement(out VisualElement hoveredElement)
    {
        hoveredElement = null;
        if (root == null || root.panel == null || Mouse.current == null)
        {
            return false;
        }

        if (root.resolvedStyle.display == DisplayStyle.None)
        {
            return false;
        }

        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, Mouse.current.position.ReadValue());
        VisualElement pickedElement = root.panel.Pick(panelPosition);
        if (pickedElement == null || pickedElement == root)
        {
            return false;
        }

        for (VisualElement current = pickedElement; current != null; current = current.parent)
        {
            if (current == root)
            {
                hoveredElement = pickedElement;
                return true;
            }
        }

        return false;
    }

    private bool TryGetRootVisualElement(out VisualElement visualElement)
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            visualElement = null;
            Debug.LogError("AssemblyUI: UIDocument is missing.");
            return false;
        }

        visualElement = uiDocument.rootVisualElement;
        if (visualElement != null) return true;

        Debug.LogError("AssemblyUI: rootVisualElement is missing.");
        return false;
    }

    private bool TryResolveUiElements(VisualElement visualElement)
    {
        root = visualElement.Q<VisualElement>(RootElementName);
        if (root == null)
        {
            Debug.LogError($"AssemblyUI: '{RootElementName}' element is missing.");
            return false;
        }

        close = root.Q<Button>(CloseButtonName);
        openParts = root.Q<Button>(OpenPartsButtonName);
        openStructures = root.Q<Button>(OpenStructuresButtonName);
        toggleSelection = root.Q<Button>(ToggleSelectionButtonName);
        removeSelected = root.Q<Button>(RemoveSelectedButtonName);
        parts = root.Q<VisualElement>(PopupElementName);
        if (parts == null)
        {
            Debug.LogError($"AssemblyUI: '{PopupElementName}' element is missing.");
            return false;
        }

        structuresPopup = root.Q<VisualElement>(StructuresPopupElementName);
        if (structuresPopup == null)
        {
            Debug.LogError($"AssemblyUI: '{StructuresPopupElementName}' element is missing.");
            return false;
        }

        closeParts = parts.Q<Button>(ClosePartsButtonName);
        partsList = parts.Q<ScrollView>(PartsListName);
        closeStructures = structuresPopup.Q<Button>(CloseStructuresButtonName);
        structuresList = structuresPopup.Q<ScrollView>(StructuresListName);
        return TryResolveFabricatorUiElements();
    }

    private void StopSelectionModeIfActive()
    {
        if (!TryGetAssembly(out Assembly assembly)) return;
        if (!assembly.IsSelectionModeActive) return;

        assembly.StopSelectionMode();
        UpdateSelectionButtonsState(false, false);
    }

    private void SyncSelectionButtonsStateFromAssembly()
    {
        if (!TryGetAssembly(out Assembly assembly))
        {
            UpdateSelectionButtonsState(false, false);
            return;
        }

        UpdateSelectionButtonsState(assembly.IsSelectionModeActive, assembly.HasSelection);
    }

    private void UpdateSelectionButtonsState(bool selectionActive, bool hasSelection)
    {
        bool assemblyControlsVisible = IsAssemblyControlsVisible();

        if (toggleSelection != null)
        {
            toggleSelection.text = selectionActive ? "Selection On" : "Selection";
            toggleSelection.style.display = assemblyControlsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (removeSelected != null)
        {
            removeSelected.style.display = assemblyControlsVisible && selectionActive ? DisplayStyle.Flex : DisplayStyle.None;
            removeSelected.SetEnabled(assemblyControlsVisible && selectionActive && hasSelection);
        }

        lastKnownSelectionMode = selectionActive;
        lastHasSelection = hasSelection;
    }

    private void RefreshUiState()
    {
        Transform currentFocus = GetCurrentFocus();
        bool fabricatorVisible = RefreshFabricatorUiState(currentFocus);
        bool assemblyControlsVisible = IsAssemblyControlsVisible() && !fabricatorVisible;
        bool structuresButtonVisible = !fabricatorVisible && ShouldShowStructuresForFocus(currentFocus);

        SetRootDisplay(assemblyControlsVisible || structuresButtonVisible || fabricatorVisible ? DisplayStyle.Flex : DisplayStyle.None);
        SetElementDisplay(close, assemblyControlsVisible);
        SetElementDisplay(openParts, assemblyControlsVisible);
        SetElementDisplay(openStructures, structuresButtonVisible);

        if (!assemblyControlsVisible)
        {
            ClosePartsPopup();
            UpdateSelectionButtonsState(false, false);
        }
        else
        {
            SyncSelectionButtonsStateFromAssembly();
        }

        if (!structuresButtonVisible)
        {
            CloseStructuresPopup();
        }
    }

    private bool IsAssemblyControlsVisible()
    {
        if (!assemblyUiVisibleRequested) return false;
        if (!TryGetAssemblyManager(out AssemblyManager assemblyManager)) return false;
        return assemblyManager.IsAssembling;
    }

    private static Transform GetCurrentFocus()
    {
        return FocusManager.currentFocus;
    }

    private static bool ShouldShowStructuresForFocus(Transform focused)
    {
        if (focused == null) return false;

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null) return false;

        return IsCoreFocus(partFocus, focused.name);
    }

    private static bool IsCoreFocus(AssemblyPartFocus partFocus, string focusName)
    {
        if (partFocus != null && partFocus.SourcePart != null &&
            string.Equals(partFocus.SourcePart.partName, CorePartName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedName = (focusName ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
        if (string.Equals(normalizedName, CorePartName, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(normalizedName, CorePartName + "Prefab", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void SetElementDisplay(VisualElement element, bool visible)
    {
        if (element == null) return;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}




