using System;
using System.Collections.Generic;
using UnityEngine;

public class AssemblyPartFocus : MonoBehaviour, UpdateFocusInfo
{
    private const int GridSize = 99;
    private const float CellSize = 0.1f;
    private const string LauncherPartName = "Launcher";

    private Part sourcePart;
    private ArtificialSatellite ownerSatellite;

    private void Awake()
    {
        BindOwnerSatelliteIfMissing();
    }

    private void OnTransformParentChanged()
    {
        BindOwnerSatelliteIfMissing();
    }

    public void Initialize(Part part, ArtificialSatellite owner)
    {
        sourcePart = part;
        if (owner != null)
        {
            ownerSatellite = owner;
        }
        else
        {
            BindOwnerSatelliteIfMissing();
        }
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string partName = sourcePart != null ? sourcePart.partName : name;
        string partType = sourcePart != null ? sourcePart.partType.ToString() : "Unknown";
        string durability = FormatPartNumericValue(sourcePart != null ? sourcePart.durability : (float?)null);
        string inventory = FormatPartNumericValue(sourcePart != null ? sourcePart.inventory : (float?)null);
        string footprint = GetFootprintLabel();
        string ownerName = OwnerSatellite != null ? OwnerSatellite.name : "N/A";
        string installedCell = GetInstalledCellLabel();

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "Name", partName },
            { "Category", "Part" },
            { "Type", partType },
            { "Durability", durability },
            { "Inventory", inventory }
        };

        if (IsLauncherPart(partName))
        {
            focusInfo.Add("Available Spaceships", LauncherSpaceshipStock.GetAvailableSpaceshipCount(OwnerSatellite).ToString());
        }

        focusInfo.Add("Footprint", footprint);
        focusInfo.Add("Installed Cell", installedCell);
        focusInfo.Add("Owner", ownerName);
        return focusInfo;
    }

    public ArtificialSatellite OwnerSatellite
    {
        get
        {
            BindOwnerSatelliteIfMissing();
            return ownerSatellite;
        }
    }

    public Part SourcePart => sourcePart;

    public bool TryGetOwnerSatellite(out ArtificialSatellite owner)
    {
        owner = OwnerSatellite;
        return owner != null;
    }

    private string GetInstalledCellLabel()
    {
        ArtificialSatellite owner = OwnerSatellite;
        if (owner == null) return "N/A";

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

    private string GetFootprintLabel()
    {
        if (sourcePart == null) return "N/A";
        return sourcePart.gridWidth + " x " + sourcePart.gridHeight;
    }

    private static string FormatPartNumericValue(float? value)
    {
        if (!value.HasValue) return "N/A";
        return value.Value.ToString("0.##");
    }

    private static bool IsLauncherPart(string partName)
    {
        return !string.IsNullOrWhiteSpace(partName) &&
               string.Equals(partName, LauncherPartName, StringComparison.OrdinalIgnoreCase);
    }

    private void BindOwnerSatelliteIfMissing()
    {
        if (ownerSatellite != null) return;
        ownerSatellite = GetComponentInParent<ArtificialSatellite>();
    }
}
