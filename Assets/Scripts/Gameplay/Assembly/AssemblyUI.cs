using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class AssemblyUI : MonoBehaviour
{
    private const string CorePartName = "Core";
    private const string LauncherPartName = "Launcher";
    private const string RootElementName = "Root";
    private const string CloseButtonName = "Close";
    private const string OpenPartsButtonName = "OpenParts";
    private const string ToggleSelectionButtonName = "ToggleSelection";
    private const string RemoveSelectedButtonName = "RemoveSelected";
    private const string PopupElementName = "PopUp";
    private const string ClosePartsButtonName = "CloseParts";
    private const string PartsListName = "PartsList";
    private const string PartNameLabelName = "PartName";
    private const string PartIconElementName = "PartIcon";

    public static AssemblyUI Instance { get; private set; }

    public VisualTreeAsset partPrefab;

    private UIDocument uiDocument;
    private VisualElement root;
    private Button close;

    private Button openParts;
    private Button toggleSelection;
    private Button removeSelected;
    private Button closeParts;
    private VisualElement parts;
    private ScrollView partsList;
    private bool lastKnownSelectionMode = false;
    private bool lastHasSelection = false;

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

        Hide();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void Update()
    {
        if (!TryGetAssembly(out Assembly assembly)) return;

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
        SetRootDisplay(DisplayStyle.Flex);
        SyncSelectionButtonsStateFromAssembly();
    }

    public void Hide()
    {
        SetRootDisplay(DisplayStyle.None);
    }

    private void OpenPartsPopup()
    {
        if (!SetPartsPopupVisible(true)) return;
        PopulatePartsList();
    }

    private void ClosePartsPopup()
    {
        if (!SetPartsPopupVisible(false)) return;
        ClearPartsList();
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

    private static bool ShouldShowInAssemblyList(Part part)
    {
        if (part == null) return false;
        if (string.Equals(part.partName, CorePartName, System.StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(part.partName, LauncherPartName, System.StringComparison.OrdinalIgnoreCase)) return false;
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

    private static void ApplyPartItemGridStyle(TemplateContainer item)
    {
        // 3 columns per row, with remaining horizontal space distributed evenly.
        item.style.flexGrow = 0;
        item.style.flexShrink = 0;
        item.style.flexBasis = Length.Percent(30f);
        item.style.maxWidth = Length.Percent(30f);
        item.style.marginBottom = 8f;
    }

    private void ClearPartsList() => partsList?.Clear();

    private static Color GetPartRepresentativeColor(Part part)
    {
        if (part == null || part.partPrefab == null) return Color.white;

        Renderer renderer = part.partPrefab.GetComponentInChildren<Renderer>(true);
        if (renderer == null || renderer.sharedMaterial == null) return Color.white;

        Material shared = renderer.sharedMaterial;
        if (shared.HasProperty("_BaseColor")) return shared.GetColor("_BaseColor");
        if (shared.HasProperty("_Color")) return shared.GetColor("_Color");
        return shared.color;
    }

    private void OnCloseClicked(ClickEvent _)
    {
        if (!TryGetAssemblyManager(out AssemblyManager assemblyManager)) return;
        assemblyManager.EndAssemblyMode();
    }

    private void OnOpenPartsClicked(ClickEvent _)
    {
        StopSelectionModeIfActive();
        OpenPartsPopup();
    }

    private void OnClosePartsClicked(ClickEvent _)
    {
        ClosePartsPopup();
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

    private void OnToggleSelectionClicked(ClickEvent _)
    {
        if (!TryGetAssembly(out Assembly assembly)) return;

        ClosePartsPopup();
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

    private void RegisterCallbacks()
    {
        RegisterClickCallback(close, OnCloseClicked);
        RegisterClickCallback(openParts, OnOpenPartsClicked);
        RegisterClickCallback(toggleSelection, OnToggleSelectionClicked);
        RegisterClickCallback(removeSelected, OnRemoveSelectedClicked);
        RegisterMouseEnterCallback(removeSelected, OnRemoveSelectedMouseEnter);
        RegisterMouseLeaveCallback(removeSelected, OnRemoveSelectedMouseLeave);
        RegisterClickCallback(closeParts, OnClosePartsClicked);
        RegisterClickCallback(partsList, OnPartItemSelected);
    }

    private void UnregisterCallbacks()
    {
        UnregisterClickCallback(close, OnCloseClicked);
        UnregisterClickCallback(openParts, OnOpenPartsClicked);
        UnregisterClickCallback(toggleSelection, OnToggleSelectionClicked);
        UnregisterClickCallback(removeSelected, OnRemoveSelectedClicked);
        UnregisterMouseEnterCallback(removeSelected, OnRemoveSelectedMouseEnter);
        UnregisterMouseLeaveCallback(removeSelected, OnRemoveSelectedMouseLeave);
        UnregisterClickCallback(closeParts, OnClosePartsClicked);
        UnregisterClickCallback(partsList, OnPartItemSelected);
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
        toggleSelection = root.Q<Button>(ToggleSelectionButtonName);
        removeSelected = root.Q<Button>(RemoveSelectedButtonName);
        parts = root.Q<VisualElement>(PopupElementName);
        if (parts == null)
        {
            Debug.LogError($"AssemblyUI: '{PopupElementName}' element is missing.");
            return false;
        }

        closeParts = parts.Q<Button>(ClosePartsButtonName);
        partsList = parts.Q<ScrollView>(PartsListName);
        return true;
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
        if (toggleSelection != null)
        {
            toggleSelection.text = selectionActive ? "Selection On" : "Selection";
        }

        if (removeSelected != null)
        {
            removeSelected.style.display = selectionActive ? DisplayStyle.Flex : DisplayStyle.None;
            removeSelected.SetEnabled(selectionActive && hasSelection);
        }

        lastKnownSelectionMode = selectionActive;
        lastHasSelection = hasSelection;
    }
}
