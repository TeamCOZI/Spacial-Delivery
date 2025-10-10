using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ThrustData
{
    public int frame;
    public Vector2 direction;
}

[System.Serializable]
public class LaunchData
{
    public Vector3 LaunchPosition;
    public Vector3 InitialVelocity;
    public int RelativeLaunchFrame;
    public List<Vector3> pathPoints;
    public List<ThrustData> thrusts = new List<ThrustData>();
}