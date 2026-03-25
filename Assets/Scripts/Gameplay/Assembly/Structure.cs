using UnityEngine;

[CreateAssetMenu(fileName = "Structure", menuName = "Spacial Delivery/Structure", order = 10)]
public class Structure : ScriptableObject
{
    public string structureName;
    public Sprite structureIcon;
    public Color iconTint = Color.white;

    [Header("Grid Footprint (Cell Units)")]
    [Min(1)] public int gridWidth = 1;
    [Min(1)] public int gridHeight = 1;

    [Header("Structure Settings")]
    [Min(0)] public int mass;
    [Min(0f)] public float durability;
    [Min(0f)] public float capacity;
    [Min(0f)] public float powerGeneration;
    [Min(0f)] public float powerConsumption;
    [Min(0f)] public float powerCapacity;
    public CraftRecipe craftRecipe;

    [Header("Facility UI")]
    public StructureFacilityKind facilityKind = StructureFacilityKind.None;

    public bool UsesFabricatorUi => facilityKind == StructureFacilityKind.Fabricator;
    public bool UsesLogisticsHubUi => facilityKind == StructureFacilityKind.LogisticsHub;
}
