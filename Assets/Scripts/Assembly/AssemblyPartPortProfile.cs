using UnityEngine;

public class AssemblyPartPortProfile : MonoBehaviour
{
    [SerializeField] private Vector3[] inputPortLocalPositions = { Vector3.zero };

    public int InputPortCount
    {
        get
        {
            if (inputPortLocalPositions == null || inputPortLocalPositions.Length == 0) return 1;
            return inputPortLocalPositions.Length;
        }
    }

    public Vector3 GetInputPortLocalPosition(int index)
    {
        if (inputPortLocalPositions == null || inputPortLocalPositions.Length == 0) return Vector3.zero;
        if (index < 0 || index >= inputPortLocalPositions.Length) return Vector3.zero;
        return inputPortLocalPositions[index];
    }

    public void SetInputPortLocalPositions(Vector3[] positions)
    {
        if (positions == null || positions.Length == 0)
        {
            inputPortLocalPositions = new[] { Vector3.zero };
            return;
        }

        inputPortLocalPositions = positions;
    }

    private void OnValidate()
    {
        if (inputPortLocalPositions == null || inputPortLocalPositions.Length == 0)
        {
            inputPortLocalPositions = new[] { Vector3.zero };
        }
    }
}
