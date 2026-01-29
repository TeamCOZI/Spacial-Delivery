using System.Collections.Generic;
using UnityEngine;

public enum starType { MainSequenceStar, RedGiant }

[RequireComponent(typeof(Light))]
public class Star : MonoBehaviour, UpdateFocusInfo
{
    [Header("Star Settings")]
    public starType starType;
    public int heat;
    public int solarWind;
    public Color lightColor;
    public int lightIntensity;
    public int lightRadius;

    private List<GameObject> childPlanets = new List<GameObject>();

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string starName = name;

        string starCelestialBodyType = "항성";

        string starHeat;
        if (heat >= 300) starHeat = "초고온";
        else if (heat >= 30) starHeat = "고온";
        else if (heat >= -10) starHeat = "평범함";
        else if (heat >= -200) starHeat = "저온";
        else starHeat = "초저온";

        string starMass;
        int mass = (int)GetComponent<Rigidbody>().mass;
        if (mass >= 1000000) starMass = "매우 강함";
        else if (mass >= 90000) starMass = "강함";
        else if (mass >= 15000) starMass = "평범함";
        else if (mass >= 3000) starMass = "약함";
        else starMass = "매우 약함";

        string starSolarWind;
        if (solarWind >= 8000) starSolarWind = "매우 강함";
        else if (solarWind >= 6000) starSolarWind = "강함";
        else if (solarWind >= 4000) starSolarWind = "평범함";
        else if (solarWind >= 2000) starSolarWind = "약함";
        else starSolarWind = "매우 약함";

        string starChildren = "";
        foreach (GameObject childPlanet in childPlanets) starChildren += childPlanet.name + ", ";

        string starResource = "N/A";

        string starPeriod = "N/A";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", starName },
            { "분류", starCelestialBodyType },
            { "온도", starHeat },
            { "중력", starMass },
            { "태양풍", starSolarWind },
            { "포획 천체", starChildren },
            { "매장 자원", starResource },
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