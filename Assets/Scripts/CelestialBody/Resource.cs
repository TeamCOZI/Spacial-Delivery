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
            /* Heat 5 */ new A() {{ResourceState.Crystal, 2}, {ResourceState.Liquid, 5}},
            /* Heat 4 */ new A() {{ResourceState.Crystal, 2}, {ResourceState.Vapor, 3}},
            /* Heat 3 */ null,
            /* Heat 2 */ null,
            /* Heat 1 */ null
        },
        // ATM 4
        {
            /* Heat 5 */ new A() {{ResourceState.Crystal, 2}, {ResourceState.Liquid, 5}},
            /* Heat 4 */ new A() {{ResourceState.Crystal, 2}, {ResourceState.Vapor, 3}},
            /* Heat 3 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 2 */ null,
            /* Heat 1 */ null
        },
        // ATM 3
        {
            /* Heat 5 */ new A() {{ResourceState.Liquid, 5}, {ResourceState.Crystal, 2}},
            /* Heat 4 */ new A() {{ResourceState.Crystal, 3}},
            /* Heat 3 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 1 */ null
        },
        // ATM 2
        {
            /* Heat 5 */ new A() {{ResourceState.Crystal, 5}},
            /* Heat 4 */ new A() {{ResourceState.Crystal, 3}},
            /* Heat 3 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 1 */ null
        },
        // ATM 1
        {
            /* Heat 5 */ new A() {{ResourceState.Crystal, 5}},
            /* Heat 4 */ new A() {{ResourceState.Crystal, 5}},
            /* Heat 3 */ null,
            /* Heat 2 */ null,
            /* Heat 1 */ null
        }
    };
    public static A[,] water = new A[,]
    {
        // ATM 5
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ null,
            /* Heat 2 */ new A() {{ResourceState.Liquid, 3}, {ResourceState.Vapor, 1}},
            /* Heat 1 */ null
        },
        // ATM 4
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ new A() {{ResourceState.Liquid, 1}, {ResourceState.Vapor, 1}},
            /* Heat 2 */ new A() {{ResourceState.Liquid, 3}},
            /* Heat 1 */ null
        },
        // ATM 3
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ new A() {{ResourceState.Liquid, 2}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 1}},
            /* Heat 1 */ null
        },
        // ATM 2
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ new A() {{ResourceState.Crystal, 1}, {ResourceState.Liquid, 1}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 1}},
            /* Heat 1 */ new A() {{ResourceState.Crystal, 2}}
        },
        // ATM 1
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ null,
            /* Heat 2 */ new A() {{ResourceState.Crystal, 1}},
            /* Heat 1 */ new A() {{ResourceState.Crystal, 1}}
        }
    };
    public static A[,] gas = new A[,]
    {
        // ATM 5
        {
            /* Heat 5 */ new A() {{ResourceState.Vapor, 3}},
            /* Heat 4 */ new A() {{ResourceState.Vapor, 3}},
            /* Heat 3 */ null,
            /* Heat 2 */ new A() {{ResourceState.Liquid, 4}, {ResourceState.Vapor, 1}},
            /* Heat 1 */ null
        },
        // ATM 4
        {
            /* Heat 5 */ new A() {{ResourceState.Vapor, 2}},
            /* Heat 4 */ new A() {{ResourceState.Vapor, 2}},
            /* Heat 3 */ new A() {{ResourceState.Vapor, 1}},
            /* Heat 2 */ new A() {{ResourceState.Liquid, 4}},
            /* Heat 1 */ null
        },
        // ATM 3
        {
            /* Heat 5 */ new A() {{ResourceState.Vapor, 1}},
            /* Heat 4 */ new A() {{ResourceState.Vapor, 2}},
            /* Heat 3 */ new A() {{ResourceState.Vapor, 1}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 1}},
            /* Heat 1 */ null
        },
        // ATM 2
        {
            /* Heat 5 */ null,
            /* Heat 4 */ new A() {{ResourceState.Vapor, 2}},
            /* Heat 3 */ new A() {{ResourceState.Vapor, 1}},
            /* Heat 2 */ new A() {{ResourceState.Crystal, 1}},
            /* Heat 1 */ new A() {{ResourceState.Crystal, 2}}
        },
        // ATM 1
        {
            /* Heat 5 */ null,
            /* Heat 4 */ null,
            /* Heat 3 */ null,
            /* Heat 2 */ new A() {{ResourceState.Crystal, 2}},
            /* Heat 1 */ new A() {{ResourceState.Crystal, 2}}
        }
    };

    public static Color[,] color = new Color[,]
    {
        // ATM 5
        {
            /* Heat 5 */ new Color32(255, 0, 0, 255),
            /* Heat 4 */ new Color32(255, 192, 0, 255),
            /* Heat 3 */ new Color32(255, 192, 0, 255),
            /* Heat 2 */ new Color32(218, 233, 248, 255),
            /* Heat 1 */ new Color32(202, 237, 251, 255)
        },
        // ATM 4
        {
            /* Heat 5 */ new Color32(255, 0, 0, 255),
            /* Heat 4 */ new Color32(255, 192, 0, 255),
            /* Heat 3 */ new Color32(71, 211, 89, 255),
            /* Heat 2 */ new Color32(218, 233, 248, 255),
            /* Heat 1 */ new Color32(202, 237, 251, 255)
        },
        // ATM 3
        {
            /* Heat 5 */ new Color32(255, 0, 0, 255),
            /* Heat 4 */ new Color32(255, 255, 0, 255),
            /* Heat 3 */ new Color32(218, 242, 208, 255),
            /* Heat 2 */ new Color32(251, 226, 213, 255),
            /* Heat 1 */ new Color32(202, 237, 251, 255)
        },
        // ATM 2
        {
            /* Heat 5 */ new Color32(217, 217, 217, 255),
            /* Heat 4 */ new Color32(255, 255, 0, 255),
            /* Heat 3 */ new Color32(255, 255, 255, 255),
            /* Heat 2 */ new Color32(251, 226, 213, 255),
            /* Heat 1 */ new Color32(202, 237, 251, 255)
        },
        // ATM 1
        {
            /* Heat 5 */ new Color32(217, 217, 217, 255),
            /* Heat 4 */ new Color32(217, 217, 217, 255),
            /* Heat 3 */ new Color32(217, 217, 217, 255),
            /* Heat 2 */ new Color32(202, 237, 251, 255),
            /* Heat 1 */ new Color32(202, 237, 251, 255)
        }
    };

    public int aTMType;
    public int heatType;

    public Dictionary<ResourceType, A> resource;

    public void Initialize(bool isJovian, int ATM, int Heat, bool hasPlasma)
    {
        if (isJovian)
        {
            resource = new Dictionary<ResourceType, A>
            {
                {ResourceType.Gas, new A {{ResourceState.Liquid, 5}, {ResourceState.Vapor, 5}}},
                {ResourceType.Plasma, new A {{ResourceState.Plasma, 1}}}
            };
            if (transform.GetComponent<MeshRenderer>() != null) transform.GetComponent<MeshRenderer>().material.color = new Color32(227, 176, 11, 255);
        }
        else
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
                    celestialBodyComponent.Heat += 200;
                    heatType = 3;
                }
                else if (aTMType == 0)
                {
                    celestialBodyComponent.Heat += 150;
                    heatType = 3;
                }
            }
            else if (heatType == 0)
            {
                if (aTMType == 4 || aTMType == 3 || aTMType == 2)
                {
                    celestialBodyComponent.ATM = Mathf.RoundToInt(celestialBodyComponent.ATM * 0.01f);
                    aTMType = 0;
                }
            }

            SetATMHeatType(ATM, Heat);

            resource = new Dictionary<ResourceType, A>
            {
                { ResourceType.Mineral, mineral[4 - aTMType, 4 - heatType] },
                { ResourceType.Water, water[4 - aTMType, 4 - heatType] },
                { ResourceType.Gas, gas[4 - aTMType, 4 - heatType] }
            };

            if (((aTMType == 0 && heatType == 3) || (aTMType == 0 && heatType == 4) || (aTMType == 1 && heatType == 4)) && hasPlasma)
            {
                resource.Add(ResourceType.Plasma, new A() {{ResourceState.Plasma, 1}});
            }
            if (transform.GetComponent<MeshRenderer>() != null) transform.GetComponent<MeshRenderer>().material.color = color[4 - aTMType, 4 - heatType];
        }
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