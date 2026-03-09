using System;
using UnityEngine;

[Serializable]
public struct Double3
{
    public double x;
    public double y;
    public double z;

    public Double3(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Double3 Zero => new Double3(0d, 0d, 0d);

    public static Double3 operator +(Double3 a, Double3 b)
    {
        return new Double3(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    public static Double3 operator -(Double3 a, Double3 b)
    {
        return new Double3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static Double3 operator *(Double3 a, double s)
    {
        return new Double3(a.x * s, a.y * s, a.z * s);
    }

    public static implicit operator Double3(Vector3 v)
    {
        return new Double3(v.x, v.y, v.z);
    }

    public Vector3 ToVector3()
    {
        return new Vector3((float)x, (float)y, (float)z);
    }

    public override string ToString()
    {
        return $"({x:0.###}, {y:0.###}, {z:0.###})";
    }
}
