using UnityEngine;

public class SolarSystemGenerator : MonoBehaviour
{
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

        GenerateSolarSystem();
    }

    public void GenerateSolarSystem()
    {
        SolarSystemFactory solarSystemFactory = new SolarSystemFactory(solarSystemSettings, transform);

        GameObject star = solarSystemFactory.GenerateStar();

        // Initial orbit = Star scale + Initial orbit
        // Orbit = Previous planet orbit + Initial orbit increase * Orbit increase multiply ^ (planetIter - 2)
        float planetOrbit = star.transform.localScale.x + solarSystemSettings.planetInitialOrbit;
        float planetOrbitIncrease = solarSystemSettings.planetInitialOrbitIncrease;

        // Instantiating inner planets.
        for (int i = 0; i < 4; i++)
        {
            BiomeSettings biomeSettings = solarSystemFactory.PlanetBiomeSettings(i);
            GameObject planet = solarSystemFactory.GeneratePlanet(star, i, planetOrbit, biomeSettings);

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            biomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
            GameObject satellite = solarSystemFactory.GenerateSatellite(planet, 0, planet.GetComponent<Planet>().scale + solarSystemSettings.satelliteInitialOrbit, biomeSettings);

            planet.GetComponent<Planet>().ChildSatellites.Add(satellite);

            star.GetComponent<Star>().ChildPlanets.Add(planet);
        }

        // Instantiating asteriod belt.
        GameObject asteroidBelt = solarSystemFactory.GenerateAsteroidBelt(star, planetOrbit);

        planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
        planetOrbit += planetOrbitIncrease;

        star.GetComponent<Star>().ChildPlanets.Add(asteroidBelt);

        // Instantiating outer planets.
        for (int i = 4; i < 6; i++)
        {
            BiomeSettings biomeSettings = solarSystemFactory.PlanetBiomeSettings(i);
            GameObject planet = solarSystemFactory.GeneratePlanet(star, i, planetOrbit, biomeSettings);

            planetOrbitIncrease *= solarSystemSettings.planetOrbitIncreaseMultiply;
            planetOrbit += planetOrbitIncrease;

            float satelliteOrbit = planet.GetComponent<Planet>().scale + solarSystemSettings.satelliteInitialOrbit;
            float satelliteOrbitIncrease = solarSystemSettings.satelliteInitialOrbitIncrease;

            for (int j = 0; j < 2; j++)
            {
                BiomeSettings satelliteBiomeSettings = solarSystemFactory.SatelliteBiomeSettings(i);
                GameObject satellite = solarSystemFactory.GenerateSatellite(planet, j, satelliteOrbit, satelliteBiomeSettings);
                
                satelliteOrbitIncrease *= solarSystemSettings.satelliteOrbitIncreaseMultiply;
                satelliteOrbit += satelliteOrbitIncrease;

                planet.GetComponent<Planet>().ChildSatellites.Add(satellite);
            }

            star.GetComponent<Star>().ChildPlanets.Add(planet);
        }

        star.GetComponent<Gravity>().GravityRadius = Mathf.RoundToInt(planetOrbit);
    }
}