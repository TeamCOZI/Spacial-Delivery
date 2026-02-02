using UnityEngine;

[CreateAssetMenu(fileName = "SolarSystemSettings", menuName = "Spacial Delivery/Solar System Settings", order = 0)]
public class SolarSystemSettings : ScriptableObject
{
    [Header("Prefabs")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    public GameObject asteroidBeltPrefab;
    public GameObject satellitePrefab;

    [Header("Planet Orbit Settings")]
    public float planetOrbitIncreaseMultiplyMin = 1.1f;
    public float planetOrbitIncreaseMultiplyMax = 1.2f;
    public Utility.NormalDistributionSettings planetOrbitIncreaseMultiplyDistribution = new Utility.NormalDistributionSettings(0.5f, 0.3f);
    public float planetInitialOrbit = 1500f;
    public float planetInitialOrbitIncrease = 3000f;

    [Header("Planet Period Settings")]
    public int planetMinPeriod = 600;
    public int planetMaxPeriod = 3600;
    
    [Header("Satellite Orbit Settings")]
    public float satelliteOrbitIncreaseMultiplyMin = 1.2f;
    public float satelliteOrbitIncreaseMulitplyMax = 1.7f;
    public Utility.NormalDistributionSettings satelliteOrbitIncreaseMulitplyDistribuiton = new Utility.NormalDistributionSettings(0.2f, 0.05f);
    public float satelliteInitialOrbit = 250f;
    public float satelliteInitialOrbitIncrease = 500f;

    [Header("Satellite Period Settings")]
    public int satelliteMinPeriod = 60;
    public int satelliteMaxPeriod = 120;

    [Header("Generation Data")]
    public StarGenerationData starData;
    public PlanetGenerationData terrestrialPlanetData;
    public PlanetGenerationData jovianPlanetData;
    public SatelliteGenerationData satelliteData;
    public AsteroidBeltGenerationData asteroidBeltData;
}