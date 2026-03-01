using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OrbitRevolution))]
public class ArtificialSatellite : MonoBehaviour, UpdateFocusInfo
{
    [Header("Artificial Satellite Settings")]
    public float scale = 0.1f;
    public float altitude = 1f;
    private const float LocalZScaleFactor = 0.1f;

    private OrbitRevolution orbitRevolution;
    private bool partFocusInitialized;

    private void Awake()
    {
        orbitRevolution = GetComponent<OrbitRevolution>();
        SmallScaleLayerUtility.ApplyRecursively(transform);
    }

    private void Start()
    {
        Vector3 localScale = transform.localScale;
        localScale.z *= LocalZScaleFactor;
        transform.localScale = localScale;

        EnsureSatelliteBodyHidden();
        InitializeExistingPartFocuses();
    }

    private void LateUpdate()
    {
        // Other systems can toggle renderers/colliders; keep the satellite body hidden.
        EnsureSatelliteBodyHidden();
        if (!partFocusInitialized)
        {
            InitializeExistingPartFocuses();
        }
    }

    public Dictionary<string, string> UpdateFocusInfo()
    {
        string artificialSatelliteName = name;

        string artificialSatelliteCenter = orbitRevolution.center.name;

        string artificialSatelliteRoute = "";

        Dictionary<string, string> focusInfo = new Dictionary<string, string>
        {
            { "이름", artificialSatelliteName },
            { "공전 모체", artificialSatelliteCenter },
            { "노선", artificialSatelliteRoute }
        };

        return focusInfo;
    }

    private void EnsureSatelliteBodyHidden()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.enabled) meshRenderer.enabled = false;

        Collider colliderComponent = GetComponent<Collider>();
        if (colliderComponent != null && colliderComponent.enabled) colliderComponent.enabled = false;
    }

    private void InitializeExistingPartFocuses()
    {
        PartDB partDb = PartDB.Instance;
        if (partDb == null) return;

        bool foundAnyPart = false;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform) continue;
            if (!child.CompareTag("Part")) continue;

            foundAnyPart = true;
            Part part = ResolvePartByName(partDb, child.name);
            if (part == null) continue;

            AssemblyPartFocus partFocus = child.GetComponent<AssemblyPartFocus>();
            if (partFocus == null) partFocus = child.gameObject.AddComponent<AssemblyPartFocus>();
            partFocus.Initialize(part, this);
        }

        if (foundAnyPart)
        {
            partFocusInitialized = true;
        }
    }

    private static Part ResolvePartByName(PartDB partDb, string rawName)
    {
        if (partDb == null || string.IsNullOrWhiteSpace(rawName)) return null;

        string name = rawName.Replace("(Clone)", "").Trim();
        Part part = partDb.GetPartByName(name);
        if (part != null) return part;

        if (name.EndsWith("Prefab"))
        {
            string withoutPrefab = name.Substring(0, name.Length - "Prefab".Length).Trim();
            part = partDb.GetPartByName(withoutPrefab);
            if (part != null) return part;
        }

        return null;
    }
}
