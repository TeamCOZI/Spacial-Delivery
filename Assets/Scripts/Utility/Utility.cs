using System.Collections.Generic;
using UnityEngine;

public interface UpdateFocusInfo
{
    Dictionary<string, string> UpdateFocusInfo();
}

public static class Utility
{
    [System.Serializable]
    public struct NormalDistributionSettings
    {
        [Range(0, 1)]
        public float mean;
        [Range(0.01f, 0.5f)]
        public float stdDev;

        public NormalDistributionSettings(float mean, float stdDev)
        {
            this.mean = mean;
            this.stdDev = stdDev;
        }
    }

    public static float normalDistribution(float mean, float stdDev)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        float randNormal = mean + stdDev * randStdNormal;

        return Mathf.Clamp01(randNormal);
    }

    public static Dictionary<int, int> primeFactorization(int maxPeriod)
    {
        Dictionary<int, int> primeFactors = new Dictionary<int, int>();

        if (maxPeriod <= 1) return primeFactors;

        while (maxPeriod % 2 == 0)
        {
            if (!primeFactors.ContainsKey(2)) primeFactors[2] = 0;
            primeFactors[2]++;
            maxPeriod /= 2;
        }

        for (int i = 3; i <= Mathf.Sqrt(maxPeriod); i += 2)
        {
            while (maxPeriod % i == 0)
            {
                if (!primeFactors.ContainsKey(i)) primeFactors[i] = 0;
                primeFactors[i]++;
                maxPeriod /= i;
            }
        }

        if (maxPeriod > 2)
        {
            if (!primeFactors.ContainsKey(maxPeriod)) primeFactors[maxPeriod] = 0;
            primeFactors[maxPeriod]++;
        }

        return primeFactors;
    }

    public static Vector3 screenMousePos(Vector3 mousePos, float planeZ = 0f)
    {
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

        if (plane.Raycast(ray, out float screenMousePos)) return ray.GetPoint(screenMousePos);
        return Vector3.zero;
    }
}
