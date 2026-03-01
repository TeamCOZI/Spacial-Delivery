using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AssemblyPartPortLayout : MonoBehaviour
{
    public enum PortSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    [System.Serializable]
    public class PortEntry
    {
        public Transform portTransform;
        public Vector2Int relativeSourceCell;
        public PortSide side = PortSide.Right;
        public AssemblyPortType portType = AssemblyPortType.Output;
    }

    [SerializeField] private List<PortEntry> ports = new List<PortEntry>();

    public List<PortEntry> Ports => ports;

    public void SetPorts(List<PortEntry> entries)
    {
        ports = entries ?? new List<PortEntry>();
    }

    public bool TryGetPortEntry(AssemblyPort port, out PortEntry entry)
    {
        entry = null;
        if (port == null || ports == null) return false;

        Transform target = port.transform;
        for (int i = 0; i < ports.Count; i++)
        {
            PortEntry candidate = ports[i];
            if (candidate == null || candidate.portTransform == null) continue;
            if (candidate.portTransform != target) continue;

            entry = candidate;
            return true;
        }

        return false;
    }
}

