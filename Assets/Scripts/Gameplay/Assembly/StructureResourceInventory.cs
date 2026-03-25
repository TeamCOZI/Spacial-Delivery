using System;
using System.Collections.Generic;
using UnityEngine;

public enum StructureResourceInventoryKind
{
    General,
    Input,
    Output
}

public class StructureResourceInventory : MonoBehaviour
{
    [Serializable]
    public struct ResourceAmount
    {
        public InventoryResourceType resourceType;
        public int amount;

        public ResourceAmount(InventoryResourceType resourceType, int amount)
        {
            this.resourceType = resourceType;
            this.amount = amount;
        }

        public string DisplayName => InventoryResourceCatalog.GetDisplayName(resourceType);
    }

    private const int DebugSeedAmountPerResource = 100;
    private static readonly InventoryResourceType[] CoreLogisticsHubStarterResourceTypes =
    {
        InventoryResourceType.Territe_Crystal,
        InventoryResourceType.Territe_Liquid,
        InventoryResourceType.Territe_Gas,
        InventoryResourceType.Nitain_Crystal,
        InventoryResourceType.Nitain_Liquid,
        InventoryResourceType.Nitain_Gas,
        InventoryResourceType.Aquid_Crystal,
        InventoryResourceType.Aquid_Liquid,
        InventoryResourceType.Aquid_Gas,
    };
    [SerializeField] private StructureResourceInventoryKind inventoryKind = StructureResourceInventoryKind.General;
    [SerializeField] private int capacity;
    [SerializeField] private List<ResourceAmount> resources = new List<ResourceAmount>();

    private int revision;

    public StructureResourceInventoryKind InventoryKind => inventoryKind;
    public int Capacity => Mathf.Max(0, capacity);
    public IReadOnlyList<ResourceAmount> Resources => resources;
    public int ResourceTypeCount => resources.Count;
    public int Revision => revision;
    public int FreeCapacity => Mathf.Max(0, Capacity - TotalAmount);

    public int TotalAmount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                total += Mathf.Max(0, resources[i].amount);
            }

            return total;
        }
    }

    public void InitializeForStructure(Structure structure)
    {
        inventoryKind = StructureResourceInventoryKind.General;
        capacity = structure != null ? Mathf.Max(0, Mathf.RoundToInt(structure.capacity)) : 0;
        if (structure == null)
        {
            ClampToCapacity();
            MarkChanged();
            return;
        }

        if (structure.UsesLogisticsHubUi && resources.Count == 0)
        {
            if (string.Equals(structure.structureName, "Core Logistics Hub", StringComparison.OrdinalIgnoreCase))
            {
                SeedDebugInventory(DebugSeedAmountPerResource, CoreLogisticsHubStarterResourceTypes);
            }
            else
            {
                SeedDebugInventory(DebugSeedAmountPerResource);
            }

            return;
        }

        ClampToCapacity();
        MarkChanged();
    }

    public void InitializeForPart(Part part)
    {
        InitializeForPart(part, StructureResourceInventoryKind.General);
    }

    public void InitializeForPart(Part part, StructureResourceInventoryKind kind)
    {
        inventoryKind = kind;
        capacity = ResolvePartCapacity(part, kind);
        ClampToCapacity();
        MarkChanged();
    }

    public bool TryGetResourceAtSlot(int slotIndex, out ResourceAmount resourceAmount)
    {
        resourceAmount = default;
        if (slotIndex < 0 || slotIndex >= resources.Count)
        {
            return false;
        }

        resourceAmount = resources[slotIndex];
        return true;
    }

    public int GetAmount(InventoryResourceType resourceType)
    {
        int index = FindResourceIndex(resourceType);
        return index >= 0 ? Mathf.Max(0, resources[index].amount) : 0;
    }

    public bool HasAmount(InventoryResourceType resourceType, int amount)
    {
        return GetAmount(resourceType) >= Mathf.Max(0, amount);
    }

    public bool TryAdd(InventoryResourceType resourceType, int amount)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return true;
        }

        if (TotalAmount + sanitizedAmount > Capacity)
        {
            return false;
        }

        int index = FindResourceIndex(resourceType);
        if (index >= 0)
        {
            ResourceAmount updated = resources[index];
            updated.amount += sanitizedAmount;
            resources[index] = updated;
        }
        else
        {
            resources.Add(new ResourceAmount(resourceType, sanitizedAmount));
        }

        CleanupZeroEntries();
        MarkChanged();
        return true;
    }

    public bool TryRemove(InventoryResourceType resourceType, int amount)
    {
        int sanitizedAmount = Mathf.Max(0, amount);
        if (sanitizedAmount <= 0)
        {
            return true;
        }

        int index = FindResourceIndex(resourceType);
        if (index < 0)
        {
            return false;
        }

        ResourceAmount current = resources[index];
        if (current.amount < sanitizedAmount)
        {
            return false;
        }

        current.amount -= sanitizedAmount;
        if (current.amount > 0)
        {
            resources[index] = current;
        }
        else
        {
            resources.RemoveAt(index);
        }

        CleanupZeroEntries();
        MarkChanged();
        return true;
    }

    public void SeedDebugInventory(int amountPerResource)
    {
        SeedDebugInventory(amountPerResource, InventoryResourceCatalog.All);
    }

    public void SeedDebugInventory(int amountPerResource, IReadOnlyList<InventoryResourceType> resourceTypes)
    {
        resources.Clear();

        if (resourceTypes == null || resourceTypes.Count == 0)
        {
            CleanupZeroEntries();
            MarkChanged();
            return;
        }

        int sanitizedAmount = Mathf.Max(0, amountPerResource);
        int remainingCapacity = Capacity;
        for (int i = 0; i < resourceTypes.Count; i++)
        {
            if (remainingCapacity <= 0)
            {
                break;
            }

            int amountToAdd = Mathf.Min(sanitizedAmount, remainingCapacity);
            if (amountToAdd <= 0)
            {
                continue;
            }

            resources.Add(new ResourceAmount(resourceTypes[i], amountToAdd));
            remainingCapacity -= amountToAdd;
        }

        CleanupZeroEntries();
        MarkChanged();
    }

    private static int ResolvePartCapacity(Part part, StructureResourceInventoryKind kind)
    {
        if (part == null)
        {
            return 0;
        }

        switch (kind)
        {
            case StructureResourceInventoryKind.Input:
                return Mathf.Max(0, part.inputCapacity);

            case StructureResourceInventoryKind.Output:
                return Mathf.Max(0, part.outputCapacity);

            default:
                return Mathf.Max(0, Mathf.RoundToInt(part.inventory));
        }
    }

    private int FindResourceIndex(InventoryResourceType resourceType)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            if (resources[i].resourceType == resourceType)
            {
                return i;
            }
        }

        return -1;
    }

    private void CleanupZeroEntries()
    {
        for (int i = resources.Count - 1; i >= 0; i--)
        {
            if (resources[i].amount > 0)
            {
                continue;
            }

            resources.RemoveAt(i);
        }
    }

    private void ClampToCapacity()
    {
        CleanupZeroEntries();
        int overflow = Mathf.Max(0, TotalAmount - Capacity);
        if (overflow <= 0)
        {
            return;
        }

        for (int i = resources.Count - 1; i >= 0 && overflow > 0; i--)
        {
            ResourceAmount current = resources[i];
            int removableAmount = Mathf.Min(current.amount, overflow);
            current.amount -= removableAmount;
            overflow -= removableAmount;

            if (current.amount > 0)
            {
                resources[i] = current;
            }
            else
            {
                resources.RemoveAt(i);
            }
        }
    }

    private void MarkChanged()
    {
        revision++;
    }
}

