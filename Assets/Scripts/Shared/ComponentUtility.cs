using UnityEngine;

public static class ComponentUtility
{
    public static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        if (target == null) return null;

        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return target.AddComponent<T>();
    }
}
