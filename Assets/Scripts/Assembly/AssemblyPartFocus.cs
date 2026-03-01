using System.Collections.Generic;
using UnityEngine;

public class AssemblyPartFocus : MonoBehaviour, UpdateFocusInfo
{
    private const int GridSize = 99;
    private const float CellSize = 0.1f;

    private Part sourcePart;
    private ArtificialSatellite ownerSatellite;

    public void Initialize(Part part, ArtificialSatellite owner)
    {
        sourcePart = part;
        ownerSatellite = owner;
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string partName = sourcePart != null ? sourcePart.partName : name;
        string partType = sourcePart != null ? sourcePart.partType.ToString() : "Unknown";
        string durability = sourcePart != null ? sourcePart.durability.ToString("0.##") : "N/A";
        string inventory = sourcePart != null ? sourcePart.inventory.ToString("0.##") : "N/A";
        string footprint = sourcePart != null
            ? sourcePart.gridWidth + " x " + sourcePart.gridHeight
            : "N/A";
        string ownerName = ownerSatellite != null ? ownerSatellite.name : "N/A";
        string installedCell = GetInstalledCellLabel();

        return new Dictionary<string, string>
        {
            { "Name", partName },
            { "Category", "Part" },
            { "Type", partType },
            { "Durability", durability },
            { "Inventory", inventory },
            { "Footprint", footprint },
            { "Installed Cell", installedCell },
            { "Owner", ownerName }
        };
    }

    public ArtificialSatellite OwnerSatellite => ownerSatellite;
    public Part SourcePart => sourcePart;

    private string GetInstalledCellLabel()
    {
        if (ownerSatellite == null) return "N/A";

        Vector2 snapOffset = GetPartSnapOffset();
        int center = GridSize / 2;
        Vector3 localPos = transform.localPosition;

        int cellX = Mathf.RoundToInt((localPos.x - snapOffset.x) / CellSize) + center;
        int cellY = Mathf.RoundToInt((localPos.y - snapOffset.y) / CellSize) + center;
        return $"({cellX}, {cellY})";
    }

    private Vector2 GetPartSnapOffset()
    {
        int width = sourcePart != null ? Mathf.Max(1, sourcePart.gridWidth) : 1;
        int height = sourcePart != null ? Mathf.Max(1, sourcePart.gridHeight) : 1;

        float offsetX = (width % 2 == 0) ? (CellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (CellSize * 0.5f) : 0f;
        return new Vector2(offsetX, offsetY);
    }
}
