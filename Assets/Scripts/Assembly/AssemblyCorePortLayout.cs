using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AssemblyCorePortLayout : MonoBehaviour
{
    public enum PortSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    [System.Serializable]
    public class OutputPortEntry
    {
        public Transform portTransform;
        public Vector2Int relativeSourceCell;
        public PortSide outputSide = PortSide.Right;
    }

    [SerializeField] private List<OutputPortEntry> outputPorts = new List<OutputPortEntry>();

    public List<OutputPortEntry> OutputPorts => outputPorts;
}
