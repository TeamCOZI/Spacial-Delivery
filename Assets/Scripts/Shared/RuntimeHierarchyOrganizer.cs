using UnityEngine;

public static class RuntimeHierarchyOrganizer
{
    private const string RuntimeRootName = "__RuntimeVisuals";

    public static Transform GetOrCreateGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            groupName = "Misc";
        }

        GameObject root = GameObject.Find(RuntimeRootName);
        if (root == null)
        {
            root = new GameObject(RuntimeRootName);
        }

        Transform group = root.transform.Find(groupName);
        if (group != null) return group;

        GameObject groupObject = new GameObject(groupName);
        groupObject.transform.SetParent(root.transform, false);
        return groupObject.transform;
    }
}
