using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct DistributionSettings
{
    [Range(0, 1)]
    public float mean;
    [Range(0.01f, 0.5f)]
    public float stdDev;

    public DistributionSettings(float mean = 0.5f, float stdDev = 0.15f)
    {
        this.mean = mean;
        this.stdDev = stdDev;
    }
}

public class SolarSystemGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    public GameObject asteriodBeltPrefab;
    public GameObject artificialSatellitePrefab;

    [Header("Generation Settings")]
    public int minPlanets = 3;
    public int maxPlanets = 5;
    public int minSatellites = 0;
    public int maxSatellites = 3;

    [Header("Star Settings")]
    public float starFactorMin = 200f;
    public float starFactorMax = 300f;
    public DistributionSettings starFactorDistribution = new DistributionSettings(0.5f, 0.5f);
    public float starScaleRatio = 1f;
    public float starGravityRatio = 50f;
    public float starGravityRadiusRatio = 10f;

    public float starLightMin = 1.5f;
    public float starLightMax = 1.75f;
    public DistributionSettings starLightDistribution = new DistributionSettings(0.5f, 0.5f);
    public float starLightIntensityRatio = 1f;
    public float starLightRadiusRatio = 100f;

    public float starMass = 1000f;

    [Header("Terrestrial Planet Settings")]
    public float terrestrialPlanetFactorMin = 5f;
    public float terrestrialPlanetFactorMax = 10f;
    public DistributionSettings terrestrialPlanetFactorDistribution = new DistributionSettings(0.5f, 0.5f);
    public float terrestrialPlanetScaleRatio = 1f;
    public float terrestrialPlanetGravityRatio = 30f;
    public float terrestrialPlanetGravityRadiusRatio = 6f;
    public float terrestrialPlanetMass = 100f;

    [Header("Jovian Planet Settings")]
    public float jovianPlanetFactorMin = 10f;
    public float jovianPlanetFactorMax = 30f;
    public DistributionSettings jovianPlanetFactorDistribution = new DistributionSettings(0.5f, 0.5f);
    public float jovianPlanetScaleRatio = 3f;
    public float jovianPlanetGravityRatio = 50f;
    public float jovianPlanetGravityRadiusRatio = 10f;
    public float jovianPlanetMass = 300f;

    [Header("Planet Orbit Settings")]
    public float planetOrbitIncreaseMultiplyMin = 1.2f;
    public float planetOrbitIncreaseMultiplyMax = 1.7f;
    public DistributionSettings planetOrbitIncreaseMultiplyDistribution = new DistributionSettings(0.2f, 0.05f);
    public float planetInitialOrbit = 100f;
    public float planetInitialOrbitIncrease = 100f;

    [Header("Planet Heat Settings")]
    public float planetInitialHeat = 200f;
    public float planetInitialHeatAxis = 500f;
    public float planetHeatRatio = 0.04f;

    [Header("Planet ATM Settings")]

    [Header("Planet Period Settings")]
    public int planetTotalPeriod = 3600;
    public int planetMinPeriod = 60;

    [Header("Satellite Settings")]
    public float satelliteFactorMin = 1f;
    public float satelliteFactorMax = 2f;
    public DistributionSettings satelliteFactorDistribution = new DistributionSettings(0.5f, 0.5f);
    public float satelliteScaleRatio = 1f;
    public float satelliteGravityRatio = 30f;
    public float satelliteGravityRadiusRatio = 6f;
    public float satelliteMass = 10f;
    
    [Header("Satellite Orbit Settings")]
    public float satelliteOrbitIncreaseMultiplyMin = 1.2f;
    public float satelliteOrbitIncreaseMulitplyMax = 1.5f;
    public DistributionSettings satelliteOrbitIncreaseMulitplyDistribuiton = new DistributionSettings(0.2f, 0.05f);
    public float satelliteInitialOrbit = 30f;
    public float satelliteInitialOrbitIncrease = 30f;

    [Header("Satellite Period Settings")]
    public int satelliteTotalPeriod = 120;
    public int satelliteMinPeriod = 60;

    void Start()
    {
        GenerateSystem();
    }

    public void GenerateSystem()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        GameObject star = Instantiate(starPrefab, transform.position, Quaternion.identity, transform);
        star.name = "Central Star";

        int starScale = Mathf.RoundToInt(Mathf.Lerp(starFactorMin, starFactorMax, NextGaussian(starFactorDistribution.mean, starFactorDistribution.stdDev)));
        star.transform.localScale = Vector3.one * starScale * starScaleRatio;
        int starGravityRadius = 0;

        Gravity gravityComponent = star.GetComponent<Gravity>();
        if (gravityComponent != null)
        {
            gravityComponent.gravity = starScale * starGravityRatio;
            starGravityRadius = (int)(starScale * starGravityRadiusRatio);
            gravityComponent.gravityRadius = starGravityRadius;
        }

        int starLightScale = Mathf.RoundToInt(Mathf.Lerp(starLightMin, starLightMax, NextGaussian(starFactorDistribution.mean, starFactorDistribution.stdDev)));

        Star starComponent = star.GetComponent<Star>();
        if (starComponent != null)
        {
            starComponent.lightIntensity = starLightScale * starLightIntensityRatio;
            starComponent.lightRadius = starLightScale * starLightRadiusRatio;
        }

        Rigidbody starRb = star.GetComponent<Rigidbody>();
        if (starRb != null)
        {
            starRb.mass = starMass;
        }

        // This variable is used as index.
        int numberOfPlanets = 0;

        // For assigning orbit speed.
        List<GameObject> planets = new List<GameObject>();

        // Initial orbit = Star scale + Initial orbit increase
        // Axis = Previous planet Axis + Previous planet gravity radius + Planet Gravity Radius + Previous orbit increase * Orbit increase multiply
        // This variable is used to assign planet attributes.
        int initialOrbit = starScale + (int)planetInitialOrbit;
        float previousPlanetGravityRadius = 0f;

        int initialHeat = 0;
        
        // Instantiating terrestrial planets.
        while (true)
        {
            int planetRandomFactor = (int)Mathf.Lerp(terrestrialPlanetFactorMin, terrestrialPlanetFactorMax, NextGaussian(terrestrialPlanetFactorDistribution.mean, terrestrialPlanetFactorDistribution.stdDev));
            float planetGravityRadius = planetRandomFactor * terrestrialPlanetGravityRadiusRatio;
            initialOrbit += (int)(previousPlanetGravityRadius + planetGravityRadius);
            if (initialOrbit > starGravityRadius * 0.3) break;
            previousPlanetGravityRadius = planetGravityRadius;
            if (numberOfPlanets == 0) initialHeat = (int)(planetInitialHeat - (initialOrbit - planetInitialHeatAxis) * planetHeatRatio);

            // Actual value assigning part.
            GameObject planet = Instantiate(planetPrefab, star.transform);
            planet.name = $"Planet {numberOfPlanets + 1}";

            int planetScale = (int)(planetRandomFactor * terrestrialPlanetScaleRatio);
            planet.transform.localScale = Vector3.one * planetScale / (starScale * starScaleRatio);

            Gravity planetGravity = planet.GetComponent<Gravity>();
            if (planetGravity != null)
            {
                planetGravity.gravity = planetRandomFactor * terrestrialPlanetGravityRatio;
                planetGravity.gravityRadius = planetGravityRadius;
            }

            Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
            if (planetOrbiter != null)
            {
                planetOrbiter.centralBody = star;

                planetOrbiter.semiMajorAxis = initialOrbit;
                planetOrbiter.semiMinorAxis = initialOrbit;
                planetOrbiter.currentAngle = Random.Range(0f, 360f);
            }

            Rigidbody planetRb = planet.GetComponent<Rigidbody>();
            if (planetRb != null)
            {
                planetRb.mass = terrestrialPlanetMass;
            }

            GenerateSatellitesFor(planet, planetScale, (int)planetGravityRadius);

            Planet planetPlanet = planet.GetComponent<Planet>();
            if (planetPlanet != null)
            {
                planetPlanet.planetClass_ = planetClass.terrestrial;
                planetPlanet.heat = initialHeat;
                if (planetPlanet.heat > 150) planetPlanet.atm = 0f;
                else planetPlanet.atm = (int)(planetGravity.gravity * 2 * (0.2f * (int)(planetPlanet.heat / 10)));
            }

            // Add planet to planets list for assigning orbit speed later.
            planets.Add(planet);

            Debug.Log(planet.name + " / Terrestrial Planet / Scale : " + planetScale + " / Heat : " + planetPlanet.heat + ", ATM : " + planetPlanet.atm);

            // Reassign orbit and heat for next iteration.
            if (numberOfPlanets > 1)
            {
                planetInitialOrbitIncrease *= Mathf.Lerp(planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, NextGaussian(planetOrbitIncreaseMultiplyDistribution.mean, planetOrbitIncreaseMultiplyDistribution.stdDev));
            }
            
            initialOrbit += (int)planetInitialOrbitIncrease;

            initialHeat -= (int)(planetInitialOrbitIncrease * planetHeatRatio);

            numberOfPlanets++;
        }

        // Instantiating asteriod belt.
        GameObject asteriodBelt = Instantiate(asteriodBeltPrefab, star.transform);
        asteriodBelt.name = "Asteriod Belt";
        int asteriodBeltIdx = numberOfPlanets;

        AsteroidbeltGenerator asteriodBeltGenerator = asteriodBelt.GetComponent<AsteroidbeltGenerator>();
        if (asteriodBeltGenerator != null)
        {
            asteriodBeltGenerator.centralBody = star;

            asteriodBeltGenerator.beltSemiMajorAxis = initialOrbit;
            asteriodBeltGenerator.beltSemiMinorAxis = initialOrbit;
            planets.Add(asteriodBelt);

            planetInitialOrbitIncrease *= Mathf.Lerp(planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, NextGaussian(planetOrbitIncreaseMultiplyDistribution.mean, planetOrbitIncreaseMultiplyDistribution.stdDev));
            initialOrbit += (int)planetInitialOrbitIncrease;

            numberOfPlanets++;
        }

        planetInitialOrbitIncrease *= Mathf.Lerp(planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, NextGaussian(planetOrbitIncreaseMultiplyDistribution.mean, planetOrbitIncreaseMultiplyDistribution.stdDev));
        initialOrbit += (int)planetInitialOrbitIncrease;

        initialHeat -= (int)(planetInitialOrbitIncrease * planetHeatRatio);


        // Instantiating jovian planets in condition.
        while (initialOrbit < starGravityRadius)
        {
            if (Random.Range(0, 10) < 9)
            {

                int planetRandomFactor = (int)Mathf.Lerp(jovianPlanetFactorMin, jovianPlanetFactorMax, NextGaussian(jovianPlanetFactorDistribution.mean, jovianPlanetFactorDistribution.stdDev));
                float planetGravityRadius = planetRandomFactor * jovianPlanetGravityRadiusRatio;
                initialOrbit += (int)(previousPlanetGravityRadius + planetGravityRadius);
                if (initialOrbit > starGravityRadius) break;
                previousPlanetGravityRadius = planetGravityRadius;

                GameObject planet = Instantiate(planetPrefab, star.transform);
                planet.name = $"Planet {numberOfPlanets}";

                int planetScale = (int)(planetRandomFactor * terrestrialPlanetScaleRatio);
                planet.transform.localScale = Vector3.one * planetScale / (starScale * starScaleRatio);

                Gravity planetGravity = planet.GetComponent<Gravity>();
                if (planetGravity != null)
                {
                    planetGravity.gravity = planetRandomFactor * jovianPlanetGravityRatio;
                    planetGravity.gravityRadius = planetGravityRadius;
                }

                Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
                if (planetOrbiter != null)
                {
                    planetOrbiter.centralBody = star;

                    planetOrbiter.semiMajorAxis = initialOrbit;
                    planetOrbiter.semiMinorAxis = initialOrbit;
                    planetOrbiter.currentAngle = Random.Range(0f, 360f);
                }

                Rigidbody planetRb = planet.GetComponent<Rigidbody>();
                if (planetRb != null)
                {
                    planetRb.mass = jovianPlanetMass;
                }

                Planet planetPlanet = planet.GetComponent<Planet>();
                if (planetPlanet != null)
                {
                    planetPlanet.planetClass_ = planetClass.jovian;
                    planetPlanet.heat = initialHeat;
                    planetPlanet.atm = planetGravity.gravity * 3;
                }

                GenerateSatellitesFor(planet, planetScale, (int)planetGravityRadius);

                // Add planet to planets list for assigning orbit speed later.
                planets.Add(planet);

                Debug.Log(planet.name + " / Jovian Planet / Scale : " + planetScale + " / Heat : " + planetPlanet.heat + ", ATM : " + planetPlanet.atm);

                // Reassign orbit for next iteration.
                planetInitialOrbitIncrease *= Mathf.Lerp(planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, NextGaussian(planetOrbitIncreaseMultiplyDistribution.mean, planetOrbitIncreaseMultiplyDistribution.stdDev));
                initialOrbit += (int)planetInitialOrbitIncrease;

                initialHeat -= (int)(planetInitialOrbitIncrease * planetHeatRatio);

                numberOfPlanets++;
            }
            else
            {
                int planetRandomFactor = (int)Mathf.Lerp(terrestrialPlanetFactorMin, terrestrialPlanetFactorMax, NextGaussian(terrestrialPlanetFactorDistribution.mean, terrestrialPlanetFactorDistribution.stdDev));
                float planetGravityRadius = planetRandomFactor * terrestrialPlanetGravityRadiusRatio;
                initialOrbit += (int)(previousPlanetGravityRadius + planetGravityRadius);
                if (initialOrbit > starGravityRadius) break;
                previousPlanetGravityRadius = planetGravityRadius;

                GameObject planet = Instantiate(planetPrefab, star.transform);
                planet.name = $"Planet {numberOfPlanets}";
                
                int planetScale = (int)(planetRandomFactor * terrestrialPlanetScaleRatio);
                planet.transform.localScale = Vector3.one * planetScale / (starScale * starScaleRatio);

                Gravity planetGravity = planet.GetComponent<Gravity>();
                if (planetGravity != null)
                {
                    planetGravity.gravity = planetRandomFactor * terrestrialPlanetGravityRatio;
                    planetGravity.gravityRadius = planetGravityRadius;
                }

                Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
                if (planetOrbiter != null)
                {
                    planetOrbiter.centralBody = star;

                    planetOrbiter.semiMajorAxis = initialOrbit;
                    planetOrbiter.semiMinorAxis = initialOrbit;
                    planetOrbiter.currentAngle = Random.Range(0f, 360f);
                }

                Rigidbody planetRb = planet.GetComponent<Rigidbody>();
                if (planetRb != null)
                {
                    planetRb.mass = terrestrialPlanetMass;
                }

                Planet planetPlanet = planet.GetComponent<Planet>();
                if (planetPlanet != null)
                {
                    planetPlanet.planetClass_ = planetClass.terrestrial;
                    planetPlanet.heat = initialHeat;
                    planetPlanet.atm = 0f;
                }

                GenerateSatellitesFor(planet, planetScale, (int)planetGravityRadius);

                // Add planet to planets list for assigning orbit speed later.
                planets.Add(planet);

                Debug.Log(planet.name + " / Terrestrial Planet / Scale : " + planetScale + " / Heat : " + planetPlanet.heat + ", ATM : " + planetPlanet.atm);

                // Reassign orbit for next iteration.
                planetInitialOrbitIncrease *= Mathf.Lerp(planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, NextGaussian(planetOrbitIncreaseMultiplyDistribution.mean, planetOrbitIncreaseMultiplyDistribution.stdDev));
                initialOrbit += (int)planetInitialOrbitIncrease;

                initialHeat -= (int)(planetInitialOrbitIncrease * planetHeatRatio);

                numberOfPlanets++;
            }
        }
        
        // Assiging orbit speed.
        List<int> periods = GeneratePeriods(planetTotalPeriod, planetMinPeriod, numberOfPlanets + 1);

        for (int i = 0; i < numberOfPlanets; i++)
        {   
            if (i == asteriodBeltIdx)
            {
                AsteroidbeltGenerator asteroidbeltGenerator = planets[i].GetComponent<AsteroidbeltGenerator>();
                asteriodBeltGenerator.orbitSpeed = 360f / periods[i];
            }
            else
            {
                Orbiter planetOrbiter = planets[i].GetComponent<Orbiter>();
                planetOrbiter.orbitSpeed = 360f / periods[i];
            }
        }

        starComponent.planets = planets;
    }

    private void GenerateSatellitesFor(GameObject planet, int planetScale, int planetGravityRadius)
    {
        int numberOfSatellites = 0;

        List<GameObject> satellites = new List<GameObject>();

        int initialOrbit = planetScale + (int)satelliteInitialOrbit;
        float previousSatelliteGravityRadius = 0f;

        while (true)
        {
            int satelliteRandomFactor = (int)Mathf.Lerp(satelliteFactorMin, satelliteFactorMax, NextGaussian(satelliteFactorDistribution.mean, satelliteFactorDistribution.stdDev));
            float satelliteGravityRadius = satelliteRandomFactor * satelliteGravityRadiusRatio;
            initialOrbit += (int)(previousSatelliteGravityRadius + satelliteGravityRadius);
            if (initialOrbit > planetGravityRadius) break;
            previousSatelliteGravityRadius = satelliteGravityRadius;
            
            GameObject satellite = Instantiate(artificialSatellitePrefab, planet.transform);
            satellite.name = $"{planet.name} - Satellite {numberOfSatellites + 1}";
            
            Planet planetPlanet_ = planet.GetComponent<Planet>();
            float thisPlanetScaleRatio;
            if (planetPlanet_.planetClass_ == planetClass.terrestrial) thisPlanetScaleRatio = terrestrialPlanetScaleRatio;
            else thisPlanetScaleRatio = jovianPlanetScaleRatio;
            satellite.transform.localScale = Vector3.one * satelliteRandomFactor * satelliteScaleRatio / (planetScale * thisPlanetScaleRatio);

            Gravity satelliteGravityComponent = satellite.GetComponent<Gravity>();
            if (satelliteGravityComponent != null)
            {
                satelliteGravityComponent.gravity = satelliteRandomFactor * satelliteGravityRatio;
                satelliteGravityComponent.gravityRadius = satelliteGravityRadius;
            }

            Orbiter satelliteOrbiter = satellite.GetComponent<Orbiter>();
            if (satelliteOrbiter != null)
            {
                satelliteOrbiter.centralBody = planet;

                satelliteOrbiter.semiMajorAxis = initialOrbit;
                satelliteOrbiter.semiMinorAxis = initialOrbit;
                satelliteOrbiter.currentAngle = Random.Range(0f, 360f);
            }

            Rigidbody satelliteRb = satellite.GetComponent<Rigidbody>();
            if (satelliteRb != null)
            {
                satelliteRb.mass = satelliteMass;
            }

            ArtificialSatellite satelliteSatellite = satellite.GetComponent<ArtificialSatellite>();
            if (satelliteSatellite != null)
            {
                satelliteSatellite.heat = planetPlanet_.heat;
                satelliteSatellite.atm = satelliteGravityComponent.gravity * 2;
            }

            satellites.Add(satellite);

            if (numberOfSatellites > 1)
            {
                satelliteInitialOrbitIncrease *= Mathf.Lerp(satelliteOrbitIncreaseMultiplyMin, satelliteOrbitIncreaseMulitplyMax, NextGaussian(satelliteOrbitIncreaseMulitplyDistribuiton.mean, satelliteOrbitIncreaseMulitplyDistribuiton.stdDev));
            }

            initialOrbit += (int)satelliteInitialOrbitIncrease;

            numberOfSatellites++;
        }
        
        List<int> periods = GeneratePeriods(satelliteTotalPeriod, satelliteMinPeriod, numberOfSatellites + 1);

        for (int i = 0; i < numberOfSatellites; i++)
        {
            Orbiter satelliteOrbiter = satellites[i].GetComponent<Orbiter>();
            satelliteOrbiter.orbitSpeed = 360f / periods[numberOfSatellites];
        }

        Planet planetPlanet = planet.GetComponent<Planet>();
        planetPlanet.satellites = satellites;
    }
    
    private List<int> GeneratePeriods(int totalPeriod, int minPeriod, int count)
    {
        const int maxRetries = 100;

        Dictionary<int, int> primeFactors = GetPrimeFactorization(totalPeriod);
        List<KeyValuePair<int, int>> factorsList = new List<KeyValuePair<int, int>>(primeFactors);

        HashSet<int> periods = new HashSet<int>();
        if (count > 0)
        {
            periods.Add(totalPeriod);
        }

        int retries = 0;
        while (periods.Count < count && retries < maxRetries)
        {
            int newPeriod = 1;
            foreach (var factor in factorsList)
            {
                int prime = factor.Key;
                int maxExponent = factor.Value;
                int exponent = Random.Range(0, maxExponent + 1);
                newPeriod *= (int)Mathf.Pow(prime, exponent);
            }

            if (newPeriod >= minPeriod)
            {
                periods.Add(newPeriod);
            }
            retries++;
        }

        if (periods.Count < count)
        {
            int p = minPeriod;
            while (periods.Count < count && p < totalPeriod)
            {
                if (totalPeriod % p == 0)
                {
                    periods.Add(p);
                }
                p += minPeriod;
            }
        }

        List<int> sortedPeriods = new List<int>(periods);
        sortedPeriods.Sort();

        while (sortedPeriods.Count < count)
        {
            sortedPeriods.Add(totalPeriod);
        }
        return sortedPeriods.GetRange(0, count);
    }

    private float NextGaussian(float mean = 0.5f, float stdDev = 0.15f)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        float randNormal = mean + stdDev * randStdNormal;
        return Mathf.Clamp01(randNormal);
    }

    private Dictionary<int, int> GetPrimeFactorization(int n)
    {
        Dictionary<int, int> factors = new Dictionary<int, int>();
        if (n <= 1) return factors;

        while (n % 2 == 0)
        {
            if (!factors.ContainsKey(2))
                factors[2] = 0;
            factors[2]++;
            n /= 2;
        }

        for (int i = 3; i <= Mathf.Sqrt(n); i += 2)
        {
            while (n % i == 0)
            {
                if (!factors.ContainsKey(i))
                    factors[i] = 0;
                factors[i]++;
                n /= i;
            }
        }

        if (n > 2)
        {
            if (!factors.ContainsKey(n))
                factors[n] = 0;
            factors[n]++;
        }

        return factors;
    }
}