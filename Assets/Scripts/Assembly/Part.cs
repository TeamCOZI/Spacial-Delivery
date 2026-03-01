using UnityEngine;

public enum PartType { Core, Pipe, Processor, Storage }

[CreateAssetMenu(fileName = "Parts", menuName = "Spacial Delivery/Part", order = 0)]
public class Part : ScriptableObject
{
    public string partName;
    public Sprite partIcon;

    public PartType partType;

    public GameObject partPrefab;
    public GameObject ghostPrefab;

    [Header("Grid Footprint (Cell Units)")]
    [Min(1)] public int gridWidth = 1;
    [Min(1)] public int gridHeight = 1;

    [Header("Part Settings")]
    public float durability;
    public float inventory;
}
