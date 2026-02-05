using UnityEngine;
using System.Collections.Generic;

public class Satellite : MonoBehaviour, UpdateFocusInfo, CelestialBody
{
    [Header("Satellite Settings")]
    public BiomeSettings biomeSettings;

    public int magneticField;
    public int solarWind;
    public int atm;
    public int heat;

    public float scale;

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
            Debug.LogError("Satellite is missing required components.");
            return null;
        }

        string satelliteName = name;

        string satelliteBiomeType = biomeSettings.biomeType.ToString();

        string satelliteHeat;
        if (biomeSettings.heat == 5) satelliteHeat = "초고온";
        else if (biomeSettings.heat == 4) satelliteHeat = "고온";
        else if (biomeSettings.heat == 3) satelliteHeat = "평범함";
        else if (biomeSettings.heat == 2) satelliteHeat = "저온";
        else satelliteHeat = "초저온";

        string satelliteMass;
        if (biomeSettings.gravity == 5) satelliteMass = "매우 강함";
        else if (biomeSettings.gravity == 4) satelliteMass = "강함";
        else if (biomeSettings.gravity == 3) satelliteMass = "평범함";
        else if (biomeSettings.gravity == 2) satelliteMass = "약함";
        else satelliteMass = "매우 약함";

        string satelliteATM;
        if (biomeSettings.atm == 5) satelliteATM = "매우 높음";
        else if (biomeSettings.atm == 4) satelliteATM = "높음";
        else if (biomeSettings.atm == 3) satelliteATM = "평범함";
        else if (biomeSettings.atm == 2) satelliteATM = "낮음";
        else  satelliteATM = "매우 낮음";

        string satelliteSolarWind;
        if (solarWind >= 5000) satelliteSolarWind = "매우 강함";
        else if (solarWind >= 4100) satelliteSolarWind = "강함";
        else if (solarWind >= 3500) satelliteSolarWind = "평범함";
        else if (solarWind >= 2000) satelliteSolarWind = "약함";
        else satelliteSolarWind = "매우 약함";

        string satelliteCenter = revolutionComponent.center.name;

        string satelliteChildren = "N/A";

        string satellitePeriod = revolutionComponent.revolutionPeriod.ToString();
        
        string satelliteResource = "";
        foreach (resource resource in new resource[] { biomeSettings.mineral, biomeSettings.water, biomeSettings.gas, biomeSettings.plasma })
        {
            if (resource.crystal > 0) satelliteResource += "Crystal * " + resource.crystal.ToString() + ", ";
            if (resource.liquid > 0) satelliteResource += "Liquid * " + resource.liquid.ToString() + ", ";
            if (resource.vapor > 0) satelliteResource += "Vapor * " + resource.vapor.ToString() + ", ";
            if (resource.plasma > 0) satelliteResource += "Plasma * " + resource.plasma.ToString() + ", ";
        }

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", satelliteName },
            { "분류", satelliteBiomeType },
            { "온도", satelliteHeat },
            { "중력", satelliteMass },
            { "기압", satelliteATM },
            { "태양풍", satelliteSolarWind },
            { "질량 중심 천체", satelliteCenter },
            { "포획 천체", satelliteChildren },
            { "공전 주기", satellitePeriod },
            { "매장 자원", satelliteResource }
        };

        return focusInfo;
    }

    public List<GameObject> ChildSatellites
    {
        get { return childSatellites; }
        set { childSatellites = value; }
    }
}