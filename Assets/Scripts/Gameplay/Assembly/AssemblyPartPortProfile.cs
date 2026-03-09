using UnityEngine;

public class AssemblyPartPortProfile : MonoBehaviour
{
    private static readonly Vector3[] DefaultInputPortLocalPositions = { Vector3.zero };

    [SerializeField] private Vector3[] inputPortLocalPositions = { Vector3.zero };

    public int InputPortCount
    {
        get
        {
            return GetValidatedInputPortPositions().Length;
        }
    }

    public Vector3 GetInputPortLocalPosition(int index)
    {
        Vector3[] positions = GetValidatedInputPortPositions();
        if (index < 0 || index >= positions.Length) return Vector3.zero;
        return positions[index];
    }

    public void SetInputPortLocalPositions(Vector3[] positions)
    {
        inputPortLocalPositions = NormalizeInputPortPositions(positions);
    }

    private void OnValidate()
    {
        inputPortLocalPositions = NormalizeInputPortPositions(inputPortLocalPositions);
    }

    private Vector3[] GetValidatedInputPortPositions()
    {
        return NormalizeInputPortPositions(inputPortLocalPositions);
    }

    private static Vector3[] NormalizeInputPortPositions(Vector3[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            return DefaultInputPortLocalPositions;
        }

        return positions;
    }
}
