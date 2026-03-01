using System;
using UnityEngine;

public class SolarSystemGenerator : MonoBehaviour
{
    public static event Action<float> starScale;

    [Header("Focus Settings")]
    public FocusManager focusManager;

    [Header("Generation Settings")]
    public SolarSystemSettings solarSystemSettings;

    void Start()
    {
        if (focusManager == null)
        {
            Debug.LogError("Solar system generator is missing required components.");
            return;
        }

        EnsureLargeWorldManagers();
        GenerateSolarSystem();
    }

    public void GenerateSolarSystem()
    {
        SolarSystemFactory solarSystemFactory = new SolarSystemFactory(solarSystemSettings, transform);

        GameObject star = solarSystemFactory.GenerateStar();
        starScale?.Invoke(star.transform.localScale.x);

        // Initial orbit = Star scale + Initial orbit
        // Orbit = Previous planet orbit + Initial orbit increase * Orbit increase multiply ^ (planetIter - 2)
        float planetOrbit = star.transform.localScale.x + WorldScale.ScaleLength(solarSystemSettings.planetInitialOrbit);
        float planetOrbitIncrease = WorldScale.ScaleLength(solarSystemSettings.planetInitialOrbitIncrease);

        // Instantiating inner planets.
        for (int i = 0; i < 4; i++)
        {
            BiomeSettings biomeSettings = solarSystemFactory.PlanetBiomeSettings(i);
            GameObject planet = solarSystemFactory.GeneratePlanet(star, i, planetOrbit, biomeSettings);

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            biomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
            GameObject satellite = solarSystemFactory.GenerateSatellite(planet, 0, planet.GetComponent<Planet>().scale + WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbit), biomeSettings);

            planet.GetComponent<Planet>().ChildSatellites.Add(satellite);
            star.GetComponent<Star>().ChildPlanets.Add(planet);
        }

        // Instantiating asteriod belt.
        GameObject asteroidBelt = solarSystemFactory.GenerateAsteroidBelt(star, planetOrbit);

        planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
        planetOrbit += planetOrbitIncrease;

        star.GetComponent<Star>().ChildPlanets.Add(asteroidBelt);

        // Instantiating outer planets.
        bool artificialSatellitePlaced = false;
        for (int i = 4; i < 6; i++)
        {
            BiomeSettings biomeSettings = solarSystemFactory.PlanetBiomeSettings(i);
            GameObject planet = solarSystemFactory.GeneratePlanet(star, i, planetOrbit, biomeSettings);

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            float satelliteOrbit = planet.GetComponent<Planet>().scale + WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbit);
            float satelliteOrbitIncrease = WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbitIncrease);

            for (int j = 0; j < 2; j++)
            {
                BiomeSettings satelliteBiomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
                GameObject satellite = solarSystemFactory.GenerateSatellite(planet, j, satelliteOrbit, satelliteBiomeSettings);

                satelliteOrbitIncrease *= solarSystemSettings.satelliteOrbitIncreaseMultiply;
                satelliteOrbit += satelliteOrbitIncrease;

                planet.GetComponent<Planet>().ChildSatellites.Add(satellite);

                // Place the artificial satellite around Planet 6's second moon (j == 1).
                if (!artificialSatellitePlaced && i == 5 && j == 1)
                {
                    GameObject artificialSatellite = Instantiate(
                        solarSystemSettings.artificialSatellitePrefab,
                        planet.transform.position,
                        Quaternion.identity,
                        transform
                    );
                    artificialSatellite.name = "Artificial Satellite";
                    EnsureWorldPosition(artificialSatellite);
                    EnsureSimulationTierTarget(artificialSatellite);

                    ArtificialSatellite artificialSatelliteComponent = artificialSatellite.GetComponent<ArtificialSatellite>();
                    Rigidbody rigidbodyComponent = artificialSatellite.GetComponent<Rigidbody>();
                    Gravity gravityComponent = artificialSatellite.GetComponent<Gravity>();
                    OrbitRevolution orbitRevolutionComponent = artificialSatellite.GetComponent<OrbitRevolution>();

                    if (artificialSatelliteComponent == null || rigidbodyComponent == null || gravityComponent == null || orbitRevolutionComponent == null)
                    {
                        Debug.LogError("Artificial Satellite Prefab is missing required components.");
                        Destroy(artificialSatellite);
                        return;
                    }

                    artificialSatellite.transform.localScale = Vector3.one * WorldScale.ScaleLength(artificialSatelliteComponent.scale * 10f);
                    rigidbodyComponent.mass = 0f;
                    gravityComponent.GravityRadius = Mathf.RoundToInt(WorldScale.ScaleLength(artificialSatelliteComponent.scale * 100f));

                    orbitRevolutionComponent.center = satellite;
                    Gravity satelliteGravity = satellite.GetComponent<Gravity>();
                    float orbitRadius = satelliteGravity != null
                        ? satelliteGravity.GravityRadius * 0.5f
                        : (planet.transform.lossyScale.x + WorldScale.ScaleLength(artificialSatelliteComponent.altitude));
                    orbitRevolutionComponent.semiMajorAxis = orbitRadius;
                    orbitRevolutionComponent.semiMinorAxis = orbitRadius;
                    orbitRevolutionComponent.currentAngle = UnityEngine.Random.Range(0, 360);
                    orbitRevolutionComponent.revolutionPeriod = 1f;

                    artificialSatelliteComponent.UpdateFocusInfo();
                    planet.GetComponent<Planet>().ChildSatellites.Add(artificialSatellite);
                    artificialSatellitePlaced = true;
                }
            }

            star.GetComponent<Star>().ChildPlanets.Add(planet);
        }

        star.GetComponent<Gravity>().GravityRadius = Mathf.RoundToInt(planetOrbit);
    }

    private static void EnsureWorldPosition(GameObject target)
    {
        if (target == null) return;

        WorldPosition wp = target.GetComponent<WorldPosition>();
        if (wp == null)
        {
            wp = target.AddComponent<WorldPosition>();
        }
        wp.SetWorldPosition(target.transform.position);
    }

    private static void EnsureSimulationTierTarget(GameObject target)
    {
        if (target == null) return;
        if (target.GetComponent<SimulationTierTarget>() == null)
        {
            target.AddComponent<SimulationTierTarget>();
        }
    }

    private static void EnsureLargeWorldManagers()
    {
        if (LargeWorldCoordinator.Instance == null)
        {
            new GameObject("LargeWorldCoordinator").AddComponent<LargeWorldCoordinator>();
        }

        if (SimulationTierManager.Instance == null)
        {
            new GameObject("SimulationTierManager").AddComponent<SimulationTierManager>();
        }
    }
}
