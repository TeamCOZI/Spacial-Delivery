using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WarehouseState : MonoBehaviour
{
    [SerializeField] private bool transportToCoreEnabled;
    [SerializeField] private float transportElapsedSeconds;

    private AssemblyPartFocus partFocus;
    private StructureResourceInventory inventory;

    public bool IsTransportToCoreEnabled => transportToCoreEnabled;

    private void Awake()
    {
        BindReferences();
    }

    private void OnEnable()
    {
        BindReferences();
    }

    private void Update()
    {
        if (!transportToCoreEnabled)
        {
            return;
        }

        BindReferences();
        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null || inventory == null)
        {
            return;
        }

        float intervalSeconds = WarehouseUtility.ResolveTransitCoreTimeSeconds(partFocus.SourcePart);
        transportElapsedSeconds = Mathf.Max(0f, transportElapsedSeconds + Mathf.Max(0f, Time.deltaTime));

        int safetyCounter = 4;
        while (transportElapsedSeconds + 0.0001f >= intervalSeconds && safetyCounter-- > 0)
        {
            transportElapsedSeconds = Mathf.Max(0f, transportElapsedSeconds - intervalSeconds);
            _ = TryTransferAllToCoreLogistics(out _);
        }
    }

    public void SetTransportToCoreEnabled(bool enabled)
    {
        if (transportToCoreEnabled == enabled)
        {
            return;
        }

        transportToCoreEnabled = enabled;
        transportElapsedSeconds = 0f;
    }

    public bool TryTransferAllToCoreLogistics(out int movedCount)
    {
        movedCount = 0;
        BindReferences();
        if (partFocus == null || partFocus.OwnerSatellite == null || inventory == null || inventory.TotalAmount <= 0)
        {
            return false;
        }

        List<StructureResourceInventory> logisticsInventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(partFocus.OwnerSatellite, logisticsInventories);
        if (logisticsInventories.Count <= 0)
        {
            return false;
        }

        List<StructureResourceInventory.ResourceAmount> snapshot = new List<StructureResourceInventory.ResourceAmount>(inventory.Resources.Count);
        IReadOnlyList<StructureResourceInventory.ResourceAmount> storedResources = inventory.Resources;
        for (int i = 0; i < storedResources.Count; i++)
        {
            snapshot.Add(storedResources[i]);
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            StructureResourceInventory.ResourceAmount resourceAmount = snapshot[i];
            int remainingAmount = Mathf.Max(0, resourceAmount.amount);
            while (remainingAmount > 0)
            {
                if (!TryStorePacketInCoreLogistics(resourceAmount.resourceType, logisticsInventories) ||
                    !inventory.TryRemove(resourceAmount.resourceType, 1))
                {
                    remainingAmount = 0;
                    break;
                }

                remainingAmount--;
                movedCount++;
            }
        }

        return movedCount > 0;
    }

    private void BindReferences()
    {
        if (partFocus == null)
        {
            partFocus = GetComponent<AssemblyPartFocus>();
        }

        if (inventory == null && partFocus != null)
        {
            ModulePartInventoryUtility.TryResolvePartInventory(partFocus, StructureResourceInventoryKind.General, out inventory);
        }
    }

    private static bool TryStorePacketInCoreLogistics(
        InventoryResourceType resourceType,
        List<StructureResourceInventory> logisticsInventories)
    {
        if (logisticsInventories == null)
        {
            return false;
        }

        for (int i = 0; i < logisticsInventories.Count; i++)
        {
            StructureResourceInventory targetInventory = logisticsInventories[i];
            if (targetInventory != null && targetInventory.TryAdd(resourceType, 1))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectCoreLogisticsInventories(
        ArtificialSatellite ownerSatellite,
        List<StructureResourceInventory> inventories)
    {
        if (inventories == null)
        {
            return;
        }

        inventories.Clear();
        if (ownerSatellite == null)
        {
            return;
        }

        StructureInstance[] instances = ownerSatellite.GetComponentsInChildren<StructureInstance>(true);
        for (int i = 0; i < instances.Length; i++)
        {
            StructureInstance instance = instances[i];
            if (instance == null || instance.SourceStructure == null || !instance.SourceStructure.UsesLogisticsHubUi)
            {
                continue;
            }

            StructureResourceInventory inventory = instance.GetComponent<StructureResourceInventory>();
            if (inventory == null)
            {
                inventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(instance.gameObject);
                inventory.InitializeForStructure(instance.SourceStructure);
            }

            if (inventory != null)
            {
                inventories.Add(inventory);
            }
        }
    }
}
