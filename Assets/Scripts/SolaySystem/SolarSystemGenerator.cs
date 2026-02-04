using System.Collections.Generic;
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

        int planetIter = 0;

        // Initial orbit = Star scale + Initial orbit increase + Planet gravity radius
        // Orbit = Previous planet orbit + Previous planet gravity radius + Planet Gravity Radius + Previous orbit increase * if (Planet 1) ? 1 : Orbit increase multiply
        float planetOrbit = star.transform.localScale.x + solarSystemSettings.planetInitialOrbit;
        float planetOrbitIncrease = solarSystemSettings.planetInitialOrbitIncrease;
        float previousPlanetGravityRadius = 0;

        List<GameObject> planets = new List<GameObject>();
        
        // Instantiating inner planets.
        while (true)
        {
            GameObject planet = solarSystemFactory.GeneratePlanet(star, ref planetIter, true, ref planetOrbit, ref planetOrbitIncrease, ref previousPlanetGravityRadius);

            if (planetOrbit - planetOrbitIncrease > star.GetComponent<Gravity>().GravityRadius * solarSystemSettings.asteroidBeltData.asteroidBeltThreshold)
            {
                Destroy(planet);
                planetIter--;
                planetOrbit -= planetOrbitIncrease;
                break;
            }

            int satelliteIter = 0;

            float satelliteOrbit = planet.transform.lossyScale.x + solarSystemSettings.satelliteInitialOrbit;
            float satelliteOrbitIncrease = solarSystemSettings.satelliteInitialOrbitIncrease;
            float previousSatelliteGravityRadius = 0;

            List<GameObject> satellites = new List<GameObject>();

            // Instantiating satellites.
            while (true)
            {
                GameObject satellite = solarSystemFactory.GenerateSatellite(planet, ref satelliteIter, ref satelliteOrbit, ref satelliteOrbitIncrease, ref previousSatelliteGravityRadius);

                if (satelliteOrbit - satelliteOrbitIncrease > planet.GetComponent<Gravity>().GravityRadius)
                {
                    Destroy(satellite);
                    satelliteIter--;
                    break;
                }

                satellites.Add(satellite);
            }

            //List<int> satellitePeriods = solarSystemFactory.GeneratePeriods(solarSystemSettings.satelliteMinPeriod, solarSystemSettings.satelliteMaxPeriod, satelliteIter);
            List<float> satellitePeriods = new List<float> {0.5f, 1f, 1.5f, 2f};

            for (int i = 0; i < satelliteIter; i++)
            {
                OrbitRevolution revolutionComponent = satellites[i].GetComponent<OrbitRevolution>();
                revolutionComponent.revolutionPeriod = satellitePeriods[i];
            }

            planet.GetComponent<Planet>().ChildSatellites = satellites;
            
            solarSystemFactory.ReassignPlanet(planet);

            planets.Add(planet);
        }

        // Instantiating asteriod belt.
        int asteroidBeltIndex = planetIter;
        GameObject asteroidBelt = solarSystemFactory.GenerateAsteroidBelt(star, ref planetIter, ref planetOrbit, ref planetOrbitIncrease, ref previousPlanetGravityRadius);

        planets.Add(asteroidBelt);

        // Instantiating outer planets.
        while (true)
        {
            // Jovian
            if (Random.Range(0, 10) < 8)
            {
                GameObject planet = solarSystemFactory.GeneratePlanet(star, ref planetIter, false, ref planetOrbit, ref planetOrbitIncrease, ref previousPlanetGravityRadius);

                if (planetOrbit - planetOrbitIncrease > star.GetComponent<Gravity>().GravityRadius)
                {
                    Destroy(planet);
                    planetIter--;
                    break;
                }

                int satelliteIter = 0;

                float satelliteOrbit = planet.transform.lossyScale.x + solarSystemSettings.satelliteInitialOrbit;
                float satelliteOrbitIncrease = solarSystemSettings.satelliteInitialOrbitIncrease;
                float previousSatelliteGravityRadius = 0;

                List<GameObject> satellites = new List<GameObject>();

                // Instantiating satellites.
                while (true)
                {
                    GameObject satellite = solarSystemFactory.GenerateSatellite(planet, ref satelliteIter, ref satelliteOrbit, ref satelliteOrbitIncrease, ref previousSatelliteGravityRadius);

                    if (satelliteOrbit - satelliteOrbitIncrease > planet.GetComponent<Gravity>().GravityRadius)
                    {
                        Destroy(satellite);
                        satelliteIter--;
                        break;
                    }

                    satellites.Add(satellite);
                }

                //List<int> satellitePeriods = solarSystemFactory.GeneratePeriods(solarSystemSettings.satelliteMinPeriod, solarSystemSettings.satelliteMaxPeriod, satelliteIter);
                List<float> satellitePeriods = new List<float> {0.5f, 1f, 1.5f, 2f};

                for (int i = 0; i < satelliteIter; i++)
                {
                    OrbitRevolution revolutionComponent = satellites[i].GetComponent<OrbitRevolution>();
                    revolutionComponent.revolutionPeriod = satellitePeriods[i];
                }

                planet.GetComponent<Planet>().ChildSatellites = satellites;

                solarSystemFactory.ReassignPlanet(planet);

                planets.Add(planet);
            }
            // Terrestrial
            else
            {
                GameObject planet = solarSystemFactory.GeneratePlanet(star, ref planetIter, true, ref planetOrbit, ref planetOrbitIncrease, ref previousPlanetGravityRadius);

                if (planetOrbit - planetOrbitIncrease > star.GetComponent<Gravity>().GravityRadius)
                {
                    Destroy(planet);
                    planetIter--;
                    break;
                }

                int satelliteIter = 0;

                float satelliteOrbit = planet.transform.lossyScale.x + solarSystemSettings.satelliteInitialOrbit;
                float satelliteOrbitIncrease = solarSystemSettings.satelliteInitialOrbitIncrease;
                float previousSatelliteGravityRadius = 0;

                List<GameObject> satellites = new List<GameObject>();

                // Instantiating satellites.
                while (true)
                {
                    GameObject satellite = solarSystemFactory.GenerateSatellite(planet, ref satelliteIter, ref satelliteOrbit, ref satelliteOrbitIncrease, ref previousSatelliteGravityRadius);

                    if (satelliteOrbit - satelliteOrbitIncrease > planet.GetComponent<Gravity>().GravityRadius)
                    {
                        Destroy(satellite);
                        satelliteIter--;
                        break;
                    }

                    satellites.Add(satellite);
                }

                // Assigning satellites revolution speed.
                //List<int> satellitePeriods = solarSystemFactory.GeneratePeriods(solarSystemSettings.satelliteMinPeriod, solarSystemSettings.satelliteMaxPeriod, satelliteIter);
                List<float> satellitePeriods = new List<float> {0.5f, 1f, 1.5f, 2f};

                for (int i = 0; i < satelliteIter; i++)
                {
                    OrbitRevolution revolutionComponent = satellites[i].GetComponent<OrbitRevolution>();
                    revolutionComponent.revolutionPeriod = satellitePeriods[i];
                }
                
                planet.GetComponent<Planet>().ChildSatellites = satellites;

                solarSystemFactory.ReassignPlanet(planet);

                planets.Add(planet);
            }
        }

        // Assigning planets revolution speed.
        //List<int> planetPeriods = solarSystemFactory.GeneratePeriods(solarSystemSettings.planetMinPeriod, solarSystemSettings.planetMaxPeriod, planetIter);
        List<int> planetPeriods = new List<int>{1, 2, 3, 4, 6, 8, 9, 18, 36, 72};
        Debug.Log(planetIter);

        for (int i = 0; i < planetIter; i++)
        {   
            if (i == asteroidBeltIndex) continue;
            else
            {
                OrbitRevolution revolutionComponent = planets[i].GetComponent<OrbitRevolution>();
                revolutionComponent.revolutionPeriod = planetPeriods[i];
            }
        }

        star.GetComponent<Star>().ChildPlanets = planets;
    }
}