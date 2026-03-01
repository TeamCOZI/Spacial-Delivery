using UnityEngine;

public static class WorldScale
{
    public const float LengthScale = 1f;

    public static float ScaleLength(float value)
    {
        return value * LengthScale;
    }

    public static int ScaleLength(int value)
    {
        return Mathf.RoundToInt(value * LengthScale);
    }

    public static Vector3 ScaleVector(Vector3 value)
    {
        return value * LengthScale;
    }

    public static float UnscaleLength(float value)
    {
        return value / Mathf.Max(0.0001f, LengthScale);
    }
}
