using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AssemblyOutputPortFocus : MonoBehaviour, UpdateFocusInfo
{
    private const string UnknownLabel = "Unknown";

    [SerializeField] private AssemblyPort port;
    [SerializeField] private ArtificialSatellite ownerSatellite;
    [SerializeField] private AssemblyPartFocus ownerPartFocus;
    [SerializeField] private string ownerLabel = UnknownLabel;
    [SerializeField] private string sideLabel = UnknownLabel;
    [SerializeField] private Vector2Int sourceCell;
    [SerializeField] private Vector2Int mappedCell;
    [SerializeField] private bool hasCellMapping;

    public AssemblyPort Port
    {
        get
        {
            if (port == null)
            {
                port = GetComponent<AssemblyPort>();
            }
            return port;
        }
    }

    public ArtificialSatellite OwnerSatellite
    {
        get
        {
            BindReferencesIfMissing();
            return ownerSatellite;
        }
    }

    public AssemblyPartFocus OwnerPartFocus
    {
        get
        {
            BindReferencesIfMissing();
            return ownerPartFocus;
        }
    }

    public string OwnerLabel => string.IsNullOrWhiteSpace(ownerLabel) ? ResolveFallbackOwnerLabel() : ownerLabel;
    public string SideLabel => string.IsNullOrWhiteSpace(sideLabel) ? ResolveDirectionLabel(Port != null ? Port.LocalDirection : transform.localPosition) : sideLabel;
    public bool HasCellMapping => hasCellMapping;
    public Vector2Int SourceCell => sourceCell;
    public Vector2Int MappedCell => mappedCell;
    public bool IsOccupied => Port != null && Port.IsOccupied;

    public Sprite DisplayIcon
    {
        get
        {
            AssemblyPartFocus partFocus = OwnerPartFocus;
            return partFocus != null && partFocus.SourcePart != null ? partFocus.SourcePart.partIcon : null;
        }
    }

    public string DisplayName
    {
        get
        {
            AssemblyPartFocus partFocus = OwnerPartFocus;
            if (partFocus != null && partFocus.SourcePart != null && !string.IsNullOrWhiteSpace(partFocus.SourcePart.partName))
            {
                return partFocus.SourcePart.partName + " Output Port";
            }

            string resolvedOwner = OwnerLabel;
            if (string.IsNullOrWhiteSpace(resolvedOwner) || string.Equals(resolvedOwner, UnknownLabel, System.StringComparison.OrdinalIgnoreCase))
            {
                return "Output Port";
            }

            return resolvedOwner + " Output Port";
        }
    }

    private void Awake()
    {
        BindReferencesIfMissing();
    }

    private void OnTransformParentChanged()
    {
        BindReferencesIfMissing();
    }

    public void Initialize(
        AssemblyPort targetPort,
        ArtificialSatellite satellite,
        AssemblyPartFocus partFocus,
        string resolvedOwnerLabel,
        string resolvedSideLabel,
        Vector2Int resolvedSourceCell,
        Vector2Int resolvedMappedCell,
        bool mapped)
    {
        port = targetPort != null ? targetPort : GetComponent<AssemblyPort>();
        ownerSatellite = satellite != null ? satellite : GetComponentInParent<ArtificialSatellite>();
        ownerPartFocus = partFocus != null ? partFocus : GetComponentInParent<AssemblyPartFocus>();
        ownerLabel = string.IsNullOrWhiteSpace(resolvedOwnerLabel) ? ResolveFallbackOwnerLabel() : resolvedOwnerLabel;
        sideLabel = string.IsNullOrWhiteSpace(resolvedSideLabel) ? ResolveDirectionLabel(port != null ? port.LocalDirection : transform.localPosition) : resolvedSideLabel;
        sourceCell = resolvedSourceCell;
        mappedCell = resolvedMappedCell;
        hasCellMapping = mapped;
    }

    public Transform ResolveCloseFocus()
    {
        AssemblyPartFocus partFocus = OwnerPartFocus;
        if (partFocus != null)
        {
            return partFocus.transform;
        }

        ArtificialSatellite satellite = OwnerSatellite;
        return satellite != null ? satellite.transform : null;
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        Dictionary<string, string> info = new Dictionary<string, string>
        {
            { "Name", DisplayName },
            { "Category", "Output Port" },
            { "Owner", OwnerLabel },
            { "Side", SideLabel },
            { "Occupied", IsOccupied ? "Connected" : "Idle" }
        };

        ArtificialSatellite satellite = OwnerSatellite;
        if (satellite != null)
        {
            info.Add("Satellite", satellite.name);
        }

        if (hasCellMapping)
        {
            info.Add("Source Cell", sourceCell.ToString());
            info.Add("Boundary Cell", mappedCell.ToString());
        }

        return info;
    }

    private void BindReferencesIfMissing()
    {
        if (port == null)
        {
            port = GetComponent<AssemblyPort>();
        }

        if (ownerPartFocus == null)
        {
            ownerPartFocus = GetComponentInParent<AssemblyPartFocus>();
        }

        if (ownerSatellite == null)
        {
            ownerSatellite = GetComponentInParent<ArtificialSatellite>();
        }

        if (string.IsNullOrWhiteSpace(ownerLabel))
        {
            ownerLabel = ResolveFallbackOwnerLabel();
        }

        if (string.IsNullOrWhiteSpace(sideLabel))
        {
            sideLabel = ResolveDirectionLabel(port != null ? port.LocalDirection : transform.localPosition);
        }
    }

    private string ResolveFallbackOwnerLabel()
    {
        AssemblyPartFocus partFocus = OwnerPartFocus;
        if (partFocus != null && partFocus.SourcePart != null && !string.IsNullOrWhiteSpace(partFocus.SourcePart.partName))
        {
            return partFocus.SourcePart.partName;
        }

        return OwnerSatellite != null ? "Core" : UnknownLabel;
    }

    private static string ResolveDirectionLabel(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.000001f)
        {
            return UnknownLabel;
        }

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        if (absX >= absY)
        {
            return direction.x >= 0f ? "Right" : "Left";
        }

        return direction.y >= 0f ? "Top" : "Bottom";
    }
}
