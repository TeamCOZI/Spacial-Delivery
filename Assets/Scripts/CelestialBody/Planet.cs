using System.Collections.Generic;
using UnityEngine;

public class Planet : MonoBehaviour, UpdateFocusInfo, CelestialBody
{
    [Header("Planet Settings")]
    public BiomeSettings biomeSettings;

    public int magneticField;
    public int solarWind;
    public int atm;
    public int heat;

    public int scale;

    private List<GameObject> childSatellites = new List<GameObject>();

    public int MagneticField
    {
        get { return magneticField; }
        set { magneticField = value; }
    }

    public int SolarWind
    {
        get { return solarWind; }
        set { solarWind = value; }
    }

    public int ATM
    {
        get { return atm; }
        set { atm = value; }
    }

    public int Heat
    {
        get { return heat; }
        set { heat = value; }
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
        OrbitRevolution revolutionComponent = GetComponent<OrbitRevolution>();

        if (rigidbodyComponent == null || revolutionComponent == null)
        {
            Debug.LogError("Planet is missing required components.");
            return null;
        }

        string planetName = name;

        string planetBiomeType = biomeSettings.biomeType.ToString();

        string planetHeat;
        if (biomeSettings.heat == 5) planetHeat = "초고온";
        else if (biomeSettings.heat == 4) planetHeat = "고온";
        else if (biomeSettings.heat == 3) planetHeat = "중간";
        else if (biomeSettings.heat == 2) planetHeat = "저온";
        else planetHeat = "초저온";

        string planetMass;
        if (biomeSettings.mass == 5) planetMass = "매우 강함";
        else if (biomeSettings.mass == 4) planetMass = "강함";
        else if (biomeSettings.mass == 3) planetMass = "평범함";
        else if (biomeSettings.mass == 2) planetMass = "약함";
        else planetMass = "매우 약함";

        string planetATM;
        if (biomeSettings.atm == 5) planetATM = "매우 높음";
        else if (biomeSettings.atm == 4) planetATM = "높음";
        else if (biomeSettings.atm == 3) planetATM = "평범함";
        else if (biomeSettings.atm == 2) planetATM = "낮음";
        else  planetATM = "매우 낮음";

        string planetSolarWind;
        if (solarWind >= 5000) planetSolarWind = "매우 강함";
        else if (solarWind >= 4100) planetSolarWind = "강함";
        else if (solarWind >= 3500) planetSolarWind = "평범함";
        else if (solarWind >= 2000) planetSolarWind = "약함";
        else planetSolarWind = "매우 약함";

        string planetCenter = revolutionComponent.center.name;

        string planetChildren = "";
        foreach (GameObject childSatellite in childSatellites) planetChildren += childSatellite.name + ", ";

        string planetPeriod = revolutionComponent.revolutionPeriod.ToString();
        
        string planetResource = "";
        foreach (resource resource in new resource[] { biomeSettings.mineral, biomeSettings.water, biomeSettings.gas, biomeSettings.plasma })
        {
            if (resource.crystal > 0) planetResource += "Crystal * " + resource.crystal.ToString() + ", ";
            if (resource.liquid > 0) planetResource += "Liquid * " + resource.liquid.ToString() + ", ";
            if (resource.vapor > 0) planetResource += "Vapor * " + resource.vapor.ToString() + ", ";
            if (resource.plasma > 0) planetResource += "Plasma * " + resource.plasma.ToString() + ", ";
        }

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", planetName },
            { "분류", planetBiomeType },
            { "온도", planetHeat },
            { "중력", planetMass },
            { "기압", planetATM},
            { "태양풍", planetSolarWind },
            { "질량 중심 천체", planetCenter },
            { "포획 천체", planetChildren },
            { "공전 주기", planetPeriod },
            { "매장 자원", planetResource }
        };

        return focusInfo;
    }
    
    public List<GameObject> ChildSatellites
    {
        get { return childSatellites; }
        set { childSatellites = value; }
    }
}