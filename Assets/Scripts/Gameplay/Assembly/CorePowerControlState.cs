using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CorePowerControlState : MonoBehaviour
{
    private const int MaxHistorySamples = 48;
    private const float SampleIntervalSeconds = 1f;

    [System.Serializable]
    private struct PowerSnapshot
    {
        public float production;
        public float consumption;
        public int storedComponentPower;
        public int componentCapacity;
        public int structureCapacity;
    }

    [SerializeField] private float currentProduction;
    [SerializeField] private float currentConsumption;
    [SerializeField] private int reservePower;
    [SerializeField] private int reserveCapacity;
    [SerializeField] private int storedComponentPower;
    [SerializeField] private int componentPowerCapacity;
    [SerializeField] private int structureReserveCapacity;
    [SerializeField] private float bufferedStructurePower;
    [SerializeField] private float sampleTimer;
    [SerializeField] private int revision;
    [SerializeField] private List<float> productionHistory = new List<float>();
    [SerializeField] private List<float> consumptionHistory = new List<float>();

    private StructureFocus structureFocus;
    private ArtificialSatellite ownerSatellite;
    private bool initializedBuffer;

    public float CurrentProduction => Mathf.Max(0f, currentProduction);
    public float CurrentConsumption => Mathf.Max(0f, currentConsumption);
    public int ReservePower => Mathf.Max(0, reservePower);
    public int ReserveCapacity => Mathf.Max(0, reserveCapacity);
    public int Revision => revision;
    public IReadOnlyList<float> ProductionHistory => productionHistory;
    public IReadOnlyList<float> ConsumptionHistory => consumptionHistory;

    private void Awake()
    {
        BindReferences();
        EvaluateImmediateState(seedHistory: true);
    }

    private void OnEnable()
    {
        BindReferences();
        EvaluateImmediateState(seedHistory: productionHistory.Count <= 0 || consumptionHistory.Count <= 0);
    }

    private void Update()
    {
        BindReferences();
        if (ownerSatellite == null)
        {
            return;
        }

        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        PowerSnapshot snapshot = BuildSnapshot(ownerSatellite);
        ApplySnapshot(snapshot, deltaTime, recordHistory: false);

        sampleTimer += deltaTime;
        while (sampleTimer >= SampleIntervalSeconds)
        {
            sampleTimer -= SampleIntervalSeconds;
            AppendHistorySample(CurrentProduction, CurrentConsumption);
        }
    }

    private void BindReferences()
    {
        if (structureFocus == null)
        {
            structureFocus = GetComponent<StructureFocus>();
        }

        if (structureFocus == null)
        {
            structureFocus = GetComponentInParent<StructureFocus>();
        }

        ownerSatellite = structureFocus != null ? structureFocus.OwnerSatellite : null;
    }

    private void EvaluateImmediateState(bool seedHistory)
    {
        if (ownerSatellite == null)
        {
            currentProduction = 0f;
            currentConsumption = 0f;
            reservePower = 0;
            reserveCapacity = 0;
            storedComponentPower = 0;
            componentPowerCapacity = 0;
            structureReserveCapacity = 0;
            bufferedStructurePower = 0f;
            initializedBuffer = false;
            if (seedHistory)
            {
                productionHistory.Clear();
                consumptionHistory.Clear();
                revision++;
            }
            return;
        }

        PowerSnapshot snapshot = BuildSnapshot(ownerSatellite);
        ApplySnapshot(snapshot, 0f, recordHistory: seedHistory);
    }

    private void ApplySnapshot(PowerSnapshot snapshot, float deltaTime, bool recordHistory)
    {
        int sanitizedStructureCapacity = Mathf.Max(0, snapshot.structureCapacity);
        if (!initializedBuffer)
        {
            bufferedStructurePower = sanitizedStructureCapacity;
            initializedBuffer = true;
        }
        else
        {
            bufferedStructurePower = Mathf.Clamp(bufferedStructurePower, 0f, sanitizedStructureCapacity);
        }

        currentProduction = Mathf.Max(0f, snapshot.production);
        currentConsumption = Mathf.Max(0f, snapshot.consumption);
        storedComponentPower = Mathf.Max(0, snapshot.storedComponentPower);
        componentPowerCapacity = Mathf.Max(0, snapshot.componentCapacity);
        structureReserveCapacity = sanitizedStructureCapacity;

        if (deltaTime > 0f && structureReserveCapacity > 0)
        {
            bufferedStructurePower += (CurrentProduction - CurrentConsumption) * deltaTime;
            bufferedStructurePower = Mathf.Clamp(bufferedStructurePower, 0f, structureReserveCapacity);
        }

        reserveCapacity = Mathf.Max(0, componentPowerCapacity + structureReserveCapacity);
        reservePower = Mathf.Clamp(Mathf.RoundToInt(storedComponentPower + bufferedStructurePower), 0, reserveCapacity);

        if (recordHistory)
        {
            if (productionHistory.Count <= 0 || consumptionHistory.Count <= 0)
            {
                productionHistory.Clear();
                consumptionHistory.Clear();
                for (int i = 0; i < MaxHistorySamples; i++)
                {
                    productionHistory.Add(CurrentProduction);
                    consumptionHistory.Add(CurrentConsumption);
                }
                revision++;
            }
            else
            {
                AppendHistorySample(CurrentProduction, CurrentConsumption);
            }
        }
    }

    private void AppendHistorySample(float production, float consumption)
    {
        productionHistory.Add(Mathf.Max(0f, production));
        consumptionHistory.Add(Mathf.Max(0f, consumption));
        TrimHistory(productionHistory);
        TrimHistory(consumptionHistory);
        revision++;
    }

    private static void TrimHistory(List<float> values)
    {
        if (values == null)
        {
            return;
        }

        while (values.Count > MaxHistorySamples)
        {
            values.RemoveAt(0);
        }
    }

    private static PowerSnapshot BuildSnapshot(ArtificialSatellite satellite)
    {
        PowerSnapshot snapshot = default;
        if (satellite == null)
        {
            return snapshot;
        }

        AssemblyPartFocus[] partFocuses = satellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || partFocus.SourcePart == null)
            {
                continue;
            }

            Part sourcePart = partFocus.SourcePart;
            bool countedCapacity = false;

            if (PowerGeneratorRecipeCatalog.IsGeneratorPart(sourcePart))
            {
                PowerGeneratorState generatorState = partFocus.GetComponent<PowerGeneratorState>();
                if (generatorState == null)
                {
                    generatorState = partFocus.GetComponentInParent<PowerGeneratorState>();
                }

                if (generatorState != null)
                {
                    snapshot.storedComponentPower += generatorState.CurrentPower;
                    snapshot.componentCapacity += generatorState.PowerCapacity;
                    countedCapacity = true;

                    if (generatorState.IsPoweredOn && generatorState.TryGetSelectedRecipe(out PowerGeneratorRecipeDefinition recipe))
                    {
                        float seconds = Mathf.Max(0.01f, recipe.generationSeconds);
                        snapshot.production += Mathf.Max(0f, recipe.powerAmount / seconds);
                    }
                }
            }
            else if (SolarPanelUtility.IsSolarPanelPart(sourcePart))
            {
                SolarPanelState panelState = partFocus.GetComponent<SolarPanelState>();
                if (panelState == null)
                {
                    panelState = partFocus.GetComponentInParent<SolarPanelState>();
                }

                if (panelState != null)
                {
                    snapshot.storedComponentPower += panelState.CurrentPower;
                    snapshot.componentCapacity += panelState.PowerCapacity;
                    countedCapacity = true;
                    float seconds = Mathf.Max(0.01f, panelState.GenerationSeconds);
                    snapshot.production += Mathf.Max(0f, panelState.CurrentGenerationAmount / seconds);
                }
            }
            else if (SolarTurbineUtility.IsSolarTurbinePart(sourcePart))
            {
                SolarTurbineState turbineState = partFocus.GetComponent<SolarTurbineState>();
                if (turbineState == null)
                {
                    turbineState = partFocus.GetComponentInParent<SolarTurbineState>();
                }

                if (turbineState != null)
                {
                    float seconds = Mathf.Max(0.01f, turbineState.GenerationSeconds);
                    snapshot.production += Mathf.Max(0f, turbineState.CurrentGenerationAmount / seconds);
                }
            }
            else if (sourcePart.powerGeneration > 0f)
            {
                snapshot.production += Mathf.Max(0f, sourcePart.powerGeneration);
            }

            if (!countedCapacity && sourcePart.powerCapacity > 0f)
            {
                snapshot.componentCapacity += Mathf.Max(0, Mathf.RoundToInt(sourcePart.powerCapacity));
            }

            snapshot.consumption += ResolvePartConsumption(partFocus);
        }

        StructureInstance[] structures = satellite.GetComponentsInChildren<StructureInstance>(true);
        for (int i = 0; i < structures.Length; i++)
        {
            StructureInstance instance = structures[i];
            if (instance == null || instance.SourceStructure == null)
            {
                continue;
            }

            Structure sourceStructure = instance.SourceStructure;
            snapshot.production += Mathf.Max(0f, sourceStructure.powerGeneration);
            snapshot.consumption += Mathf.Max(0f, sourceStructure.powerConsumption);
            snapshot.structureCapacity += Mathf.Max(0, Mathf.RoundToInt(sourceStructure.powerCapacity));
        }

        return snapshot;
    }

    private static float ResolvePartConsumption(AssemblyPartFocus partFocus)
    {
        if (partFocus == null || partFocus.SourcePart == null)
        {
            return 0f;
        }

        Part sourcePart = partFocus.SourcePart;
        float nominalConsumption = Mathf.Max(0f, sourcePart.powerConsumption);
        if (nominalConsumption <= 0f)
        {
            return 0f;
        }

        if (sourcePart.partType == PartType.Pipe)
        {
            return nominalConsumption;
        }

        if (WarehouseUtility.IsWarehousePart(sourcePart))
        {
            return nominalConsumption;
        }

        if (PowerGeneratorRecipeCatalog.IsGeneratorPart(sourcePart))
        {
            PowerGeneratorState generatorState = partFocus.GetComponent<PowerGeneratorState>();
            if (generatorState == null)
            {
                generatorState = partFocus.GetComponentInParent<PowerGeneratorState>();
            }

            return generatorState != null && generatorState.IsPoweredOn ? nominalConsumption : 0f;
        }

        if (SolarPanelUtility.IsSolarPanelPart(sourcePart) || SolarTurbineUtility.IsSolarTurbinePart(sourcePart))
        {
            return nominalConsumption;
        }

        ModuleProcessingState processingState = partFocus.GetComponent<ModuleProcessingState>();
        if (processingState == null)
        {
            processingState = partFocus.GetComponentInParent<ModuleProcessingState>();
        }

        if (processingState != null)
        {
            return processingState.IsPoweredOn ? nominalConsumption : 0f;
        }

        return nominalConsumption;
    }
}
