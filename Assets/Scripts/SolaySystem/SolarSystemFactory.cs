using System.Collections.Generic;
using UnityEngine;

public class SolarSystemFactory
{
    private readonly SolarSystemSettings solarSystemSettings;
    private readonly Transform transform;

    public SolarSystemFactory(SolarSystemSettings solarSystemSettings, Transform transform)
    {
        this.solarSystemSettings = solarSystemSettings;
        this.transform = transform;
    }

    // Generation Step 1 - Generates star.
    public GameObject GenerateStar()
    {
        GameObject star = Object.Instantiate(solarSystemSettings.starPrefab, Vector3.zero, Quaternion.identity, transform);
        star.name = "Central Star";

        Star starComponent = star.GetComponent<Star>();
        Rigidbody rigidbodyComponent = star.GetComponent<Rigidbody>();

        if (starComponent == null || rigidbodyComponent == null)
        {
            Debug.LogError("Star Prefab is missing required components.");
            Object.Destroy(star);
            return null;
        }

        // Generates and assigns random factor.
        star.transform.localScale = Vector3.one * solarSystemSettings.starScale;
        rigidbodyComponent.mass = solarSystemSettings.starMass;

        starComponent.starType = starType.MainSequenceStar;

        return star;
    }

    public BiomeSettings PlanetBiomeSettings(int iter)
    {
        BiomeSettings biomeSettings;
        switch (iter)
        {
            case 0:
                int random = Random.Range(0, 2);
                if (random == 0) biomeSettings = solarSystemSettings.iron;
                else biomeSettings = solarSystemSettings.lava;
                break;
            case 1:
                random = Random.Range(0, 2);
                if (random == 0) biomeSettings = solarSystemSettings.greenHouse;
                else biomeSettings = solarSystemSettings.desert;
                break;
            case 2:
                random = Random.Range(0, 4);
                if (random == 0) biomeSettings = solarSystemSettings.tropics;
                else if (random == 1) biomeSettings = solarSystemSettings.temperate;
                else if (random == 2) biomeSettings = solarSystemSettings.ocean;
                else biomeSettings = solarSystemSettings.alpine;
                break;
            case 3:
                random = Random.Range(0, 2);
                if (random == 0) biomeSettings = solarSystemSettings.antartic;
                else biomeSettings = solarSystemSettings.glacier;
                break;
            default:
                random = Random.Range(0, 2);
                if (random == 0) biomeSettings = solarSystemSettings.gasGiant;
                else biomeSettings = solarSystemSettings.iceGiant;
                break;
        }
        return biomeSettings;
    }

    // Generation Step 2 - Generate planets of star.
    public GameObject GeneratePlanet(GameObject star, int planetIter, float planetOrbit, BiomeSettings planetBiomeSettings)
    {
        GameObject planet = Object.Instantiate(solarSystemSettings.planetPrefab, star.transform);
        planet.name = "Planet " + (planetIter + 1);

        Planet planetComponent = planet.GetComponent<Planet>();
        Rigidbody rigidbodyComponent = planet.GetComponent<Rigidbody>();
        Gravity gravityComponent = planet.GetComponent<Gravity>();
        OrbitRevolution revolutionComponent = planet.GetComponent<OrbitRevolution>();

        if (planetComponent == null || rigidbodyComponent == null || gravityComponent == null || revolutionComponent == null)
        {
            Debug.LogError("Planet Prefab is missing required components.");
            Object.Destroy(planet);
            return null;
        }

        planetComponent.biomeSettings = planetBiomeSettings;

        planet.GetComponent<MeshRenderer>().material.color = planetBiomeSettings.color;

        planet.transform.localScale = Vector3.one * planetBiomeSettings.scale / star.transform.localScale.x;
        planetComponent.scale = Mathf.RoundToInt(planet.transform.lossyScale.x);
        rigidbodyComponent.mass = planetBiomeSettings.mass;
        gravityComponent.GravityRadius = planetBiomeSettings.gravityRadius;

        revolutionComponent.center = star;
        revolutionComponent.semiMajorAxis = Mathf.RoundToInt(planetOrbit);
        revolutionComponent.semiMinorAxis = Mathf.RoundToInt(planetOrbit);
        revolutionComponent.currentAngle = Random.Range(0, 360);
        revolutionComponent.revolutionPeriod = new int[] { 1, 2, 3, 4, 6, 8, 9, 18, 36, 72}[planetIter];

        planetComponent.UpdateFocusInfo();

        return planet;
    }
    
    public BiomeSettings SatelliteBiomeSettings(int iter)
    {
        BiomeSettings biomeSettings;

        if (iter < 3) biomeSettings = solarSystemSettings.ironSatellite;
        else biomeSettings = solarSystemSettings.glacierSatellite;

        return biomeSettings;
    }

    // Generation Step 3 - Generate satellites of planets.
    public GameObject GenerateSatellite(GameObject planet, int satelliteIter, float satelliteOrbit, BiomeSettings satelliteBiomeSettings)
    {
        GameObject satellite = Object.Instantiate(solarSystemSettings.satellitePrefab, planet.transform);
        satellite.name = planet.name + " - Satellite " + (satelliteIter + 1);

        Satellite satelliteComponent = satellite.GetComponent<Satellite>();
        Rigidbody rigidbodyComponent = satellite.GetComponent<Rigidbody>();
        Gravity gravityComponent = satellite.GetComponent<Gravity>();
        OrbitRevolution revolutionComponent = satellite.GetComponent<OrbitRevolution>();

        if (satelliteComponent == null || rigidbodyComponent == null || gravityComponent == null || revolutionComponent == null)
        {
            Debug.LogError("Satellite prefab is missing required components.");
            Object.Destroy(satellite);
            return null;
        }

        satelliteComponent.biomeSettings = satelliteBiomeSettings;

        satellite.GetComponent<MeshRenderer>().material.color = satelliteBiomeSettings.color;
        
        satellite.transform.localScale = Vector3.one * satelliteBiomeSettings.scale / planet.transform.lossyScale.x;
        satelliteComponent.scale = Mathf.RoundToInt(satellite.transform.lossyScale.x);
        rigidbodyComponent.mass = Mathf.RoundToInt(satelliteBiomeSettings.mass);
        gravityComponent.GravityRadius = Mathf.RoundToInt(satelliteBiomeSettings.gravityRadius);

        revolutionComponent.center = planet;
        revolutionComponent.semiMajorAxis = Mathf.RoundToInt(satelliteOrbit);
        revolutionComponent.semiMinorAxis = Mathf.RoundToInt(satelliteOrbit);
        revolutionComponent.currentAngle = Random.Range(0, 360);
        revolutionComponent.revolutionPeriod = new float[] {0.5f, 1f, 1.5f, 2f}[satelliteIter];

        satelliteComponent.UpdateFocusInfo();

        return satellite;
    }

    // Generation Step 4 - Generate asteroid belt.
    public GameObject GenerateAsteroidBelt(GameObject star, float planetOrbit)
    {
        GameObject asteroidBelt = Object.Instantiate(solarSystemSettings.asteroidBeltPrefab, star.transform);
        asteroidBelt.name = "Asteroid Belt";

        AsteroidBelt asteroidBeltComponent = asteroidBelt.GetComponent<AsteroidBelt>();

        if (asteroidBeltComponent == null)
        {
            Debug.LogError("Asteroid Belt is missing required component.");
            Object.Destroy(asteroidBelt);
            return null;
        }

        asteroidBeltComponent.asteroidBeltOrbit = Mathf.RoundToInt(planetOrbit);
        asteroidBeltComponent.asteroidBeltWidth = Mathf.RoundToInt(solarSystemSettings.asteroidBeltWidth);

        return asteroidBelt;
    }
    
    // Generation Step 5 - Generate and assign period.
    public List<int> GeneratePeriods(int minPeriod, int maxPeriod, int iter)
    {
        Dictionary<int, int> primeFactors = Utility.primeFactorization(maxPeriod);

        // No duplicates.
        List<int> periods = new List<int>();

        while (periods.Count < iter)
        {
            int newPeriod = 1;
            
            foreach (var primeFactor in primeFactors)
            {
                int prime = primeFactor.Key;
                int maxExponent = primeFactor.Value;
                int exponent = Random.Range(0, maxExponent + 1);
                newPeriod *= (int)Mathf.Pow(prime, exponent);
            }

            if (newPeriod >= minPeriod)
            {
                periods.Add(newPeriod);
            }
        }

        periods.Sort();

        return periods;
    }
}