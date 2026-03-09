using System.Collections.Generic;
using UnityEngine;

public enum starType { MainSequenceStar, RedGiant }

public class Star : MonoBehaviour, UpdateFocusInfo
{
    [Header("Star Settings")]
    public starType starType;

    private List<GameObject> childPlanets = new List<GameObject>();

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string starName = name;

        string starCelestialBodyType = "항성";

        string starMass;
        int mass = (int)GetComponent<Rigidbody>().mass;
        if (mass >= 1000000) starMass = "매우 강함";
        else if (mass >= 90000) starMass = "강함";
        else if (mass >= 15000) starMass = "평범함";
        else if (mass >= 3000) starMass = "약함";
        else starMass = "매우 약함";

        string starChildren = "";
        foreach (GameObject childPlanet in childPlanets) starChildren += childPlanet.name + ", ";

        string starPeriod = "N/A";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", starName },
            { "분류", starCelestialBodyType },
            { "중력", starMass },
            { "포획 천체", starChildren },
            { "진화까지 남은 주기", starPeriod }
        };

        return focusInfo;
    }
    
    public List<GameObject> ChildPlanets
    {
        get { return childPlanets; }
        set { childPlanets = value; }
    }
}