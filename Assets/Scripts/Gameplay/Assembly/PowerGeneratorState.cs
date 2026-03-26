using UnityEngine;

[DisallowMultipleComponent]
public class PowerGeneratorState : MonoBehaviour
{
    private const string IdleStatus = "IDLE";
    private const string OffStatus = "OFF";
    private const string NoRecipeStatus = "NO RECIPE";
    private const string NoInputStatus = "NO INPUT";
    private const string PowerFullStatus = "POWER FULL";
    private const string NoInventoryStatus = "NO INVENTORY";
    private const string UnsupportedStatus = "UNSUPPORTED";
    private const string RunningStatus = "RUNNING";

    [SerializeField] private string statusLabel = OffStatus;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool isPoweredOn;
    [SerializeField] private int selectedRecipeIndex = -1;
    [SerializeField] private int currentPower;
    [SerializeField] private bool hasLastGeneratedPower;
    [SerializeField] private InventoryResourceType lastInputResourceType;
    [SerializeField] private int lastGeneratedPowerAmount;

    private AssemblyPartFocus partFocus;
    private float generationTimer;

    public string StatusLabel => statusLabel;
    public bool IsRunning => isRunning;
    public bool IsPoweredOn => isPoweredOn;
    public int SelectedRecipeIndex => selectedRecipeIndex;
    public int CurrentPower => Mathf.Max(0, currentPower);
    public int PowerCapacity => ResolvePowerCapacity();
    public int FreePowerCapacity => Mathf.Max(0, PowerCapacity - CurrentPower);
    public bool HasLastGeneratedPower => hasLastGeneratedPower;
    public InventoryResourceType LastInputResourceType => lastInputResourceType;
    public int LastGeneratedPowerAmount => lastGeneratedPowerAmount;

    private void Awake()
    {
        BindReferences();
        EnsureSelectedRecipe();
        ClampPower();
    }

    private void OnEnable()
    {
        BindReferences();
        EnsureSelectedRecipe();
        ClampPower();
    }

    private void OnDisable()
    {
        isRunning = false;
        generationTimer = 0f;
    }

    private void Update()
    {
        BindReferences();
        EnsureSelectedRecipe();
        ClampPower();

        float deltaTime = Time.deltaTime;
        if (deltaTime < 0f)
        {
            deltaTime = 0f;
        }

        UpdateGeneration(deltaTime);
    }

    public void SetPowerState(bool poweredOn)
    {
        isPoweredOn = poweredOn;
        isRunning = false;
        generationTimer = 0f;
        statusLabel = isPoweredOn ? IdleStatus : OffStatus;
    }

    public void SelectRecipe(int recipeIndex)
    {
        int recipeCount = PowerGeneratorRecipeCatalog.GetRecipes(partFocus).Count;
        if (recipeIndex < 0 || recipeIndex >= recipeCount)
        {
            selectedRecipeIndex = -1;
            statusLabel = NoRecipeStatus;
            return;
        }

        selectedRecipeIndex = recipeIndex;
        generationTimer = 0f;
        statusLabel = isPoweredOn ? IdleStatus : OffStatus;
    }

    public bool TryGetSelectedRecipe(out PowerGeneratorRecipeDefinition recipe)
    {
        recipe = default;
        var recipes = PowerGeneratorRecipeCatalog.GetRecipes(partFocus);
        if (selectedRecipeIndex < 0 || selectedRecipeIndex >= recipes.Count)
        {
            return false;
        }

        recipe = recipes[selectedRecipeIndex];
        return true;
    }

    public bool TryConsumePower(int amount)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return true;
        }

        if (CurrentPower < sanitizedAmount)
        {
            return false;
        }

        currentPower -= sanitizedAmount;
        ClampPower();
        return true;
    }

    private void UpdateGeneration(float deltaTime)
    {
        if (!isPoweredOn)
        {
            isRunning = false;
            generationTimer = 0f;
            statusLabel = OffStatus;
            return;
        }

        if (!TryResolveGenerationStep(
            out StructureResourceInventory inputInventory,
            out PowerGeneratorRecipeDefinition recipe,
            out string failureReason))
        {
            isRunning = false;
            generationTimer = 0f;
            statusLabel = failureReason;
            return;
        }

        isRunning = true;
        generationTimer += deltaTime;
        float generationSeconds = Mathf.Max(0.01f, recipe.generationSeconds);
        while (generationTimer >= generationSeconds)
        {
            generationTimer -= generationSeconds;
            if (!TryGenerateOne(inputInventory, recipe))
            {
                generationTimer = 0f;
                break;
            }

            if (!TryResolveGenerationStep(out inputInventory, out recipe, out failureReason))
            {
                isRunning = false;
                statusLabel = failureReason;
                break;
            }

            generationSeconds = Mathf.Max(0.01f, recipe.generationSeconds);
        }

        if (isRunning && statusLabel == IdleStatus)
        {
            statusLabel = RunningStatus;
        }
    }

    private bool TryGenerateOne(StructureResourceInventory inputInventory, PowerGeneratorRecipeDefinition recipe)
    {
        if (inputInventory == null)
        {
            statusLabel = NoInventoryStatus;
            return false;
        }

        if (!inputInventory.TryRemove(recipe.inputResourceType, recipe.inputAmount))
        {
            statusLabel = NoInputStatus;
            return false;
        }

        if (CurrentPower + recipe.powerAmount > PowerCapacity)
        {
            _ = inputInventory.TryAdd(recipe.inputResourceType, recipe.inputAmount);
            statusLabel = PowerFullStatus;
            return false;
        }

        currentPower += Mathf.Max(0, recipe.powerAmount);
        ClampPower();
        hasLastGeneratedPower = true;
        lastInputResourceType = recipe.inputResourceType;
        lastGeneratedPowerAmount = Mathf.Max(0, recipe.powerAmount);
        statusLabel = BuildRunningStatus(recipe);
        return true;
    }

    private bool TryResolveGenerationStep(
        out StructureResourceInventory inputInventory,
        out PowerGeneratorRecipeDefinition recipe,
        out string failureReason)
    {
        inputInventory = null;
        recipe = default;
        failureReason = IdleStatus;

        if (partFocus == null || partFocus.SourcePart == null || !PowerGeneratorRecipeCatalog.IsGeneratorPart(partFocus.SourcePart))
        {
            failureReason = UnsupportedStatus;
            return false;
        }

        if (!ModulePartInventoryUtility.TryResolveInputInventory(partFocus, out inputInventory) || inputInventory == null)
        {
            failureReason = NoInventoryStatus;
            return false;
        }

        if (!TryGetSelectedRecipe(out recipe))
        {
            failureReason = NoRecipeStatus;
            return false;
        }

        if (inputInventory.GetAmount(recipe.inputResourceType) < recipe.inputAmount)
        {
            failureReason = NoInputStatus;
            return false;
        }

        if (CurrentPower + recipe.powerAmount > PowerCapacity)
        {
            failureReason = PowerFullStatus;
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
        var recipes = PowerGeneratorRecipeCatalog.GetRecipes(partFocus);
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

    private int ResolvePowerCapacity()
    {
        return partFocus != null && partFocus.SourcePart != null
            ? Mathf.Max(0, Mathf.RoundToInt(partFocus.SourcePart.powerCapacity))
            : 0;
    }

    private void ClampPower()
    {
        currentPower = Mathf.Clamp(currentPower, 0, ResolvePowerCapacity());
    }

    private static string BuildRunningStatus(PowerGeneratorRecipeDefinition recipe)
    {
        return $"{RunningStatus} / {FormatResourceLabel(recipe.inputResourceType)} x{recipe.inputAmount} -> POWER +{Mathf.Max(0, recipe.powerAmount)}";
    }

    private static string FormatResourceLabel(InventoryResourceType resourceType)
    {
        return InventoryResourceCatalog.GetDisplayName(resourceType).Replace("_", " ");
    }
}