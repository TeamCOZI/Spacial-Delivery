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
    private Button closeParts;
    private VisualElement parts;
    private ScrollView partsList;

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
        RegisterClickCallback(closeParts, OnClosePartsClicked);
        RegisterClickCallback(partsList, OnPartItemSelected);
    }

    private void UnregisterCallbacks()
    {
        UnregisterClickCallback(close, OnCloseClicked);
        UnregisterClickCallback(openParts, OnOpenPartsClicked);
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
}
