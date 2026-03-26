using UnityEngine;

[DisallowMultipleComponent]
public class ModuleProcessingState : MonoBehaviour
{
    private const string IdleStatus = "IDLE";
    private const string OffStatus = "OFF";
    private const string NoRecipeStatus = "NO RECIPE";
    private const string NoInputStatus = "NO INPUT";
    private const string OutputFullStatus = "OUTPUT FULL";
    private const string NoInventoryStatus = "NO INVENTORY";
    private const string UnsupportedStatus = "UNSUPPORTED";
    private const string RunningStatus = "RUNNING";

    [SerializeField] private string statusLabel = OffStatus;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool isPoweredOn = false;
    [SerializeField] private int selectedRecipeIndex = -1;
    [SerializeField] private bool hasLastProcessedResource;
    [SerializeField] private InventoryResourceType lastInputResourceType;
    [SerializeField] private InventoryResourceType lastOutputResourceType;

    private AssemblyPartFocus partFocus;
    private float processTimer;

    public string StatusLabel => statusLabel;
    public bool IsRunning => isRunning;
    public bool IsPoweredOn => isPoweredOn;
    public int SelectedRecipeIndex => selectedRecipeIndex;
    public bool HasLastProcessedResource => hasLastProcessedResource;
    public InventoryResourceType LastInputResourceType => lastInputResourceType;
    public InventoryResourceType LastOutputResourceType => lastOutputResourceType;

    private void Awake()
    {
        BindReferences();
        EnsureSelectedRecipe();
    }

    private void OnEnable()
    {
        BindReferences();
        EnsureSelectedRecipe();
    }

    private void OnDisable()
    {
        isRunning = false;
        processTimer = 0f;
    }

    private void Update()
    {
        BindReferences();
        EnsureSelectedRecipe();

        float deltaTime = Time.deltaTime;
        if (deltaTime < 0f)
        {
            deltaTime = 0f;
        }

        UpdateProcessing(deltaTime);
    }

    public void SetPowerState(bool poweredOn)
    {
        isPoweredOn = poweredOn;
        isRunning = false;
        processTimer = 0f;
        statusLabel = isPoweredOn ? IdleStatus : OffStatus;
    }

    public void SelectRecipe(int recipeIndex)
    {
        int recipeCount = ModuleRecipeCatalog.GetRecipes(partFocus).Count;
        if (recipeIndex < 0 || recipeIndex >= recipeCount)
        {
            selectedRecipeIndex = -1;
            statusLabel = NoRecipeStatus;
            return;
        }

        selectedRecipeIndex = recipeIndex;
        processTimer = 0f;
        statusLabel = isPoweredOn ? IdleStatus : OffStatus;
    }

    public bool TryGetSelectedRecipe(out ModuleRecipeDefinition recipe)
    {
        recipe = default;
        var recipes = ModuleRecipeCatalog.GetRecipes(partFocus);
        if (selectedRecipeIndex < 0 || selectedRecipeIndex >= recipes.Count)
        {
            return false;
        }

        recipe = recipes[selectedRecipeIndex];
        return true;
    }

    private void UpdateProcessing(float deltaTime)
    {
        if (!isPoweredOn)
        {
            isRunning = false;
            processTimer = 0f;
            statusLabel = OffStatus;
            return;
        }

        if (!TryResolveProcessStep(
            out StructureResourceInventory inputInventory,
            out StructureResourceInventory outputInventory,
            out ModuleRecipeDefinition recipe,
            out string failureReason))
        {
            isRunning = false;
            processTimer = 0f;
            statusLabel = failureReason;
            return;
        }

        isRunning = true;
        processTimer += deltaTime;
        float processSeconds = Mathf.Max(0.01f, recipe.processSeconds);
        while (processTimer >= processSeconds)
        {
            processTimer -= processSeconds;
            if (!TryProcessOne(inputInventory, outputInventory, recipe))
            {
                processTimer = 0f;
                break;
            }

            if (!TryResolveProcessStep(out inputInventory, out outputInventory, out recipe, out failureReason))
            {
                isRunning = false;
                statusLabel = failureReason;
                break;
            }

            processSeconds = Mathf.Max(0.01f, recipe.processSeconds);
        }

        if (isRunning && statusLabel == IdleStatus)
        {
            statusLabel = RunningStatus;
        }
    }

    private bool TryProcessOne(
        StructureResourceInventory inputInventory,
        StructureResourceInventory outputInventory,
        ModuleRecipeDefinition recipe)
    {
        if (inputInventory == null || outputInventory == null)
        {
            statusLabel = NoInventoryStatus;
            return false;
        }

        if (!inputInventory.TryRemove(recipe.inputResourceType, recipe.inputAmount))
        {
            statusLabel = NoInputStatus;
            return false;
        }

        bool removedSecondaryInput = false;
        bool removedTertiaryInput = false;
        bool removedQuaternaryInput = false;

        if (recipe.HasSecondaryInput)
        {
            if (!inputInventory.TryRemove(recipe.secondaryInputResourceType, recipe.secondaryInputAmount))
            {
                _ = inputInventory.TryAdd(recipe.inputResourceType, recipe.inputAmount);
                statusLabel = NoInputStatus;
                return false;
            }

            removedSecondaryInput = true;
        }

        if (recipe.HasTertiaryInput)
        {
            if (!inputInventory.TryRemove(recipe.tertiaryInputResourceType, recipe.tertiaryInputAmount))
            {
                RestoreInputs(inputInventory, recipe, removedSecondaryInput, removedTertiaryInput, removedQuaternaryInput);
                statusLabel = NoInputStatus;
                return false;
            }

            removedTertiaryInput = true;
        }

        if (recipe.HasQuaternaryInput)
        {
            if (!inputInventory.TryRemove(recipe.quaternaryInputResourceType, recipe.quaternaryInputAmount))
            {
                RestoreInputs(inputInventory, recipe, removedSecondaryInput, removedTertiaryInput, removedQuaternaryInput);
                statusLabel = NoInputStatus;
                return false;
            }

            removedQuaternaryInput = true;
        }

        if (!outputInventory.TryAdd(recipe.outputResourceType, recipe.outputAmount))
        {
            RestoreInputs(inputInventory, recipe, removedSecondaryInput, removedTertiaryInput, removedQuaternaryInput);
            statusLabel = OutputFullStatus;
            return false;
        }

        hasLastProcessedResource = true;
        lastInputResourceType = recipe.inputResourceType;
        lastOutputResourceType = recipe.outputResourceType;
        statusLabel = BuildRunningStatus(recipe);
        return true;
    }

    private bool TryResolveProcessStep(
        out StructureResourceInventory inputInventory,
        out StructureResourceInventory outputInventory,
        out ModuleRecipeDefinition recipe,
        out string failureReason)
    {
        inputInventory = null;
        outputInventory = null;
        recipe = default;
        failureReason = IdleStatus;

        if (partFocus == null || partFocus.SourcePart == null || !ModulePartInventoryUtility.UsesSplitInventories(partFocus.SourcePart))
        {
            failureReason = UnsupportedStatus;
            return false;
        }

        if (!ModulePartInventoryUtility.TryResolveInputInventory(partFocus, out inputInventory) ||
            !ModulePartInventoryUtility.TryResolveOutputInventory(partFocus, out outputInventory) ||
            inputInventory == null ||
            outputInventory == null)
        {
            failureReason = NoInventoryStatus;
            return false;
        }

        if (!TryGetSelectedRecipe(out recipe))
        {
            failureReason = NoRecipeStatus;
            return false;
        }

        if (!HasRequiredInputs(inputInventory, recipe))
        {
            failureReason = NoInputStatus;
            return false;
        }

        if (outputInventory.TotalAmount + recipe.outputAmount > outputInventory.Capacity)
        {
            failureReason = OutputFullStatus;
            return false;
        }

        failureReason = RunningStatus;
        return true;
    }

    private void BindReferences()
    {
        if (partFocus == null)
        {
            partFocus = GetComponent<AssemblyPartFocus>();
        }

        if (partFocus == null)
        {
            partFocus = GetComponentInParent<AssemblyPartFocus>();
        }
    }

    private void EnsureSelectedRecipe()
    {
        var recipes = ModuleRecipeCatalog.GetRecipes(partFocus);
        if (recipes.Count <= 0)
        {
            selectedRecipeIndex = -1;
            return;
        }

        if (selectedRecipeIndex < 0 || selectedRecipeIndex >= recipes.Count)
        {
            selectedRecipeIndex = 0;
        }
    }

    private static bool HasRequiredInputs(StructureResourceInventory inputInventory, ModuleRecipeDefinition recipe)
    {
        if (inputInventory == null)
        {
            return false;
        }

        if (inputInventory.GetAmount(recipe.inputResourceType) < recipe.inputAmount)
        {
            return false;
        }

        if (recipe.HasSecondaryInput && inputInventory.GetAmount(recipe.secondaryInputResourceType) < recipe.secondaryInputAmount)
        {
            return false;
        }

        if (recipe.HasTertiaryInput && inputInventory.GetAmount(recipe.tertiaryInputResourceType) < recipe.tertiaryInputAmount)
        {
            return false;
        }

        return !recipe.HasQuaternaryInput || inputInventory.GetAmount(recipe.quaternaryInputResourceType) >= recipe.quaternaryInputAmount;
    }

    private static void RestoreInputs(
        StructureResourceInventory inputInventory,
        ModuleRecipeDefinition recipe,
        bool removedSecondaryInput,
        bool removedTertiaryInput,
        bool removedQuaternaryInput)
    {
        if (removedQuaternaryInput)
        {
            _ = inputInventory.TryAdd(recipe.quaternaryInputResourceType, recipe.quaternaryInputAmount);
        }

        if (removedTertiaryInput)
        {
            _ = inputInventory.TryAdd(recipe.tertiaryInputResourceType, recipe.tertiaryInputAmount);
        }

        if (removedSecondaryInput)
        {
            _ = inputInventory.TryAdd(recipe.secondaryInputResourceType, recipe.secondaryInputAmount);
        }

        _ = inputInventory.TryAdd(recipe.inputResourceType, recipe.inputAmount);
    }

    private static string BuildRunningStatus(ModuleRecipeDefinition recipe)
    {
        return $"{RunningStatus} / {FormatInputLabel(recipe)} -> {FormatResourceLabel(recipe.outputResourceType)} x{recipe.outputAmount}";
    }

    private static string FormatInputLabel(ModuleRecipeDefinition recipe)
    {
        string label = $"{FormatResourceLabel(recipe.inputResourceType)} x{recipe.inputAmount}";
        if (recipe.HasSecondaryInput)
        {
            label += $" + {FormatResourceLabel(recipe.secondaryInputResourceType)} x{recipe.secondaryInputAmount}";
        }

        if (recipe.HasTertiaryInput)
        {
            label += $" + {FormatResourceLabel(recipe.tertiaryInputResourceType)} x{recipe.tertiaryInputAmount}";
        }

        if (recipe.HasQuaternaryInput)
        {
            label += $" + {FormatResourceLabel(recipe.quaternaryInputResourceType)} x{recipe.quaternaryInputAmount}";
        }

        return label;
    }

    private static string FormatResourceLabel(InventoryResourceType resourceType)
    {
        return InventoryResourceCatalog.GetDisplayName(resourceType).Replace("_", " ");
    }
}