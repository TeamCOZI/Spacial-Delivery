using UnityEngine;

[System.Serializable]
public struct StarGenerationData
{
    [Header("Star Factor Settings")]
    public float starFactorMin;
    public float starFactorMax;
    public Utility.NormalDistributionSettings starFactorDistribution;

    [Header("Star Settings")]
    public float starScaleRatio;
    public float starMassRatio;
    public float starGravityRadiusRatio;

    public float starHeatRatio;
    public float starSolarWindRatio;

    [Header("Star Light Settings")]
    public Color starLightColor;
    public float starLightIntensityRatio;
    public float starLightRadiusRatio;
}

[System.Serializable]
public struct PlanetGenerationData
{
    [Header("Planet Factor Settings")]
    public float planetFactorMin;
    public float planetFactorMax;
    public Utility.NormalDistributionSettings planetFactorDistribution;

    [Header("Planet Settings")]
    public float planetScaleRatio;
    public float planetMassRatio;
    public float planetGravityRadiusRatio;
}

[System.Serializable]
public struct AsteroidBeltGenerationData
{
    [Header("Asteriod Belt Settings")]
    public float asteroidBeltThreshold;
}

[System.Serializable]
public struct SatelliteGenerationData
{
    [Header("Satellite Factor Settings")]
    public float satelliteFactorMin;
    public float satelliteFactorMax;
    public Utility.NormalDistributionSettings satelliteFactorDistribution;

    [Header("Satellite Settings")]
    public float satelliteScaleRatio;
    public float satelliteMassRatio;
    public float satelliteGravityRadiusRatio;
}