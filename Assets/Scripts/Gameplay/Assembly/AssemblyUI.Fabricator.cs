using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class AssemblyUI
{
    private const string FabricatorPopupElementName = "FabricatorPopUp";
    private const string FabricatorTitleLabelName = "FabricatorTitle";
    private const string CloseFabricatorButtonName = "CloseFabricator";
    private const string FabricatorResultsListName = "FabricatorResultsList";
    private const string FabricatorResultsEmptyLabelName = "FabricatorResultsEmptyLabel";
    private const string FabricatorCraftTimeLabelName = "FabricatorCraftTimeLabel";
    private const string FabricatorProcessLabelName = "FabricatorProcessLabel";
    private const string FabricatorResultLabelName = "FabricatorResultLabel";
    private const string FabricatorCountLabelName = "FabricatorCountLabel";
    private const string FabricatorIncreaseCountButtonName = "FabricatorIncreaseCount";
    private const string FabricatorDecreaseCountButtonName = "FabricatorDecreaseCount";
    private const string FabricatorManufactureButtonName = "FabricatorManufacture";
    private const string FabricatorAutoManufactureButtonName = "FabricatorAutoManufacture";
    private const string FabricatorFilterAllButtonName = "FabricatorFilterAll";
    private const string FabricatorFilterHabitatButtonName = "FabricatorFilterHabitat";
    private const string FabricatorFilterMaterialButtonName = "FabricatorFilterMaterial";
    private const string FabricatorFilterSpaceshipButtonName = "FabricatorFilterSpaceship";
    private const string FabricatorFilterMiscButtonName = "FabricatorFilterMisc";
    private const int MinimumFabricatorRequestedCount = 1;

    private static readonly Color FabricatorSelectedFilterBackground = new Color(0.16f, 0.35f, 0.49f, 1f);
    private static readonly Color FabricatorSelectedFilterText = Color.white;
    private static readonly Color FabricatorIdleFilterBackground = new Color(0.68f, 0.86f, 0.96f, 1f);
    private static readonly Color FabricatorIdleFilterText = new Color(0.06f, 0.11f, 0.15f, 1f);

    private VisualElement fabricatorPopup;
    private Label fabricatorTitle;
    private Button closeFabricator;
    private ScrollView fabricatorResultsList;
    private Label fabricatorResultsEmptyLabel;
    private Label fabricatorCraftTimeLabel;
    private Label fabricatorProcessLabel;
    private Label fabricatorResultLabel;
    private Label fabricatorCountLabel;
    private Button fabricatorIncreaseCount;
    private Button fabricatorDecreaseCount;
    private Button fabricatorManufacture;
    private Button fabricatorAutoManufacture;
    private readonly List<Button> fabricatorFilterButtons = new List<Button>();
    private int selectedFabricatorFilterIndex;
    private int requestedFabricatorCount = MinimumFabricatorRequestedCount;
    private StructureFocus activeFabricatorFocus;

    private bool TryResolveFabricatorUiElements()
    {
        fabricatorPopup = root.Q<VisualElement>(FabricatorPopupElementName);
        if (fabricatorPopup == null)
        {
            Debug.LogError($"AssemblyUI: '{FabricatorPopupElementName}' element is missing.");
            return false;
        }

        fabricatorTitle = fabricatorPopup.Q<Label>(FabricatorTitleLabelName);
        closeFabricator = fabricatorPopup.Q<Button>(CloseFabricatorButtonName);
        fabricatorResultsList = fabricatorPopup.Q<ScrollView>(FabricatorResultsListName);
        fabricatorResultsEmptyLabel = fabricatorPopup.Q<Label>(FabricatorResultsEmptyLabelName);
        fabricatorCraftTimeLabel = fabricatorPopup.Q<Label>(FabricatorCraftTimeLabelName);
        fabricatorProcessLabel = fabricatorPopup.Q<Label>(FabricatorProcessLabelName);
        fabricatorResultLabel = fabricatorPopup.Q<Label>(FabricatorResultLabelName);
        fabricatorCountLabel = fabricatorPopup.Q<Label>(FabricatorCountLabelName);
        fabricatorIncreaseCount = fabricatorPopup.Q<Button>(FabricatorIncreaseCountButtonName);
        fabricatorDecreaseCount = fabricatorPopup.Q<Button>(FabricatorDecreaseCountButtonName);
        fabricatorManufacture = fabricatorPopup.Q<Button>(FabricatorManufactureButtonName);
        fabricatorAutoManufacture = fabricatorPopup.Q<Button>(FabricatorAutoManufactureButtonName);

        fabricatorFilterButtons.Clear();
        AddFabricatorFilterButton(FabricatorFilterAllButtonName, 0);
        AddFabricatorFilterButton(FabricatorFilterHabitatButtonName, 1);
        AddFabricatorFilterButton(FabricatorFilterMaterialButtonName, 2);
        AddFabricatorFilterButton(FabricatorFilterSpaceshipButtonName, 3);
        AddFabricatorFilterButton(FabricatorFilterMiscButtonName, 4);

        return true;
    }

    private void ConfigureFabricatorUi()
    {
        selectedFabricatorFilterIndex = 0;
        requestedFabricatorCount = MinimumFabricatorRequestedCount;
        activeFabricatorFocus = null;
        UpdateFabricatorFilterVisuals();
        UpdateFabricatorCountLabel();
        SetFabricatorPlaceholderTexts();
        HideFabricatorPopup();
    }

    private void RegisterFabricatorCallbacks()
    {
        RegisterClickCallback(closeFabricator, OnCloseFabricatorClicked);
        RegisterClickCallback(fabricatorIncreaseCount, OnFabricatorIncreaseCountClicked);
        RegisterClickCallback(fabricatorDecreaseCount, OnFabricatorDecreaseCountClicked);
        RegisterClickCallback(fabricatorManufacture, OnFabricatorManufactureClicked);
        RegisterClickCallback(fabricatorAutoManufacture, OnFabricatorAutoManufactureClicked);

        for (int i = 0; i < fabricatorFilterButtons.Count; i++)
        {
            RegisterClickCallback(fabricatorFilterButtons[i], OnFabricatorFilterClicked);
        }
    }

    private void UnregisterFabricatorCallbacks()
    {
        UnregisterClickCallback(closeFabricator, OnCloseFabricatorClicked);
        UnregisterClickCallback(fabricatorIncreaseCount, OnFabricatorIncreaseCountClicked);
        UnregisterClickCallback(fabricatorDecreaseCount, OnFabricatorDecreaseCountClicked);
        UnregisterClickCallback(fabricatorManufacture, OnFabricatorManufactureClicked);
        UnregisterClickCallback(fabricatorAutoManufacture, OnFabricatorAutoManufactureClicked);

        for (int i = 0; i < fabricatorFilterButtons.Count; i++)
        {
            UnregisterClickCallback(fabricatorFilterButtons[i], OnFabricatorFilterClicked);
        }
    }

    private bool RefreshFabricatorUiState(Transform focused)
    {
        if (!assemblyUiVisibleRequested)
        {
            HideFabricatorPopup();
            return false;
        }

        if (!TryResolveFocusedFabricatorStructure(focused, out StructureFocus structureFocus))
        {
            HideFabricatorPopup();
            return false;
        }

        if (activeFabricatorFocus != structureFocus)
        {
            activeFabricatorFocus = structureFocus;
            selectedFabricatorFilterIndex = 0;
            requestedFabricatorCount = MinimumFabricatorRequestedCount;
            UpdateFabricatorFilterVisuals();
            UpdateFabricatorCountLabel();
            SetFabricatorPlaceholderTexts();
        }

        ClosePartsPopup();
        CloseStructuresPopup();
        SetFabricatorPopupVisible(true);
        return true;
    }

    private void HideFabricatorPopup()
    {
        SetFabricatorPopupVisible(false);
        activeFabricatorFocus = null;
    }

    private bool SetFabricatorPopupVisible(bool visible)
    {
        if (fabricatorPopup == null) return false;
        fabricatorPopup.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        return true;
    }

    private void AddFabricatorFilterButton(string elementName, int index)
    {
        if (fabricatorPopup == null) return;

        Button button = fabricatorPopup.Q<Button>(elementName);
        if (button == null) return;

        button.userData = index;
        fabricatorFilterButtons.Add(button);
    }

    private void OnCloseFabricatorClicked(ClickEvent _)
    {
        Transform closeTarget = ResolveFabricatorCloseFocus(activeFabricatorFocus);
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (closeTarget != null)
            {
                CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            }

            focusManager.SetFocus(closeTarget);
            return;
        }

        HideFabricatorPopup();
    }

    private void OnFabricatorIncreaseCountClicked(ClickEvent _)
    {
        requestedFabricatorCount++;
        UpdateFabricatorCountLabel();
    }

    private void OnFabricatorDecreaseCountClicked(ClickEvent _)
    {
        requestedFabricatorCount = Mathf.Max(MinimumFabricatorRequestedCount, requestedFabricatorCount - 1);
        UpdateFabricatorCountLabel();
    }

    private void OnFabricatorManufactureClicked(ClickEvent _)
    {
        if (activeFabricatorFocus == null || activeFabricatorFocus.SourceStructure == null) return;
        if (activeFabricatorFocus.OwnerSatellite == null) return;

        LauncherSpaceshipStock shipStock = LauncherSpaceshipStock.ResolveForSatellite(activeFabricatorFocus.OwnerSatellite, createIfMissing: true);
        if (shipStock == null) return;

        shipStock.AddAvailableSpaceships(1);
        Debug.Log($"Fabricator manufacture requested: structure={activeFabricatorFocus.SourceStructure.structureName}, count={requestedFabricatorCount}, auto=false, availableSpaceships={shipStock.AvailableSpaceshipCount}");
    }

    private void OnFabricatorAutoManufactureClicked(ClickEvent _)
    {
        if (activeFabricatorFocus == null || activeFabricatorFocus.SourceStructure == null) return;
        Debug.Log($"Fabricator manufacture requested: structure={activeFabricatorFocus.SourceStructure.structureName}, count={requestedFabricatorCount}, auto=true");
    }

    private void OnFabricatorFilterClicked(ClickEvent clickEvent)
    {
        if (!TryResolveFabricatorFilterIndex(clickEvent, out int filterIndex)) return;

        selectedFabricatorFilterIndex = filterIndex;
        UpdateFabricatorFilterVisuals();
    }

    private void UpdateFabricatorFilterVisuals()
    {
        for (int i = 0; i < fabricatorFilterButtons.Count; i++)
        {
            Button button = fabricatorFilterButtons[i];
            if (button == null) continue;

            bool isSelected = i == selectedFabricatorFilterIndex;
            button.style.backgroundColor = new StyleColor(isSelected ? FabricatorSelectedFilterBackground : FabricatorIdleFilterBackground);
            button.style.color = new StyleColor(isSelected ? FabricatorSelectedFilterText : FabricatorIdleFilterText);
        }
    }

    private void UpdateFabricatorCountLabel()
    {
        if (fabricatorCountLabel == null) return;
        fabricatorCountLabel.text = $"제작할 개수 : {requestedFabricatorCount}";
    }

    private void SetFabricatorPlaceholderTexts()
    {
        string structureName = activeFabricatorFocus != null && activeFabricatorFocus.SourceStructure != null
            ? activeFabricatorFocus.SourceStructure.structureName
            : "Integrated Fabricator";

        if (fabricatorTitle != null)
        {
            fabricatorTitle.text = structureName;
        }

        if (fabricatorResultsEmptyLabel != null)
        {
            fabricatorResultsEmptyLabel.text = "결과물 목록";
        }

        if (fabricatorCraftTimeLabel != null)
        {
            fabricatorCraftTimeLabel.text = "제작 시간";
        }

        if (fabricatorProcessLabel != null)
        {
            fabricatorProcessLabel.text = "제작 프로세스";
        }

        if (fabricatorResultLabel != null)
        {
            fabricatorResultLabel.text = "결과물";
        }

        if (fabricatorResultsList != null)
        {
            fabricatorResultsList.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }
    }

    private static bool TryResolveFocusedFabricatorStructure(Transform focused, out StructureFocus structureFocus)
    {
        structureFocus = null;
        if (focused == null) return false;

        structureFocus = focused.GetComponent<StructureFocus>();
        if (structureFocus == null) return false;
        if (!structureFocus.UsesFabricatorUi) return false;
        if (structureFocus.OwnerSatellite == null) return false;
        return true;
    }

    private static Transform ResolveFabricatorCloseFocus(StructureFocus structureFocus)
    {
        if (structureFocus == null || structureFocus.OwnerSatellite == null)
        {
            return null;
        }

        AssemblyPartFocus[] partFocuses = structureFocus.OwnerSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || partFocus.SourcePart == null) continue;
            if (string.Equals(partFocus.SourcePart.partName, CorePartName, StringComparison.OrdinalIgnoreCase))
            {
                return partFocus.transform;
            }
        }

        return structureFocus.OwnerSatellite.transform;
    }

    private static bool TryResolveFabricatorFilterIndex(ClickEvent clickEvent, out int filterIndex)
    {
        filterIndex = -1;
        if (clickEvent == null) return false;

        VisualElement target = clickEvent.target as VisualElement;
        if (target == null) return false;

        Button button = target as Button ?? target.GetFirstAncestorOfType<Button>();
        if (button == null || !(button.userData is int buttonIndex)) return false;

        filterIndex = buttonIndex;
        return true;
    }
}
