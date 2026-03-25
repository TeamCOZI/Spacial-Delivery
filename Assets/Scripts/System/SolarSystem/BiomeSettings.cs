using UnityEngine;
using UnityEngine.Serialization;

public enum BiomeType { Alpine, Antartic, Desert, GasGiant, Glacier, GlacierSatellite, GreenHouse, IceGiant, Iron, IronSatellite, Lava, Ocean, Temperate, Tropics }

[System.Serializable]
public struct resource
{
    public int crystal;
    [FormerlySerializedAs("aquid")]
    public int liquid;
    [FormerlySerializedAs("vapor")]
    public int gas;
    public int plasma;
}

[CreateAssetMenu(fileName = "BiomeSettings", menuName = "Spacial Delivery/Biome Settings", order = 0)]
public class BiomeSettings : ScriptableObject
{
    public BiomeType biomeType;

    public Color color;
    
    [Header("Physics Settings")]
    public int scale;
    public int mass;
    public int gravityRadius;

    [Header("Biome Level Settings")]
    public int gravity;
    public int heat;
    public int atm;

    [Header("Resource Settings")]
    public resource Terite = new resource();
    [FormerlySerializedAs("Nectar")]
    public resource Aquid = new resource();
    public resource Nitain = new resource();
    public resource plasma = new resource();
}
