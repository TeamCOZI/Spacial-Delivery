using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LaunchData
{
    public Vector3 LaunchPosition;
    public Vector3 InitialVelocity;
    public int RelativeLaunchFrame;
    public List<Vector3> pathPoints;
}