using UnityEngine;

public enum PartType { Core, Pipe, Processor, Storage }

[CreateAssetMenu(fileName = "Parts", menuName = "Spacial Delivery/Part", order = 0)]
public class Part : ScriptableObject
{
    private const float DefaultCellSize = 0.1f;
    private const float MassPerCellArea = 100f;

    public string partName;
    public Sprite partIcon;

    public PartType partType;

    public GameObject partPrefab;
    public GameObject ghostPrefab;
    public GameObject cornerPrefab;
    public GameObject cornerGhostPrefab;
    public GameObject cornerEndPrefab;
    public GameObject cornerEndGhostPrefab;
    public GameObject endPrefab;
    public GameObject endGhostPrefab;

    [Header("Grid Footprint (Cell Units)")]
    [Min(1)] public int gridWidth = 1;
    [Min(1)] public int gridHeight = 1;

    [Header("Part Settings")]
    [Min(0)] public int mass;
    public float durability;
    public float inventory;

    private void OnValidate()
    {
        mass = CalculateMassFromScaleOrGrid();
    }

    private int CalculateMassFromScaleOrGrid()
    {
        if (partPrefab != null)
        {
            Vector3 scale = partPrefab.transform.localScale;
            float widthCells = Mathf.Abs(scale.x) / Mathf.Max(0.0001f, DefaultCellSize);
            float heightCells = Mathf.Abs(scale.y) / Mathf.Max(0.0001f, DefaultCellSize);
            float areaCells = widthCells * heightCells;
            return Mathf.Max(0, Mathf.RoundToInt(areaCells * MassPerCellArea));
        }

        int width = Mathf.Max(1, gridWidth);
        int height = Mathf.Max(1, gridHeight);
        return Mathf.Max(0, Mathf.RoundToInt(width * height * MassPerCellArea));
    }
}
