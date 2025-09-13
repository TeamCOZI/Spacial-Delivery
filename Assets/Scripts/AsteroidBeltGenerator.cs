using UnityEngine;

public class AsteroidbeltGenerator : MonoBehaviour
{
    [Header("Belt Settings")]
    public GameObject centralBody;
    public GameObject asteroidPrefab;
    public int numberOfAsteroids = 200;
    public bool clockwise = false;

    [Header("Orbit Shape")]
    public float beltSemiMajorAxis = 10f;
    public float beltSemiMinorAxis = 10f;
    public float beltTiltDegrees = 0f;

    [Header("Orbit Speed")]
    public float orbitSpeed = 10f;

    private void Start()
    {
        if (centralBody == null || asteroidPrefab == null)
        {
            return;
        }

        for (int i = 0; i < numberOfAsteroids; i++)
        {
            GameObject asteroid = Instantiate(asteroidPrefab, transform);

            Orbiter orbiter = asteroid.GetComponent<Orbiter>();
            if (orbiter == null)
            {
                Destroy(asteroid);
                continue;
            }

            float randonMultiplier = Random.Range(1.0f, 1.3f);

            orbiter.centralBody = centralBody;
            orbiter.semiMajorAxis = beltSemiMajorAxis * randonMultiplier;
            orbiter.semiMinorAxis = beltSemiMinorAxis * randonMultiplier;
            orbiter.orbitTiltDegrees = beltTiltDegrees;
            orbiter.orbitSpeed = orbitSpeed;
            orbiter.clockwise = clockwise;
            orbiter.currentAngle = Random.Range(0f, 360f);
        }
    }
}