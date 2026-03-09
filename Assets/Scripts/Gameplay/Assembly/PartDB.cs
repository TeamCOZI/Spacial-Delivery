using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartDB : MonoBehaviour
{
    public static PartDB Instance { get; private set; }

    private List<Part> partsList;

    private Dictionary<string, Part> partsDict;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        partsList = Resources.LoadAll<Part>("Parts").ToList();

        partsDict = new Dictionary<string, Part>();
        foreach (Part part in partsList)
        {
            if (!partsDict.ContainsKey(part.partName)) partsDict.Add(part.partName, part);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public List<Part> GetAllParts()
    {
        return partsList;
    }

    public Part GetPartByName(string partName)
    {
        partsDict.TryGetValue(partName, out Part part);
        return part;
    }
}
