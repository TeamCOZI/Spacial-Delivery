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
    public DistributionSettings starProportionDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings lightIntensityDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings lightRadiusDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings planetPhysicsDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings planetOrbitDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings satellitePhysicalDistribution = new DistributionSettings(0.5f, 0.15f);
    public DistributionSettings satelliteOrbitSpeedDistribution = new DistributionSettings(0.5f, 0.15f);

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

        float randomFactor = NextGaussian(starProportionDistribution.mean, starProportionDistribution.stdDev);
        float starScale = Mathf.Lerp(200f, 400f, randomFactor);
        star.transform.localScale = Vector3.one * starScale;

        Gravity gravityComponent = star.GetComponent<Gravity>();
        if (gravityComponent != null)
        {
            gravityComponent.gravity = starScale * 50f;
            gravityComponent.gravityRadius = starScale * 10f;
        }

        float lightIntensity = Mathf.Lerp(1.5f, 1.75f, NextGaussian(lightIntensityDistribution.mean, lightIntensityDistribution.stdDev));
        float lightRadius = Mathf.Lerp(150f, 175f, NextGaussian(lightRadiusDistribution.mean, lightRadiusDistribution.stdDev));

        Star starComponent = star.GetComponent<Star>();
        if (starComponent != null)
        {
            starComponent.lightIntensity = lightIntensity;
            starComponent.lightRadius = lightRadius;
        }

        int numberOfPlanets = Random.Range(minPlanets, maxPlanets + 1);
        for (int i = 0; i < numberOfPlanets; i++)
        {
            GameObject planet = Instantiate(planetPrefab, transform);
            planet.name = $"Planet {i + 1}";

            float physicalFactor = NextGaussian(planetPhysicsDistribution.mean, planetPhysicsDistribution.stdDev);
            float planetScale = Mathf.Lerp(3f, 15f, physicalFactor);

            Gravity planetGravity = planet.GetComponent<Gravity>();
            if (planetGravity != null)
            {
                planetGravity.gravity = Mathf.Lerp(100f, 200f, physicalFactor);
                planetGravity.gravityRadius = Mathf.Lerp(20f, 100f, physicalFactor);
            }

            Orbiter planetOrbiter = planet.GetComponent<Orbiter>();
            if (planetOrbiter != null)
            {
                planetOrbiter.centralBody = star;

                float orbiterFactor = NextGaussian(planetOrbitDistribution.mean, planetOrbitDistribution.stdDev);
                float axis = Mathf.Lerp(500f, 3000f, orbiterFactor);
                float speed = Mathf.Lerp(1f, 60f, 1 - orbiterFactor);

                planetOrbiter.semiMajorAxis = axis;
                planetOrbiter.semiMinorAxis = axis;
                planetOrbiter.orbitSpeed = speed;
                planetOrbiter.currentAngle = Random.Range(0f, 360f);
            }

            GenerateSatellitesFor(planet);
        }
    }

    private void GenerateSatellitesFor(GameObject planet)
    {
        int numberOfSatellites = Random.Range(minSatellites, maxSatellites + 1);
        for (int i = 0; i < numberOfSatellites; i++)
        {
            GameObject satellite = Instantiate(artificialSatellitePrefab, planet.transform);
            satellite.name = $"{planet.name} - Satellite {i + 1}";

            float PhysicalFactor = NextGaussian(satellitePhysicalDistribution.mean, satellitePhysicalDistribution.stdDev);
            float satelliteScale = Mathf.Lerp(1f, 2f, PhysicalFactor);
            satellite.transform.localScale = Vector3.one * satelliteScale;

            Orbiter satelliteOrbiter = satellite.GetComponent<Orbiter>();
            if (satelliteOrbiter != null)
            {
                satelliteOrbiter.centralBody = planet;

                float planetScale = planet.transform.localScale.x;
                float axis = planetScale * 2f;
                float speed = Mathf.Lerp(1f, 10f, NextGaussian(satelliteOrbitSpeedDistribution.mean, satelliteOrbitSpeedDistribution.stdDev));

                satelliteOrbiter.semiMajorAxis = axis;
                satelliteOrbiter.semiMinorAxis = axis;
                satelliteOrbiter.orbitSpeed = speed;
                satelliteOrbiter.currentAngle = Random.Range(0f, 360f);
            }
        }
    }

    private float NextGaussian(float mean = 0.5f, float stdDev = 0.15f)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        float randNormal = mean + stdDev * randStdNormal;
        return Mathf.Clamp01(randNormal);
    }
}