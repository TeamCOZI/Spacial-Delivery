using UnityEngine;
using System.Collections.Generic;

public class ObjectPool
{
    private readonly GameObject prefab;
    private readonly Queue<GameObject> availableObjects = new Queue<GameObject>();
    private readonly Transform parent;

    public ObjectPool(GameObject prefab, int initialSize, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            obj.SetActive(false);
            availableObjects.Enqueue(obj);
        }
    }

    public GameObject Get()
    {
        if (availableObjects.Count > 0)
        {
            GameObject obj = availableObjects.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            return obj;
        }
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        availableObjects.Enqueue(obj);
    }
}