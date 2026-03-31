using UnityEngine;

[DisallowMultipleComponent]
public class FilterPipeState : MonoBehaviour
{
    [SerializeField] private bool hasFilteredResource;
    [SerializeField] private InventoryResourceType filteredResourceType;

    public void SetFilteredResource(InventoryResourceType resourceType)
    {
        if (hasFilteredResource && filteredResourceType == resourceType)
        {
            ClearFilteredResource();
            return;
        }

        hasFilteredResource = true;
        filteredResourceType = resourceType;
    }

    public void ClearFilteredResource()
    {
        hasFilteredResource = false;
    }

    public bool TryGetFilteredResource(out InventoryResourceType resourceType)
    {
        resourceType = filteredResourceType;
        return hasFilteredResource;
    }

    public bool AllowsResource(InventoryResourceType resourceType)
    {
        return hasFilteredResource && filteredResourceType == resourceType;
    }
}
