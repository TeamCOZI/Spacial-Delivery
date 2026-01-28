using System.Collections.Generic;
using UnityEngine;

public enum PlanetType { terrestrial, jovian }

public class Planet : MonoBehaviour, CelestialBody
{
    [Header("Planet Settings")]
    public PlanetType planetType;
    public int position;
    public int magneticField;
    public int solarWind;
    public int atm;
    public int heat;

    public int scale;

    private List<GameObject> childSatellites;

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

        string planetName = name;

        string planetCelestialBodyType = planetType == PlanetType.terrestrial ? "암석형 행성" : "가스형 행성";

        string planetHeat;
        if (heat >= 300) planetHeat = "초고온";
        else if (heat >= 30) planetHeat = "고온";
        else if (heat >= -10) planetHeat = "평범함";
        else if (heat >= -200) planetHeat = "저온";
        else planetHeat = "초저온";

        string planetMass;
        int mass = Mathf.RoundToInt(rigidbodyComponent.mass);
        if (mass >= 1000000) planetMass = "매우 강함";
        else if (mass >= 90000) planetMass = "강함";
        else if (mass >= 15000) planetMass = "평범함";
        else if (mass >= 3000) planetMass = "약함";
        else planetMass = "매우 약함";

        string planetATM;
        if (atm >= 600) planetATM = "매우 높음";
        else if (atm >= 200) planetATM = "높음";
        else if (atm >= 100) planetATM = "평범함";
        else if (atm >= 50) planetATM = "낮음";
        else  planetATM = "매우 낮음";

        string planetSolarWind;
        if (solarWind >= 8000) planetSolarWind = "매우 강함";
        else if (solarWind >= 6000) planetSolarWind = "강함";
        else if (solarWind >= 4000) planetSolarWind = "평범함";
        else if (solarWind >= 2000) planetSolarWind = "약함";
        else planetSolarWind = "매우 약함";

        string planetCenter = revolutionComponent.center.name;

        string planetChildren = "";
        foreach (GameObject childSatellite in childSatellites) planetChildren += childSatellite.name + ", ";

        string planetPeriod = Mathf.RoundToInt(360f / revolutionComponent.revolutionSpeed).ToString();
        
        string planetResource = "";
        resourceComponent.Initialize(this.atm, heat);
        foreach (KeyValuePair<ResourceType, Dictionary<ResourceState, int>> resource in resourceComponent.resource)
        {
            if (resource.Value == null) continue;

            planetResource += resource.Key.ToString() + " - ";

            foreach (KeyValuePair<ResourceState, int> value in resource.Value) planetResource += value.Key.ToString() + " * " + value.Value + ", ";
        }

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", planetName },
            { "분류", planetCelestialBodyType },
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

    public int GetHeat()
    {
        return heat;
    }

    public void SetHeat(int heat)
    {
        this.heat = heat;
    }

    public int GetATM()
    {
        return atm;
    }

    public void SetATM(int ATM)
    {
        this.atm = ATM;
    }
    
    public List<GameObject> ChildSatellites
    {
        get { return childSatellites; }
        set { childSatellites = value; }
    }
}