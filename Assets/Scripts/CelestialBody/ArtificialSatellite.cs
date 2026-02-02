using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OrbitRevolution))]
public class ArtificialSatellite : MonoBehaviour, UpdateFocusInfo
{
    [Header("Artificial Satellite Settings")]
    public float scale = 0.01f;
    public float altitude = 1f;

    private OrbitRevolution orbitRevolution;

    private void Awake()
    {
        orbitRevolution = GetComponent<OrbitRevolution>();
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string artificialSatelliteName = name;

        string artificialSatelliteCenter = orbitRevolution.center.name;

        string artificialSatelliteRoute = "";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", artificialSatelliteName },
            { "공전 모체", artificialSatelliteCenter },
            { "노선", artificialSatelliteRoute }
        };

        return focusInfo;
    }
}