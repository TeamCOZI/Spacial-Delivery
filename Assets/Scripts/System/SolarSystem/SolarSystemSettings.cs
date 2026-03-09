using UnityEngine;

[CreateAssetMenu(fileName = "SolarSystemSettings", menuName = "Spacial Delivery/Solar System Settings", order = 0)]
public class SolarSystemSettings : ScriptableObject
{
    [Header("Prefabs")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    public GameObject asteroidBeltPrefab;
    public GameObject satellitePrefab;
    public GameObject artificialSatellitePrefab;

    [Header("Star Settings")]
    public int starScale = 500;
    public int starMass = 500;

    [Header("Planet Orbit Settings")]
    public float planetInitialOrbit = 1500f;
    public float planetInitialOrbitIncrease = 3000f;
    public float planetOrbitIncreaseMultiply = 1.1f;
    
    [Header("Satellite Orbit Settings")]
    public float satelliteInitialOrbit = 250f;
    public float satelliteInitialOrbitIncrease = 500f;
    public float satelliteOrbitIncreaseMultiply = 1.2f;

    [Header("Asteroid Belt Settings")]
    public float asteroidBeltWidth = 350f;

    [Header("Planet Biome Settings")]
    public BiomeSettings alpine;
    public BiomeSettings antartic;
    public BiomeSettings desert;
    public BiomeSettings gasGiant;
    public BiomeSettings glacier;
    public BiomeSettings greenHouse;
    public BiomeSettings iceGiant;
    public BiomeSettings iron;
    public BiomeSettings lava;
    public BiomeSettings ocean;
    public BiomeSettings temperate;
    public BiomeSettings tropics;

    [Header("Satellite Biome Settings")]
    public BiomeSettings glacierSatellite;
    public BiomeSettings ironSatellite;
}