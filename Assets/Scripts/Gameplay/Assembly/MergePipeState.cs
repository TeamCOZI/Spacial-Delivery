using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MergePipeState : MonoBehaviour
{
    private static readonly AssemblyPartPortLayout.PortSide[] LocalIntakeOrder =
    {
        AssemblyPartPortLayout.PortSide.Left,
        AssemblyPartPortLayout.PortSide.Top,
        AssemblyPartPortLayout.PortSide.Bottom
    };

    [SerializeField] private int nextInputIndex;

    public bool TrySelectPreviewInputSide(IReadOnlyList<AssemblyPartPortLayout.PortSide> availableWorldSides, out AssemblyPartPortLayout.PortSide selectedWorldSide)
    {
        selectedWorldSide = AssemblyPartPortLayout.PortSide.Left;
        if (availableWorldSides == null || availableWorldSides.Count == 0)
        {
            return false;
        }

        AssemblyPartPortLayout.PortSide[] worldOrder = BuildWorldIntakeOrder();
        int startIndex = PositiveModulo(nextInputIndex, worldOrder.Length);
        for (int offset = 0; offset < worldOrder.Length; offset++)
        {
            AssemblyPartPortLayout.PortSide candidate = worldOrder[(startIndex + offset) % worldOrder.Length];
            for (int i = 0; i < availableWorldSides.Count; i++)
            {
                if (availableWorldSides[i] != candidate)
                {
                    continue;
                }

                selectedWorldSide = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TrySelectPreviewInputDirection(IReadOnlyList<Vector2Int> availableWorldDirections, out Vector2Int selectedWorldDirection)
    {
        selectedWorldDirection = Vector2Int.zero;
        if (availableWorldDirections == null || availableWorldDirections.Count == 0)
        {
            return false;
        }

        List<AssemblyPartPortLayout.PortSide> availableWorldSides = new List<AssemblyPartPortLayout.PortSide>(availableWorldDirections.Count);
        for (int i = 0; i < availableWorldDirections.Count; i++)
        {
            Vector2Int direction = availableWorldDirections[i];
            if (direction == Vector2Int.zero)
            {
                continue;
            }

            AssemblyPartPortLayout.PortSide side = ConvertDirectionToSide(direction);
            if (!availableWorldSides.Contains(side))
            {
                availableWorldSides.Add(side);
            }
        }

        if (!TrySelectPreviewInputSide(availableWorldSides, out AssemblyPartPortLayout.PortSide selectedWorldSide))
        {
            return false;
        }

        selectedWorldDirection = ConvertSideToDirection(selectedWorldSide);
        return selectedWorldDirection != Vector2Int.zero;
    }

    public void CommitSelectedInputSide(AssemblyPartPortLayout.PortSide selectedWorldSide)
    {
        AssemblyPartPortLayout.PortSide[] worldOrder = BuildWorldIntakeOrder();
        for (int i = 0; i < worldOrder.Length; i++)
        {
            if (worldOrder[i] != selectedWorldSide)
            {
                continue;
            }

            nextInputIndex = PositiveModulo(i + 1, worldOrder.Length);
            return;
        }
    }

    public void CommitSelectedInputDirection(Vector2Int selectedWorldDirection)
    {
        if (selectedWorldDirection == Vector2Int.zero)
        {
            return;
        }

        CommitSelectedInputSide(ConvertDirectionToSide(selectedWorldDirection));
    }

    private AssemblyPartPortLayout.PortSide[] BuildWorldIntakeOrder()
    {
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(transform.localRotation);
        AssemblyPartPortLayout.PortSide[] worldOrder = new AssemblyPartPortLayout.PortSide[LocalIntakeOrder.Length];
        for (int i = 0; i < LocalIntakeOrder.Length; i++)
        {
            worldOrder[i] = RotateSide(LocalIntakeOrder[i], quarterTurns);
        }

        return worldOrder;
    }

    private static AssemblyPartPortLayout.PortSide RotateSide(AssemblyPartPortLayout.PortSide side, int quarterTurns)
    {
        Vector2Int baseDirection = ConvertSideToDirection(side);
        Vector2Int rotatedDirection = AssemblyMathUtility.RotateCellOffset(baseDirection, quarterTurns);
        return ConvertDirectionToSide(rotatedDirection);
    }

    private static Vector2Int ConvertSideToDirection(AssemblyPartPortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top:
                return Vector2Int.up;
            case AssemblyPartPortLayout.PortSide.Bottom:
                return Vector2Int.down;
            case AssemblyPartPortLayout.PortSide.Left:
                return Vector2Int.left;
            case AssemblyPartPortLayout.PortSide.Right:
                return Vector2Int.right;
            default:
                return Vector2Int.zero;
        }
    }

    private static AssemblyPartPortLayout.PortSide ConvertDirectionToSide(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            return AssemblyPartPortLayout.PortSide.Top;
        }

        if (direction == Vector2Int.down)
        {
            return AssemblyPartPortLayout.PortSide.Bottom;
        }

        if (direction == Vector2Int.left)
        {
            return AssemblyPartPortLayout.PortSide.Left;
        }

        return AssemblyPartPortLayout.PortSide.Right;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        if (modulo <= 0)
        {
            return 0;
        }

        int result = value % modulo;
        if (result < 0)
        {
            result += modulo;
        }

        return result;
    }
}
