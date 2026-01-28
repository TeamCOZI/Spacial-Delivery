using System.Collections.Generic;
using UnityEngine;

using A = System.Collections.Generic.Dictionary<ResourceState, int>;

public enum ResourceType { Mineral, Water, Gas, Plasma }
public enum ResourceState { Crystal, Liquid, Vapor, Plasma }

public class Resource : MonoBehaviour
{
    public static A[,] mineral = new A[,]
    {
        // ATM 5
        {
            new A() {{ResourceState.Crystal, 2}, {ResourceState.Liquid, 5}},
            new A() {{ResourceState.Crystal, 2}, {ResourceState.Vapor, 3}},
            null,
            null,
            null
        },
        // ATM 4
        {
            new A() {{ResourceState.Crystal, 2}, {ResourceState.Liquid, 5}},
            new A() {{ResourceState.Crystal, 2}, {ResourceState.Vapor, 3}},
            new A() {{ResourceState.Crystal, 2}},
            null,
            null
        },
        // ATM 3
        {
            new A() {{ResourceState.Liquid, 5}, {ResourceState.Crystal, 2}},
            new A() {{ResourceState.Crystal, 3}},
            new A() {{ResourceState.Crystal, 2}},
            new A() {{ResourceState.Crystal, 2}},
            null
        },
        // ATM 2
        {
            new A() {{ResourceState.Crystal, 5}},
            new A() {{ResourceState.Crystal, 3}},
            new A() {{ResourceState.Crystal, 2}},
            new A() {{ResourceState.Crystal, 2}},
            null
        },
        // ATM 1
        {
            new A() {{ResourceState.Crystal, 5}},
            new A() {{ResourceState.Crystal, 5}},
            null,
            null,
            null
        }
    };
    public static A[,] water = new A[,]
    {
        // ATM 5
        {
            null,
            null,
            null,
            new A() {{ResourceState.Liquid, 3}, {ResourceState.Vapor, 1}},
            null
        },
        // ATM 4
        {
            null,
            null,
            new A() {{ResourceState.Liquid, 1}, {ResourceState.Vapor, 1}},
            new A() {{ResourceState.Liquid, 3}},
            null
        },
        // ATM 3
        {
            null,
            null,
            new A() {{ResourceState.Liquid, 2}},
            new A() {{ResourceState.Crystal, 1}},
            null
        },
        // ATM 2
        {
            null,
            null,
            new A() {{ResourceState.Crystal, 1}, {ResourceState.Liquid, 1}},
            new A() {{ResourceState.Crystal, 1}},
            new A() {{ResourceState.Crystal, 2}}
        },
        // ATM 1
        {
            null,
            null,
            null,
            new A() {{ResourceState.Crystal, 1}},
            new A() {{ResourceState.Crystal, 1}}
        }
    };
    public static A[,] gas = new A[,]
    {
        // ATM 5
        {
            new A() {{ResourceState.Vapor, 3}},
            new A() {{ResourceState.Vapor, 3}},
            null,
            new A() {{ResourceState.Liquid, 4}, {ResourceState.Vapor, 1}},
            null
        },
        // ATM 4
        {
            new A() {{ResourceState.Vapor, 2}},
            new A() {{ResourceState.Vapor, 2}},
            new A() {{ResourceState.Vapor, 1}},
            new A() {{ResourceState.Liquid, 4}},
            null
        },
        // ATM 3
        {
            new A() {{ResourceState.Vapor, 1}},
            new A() {{ResourceState.Vapor, 2}},
            new A() {{ResourceState.Vapor, 1}},
            new A() {{ResourceState.Crystal, 1}},
            null
        },
        // ATM 2
        {
            null,
            new A() {{ResourceState.Vapor, 2}},
            new A() {{ResourceState.Vapor, 1}},
            new A() {{ResourceState.Crystal, 1}},
            new A() {{ResourceState.Crystal, 2}}
        },
        // ATM 1
        {
            null,
            null,
            null,
            new A() {{ResourceState.Crystal, 2}},
            new A() {{ResourceState.Crystal, 2}}
        }
    };

    public int aTMType;
    public int heatType;

    public Dictionary<ResourceType, A> resource;

    public void Initialize(int ATM, int Heat)
    {
        SetATMHeatType(ATM, Heat);

        CelestialBody celestialBodyComponent = transform.GetComponent<CelestialBody>();
        if (celestialBodyComponent == null)
        {
            Debug.LogError("Resource is missing required component.");
            return;
        }

        if (heatType == 2)
        {
            if (aTMType == 4)
            {
                celestialBodyComponent.SetHeat(celestialBodyComponent.GetHeat() + 200);
                heatType = 3;
            }
            else if (aTMType == 0)
            {
                celestialBodyComponent.SetHeat(celestialBodyComponent.GetHeat() + 150);
                heatType = 3;
            }
        }
        else if (heatType == 0)
        {
            if (aTMType == 4 || aTMType == 3 || aTMType == 2)
            {
                celestialBodyComponent.SetATM(Mathf.RoundToInt(celestialBodyComponent.GetATM() * 0.01f));
                aTMType = 0;
            }
        }

        SetATMHeatType(ATM, Heat);

        resource = new Dictionary<ResourceType, A>
        {
            { ResourceType.Mineral, mineral[aTMType, heatType] },
            { ResourceType.Water, water[aTMType, heatType] },
            { ResourceType.Gas, gas[aTMType, heatType] }
        };
    }

    private void SetATMHeatType(int ATM, int Heat)
    {
        if (ATM >= 600) aTMType = 4;
        else if (ATM >= 200) aTMType = 3;
        else if (ATM >= 100) aTMType = 2;
        else if (ATM >= 50) aTMType = 1;
        else  aTMType = 0;

        if (Heat >= 300) heatType = 4;
        else if (Heat >= 30) heatType = 3;
        else if (Heat >= -10) heatType = 2;
        else if (Heat >= -200) heatType = 1;
        else heatType = 0;
    }
}