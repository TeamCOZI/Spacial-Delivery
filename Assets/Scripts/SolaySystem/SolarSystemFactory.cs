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
        Gravity gravityComponent = star.GetComponent<Gravity>();

        if (starComponent == null || gravityComponent == null || rigidbodyComponent == null)
        {
            Debug.LogError("Star Prefab is missing required components.");
            Object.Destroy(star);
            return null;
        }

        // Generates and assigns random factor.
        float starFactor = Mathf.Lerp(solarSystemSettings.starData.starFactorMin, solarSystemSettings.starData.starFactorMax, Utility.normalDistribution(solarSystemSettings.starData.starFactorDistribution.mean, solarSystemSettings.starData.starFactorDistribution.stdDev));

        star.transform.localScale = Vector3.one * Mathf.RoundToInt(starFactor * solarSystemSettings.starData.starScaleRatio);
        rigidbodyComponent.mass = Mathf.RoundToInt(starFactor * solarSystemSettings.starData.starMassRatio);
        gravityComponent.GravityRadius = Mathf.RoundToInt(starFactor * solarSystemSettings.starData.starGravityRadiusRatio);
        
        starComponent.heat = Mathf.RoundToInt(starFactor * solarSystemSettings.starData.starHeatRatio);
        starComponent.solarWind = Mathf.RoundToInt(starFactor * solarSystemSettings.starData.starSolarWindRatio);

        starComponent.starType = starType.MainSequenceStar;

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
        OrbitRevolution revolutionComponent = planet.GetComponent<OrbitRevolution>();

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
        if (planetIter == 2) planetFactor = Mathf.Lerp(7, planetData.planetFactorMax, Utility.normalDistribution(planetData.planetFactorDistribution.mean, planetData.planetFactorDistribution.stdDev));
        planetOrbit += previousPlanetGravityRadius;

        planet.transform.localScale = Vector3.one * Mathf.RoundToInt(planetFactor * planetData.planetScaleRatio) / star.transform.lossyScale.x;
        planetComponent.scale = Mathf.RoundToInt(planet.transform.lossyScale.x);
        rigidbodyComponent.mass = Mathf.RoundToInt(planetFactor * planetData.planetMassRatio);
        previousPlanetGravityRadius = planetFactor * planetData.planetGravityRadiusRatio;
        gravityComponent.GravityRadius = Mathf.RoundToInt(previousPlanetGravityRadius);
        planetOrbit += previousPlanetGravityRadius;

        revolutionComponent.center = star;
        revolutionComponent.semiMajorAxis = Mathf.RoundToInt(planetOrbit);
        revolutionComponent.semiMinorAxis = Mathf.RoundToInt(planetOrbit);
        revolutionComponent.currentAngle = Random.Range(0, 360);

        // Assigns Planet type, Heat.
        Star starComponent = star.GetComponent<Star>();

        planetComponent.planetType = isTerrestrial ? PlanetType.terrestrial : PlanetType.jovian;
        planetComponent.position = Mathf.RoundToInt(97 - (planetOrbit - star.transform.lossyScale.x) / star.GetComponent<Gravity>().GravityRadius * 100);
        planetComponent.solarWind = Mathf.RoundToInt(planetComponent.position * 0.012f * starComponent.solarWind);
        planetComponent.heat = Mathf.RoundToInt(starComponent.heat * 0.11f - 20 * (100 - planetComponent.position));

        // Reassigning for next iteration.
        if (planetIter > 0)
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
        OrbitRevolution revolutionComponent = satellite.GetComponent<OrbitRevolution>();

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
        satelliteComponent.scale = Mathf.Round(satellite.transform.lossyScale.x * 10) / 10;
        rigidbodyComponent.mass = Mathf.RoundToInt(satelliteFactor * solarSystemSettings.satelliteData.satelliteMassRatio);
        previousSatelliteGravityRadius = satelliteFactor * solarSystemSettings.satelliteData.satelliteGravityRadiusRatio;
        gravityComponent.GravityRadius = Mathf.RoundToInt(previousSatelliteGravityRadius);
        satelliteOrbit += previousSatelliteGravityRadius;

        revolutionComponent.center = planet;
        revolutionComponent.semiMajorAxis = Mathf.RoundToInt(satelliteOrbit);
        revolutionComponent.semiMinorAxis = Mathf.RoundToInt(satelliteOrbit);
        revolutionComponent.currentAngle = Random.Range(0, 360);

        // Assigns Magnetic field, Solar wind, ATM, Heat.
        Planet planetComponent = planet.GetComponent<Planet>();

        // Magnetic Field
        satelliteComponent.magneticField = Mathf.RoundToInt(satellite.transform.lossyScale.x * 100 + satelliteComponent.ChildSatellites.Count * 1000);

        // ATM
        satelliteComponent.atm = Mathf.RoundToInt((rigidbodyComponent.mass * 0.1f - (planetComponent.solarWind * 0.2f - satelliteComponent.magneticField)) * planetComponent.heat * 0.001f + 50);
        if (satelliteComponent.atm <= 0) satelliteComponent.atm = Mathf.RoundToInt(rigidbodyComponent.mass * 0.001f + satelliteComponent.magneticField * 0.05f);

        // Heat
        if (satelliteComponent.atm >= 50) satelliteComponent.heat = Mathf.RoundToInt((planetComponent.heat + Mathf.Pow(satelliteComponent.atm, 3) * 0.000001f) * 1.25f - 50);
        else if (Mathf.Abs(planetComponent.heat) <= 150) satelliteComponent.heat = Mathf.RoundToInt(150 * planetComponent.heat / Mathf.Abs(planetComponent.heat) + planetComponent.heat * 0.3f);
        else satelliteComponent.heat = planetComponent.heat;

        // Solar Wind
        satelliteComponent.solarWind = Mathf.RoundToInt(planetComponent.solarWind * 0.5f - satelliteComponent.magneticField);

        // Reassigning for next iteration.
        if (satelliteIter > 0)
        {
            satelliteOrbitIncrease *= Mathf.Lerp(solarSystemSettings.satelliteOrbitIncreaseMultiplyMin, solarSystemSettings.satelliteOrbitIncreaseMulitplyMax, Utility.normalDistribution(solarSystemSettings.satelliteOrbitIncreaseMulitplyDistribuiton.mean, solarSystemSettings.satelliteOrbitIncreaseMulitplyDistribuiton.stdDev));
        }
        
        satelliteOrbit += gravityComponent.GravityRadius + satelliteOrbitIncrease;

        satelliteIter++;

        satelliteComponent.UpdateFocusInfo();

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
        asteroidBeltComponent.semiMajorAxis = Mathf.RoundToInt(planetOrbit);
        asteroidBeltComponent.semiMinorAxis = Mathf.RoundToInt(planetOrbit);

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

        // Magnetic Field
        planetComponent.magneticField = Mathf.RoundToInt(planet.transform.lossyScale.x * 100 + planetComponent.ChildSatellites.Count * 1000);
        if (planetComponent.planetType == PlanetType.terrestrial)
        {
            // Terrestrial ATM
            planetComponent.atm = Mathf.RoundToInt((rigidbodyComponent.mass * 0.1f - (planetComponent.solarWind * 0.2f - planetComponent.magneticField)) * planetComponent.heat * 0.001f + 50);
            if (planetComponent.atm <= 0) planetComponent.atm = Mathf.RoundToInt(rigidbodyComponent.mass * 0.001f + planetComponent.magneticField * 0.05f);

            // Terrestrial Heat
            if (planetComponent.atm >= 50) planetComponent.heat = Mathf.RoundToInt((planetComponent.heat + Mathf.Pow(planetComponent.atm, 3) * 0.000001f) * 1.25f - 50);
            else if (Mathf.Abs(planetComponent.heat) <= 150) planetComponent.heat = Mathf.RoundToInt(150 * planetComponent.heat / Mathf.Abs(planetComponent.heat) + planetComponent.heat * 0.3f);
        }
        else
        {
            // Jovian ATM
            planetComponent.atm = Mathf.RoundToInt(rigidbodyComponent.mass * 0.5f);

            // Jovian Heat
            planetComponent.heat = Mathf.RoundToInt(rigidbodyComponent.mass * 0.2f);
        }
        // Solar Wind
        planetComponent.solarWind = Mathf.RoundToInt(planetComponent.solarWind * 0.5f - planetComponent.magneticField);

        planetComponent.UpdateFocusInfo();
    }
}