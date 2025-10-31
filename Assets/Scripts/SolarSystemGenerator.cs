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
    public GameObject artificialSatellitePrefab;

    [Header("Generation Settings")]
    public int minPlanets = 3;
    public int maxPlanets = 5;
    public int minSatellites = 0;
    public int maxSatellites = 3;

    [Header("Distribution Controls")]
    public float starScaleMin = 200f;
    public float starScaleMax = 300f;
    public DistributionSettings starScaleDistribution = new DistributionSettings(0.5f, 0.5f);
    public float starScaleRatio = 1f;
    public float starGravityRatio = 50f;
    public float starGravityRadiusRatio = 10f;

    public float starLightMin = 1.5f;
    public float starLightMax = 1.75f;
    public DistributionSettings starLightDistribution = new DistributionSettings(0.5f, 0.5f);
    public float starLightIntensityRatio = 1f;
    public float starLightRadiusRatio = 100f;

    public float planetScaleMin = 5f;
    public float planetScaleMax = 10f;
    public DistributionSettings planetScaleDistribution = new DistributionSettings(0.5f, 0.5f);
    public float planetScaleRatio = 1f;
    public float planetGravityRatio = 30f;
    public float planetGravityRadiusRatio = 6f;

    public float planetOrbitIncreaseMultiplyMin = 1.2f;
    public float planetOrbitIncreaseMultiplyMax = 1.7f;
    public DistributionSettings planetOrbitIncreaseMultiplyDistribution = new DistributionSettings(0.2f, 0.05f);

    public float satelliteScaleMin = 1f;
    public float satelliteScaleMax = 2f;
    public DistributionSettings satelliteScaleDistribution = new DistributionSettings(0.5f, 0.5f);
    public float satelliteScaleRatio = 1f;
    public float satelliteGravityRatio = 30f;
    public float satelliteGravityRadiusRatio = 6f;
    
    public float satelliteOrbitIncreaseMultiplyMin = 1.2f;
    public float satelliteOrbitIncreaseMulitplyMax = 1.5f;
    public DistributionSettings satelliteOrbitIncreaseMulitplyDistribuiton = new DistributionSettings(0.2f, 0.05f);

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

        int starScale = Mathf.RoundToInt(Mathf.Lerp(starScaleMin, starScaleMax, NextGaussian(starScaleDistribution.mean, starScaleDistribution.stdDev)));
        star.transform.localScale = Vector3.one * starScale * starScaleRatio;
        int starGravityRadius = 0;

        Gravity gravityComponent = star.GetComponent<Gravity>();
        if (gravityComponent != null)
        {
            gravityComponent.gravity = starScale * starGravityRatio;
            starGravityRadius = (int)(starScale * starGravityRadiusRatio);
            gravityComponent.gravityRadius = starGravityRadius;
        }

        int starLightScale = Mathf.RoundToInt(Mathf.Lerp(starLightMin, starLightMax, NextGaussian(starScaleDistribution.mean, starScaleDistribution.stdDev)));

        Star starComponent = star.GetComponent<Star>();
        if (starComponent != null)
        {
            starComponent.lightIntensity = starLightScale * starLightIntensityRatio;
            starComponent.lightRadius = starLightScale * starLightRadiusRatio;
        }

        int numberOfPlanets = 10;
        List<int> planetRandomFactors = GenerateRandomFactors(numberOfPlanets, planetScaleMin, planetScaleMax, planetScaleDistribution);
        List<int> radii = GenerateOrbitRadii(ref numberOfPlanets, starScale, starGravityRadius, planetRandomFactors, planetScaleRatio, 100, planetOrbitIncreaseMultiplyMin, planetOrbitIncreaseMultiplyMax, planetOrbitIncreaseMultiplyDistribution);
        List<int> periods = GeneratePeriods(3600, 60, numberOfPlanets);

        for (int i = 0; i < numberOfPlanets - 1; i++)
        {
            GameObject planet = Instantiate(planetPrefab, star.transform);
            planet.name = $"Planet {i + 1}";

            planet.transform.localScale = Vector3.one * planetRandomFactors[i] / starScale * planetScaleRatio;
            int planetGravityRadius = 0;

            Gravity planetGravity = planet.GetComponent<Gravity>();
            if (planetGravity != null)
            {
                planetGravity.gravity = planetRandomFactors[i] * planetGravityRatio;
                planetGravityRadius = (int)(planetRandomFactors[i] * planetGravityRadiusRatio);
                planetGravity.gravityRadius = planetGravityRadius;
            }

            Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
            if (planetOrbiter != null)
            {
                planetOrbiter.centralBody = star;

                planetOrbiter.semiMajorAxis = radii[i];
                planetOrbiter.semiMinorAxis = radii[i];
                planetOrbiter.orbitSpeed = 360f / periods[i];
                planetOrbiter.currentAngle = Random.Range(0f, 360f);
            }

            GenerateSatellitesFor(planet, planetRandomFactors[i], planetGravityRadius);
        }
    }

    private void GenerateSatellitesFor(GameObject planet, int planetScale, int planetGravityRadius)
    {
        int numberOfSatellites = 10;
        List<int> satelliteRandomFactors = GenerateRandomFactors(numberOfSatellites, satelliteScaleMin, satelliteScaleMax, satelliteScaleDistribution);
        List<int> radii = GenerateOrbitRadii(ref numberOfSatellites, planetScale, planetGravityRadius, satelliteRandomFactors, satelliteScaleRatio, 30, satelliteOrbitIncreaseMultiplyMin, satelliteOrbitIncreaseMulitplyMax, satelliteOrbitIncreaseMulitplyDistribuiton);
        List<int> periods = GeneratePeriods(3600, 360, numberOfSatellites);

        for (int i = 0; i < numberOfSatellites - 1; i++)
        {
            GameObject satellite = Instantiate(artificialSatellitePrefab, planet.transform);
            satellite.name = $"{planet.name} - Satellite {i + 1}";

            satellite.transform.localScale = Vector3.one * satelliteRandomFactors[i] / planetScale * satelliteScaleRatio;

            Gravity satelliteGravityComponent = satellite.GetComponent<Gravity>();
            if (satelliteGravityComponent != null)
            {
                satelliteGravityComponent.gravity = satelliteRandomFactors[i] * satelliteGravityRatio;
                satelliteGravityComponent.gravityRadius = satelliteRandomFactors[i] * satelliteGravityRadiusRatio;
            }

            Orbiter satelliteOrbiter = satellite.GetComponent<Orbiter>();
            if (satelliteOrbiter != null)
            {
                satelliteOrbiter.centralBody = planet;

                satelliteOrbiter.semiMajorAxis = radii[i];
                satelliteOrbiter.semiMinorAxis = radii[i];
                satelliteOrbiter.orbitSpeed = 360f / periods[i];
                satelliteOrbiter.currentAngle = Random.Range(0f, 360f);
            }
        }
    }
    
    private List<int> GenerateRandomFactors(int count, float scaleMin, float scaleMax, DistributionSettings scaleDistribution)
    {
        List<int> randomFactors = new List<int>();

        for (int i = 0; i < count; i++)
        {
            int randomFactor = Mathf.RoundToInt(Mathf.Lerp(scaleMin, scaleMax, NextGaussian(scaleDistribution.mean, scaleDistribution.stdDev)));
            randomFactors.Add(randomFactor);
        }

        return randomFactors;
    }

    private List<int> GenerateOrbitRadii(ref int count, int parentScale, int parentGravityRadius, List<int> randomFactors, float scaleRatio, float initialOrbitIncrease, float orbitIncreaseMultiplyMin, float orbitIncreaseMultiplyMax, DistributionSettings orbitIncreaseMultiplyDistribution)
    {
        List<int> radii = new List<int>();

        float initialOrbit = parentScale + initialOrbitIncrease;
        radii.Add((int)initialOrbit);

        initialOrbit += randomFactors[0] * scaleRatio + initialOrbitIncrease;
        radii.Add((int)initialOrbit);

        count = 2;

        while (parentGravityRadius > initialOrbit)
        {
            float orbitIncreaseMultiply = NextGaussian(orbitIncreaseMultiplyDistribution.mean, orbitIncreaseMultiplyDistribution.stdDev);
            initialOrbitIncrease *= Mathf.Lerp(orbitIncreaseMultiplyMin, orbitIncreaseMultiplyMax, orbitIncreaseMultiply);
            initialOrbit += randomFactors[count - 1] * scaleRatio + Mathf.RoundToInt(initialOrbitIncrease);
            radii.Add((int)initialOrbit);
            count++;
        }

        return radii;
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