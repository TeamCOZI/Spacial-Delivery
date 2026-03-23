using System;
using UnityEngine;

public class SolarSystemGenerator : MonoBehaviour
{
    private const string LargeWorldCoordinatorPrefabPath = "Prefabs/System/LargeWorldCoordinatorPrefab";
    private const string SimulationTierManagerPrefabPath = "Prefabs/System/SimulationTierManagerPrefab";

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
        if (star == null)
        {
            Debug.LogError("SolarSystemGenerator: Failed to generate star.");
            return;
        }
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
            if (planet == null)
            {
                Debug.LogError($"SolarSystemGenerator: Failed to generate inner planet index {i}.");
                continue;
            }

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            biomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
            GameObject satellite = solarSystemFactory.GenerateSatellite(planet, 0, planet.GetComponent<Planet>().scale + WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbit), biomeSettings);
            if (satellite == null)
            {
                Debug.LogError($"SolarSystemGenerator: Failed to generate inner satellite for planet index {i}.");
                continue;
            }

            planet.GetComponent<Planet>().ChildSatellites.Add(satellite);
            star.GetComponent<Star>().ChildPlanets.Add(planet);

            if (!TryGenerateArtificialSatellite(planet))
            {
                return;
            }
        }

        // Instantiating asteriod belt.
        GameObject asteroidBelt = solarSystemFactory.GenerateAsteroidBelt(star, planetOrbit);
        if (asteroidBelt == null)
        {
            Debug.LogError("SolarSystemGenerator: Failed to generate asteroid belt.");
            return;
        }

        planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
        planetOrbit += planetOrbitIncrease;

        star.GetComponent<Star>().ChildPlanets.Add(asteroidBelt);

        // Instantiating outer planets.
        for (int i = 4; i < 6; i++)
        {
            BiomeSettings biomeSettings = solarSystemFactory.PlanetBiomeSettings(i);
            GameObject planet = solarSystemFactory.GeneratePlanet(star, i, planetOrbit, biomeSettings);
            if (planet == null)
            {
                Debug.LogError($"SolarSystemGenerator: Failed to generate outer planet index {i}.");
                continue;
            }

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            float satelliteOrbit = planet.GetComponent<Planet>().scale + WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbit);
            float satelliteOrbitIncrease = WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbitIncrease);

            for (int j = 0; j < 2; j++)
            {
                BiomeSettings satelliteBiomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
                GameObject satellite = solarSystemFactory.GenerateSatellite(planet, j, satelliteOrbit, satelliteBiomeSettings);
                if (satellite == null)
                {
                    Debug.LogError($"SolarSystemGenerator: Failed to generate outer satellite index {i}-{j}.");
                    continue;
                }

                satelliteOrbitIncrease *= solarSystemSettings.satelliteOrbitIncreaseMultiply;
                satelliteOrbit += satelliteOrbitIncrease;

                planet.GetComponent<Planet>().ChildSatellites.Add(satellite);
            }

            star.GetComponent<Star>().ChildPlanets.Add(planet);

            if (!TryGenerateArtificialSatellite(planet))
            {
                return;
            }
        }

        star.GetComponent<Gravity>().GravityRadius = Mathf.RoundToInt(planetOrbit);
    }

    private bool TryGenerateArtificialSatellite(GameObject planet)
    {
        if (solarSystemSettings == null || solarSystemSettings.artificialSatellitePrefab == null)
        {
            Debug.LogError("SolarSystemGenerator: Artificial satellite prefab is not assigned.");
            return false;
        }

        if (planet == null)
        {
            Debug.LogError("SolarSystemGenerator: Cannot create an artificial satellite for a null planet.");
            return false;
        }

        Planet planetComponent = planet.GetComponent<Planet>();
        if (planetComponent == null)
        {
            Debug.LogError($"SolarSystemGenerator: {planet.name} is missing a Planet component.");
            return false;
        }

        GameObject artificialSatellite = Instantiate(
            solarSystemSettings.artificialSatellitePrefab,
            planet.transform.position,
            Quaternion.identity,
            transform
        );
        artificialSatellite.name = planet.name + " - Artificial Satellite";
        if (!TryInitializeWorldComponents(artificialSatellite, "Artificial Satellite Prefab"))
        {
            Destroy(artificialSatellite);
            return false;
        }

        ArtificialSatellite artificialSatelliteComponent = artificialSatellite.GetComponent<ArtificialSatellite>();
        Rigidbody rigidbodyComponent = artificialSatellite.GetComponent<Rigidbody>();
        Gravity gravityComponent = artificialSatellite.GetComponent<Gravity>();
        OrbitRevolution orbitRevolutionComponent = artificialSatellite.GetComponent<OrbitRevolution>();

        if (artificialSatelliteComponent == null || rigidbodyComponent == null || gravityComponent == null || orbitRevolutionComponent == null)
        {
            Debug.LogError("Artificial Satellite Prefab is missing required components.");
            Destroy(artificialSatellite);
            return false;
        }

        artificialSatellite.transform.localScale = Vector3.one;
        rigidbodyComponent.mass = 0f;
        DoubleMassIfGravityExists(artificialSatellite, rigidbodyComponent);
        gravityComponent.GravityRadius = Mathf.RoundToInt(WorldScale.ScaleLength(artificialSatelliteComponent.scale * 100f));

        float orbitRadius = planetComponent.scale + Mathf.Max(
            WorldScale.ScaleLength(artificialSatelliteComponent.altitude),
            WorldScale.ScaleLength(solarSystemSettings.satelliteInitialOrbit * 0.5f));

        orbitRevolutionComponent.center = planet;
        orbitRevolutionComponent.semiMajorAxis = orbitRadius;
        orbitRevolutionComponent.semiMinorAxis = orbitRadius;
        orbitRevolutionComponent.currentAngle = UnityEngine.Random.Range(0, 360);
        orbitRevolutionComponent.revolutionPeriod = 1f;

        artificialSatelliteComponent.UpdateFocusInfo();
        planetComponent.ChildSatellites.Add(artificialSatellite);

        return true;
    }

    private static void DoubleMassIfGravityExists(GameObject celestialBody, Rigidbody rigidbodyComponent)
    {
        if (celestialBody == null || rigidbodyComponent == null) return;
        if (celestialBody.GetComponent<Gravity>() == null) return;
        rigidbodyComponent.mass *= 2f;
    }

    private static bool TryInitializeWorldComponents(GameObject target, string prefabName)
    {
        if (target == null) return false;

        WorldPosition wp = target.GetComponent<WorldPosition>();
        SimulationTierTarget simulationTierTarget = target.GetComponent<SimulationTierTarget>();
        if (wp == null || simulationTierTarget == null)
        {
            Debug.LogError($"{prefabName} is missing required world components (WorldPosition, SimulationTierTarget).");
            return false;
        }

        wp.SetWorldPosition(target.transform.position);
        return true;
    }

    private static void EnsureLargeWorldManagers()
    {
        if (LargeWorldCoordinator.Instance == null)
        {
            InstantiateManagerPrefabOrFallback<LargeWorldCoordinator>(
                LargeWorldCoordinatorPrefabPath,
                "LargeWorldCoordinator");
        }

        if (SimulationTierManager.Instance == null)
        {
            InstantiateManagerPrefabOrFallback<SimulationTierManager>(
                SimulationTierManagerPrefabPath,
                "SimulationTierManager");
        }
    }

    private static void InstantiateManagerPrefabOrFallback<T>(string resourcePath, string fallbackName)
        where T : Component
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab != null)
        {
            GameObject managerObject = Instantiate(prefab);
            managerObject.name = prefab.name;
            if (managerObject.GetComponent<T>() != null) return;

            Debug.LogError($"SolarSystemGenerator: {prefab.name} is missing {typeof(T).Name}. Falling back to runtime object.");
            Destroy(managerObject);
        }

        new GameObject(fallbackName).AddComponent<T>();
    }

}

