using System.Collections.Generic;
using Unity.VisualScripting;
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
        Gravity gravityComponent = star.GetComponent<Gravity>();

        if (starComponent == null || gravityComponent == null || rigidbodyComponent == null)
        {
            Debug.LogError("Star Prefab is missing required components.");
            Object.Destroy(star);
            return null;
        }

        // Generates and assigns random factor.
        float starFactor = Mathf.Lerp(solarSystemSettings.starData.starFactorMin, solarSystemSettings.starData.starFactorMax, Utility.normalDistribution(solarSystemSettings.starData.starFactorDistribution.mean, solarSystemSettings.starData.starFactorDistribution.stdDev));

        star.transform.localScale = Vector3.one * (int)(starFactor * solarSystemSettings.starData.starScaleRatio);
        rigidbodyComponent.mass = (int)(starFactor * solarSystemSettings.starData.starMassRatio);
        gravityComponent.GravityRadius = (int)(starFactor * solarSystemSettings.starData.starGravityRadiusRatio);
        
        starComponent.heat = Mathf.RoundToInt(starFactor * 22);
        starComponent.solarWind = Mathf.RoundToInt(starComponent.heat * 1.75f);

        starComponent.starType = starType.MainSequenceStar;
        starComponent.lightColor = Color.red;
        starComponent.lightIntensity = (int)(starFactor * solarSystemSettings.starData.starLightIntensityRatio);
        starComponent.lightRadius = (int)(starFactor * solarSystemSettings.starData.starLightRadiusRatio);

        return star;
    }

    // Generation Step 2 - Generate planets of star.
    public GameObject GeneratePlanet(GameObject star, ref int planetIter, bool isTerrestrial, ref float planetOrbit, ref float planetOrbitIncrease, ref float previousPlanetGravityRadius)
    {
        GameObject planet = Object.Instantiate(solarSystemSettings.planetPrefab, star.transform);
        planet.name = "Planet " + (planetIter + 1);

        Planet planetComponent = planet.GetComponent<Planet>();
        Rigidbody rigidbodyComponent = planet.GetComponent<Rigidbody>();
        Gravity gravityComponent = planet.GetComponent<Gravity>();
        Revolution revolutionComponent = planet.GetComponent<Revolution>();

        if (planetComponent == null || rigidbodyComponent == null || gravityComponent == null || revolutionComponent == null)
        {
            Debug.LogError("Planet Prefab is missing required components.");
            Object.Destroy(planet);
            return null;
        }

        PlanetGenerationData planetData;
        if (isTerrestrial) planetData = solarSystemSettings.terrestrialPlanetData;
        else planetData = solarSystemSettings.jovianPlanetData;

        // Generates and assigns random factor.
        float planetFactor = Mathf.Lerp(planetData.planetFactorMin, planetData.planetFactorMax, Utility.normalDistribution(planetData.planetFactorDistribution.mean, planetData.planetFactorDistribution.stdDev));
        planetOrbit += previousPlanetGravityRadius;
        Debug.Log(planetFactor);

        planet.transform.localScale = Vector3.one * (int)(planetFactor * planetData.planetScaleRatio) / star.transform.lossyScale.x;
        planetComponent.scale = Mathf.RoundToInt(transform.lossyScale.x);
        rigidbodyComponent.mass = (int)(planetFactor * planetData.planetMassRatio);
        previousPlanetGravityRadius = planetFactor * planetData.planetGravityRadiusRatio;
        gravityComponent.GravityRadius = (int)previousPlanetGravityRadius;
        planetOrbit += previousPlanetGravityRadius;

        revolutionComponent.center = star;
        revolutionComponent.semiMajorAxis = (int)planetOrbit;
        revolutionComponent.semiMinorAxis = (int)planetOrbit;
        revolutionComponent.currentAngle = Random.Range(0, 360);

        // Assigns Planet type, Heat.
        Star starComponent = star.GetComponent<Star>();

        planetComponent.position = (int)(97 - (planetOrbit - star.transform.localScale.x) / star.GetComponent<Gravity>().GravityRadius * 100);
        planetComponent.planetType = isTerrestrial ? PlanetType.terrestrial : PlanetType.jovian;
        planetComponent.solarWind = (int)(starComponent.solarWind * planetComponent.position * 0.01);
        planetComponent.heat = (int)(starComponent.heat * 0.11 - 20 * (100 - planetComponent.position));

        // Reassigning for next iteration.
        if (planetIter > 1)
        {
            planetOrbitIncrease *= Mathf.Lerp(solarSystemSettings.planetOrbitIncreaseMultiplyMin, solarSystemSettings.planetOrbitIncreaseMultiplyMax, Utility.normalDistribution(solarSystemSettings.planetOrbitIncreaseMultiplyDistribution.mean, solarSystemSettings.planetOrbitIncreaseMultiplyDistribution.stdDev));
        }

        planetOrbit += planetOrbitIncrease;

        planetIter++;

        return planet;
    }
    
    // Generation Step 3 - Generate satellites of planets.
    public GameObject GenerateSatellite(GameObject planet, ref int satelliteIter, ref float satelliteOrbit, ref float satelliteOrbitIncrease, ref float previousSatelliteGravityRadius)
    {
        GameObject satellite = Object.Instantiate(solarSystemSettings.satellitePrefab, planet.transform);
        satellite.name = planet.name + " - Satellite " + (satelliteIter + 1);

        Satellite satelliteComponent = satellite.GetComponent<Satellite>();
        Rigidbody rigidbodyComponent = satellite.GetComponent<Rigidbody>();
        Gravity gravityComponent = satellite.GetComponent<Gravity>();
        Revolution revolutionComponent = satellite.GetComponent<Revolution>();

        if (satelliteComponent == null || rigidbodyComponent == null || gravityComponent == null || revolutionComponent == null)
        {
            Debug.LogError("Satellite prefab is missing required components.");
            Object.Destroy(satellite);
            return null;
        }

        // Generates and assigns random factor.
        float satelliteFactor = Mathf.Lerp(solarSystemSettings.satelliteData.satelliteFactorMin, solarSystemSettings.satelliteData.satelliteFactorMax, Utility.normalDistribution(solarSystemSettings.satelliteData.satelliteFactorDistribution.mean, solarSystemSettings.satelliteData.satelliteFactorDistribution.stdDev));
        satelliteOrbit += previousSatelliteGravityRadius;

        satellite.transform.localScale = Vector3.one * Mathf.Round(satelliteFactor * solarSystemSettings.satelliteData.satelliteScaleRatio * 10) / 10 / planet.transform.lossyScale.x;
        rigidbodyComponent.mass = (int)(satelliteFactor * solarSystemSettings.satelliteData.satelliteMassRatio);
        previousSatelliteGravityRadius = satelliteFactor * solarSystemSettings.satelliteData.satelliteGravityRadiusRatio;
        gravityComponent.GravityRadius = (int)previousSatelliteGravityRadius;
        satelliteOrbit += previousSatelliteGravityRadius;

        revolutionComponent.center = planet;
        revolutionComponent.semiMajorAxis = (int)satelliteOrbit;
        revolutionComponent.semiMinorAxis = (int)satelliteOrbit;
        revolutionComponent.currentAngle = Random.Range(0, 360);

        // Assigns Magnetic field, Solar wind, ATM, Heat.
        Planet planetComponent = planet.GetComponent<Planet>();

        satelliteComponent.magneticField = (int)(satellite.transform.lossyScale.x * 100);
        satelliteComponent.solarWind = planetComponent.solarWind - satelliteComponent.magneticField;
        satelliteComponent.atm = (int)((rigidbodyComponent.mass * 0.1 - (satelliteComponent.solarWind * 0.25 - satelliteComponent.magneticField)) * (planetComponent.heat + 75) * 0.01);
        if (satelliteComponent.atm <= 0) satelliteComponent.atm = (int)(rigidbodyComponent.mass * 0.001 + satelliteComponent.magneticField * 0.05);
        if (satelliteComponent.atm >= 50) satelliteComponent.heat = (int)(planetComponent.heat + Mathf.Pow(satelliteComponent.atm, 2) * 0.0005);
        else if (Mathf.Abs(planetComponent.heat) <= 50) satelliteComponent.heat = (int)(Mathf.Abs(planetComponent.heat + 100) * (planetComponent.heat / Mathf.Abs(planetComponent.heat)));
        else satelliteComponent.heat = planetComponent.heat;

        // Reassigning for next iteration.
        if (satelliteIter > 1)
        {
            satelliteOrbitIncrease *= Mathf.Lerp(solarSystemSettings.satelliteOrbitIncreaseMultiplyMin, solarSystemSettings.satelliteOrbitIncreaseMulitplyMax, Utility.normalDistribution(solarSystemSettings.satelliteOrbitIncreaseMulitplyDistribuiton.mean, solarSystemSettings.satelliteOrbitIncreaseMulitplyDistribuiton.stdDev));
        }
        
        satelliteOrbit += gravityComponent.GravityRadius + satelliteOrbitIncrease;

        satelliteIter++;

        return satellite;
    }

    // Generation Step 4 - Generate asteroid belt.
    public GameObject GenerateAsteroidBelt(GameObject star, ref int planetIter, ref float planetOrbit, ref float planetOrbitIncrease, ref float previousPlanetGravityRadius)
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

        planetOrbit += previousPlanetGravityRadius + asteroidBeltComponent.asteroidBeltWidth;

        asteroidBeltComponent.center = star;
        asteroidBeltComponent.semiMajorAxis = (int)planetOrbit;
        asteroidBeltComponent.semiMinorAxis = (int)planetOrbit;

        planetOrbitIncrease *= Mathf.Lerp(solarSystemSettings.planetOrbitIncreaseMultiplyMin, solarSystemSettings.planetOrbitIncreaseMultiplyMax, Utility.normalDistribution(solarSystemSettings.planetOrbitIncreaseMultiplyDistribution.mean, solarSystemSettings.planetOrbitIncreaseMultiplyDistribution.stdDev));

        planetOrbit += planetOrbitIncrease;

        planetIter++;

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

    public void ReassignPlanet(GameObject planet)
    {
        Planet planetComponent = planet.GetComponent<Planet>();
        Rigidbody rigidbodyComponent = planet.GetComponent<Rigidbody>();

        planetComponent.magneticField = (int)(planet.transform.lossyScale.x * planetComponent.ChildSatellites.Count * 1000);
        planetComponent.solarWind -= planetComponent.magneticField;
        if (planetComponent.planetType == PlanetType.terrestrial)
        {
            planetComponent.atm = (int)((rigidbodyComponent.mass * 0.1 - (planetComponent.solarWind * 0.25 - planetComponent.magneticField)) * (planetComponent.heat + 75) * 0.001);
            if (planetComponent.atm <= 0) planetComponent.atm = Mathf.RoundToInt(rigidbodyComponent.mass * 0.001f + planetComponent.magneticField * 0.05f);

            if (planetComponent.atm >= 50) planetComponent.heat = (int)(planetComponent.heat + Mathf.Pow(planetComponent.atm, 2) * 0.0005);
            else if (Mathf.Abs(planetComponent.heat) <= 50) planetComponent.heat = (int)(Mathf.Abs(planetComponent.heat) + 100) * (planetComponent.heat / Mathf.Abs(planetComponent.heat));
        }
        else
        {
            planetComponent.atm = (int)(rigidbodyComponent.mass * 0.5);
            planetComponent.heat = (int)(rigidbodyComponent.mass * 0.2);
        }
    }
}