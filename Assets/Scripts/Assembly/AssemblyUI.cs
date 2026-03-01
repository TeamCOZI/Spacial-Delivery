using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class AssemblyUI : MonoBehaviour
{
    public static AssemblyUI Instance { get; private set; }

    public VisualTreeAsset partPrefab;

    private VisualElement root;
    private Button close;

    private Button openParts;
    private Button closeParts;
    private VisualElement parts;
    private ScrollView partsList;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        VisualElement visualElement = GetComponent<UIDocument>().rootVisualElement;

        root = visualElement.Q<VisualElement>("Root");
        close = root.Q<Button>("Close");

        openParts = root.Q<Button>("OpenParts");
        parts = root.Q<VisualElement>("PopUp");
        closeParts = parts.Q<Button>("CloseParts");
        partsList = parts.Q<ScrollView>("PartsList");

        if (close != null) close.RegisterCallback<ClickEvent>(ev => AssemblyManager.Instance?.EndAssemblyMode());
        if (openParts != null) openParts.RegisterCallback<ClickEvent>(ev => OpenPartsPopup());
        if (closeParts != null) closeParts.RegisterCallback<ClickEvent>(ev => ClosePartsPopup());

        partsList?.UnregisterCallback<ClickEvent>(OnPartItemSelected);
        partsList?.RegisterCallback<ClickEvent>(OnPartItemSelected);
        ConfigurePartsGrid();

        Hide();
    }

    public void Show()
    {
        if (root == null) return;

        root.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (root == null) return;

        root.style.display = DisplayStyle.None;
    }

    private void OpenPartsPopup()
    {
        if (parts == null) return;

        parts.style.display = DisplayStyle.Flex;
        PopulatePartsList();
    }

    private void ClosePartsPopup()
    {
        if (parts == null) return;

        parts.style.display = DisplayStyle.None;
        ClearPartsList();
    }

    private void PopulatePartsList()
    {
        if (partPrefab == null || partsList == null)
        {
            Debug.LogError("Part prefab or parts list is missing.");
            return;
        }
        ClearPartsList();

        List<Part> parts = PartDB.Instance.GetAllParts();

        foreach (Part part in parts)
        {
            TemplateContainer item = partPrefab.Instantiate();
            ApplyPartItemGridStyle(item);

            item.userData = part;

            item.Q<Label>("PartName").text = part.partName;
            item.Q<VisualElement>("PartIcon").style.backgroundImage = new StyleBackground(part.partIcon);

            partsList.Add(item);
        }
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

    private void OnPartItemSelected(ClickEvent clickEvenet)
    {
        VisualElement target = clickEvenet.target as VisualElement;
        if (target == null) return;

        TemplateContainer itemRoot = target.GetFirstAncestorOfType<TemplateContainer>();
        if (itemRoot == null) return;

        Part part = itemRoot.userData as Part;
        if (part == null) return;

        Debug.Log("Selected part: " + part.partName);
        
        ClosePartsPopup();

        Assembly.Instance.SelectPart(part);
    }
}
