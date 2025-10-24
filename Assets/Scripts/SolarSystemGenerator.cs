using System.Collections.Generic;
using Unity.VisualScripting;
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
    public GameObject artificialSatellitePrefab;

    [Header("Generation Settings")]
    public int minPlanets = 3;
    public int maxPlanets = 5;
    public int minSatellites = 0;
    public int maxSatellites = 3;

    [Header("Distribution Controls")]
    public DistributionSettings starDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings planetDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings satelliteDistribution = new DistributionSettings(0.5f, 0.15f);

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

        float starRandomFactor = NextGaussian(starDistribution.mean, starDistribution.stdDev);
        star.transform.localScale = Vector3.one * Mathf.RoundToInt(Mathf.Lerp(200f, 400f, starRandomFactor));

        Gravity gravityComponent = star.GetComponent<Gravity>();
        if (gravityComponent != null)
        {
            gravityComponent.gravity = Mathf.RoundToInt(Mathf.Lerp(10000f, 20000f, starRandomFactor));
            gravityComponent.gravityRadius = Mathf.RoundToInt(Mathf.Lerp(2000f, 4000f, starRandomFactor));
        }

        Star starComponent = star.GetComponent<Star>();
        if (starComponent != null)
        {
            starComponent.lightIntensity = Mathf.RoundToInt(Mathf.Lerp(1.5f, 1.75f, starRandomFactor));
            starComponent.lightRadius = Mathf.RoundToInt(Mathf.Lerp(150f, 175f, starRandomFactor));
        }

        int numberOfPlanets = 0;
        if (gravityComponent != null)
        {
            numberOfPlanets = Mathf.FloorToInt(gravityComponent.gravity / 5000f);
            numberOfPlanets = Mathf.Max(1, numberOfPlanets);
        }
        else
        {
            numberOfPlanets = minPlanets;
        }

        float planetRandomFactor = NextGaussian(planetDistribution.mean, planetDistribution.stdDev);
        List<float> radii = GenerateOrbitRadii(500f, 600f, numberOfPlanets, planetRandomFactor);
        List<int> periods = GeneratePeriods(3600, 60, numberOfPlanets);

        for (int i = 0; i < numberOfPlanets; i++)
        {
            GameObject planet = Instantiate(planetPrefab, star.transform);
            planet.name = $"Planet {i + 1}";

            Vector3 desiredPlanetScale = Vector3.one * Mathf.Lerp(3f, 15f, planetRandomFactor);
            planet.transform.localScale = desiredPlanetScale / star.transform.localScale.x;

            Gravity planetGravity = planet.GetComponent<Gravity>();
            if (planetGravity != null)
            {
                planetGravity.gravity = Mathf.Lerp(100f, 200f, planetRandomFactor);
                planetGravity.gravityRadius = Mathf.Lerp(30f, 60f, planetRandomFactor);
            }

            Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
            if (planetOrbiter != null)
            {
                planetOrbiter.centralBody = star;

                float axis = radii[i];
                float speed = 360f / periods[i];

                planetOrbiter.semiMajorAxis = axis;
                planetOrbiter.semiMinorAxis = axis;
                planetOrbiter.orbitSpeed = speed;
                planetOrbiter.currentAngle = Random.Range(0f, 360f);
            }

            GenerateSatellitesFor(planet, desiredPlanetScale);
        }
    }

    private void GenerateSatellitesFor(GameObject planet, Vector3 desiredPlanetScale)
    {
        Gravity planetGravityComponent = planet.GetComponent<Gravity>();
        int numberOfSatellites = 0;
        if (planetGravityComponent != null)
        {
            numberOfSatellites = Mathf.FloorToInt(planetGravityComponent.gravity / 50f);
        }
        else
        {
            numberOfSatellites = minSatellites;
        }

        float satelliteRandomFactor = NextGaussian(satelliteDistribution.mean, satelliteDistribution.stdDev);
        List<float> radii = GenerateOrbitRadii(desiredPlanetScale.x + 5f, desiredPlanetScale.x + 6f, numberOfSatellites, satelliteRandomFactor);
        List<int> periods = GeneratePeriods(180, 60, numberOfSatellites);
        
        for (int i = 0; i < numberOfSatellites; i++)
        {
            GameObject satellite = Instantiate(artificialSatellitePrefab, planet.transform);
            satellite.name = $"{planet.name} - Satellite {i + 1}";
            
            Vector3 desiredSatelliteScale = Vector3.one * Mathf.Lerp(1f, 2f, satelliteRandomFactor);
            satellite.transform.localScale = desiredSatelliteScale / desiredPlanetScale.x;

            Gravity satelliteGravityComponent = satellite.GetComponent<Gravity>();
            if (satelliteGravityComponent != null)
            {
                satelliteGravityComponent.gravity = Mathf.Lerp(30f, 60f, satelliteRandomFactor);
                satelliteGravityComponent.gravityRadius = Mathf.Lerp(6f, 12f, satelliteRandomFactor);
            }

            Orbiter satelliteOrbiter = satellite.GetComponent<Orbiter>();
            if (satelliteOrbiter != null)
            {
                satelliteOrbiter.centralBody = planet;

                satelliteOrbiter.semiMajorAxis = radii[i];
                satelliteOrbiter.semiMinorAxis = radii[i];
                satelliteOrbiter.orbitSpeed = periods[i];
                satelliteOrbiter.currentAngle = Random.Range(0f, 360f);
            }
        }
    }

    private List<float> GenerateOrbitRadii(float minRadius, float maxRadius, int count, float planetRandomFactor)
    {
        const int maxRetries = 100;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            List<float> radii = new List<float>
            {
                Mathf.RoundToInt(Mathf.Lerp(minRadius, maxRadius, planetRandomFactor))
            };

            float nextRadius = radii[0];
            for (int i = 1; i < count; i++)
            {
                nextRadius = Mathf.Round(Random.Range(1.2f, 2f) * nextRadius);
                radii.Add(nextRadius);
            }

            if (radii.Count == count && radii[count - 1] <= 3000)
            {
                return radii;
            }
        }

        List<float> fallbackRadii = new List<float>();
        float r = 500f;
        for (int i = 0; i < count; i++)
        {
            fallbackRadii.Add(r);
            r += 250f;
        }
        return fallbackRadii;
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