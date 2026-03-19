using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StructureInstance))]
public class StructureFocus : MonoBehaviour, UpdateFocusInfo
{
    private StructureInstance structureInstance;

    public Structure SourceStructure => structureInstance != null ? structureInstance.SourceStructure : null;
    public Vector2Int InstalledCenterCell => structureInstance != null ? structureInstance.InstalledCenterCell : Vector2Int.zero;

    public ArtificialSatellite OwnerSatellite
    {
        get
        {
            return GetComponentInParent<ArtificialSatellite>();
        }
    }

    public bool UsesFabricatorUi => SourceStructure != null && SourceStructure.UsesFabricatorUi;

    private void Awake()
    {
        structureInstance = GetComponent<StructureInstance>();
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        Structure sourceStructure = SourceStructure;
        ArtificialSatellite ownerSatellite = OwnerSatellite;

        return new Dictionary<string, string>
        {
            { "Name", sourceStructure != null ? sourceStructure.structureName : name },
            { "Category", "Structure" },
            { "Facility", sourceStructure != null ? sourceStructure.facilityKind.ToString() : StructureFacilityKind.None.ToString() },
            { "Footprint", sourceStructure != null ? $"{sourceStructure.gridWidth} x {sourceStructure.gridHeight}" : "N/A" },
            { "Installed Cell", $"({InstalledCenterCell.x}, {InstalledCenterCell.y})" },
            { "Owner", ownerSatellite != null ? ownerSatellite.name : "N/A" }
        };
    }
}
