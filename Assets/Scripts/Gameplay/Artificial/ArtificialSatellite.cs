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
    private bool coreFocusUiInitialized;

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
        EnsureCoreFocusGridUIs();
    }

    private void LateUpdate()
    {
        // Other systems can toggle renderers/colliders; keep the satellite body hidden.
        EnsureSatelliteBodyHidden();
        EnsureCriticalPartVisualsVisible();
        if (!partFocusInitialized)
        {
            InitializeExistingPartFocuses();
        }

        if (!coreFocusUiInitialized)
        {
            EnsureCoreFocusGridUIs();
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

    private void EnsureCriticalPartVisualsVisible()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform) continue;

            if (!ShouldKeepPartVisualsVisible(child)) continue;

            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }

            MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                if (renderers[j] != null && !renderers[j].enabled)
                {
                    renderers[j].enabled = true;
                }
            }
        }
    }

    private static bool ShouldKeepPartVisualsVisible(Transform target)
    {
        if (target == null) return false;
        if (!target.CompareTag("Part")) return false;

        string objectName = target.name ?? string.Empty;
        if (objectName.IndexOf("Launcher", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (objectName.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

        AssemblyPartFocus partFocus = target.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null) return false;

        string partName = partFocus.SourcePart.partName ?? string.Empty;
        if (partName.IndexOf("Launcher", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (partName.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

        return false;
    }

    private void InitializeExistingPartFocuses()
    {
        if (!GameplayRuntimeAccess.TryGetPartDb(out PartDB partDb)) return;

        bool foundAnyPart = false;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform) continue;
            if (!child.CompareTag("Part")) continue;

            foundAnyPart = true;
            AssemblyPartFocus partFocus = child.GetComponent<AssemblyPartFocus>();
            if (partFocus == null) partFocus = child.gameObject.AddComponent<AssemblyPartFocus>();

            Part part = ResolvePartByName(partDb, child.name);
            partFocus.Initialize(part, this);
        }

        if (foundAnyPart)
        {
            partFocusInitialized = true;
        }
    }

    private void EnsureCoreFocusGridUIs()
    {
        if (!GameplayRuntimeAccess.TryGetPartDb(out PartDB partDb)) return;

        bool foundCore = false;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform) continue;
            if (!child.CompareTag("Part")) continue;

            Part part = ResolvePartByName(partDb, child.name);
            if (part == null) continue;
            if (!IsCorePart(part)) continue;

            foundCore = true;
            CoreFocusGridUI gridUi = child.GetComponent<CoreFocusGridUI>();
            if (gridUi == null)
            {
                child.gameObject.AddComponent<CoreFocusGridUI>();
            }
        }

        if (foundCore)
        {
            coreFocusUiInitialized = true;
        }
    }

    private static bool IsCorePart(Part part)
    {
        if (part == null) return false;
        if (string.Equals(part.partName, "Core", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
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
