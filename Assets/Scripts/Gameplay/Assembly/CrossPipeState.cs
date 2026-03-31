using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CrossPipeState : MonoBehaviour
{
    [SerializeField] private Vector2Int primaryInputDirection = Vector2Int.left;
    [SerializeField] private Vector2Int primaryOutputDirection = Vector2Int.right;
    [SerializeField] private Vector2Int secondaryInputDirection = Vector2Int.down;
    [SerializeField] private Vector2Int secondaryOutputDirection = Vector2Int.up;
    [SerializeField] private int nextInputIndex;

    private readonly List<Vector2Int> reusableDirectionsBuffer = new List<Vector2Int>(2);

    public void Configure(
        Vector2Int firstInputDirection,
        Vector2Int firstOutputDirection,
        Vector2Int secondInputDirection,
        Vector2Int secondOutputDirection)
    {
        primaryInputDirection = SanitizeDirection(firstInputDirection);
        primaryOutputDirection = SanitizeDirection(firstOutputDirection);
        secondaryInputDirection = SanitizeDirection(secondInputDirection);
        secondaryOutputDirection = SanitizeDirection(secondOutputDirection);

        int configuredLaneCount = GetConfiguredInputDirections(reusableDirectionsBuffer);
        reusableDirectionsBuffer.Clear();
        nextInputIndex = PositiveModulo(nextInputIndex, configuredLaneCount);
    }

    public bool TryResolvePairedOutputDirection(Vector2Int inputDirection, out Vector2Int outputDirection)
    {
        outputDirection = Vector2Int.zero;
        Vector2Int sanitizedInputDirection = SanitizeDirection(inputDirection);
        if (sanitizedInputDirection == Vector2Int.zero)
        {
            return false;
        }

        if (sanitizedInputDirection == primaryInputDirection && primaryOutputDirection != Vector2Int.zero)
        {
            outputDirection = primaryOutputDirection;
            return true;
        }

        if (sanitizedInputDirection == secondaryInputDirection && secondaryOutputDirection != Vector2Int.zero)
        {
            outputDirection = secondaryOutputDirection;
            return true;
        }

        return false;
    }

    public bool HasInputDirection(Vector2Int inputDirection)
    {
        Vector2Int sanitizedInputDirection = SanitizeDirection(inputDirection);
        return sanitizedInputDirection != Vector2Int.zero &&
               (sanitizedInputDirection == primaryInputDirection || sanitizedInputDirection == secondaryInputDirection);
    }

    public bool HasOutputDirection(Vector2Int outputDirection)
    {
        Vector2Int sanitizedOutputDirection = SanitizeDirection(outputDirection);
        return sanitizedOutputDirection != Vector2Int.zero &&
               (sanitizedOutputDirection == primaryOutputDirection || sanitizedOutputDirection == secondaryOutputDirection);
    }

    public bool TrySelectPreviewInputDirection(IReadOnlyList<Vector2Int> availableWorldDirections, out Vector2Int selectedWorldDirection)
    {
        selectedWorldDirection = Vector2Int.zero;
        if (availableWorldDirections == null || availableWorldDirections.Count == 0)
        {
            return false;
        }

        int configuredLaneCount = GetConfiguredInputDirections(reusableDirectionsBuffer);
        if (configuredLaneCount <= 0)
        {
            reusableDirectionsBuffer.Clear();
            return false;
        }

        int startIndex = PositiveModulo(nextInputIndex, configuredLaneCount);
        for (int offset = 0; offset < configuredLaneCount; offset++)
        {
            Vector2Int candidate = reusableDirectionsBuffer[(startIndex + offset) % configuredLaneCount];
            for (int i = 0; i < availableWorldDirections.Count; i++)
            {
                if (SanitizeDirection(availableWorldDirections[i]) != candidate)
                {
                    continue;
                }

                selectedWorldDirection = candidate;
                reusableDirectionsBuffer.Clear();
                return true;
            }
        }

        reusableDirectionsBuffer.Clear();
        return false;
    }

    public void CommitSelectedInputDirection(Vector2Int selectedWorldDirection)
    {
        Vector2Int sanitizedDirection = SanitizeDirection(selectedWorldDirection);
        if (sanitizedDirection == Vector2Int.zero)
        {
            return;
        }

        int configuredLaneCount = GetConfiguredInputDirections(reusableDirectionsBuffer);
        for (int i = 0; i < configuredLaneCount; i++)
        {
            if (reusableDirectionsBuffer[i] != sanitizedDirection)
            {
                continue;
            }

            nextInputIndex = PositiveModulo(i + 1, configuredLaneCount);
            reusableDirectionsBuffer.Clear();
            return;
        }

        reusableDirectionsBuffer.Clear();
    }

    private int GetConfiguredInputDirections(List<Vector2Int> directions)
    {
        if (directions == null)
        {
            return 0;
        }

        directions.Clear();
        AddUniqueDirection(directions, primaryInputDirection);
        AddUniqueDirection(directions, secondaryInputDirection);
        return directions.Count;
    }

    private static void AddUniqueDirection(List<Vector2Int> directions, Vector2Int direction)
    {
        Vector2Int sanitizedDirection = SanitizeDirection(direction);
        if (directions == null || sanitizedDirection == Vector2Int.zero || directions.Contains(sanitizedDirection))
        {
            return;
        }

        directions.Add(sanitizedDirection);
    }

    private static Vector2Int SanitizeDirection(Vector2Int direction)
    {
        return Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1 ? direction : Vector2Int.zero;
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
