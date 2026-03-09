#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class AssemblyPortConnectivityTests
{
    private readonly System.Collections.Generic.List<GameObject> createdObjects =
        new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    [Test]
    public void TryGetPortEntry_ReturnsRegisteredEntry()
    {
        GameObject root = Create("PartRoot");
        AssemblyPartPortLayout layout = root.AddComponent<AssemblyPartPortLayout>();

        GameObject portGo = Create("OutputPort");
        portGo.transform.SetParent(root.transform, false);
        AssemblyPort port = portGo.AddComponent<AssemblyPort>();

        var entry = new AssemblyPartPortLayout.PortEntry
        {
            portTransform = port.transform,
            relativeSourceCell = new Vector2Int(1, 0),
            side = AssemblyPartPortLayout.PortSide.Right,
            portType = AssemblyPortType.Output
        };

        layout.SetPorts(new System.Collections.Generic.List<AssemblyPartPortLayout.PortEntry> { entry });

        bool found = layout.TryGetPortEntry(port, out AssemblyPartPortLayout.PortEntry resolved);

        Assert.IsTrue(found);
        Assert.IsNotNull(resolved);
        Assert.AreEqual(new Vector2Int(1, 0), resolved.relativeSourceCell);
        Assert.AreEqual(AssemblyPortType.Output, resolved.portType);
    }

    [Test]
    public void TryGetPortEntry_ReturnsFalseWhenPortIsNotMapped()
    {
        GameObject root = Create("PartRoot");
        AssemblyPartPortLayout layout = root.AddComponent<AssemblyPartPortLayout>();
        layout.SetPorts(new System.Collections.Generic.List<AssemblyPartPortLayout.PortEntry>());

        GameObject portGo = Create("Port");
        AssemblyPort port = portGo.AddComponent<AssemblyPort>();

        bool found = layout.TryGetPortEntry(port, out AssemblyPartPortLayout.PortEntry resolved);

        Assert.IsFalse(found);
        Assert.IsNull(resolved);
    }

    [Test]
    public void AssemblyPort_SetOccupied_UpdatesOccupancyFlag()
    {
        GameObject go = Create("Port");
        AssemblyPort port = go.AddComponent<AssemblyPort>();

        Assert.IsFalse(port.IsOccupied);
        port.SetOccupied(true);
        Assert.IsTrue(port.IsOccupied);
        port.SetOccupied(false);
        Assert.IsFalse(port.IsOccupied);
    }

    private GameObject Create(string name)
    {
        GameObject go = new GameObject(name);
        createdObjects.Add(go);
        return go;
    }
}
#endif
