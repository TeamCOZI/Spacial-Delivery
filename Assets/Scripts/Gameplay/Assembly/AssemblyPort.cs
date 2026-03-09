using UnityEngine;

public enum AssemblyPortType
{
    Input,
    Output
}

public class AssemblyPort : MonoBehaviour
{
    [SerializeField] private AssemblyPortType portType = AssemblyPortType.Output;
    [SerializeField] private Vector3 localDirection = Vector3.up;

    [SerializeField] private bool isOccupied = false;

    public AssemblyPortType PortType
    {
        get => portType;
        set => portType = value;
    }

    public Vector3 LocalDirection
    {
        get => localDirection;
        set => localDirection = value;
    }

    public bool IsOccupied => isOccupied;

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }
}
