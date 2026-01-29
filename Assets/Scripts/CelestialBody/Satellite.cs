using UnityEngine;
using System.Collections.Generic;

public class Satellite : MonoBehaviour, UpdateFocusInfo
{
    public int magneticField;
    public int solarWind;
    public int atm;
    public int heat;

    public float scale;

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
        Revolution revolutionComponent = GetComponent<Revolution>();
        Resource resourceComponent = GetComponent<Resource>();

        if (rigidbodyComponent == null || revolutionComponent == null || resourceComponent == null)
        {
            Debug.LogError("Planet is missing required components.");
            return null;
        }

        string satelliteName = name;

        string satelliteCelestialBodyType = "위성";

        string satelliteHeat;
        if (heat >= 300) satelliteHeat = "초고온";
        else if (heat >= 30) satelliteHeat = "고온";
        else if (heat >= -10) satelliteHeat = "평범함";
        else if (heat >= -200) satelliteHeat = "저온";
        else satelliteHeat = "초저온";

        string satelliteMass;
        int mass = Mathf.RoundToInt(rigidbodyComponent.mass);
        if (mass >= 1000000) satelliteMass = "매우 강함";
        else if (mass >= 90000) satelliteMass = "강함";
        else if (mass >= 15000) satelliteMass = "평범함";
        else if (mass >= 3000) satelliteMass = "약함";
        else satelliteMass = "매우 약함";

        string satelliteATM;
        if (atm >= 600) satelliteATM = "매우 높음";
        else if (atm >= 200) satelliteATM = "높음";
        else if (atm >= 100) satelliteATM = "평범함";
        else if (atm >= 50) satelliteATM = "낮음";
        else  satelliteATM = "매우 낮음";

        string satelliteSolarWind;
        if (solarWind >= 8000) satelliteSolarWind = "매우 강함";
        else if (solarWind >= 6000) satelliteSolarWind = "강함";
        else if (solarWind >= 4000) satelliteSolarWind = "평범함";
        else if (solarWind >= 2000) satelliteSolarWind = "약함";
        else satelliteSolarWind = "매우 약함";

        string satelliteCenter = revolutionComponent.center.name;

        string satelliteChildren = "N/A";

        string satellitePeriod = Mathf.RoundToInt(360f / revolutionComponent.revolutionSpeed).ToString();
        
        string satelliteResource = "N/A";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", satelliteName },
            { "분류", satelliteCelestialBodyType },
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
}