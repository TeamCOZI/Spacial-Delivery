using UnityEngine;

public class StructureInstance : MonoBehaviour
{
    [SerializeField] private Structure sourceStructure;
    [SerializeField] private Vector2Int installedCenterCell;

    public Structure SourceStructure => sourceStructure;
    public Vector2Int InstalledCenterCell => installedCenterCell;

    public void Initialize(Structure structure, Vector2Int centerCell)
    {
        sourceStructure = structure;
        installedCenterCell = centerCell;
    }
}
