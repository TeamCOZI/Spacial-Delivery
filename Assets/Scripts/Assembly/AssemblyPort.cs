using UnityEngine;

public enum AssemblyPortType
{
    Input,
    Output
}

public class AssemblyPort : MonoBehaviour
{
    public AssemblyPortType portType = AssemblyPortType.Output;
    public Vector3 localDirection = Vector3.up;

    [SerializeField] private bool isOccupied = false;

    public bool IsOccupied => isOccupied;

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }
}
