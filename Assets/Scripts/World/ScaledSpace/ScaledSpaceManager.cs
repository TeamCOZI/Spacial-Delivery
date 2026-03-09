using System.Collections.Generic;
using UnityEngine;

public class ScaledSpaceManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject scaledProxyPrefab;

    [Header("Activation")]
    [Min(1f)] public float enterScaledModeCameraZ = 30000f;
    [Min(1f)] public float exitScaledModeCameraZ = 25000f;

    [Header("Compression")]
    [Min(1f)] public float positionCompression = 20f;
    [Min(1f)] public float scaleCompression = 20f;
    [Min(0.0001f)] public float minProxyScale = 0.01f;

    [Header("Rendering")]
    public bool hideOriginalRenderersInScaledMode = true;
    public bool includeChildrenRenderers = false;
    public bool hideOrbitLinesInScaledMode = false;

    [Header("Performance")]
    [Range(1, 30)] public int proxyUpdateIntervalFrames = 1;
    [Range(60, 3600)] public int proxyRebuildIntervalFrames = 600;
    public bool enablePeriodicRebuild = false;

    private readonly List<ProxyEntry> proxies = new List<ProxyEntry>();
    private readonly Dictionary<int, ProxyEntry> proxyBySourceId = new Dictionary<int, ProxyEntry>();

    private Camera mainCamera;
    private Transform proxyRoot;
    private bool isScaledMode;
    private int proxyRebuildFrameCounter;

    private const string ScaledProxyPrefabResourcePath = "Prefabs/System/ScaledSpaceProxyPrefab";

    private class ProxyEntry
    {
        public Transform source;
        public Transform proxy;
        public Renderer[] sourceRenderers;
        public LineRenderer[] sourceOrbitLines;
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        enterScaledModeCameraZ = Mathf.Max(1f, WorldScale.ScaleLength(enterScaledModeCameraZ));
        exitScaledModeCameraZ = Mathf.Max(1f, WorldScale.ScaleLength(exitScaledModeCameraZ));
        proxyUpdateIntervalFrames = Mathf.Max(1, proxyUpdateIntervalFrames);
        proxyRebuildIntervalFrames = Mathf.Max(60, proxyRebuildIntervalFrames);

        if (scaledProxyPrefab == null)
        {
            scaledProxyPrefab = Resources.Load<GameObject>(ScaledProxyPrefabResourcePath);
        }

        GameObject root = new GameObject("ScaledSpaceRoot");
        root.transform.SetParent(transform, false);
        proxyRoot = root.transform;
    }

    private void Start()
    {
        RebuildProxies();
        SetScaledMode(false);
        WorldOriginManager.worldShifted += HandleWorldShift;
    }

    private void OnDestroy()
    {
        WorldOriginManager.worldShifted -= HandleWorldShift;

        for (int i = 0; i < proxies.Count; i++)
        {
            ProxyEntry entry = proxies[i];
            if (entry != null && entry.proxy != null)
            {
                Destroy(entry.proxy.gameObject);
            }
        }

        proxies.Clear();
        proxyBySourceId.Clear();
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float cameraZ = Mathf.Abs(mainCamera.transform.position.z);
        bool shouldUseScaledMode = isScaledMode
            ? cameraZ >= exitScaledModeCameraZ
            : cameraZ >= enterScaledModeCameraZ;

        if (shouldUseScaledMode != isScaledMode)
        {
            SetScaledMode(shouldUseScaledMode);
        }

        if (isScaledMode) UpdateProxies();

        if (!isScaledMode || !enablePeriodicRebuild) return;

        proxyRebuildFrameCounter++;
        if (proxyRebuildFrameCounter >= proxyRebuildIntervalFrames)
        {
            proxyRebuildFrameCounter = 0;
            RebuildProxies();
        }
    }

    public void RebuildProxies()
    {
        CleanupMissingEntries();

        MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = allBehaviours[i];
            if (!IsProxyTarget(behaviour)) continue;

            Transform source = behaviour.transform;
            if (source == null) continue;

            int sourceId = source.GetInstanceID();
            if (proxyBySourceId.ContainsKey(sourceId)) continue;

            ProxyEntry entry = TryCreateProxyEntry(source);
            if (entry == null) continue;

            proxies.Add(entry);
            proxyBySourceId[sourceId] = entry;
        }
    }

    private void CleanupMissingEntries()
    {
        for (int i = proxies.Count - 1; i >= 0; i--)
        {
            ProxyEntry entry = proxies[i];
            if (entry.source != null && entry.proxy != null) continue;

            if (entry.proxy != null) Destroy(entry.proxy.gameObject);
            if (entry.source != null) proxyBySourceId.Remove(entry.source.GetInstanceID());
            proxies.RemoveAt(i);
        }
    }

    private bool IsProxyTarget(MonoBehaviour behaviour)
    {
        if (behaviour == null) return false;
        if (!(behaviour is UpdateFocusInfo)) return false;
        if (behaviour.GetComponentInParent<ScaledSpaceProxyTarget>() != null) return false;
        if (!HasSupportedSourceComponent(behaviour)) return false;
        return true;
    }

    private static bool HasSupportedSourceComponent(MonoBehaviour behaviour)
    {
        if (behaviour == null) return false;
        return behaviour.TryGetComponent<OrbitRevolution>(out _) || behaviour.TryGetComponent<Star>(out _);
    }

    private ProxyEntry TryCreateProxyEntry(Transform source)
    {
        MeshFilter sourceMeshFilter = source.GetComponent<MeshFilter>();
        MeshRenderer sourceMeshRenderer = source.GetComponent<MeshRenderer>();
        if (sourceMeshFilter == null || sourceMeshRenderer == null) return null;

        GameObject proxy = CreateProxyObject(source);
        if (proxy == null) return null;
        proxy.transform.SetParent(proxyRoot, false);

        MeshFilter proxyMeshFilter = proxy.GetComponent<MeshFilter>();
        if (proxyMeshFilter == null)
        {
            Debug.LogError("ScaledSpaceManager: Scaled proxy prefab is missing MeshFilter.");
            Destroy(proxy);
            return null;
        }
        proxyMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer proxyRenderer = proxy.GetComponent<MeshRenderer>();
        if (proxyRenderer == null)
        {
            Debug.LogError("ScaledSpaceManager: Scaled proxy prefab is missing MeshRenderer.");
            Destroy(proxy);
            return null;
        }
        proxyRenderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;

        SphereCollider proxyCollider = proxy.GetComponent<SphereCollider>();
        if (proxyCollider == null)
        {
            Debug.LogError("ScaledSpaceManager: Scaled proxy prefab is missing SphereCollider.");
            Destroy(proxy);
            return null;
        }
        proxyCollider.radius = ComputeProxyColliderRadius(sourceMeshFilter.sharedMesh);

        ScaledSpaceProxyTarget proxyTarget = proxy.GetComponent<ScaledSpaceProxyTarget>();
        if (proxyTarget == null)
        {
            Debug.LogError("ScaledSpaceManager: Scaled proxy prefab is missing ScaledSpaceProxyTarget.");
            Destroy(proxy);
            return null;
        }
        proxyTarget.source = source;

        Renderer[] sourceRenderers = CollectSourceRenderers(source);

        ProxyEntry entry = new ProxyEntry
        {
            source = source,
            proxy = proxy.transform,
            sourceRenderers = sourceRenderers,
            sourceOrbitLines = source.GetComponentsInChildren<LineRenderer>(true)
        };

        proxy.SetActive(isScaledMode);
        return entry;
    }

    private GameObject CreateProxyObject(Transform source)
    {
        GameObject proxy = scaledProxyPrefab != null
            ? Instantiate(scaledProxyPrefab)
            : new GameObject("ScaledSpaceProxyFallback", typeof(MeshFilter), typeof(MeshRenderer), typeof(SphereCollider), typeof(ScaledSpaceProxyTarget));

        proxy.name = source.name + " [ScaledProxy]";
        proxy.tag = source.tag;
        proxy.layer = source.gameObject.layer;
        return proxy;
    }

    private float ComputeProxyColliderRadius(Mesh mesh)
    {
        if (mesh == null) return 0.5f;

        Vector3 extents = mesh.bounds.extents;
        float radius = Mathf.Max(extents.x, extents.y, extents.z);
        return Mathf.Max(0.01f, radius);
    }

    private void HandleWorldShift(Vector3 shiftDelta)
    {
        if (!isScaledMode) return;
        UpdateProxies();
    }

    private void UpdateProxies()
    {
        Vector3 reference = FocusManager.currentFocus != null
            ? FocusManager.currentFocus.position
            : Vector3.zero;

        for (int i = proxies.Count - 1; i >= 0; i--)
        {
            ProxyEntry entry = proxies[i];
            if (entry.source == null || entry.proxy == null)
            {
                if (entry.proxy != null) Destroy(entry.proxy.gameObject);
                proxies.RemoveAt(i);
                continue;
            }

            Vector3 sourcePosition = entry.source.position;
            Vector3 scaledPosition = reference + (sourcePosition - reference) / positionCompression;

            Vector3 sourceLossyScale = entry.source.lossyScale;
            Vector3 scaledScale = sourceLossyScale / scaleCompression;
            scaledScale.x = Mathf.Max(minProxyScale, Mathf.Abs(scaledScale.x));
            scaledScale.y = Mathf.Max(minProxyScale, Mathf.Abs(scaledScale.y));
            scaledScale.z = Mathf.Max(minProxyScale, Mathf.Abs(scaledScale.z));

            entry.proxy.position = scaledPosition;
            entry.proxy.rotation = entry.source.rotation;
            entry.proxy.localScale = scaledScale;
        }
    }

    private void SetScaledMode(bool enabled)
    {
        isScaledMode = enabled;
        proxyRebuildFrameCounter = 0;
        if (proxyRoot != null)
        {
            proxyRoot.gameObject.SetActive(enabled);
        }

        if (enabled)
        {
            RebuildProxies();
        }

        for (int i = 0; i < proxies.Count; i++)
        {
            ProxyEntry entry = proxies[i];
            bool isStarSource = entry.source != null && entry.source.GetComponent<Star>() != null;
            if (enabled)
            {
                entry.sourceRenderers = CollectSourceRenderers(entry.source);
                if (hideOrbitLinesInScaledMode && entry.source != null)
                {
                    entry.sourceOrbitLines = entry.source.GetComponentsInChildren<LineRenderer>(true);
                }
            }

            if (entry.proxy != null)
            {
                // Keep stars always visible via original renderer path.
                entry.proxy.gameObject.SetActive(enabled && !isStarSource);
            }

            if (hideOriginalRenderersInScaledMode && entry.sourceRenderers != null)
            {
                for (int j = 0; j < entry.sourceRenderers.Length; j++)
                {
                    Renderer renderer = entry.sourceRenderers[j];
                    if (renderer == null) continue;
                    renderer.enabled = isStarSource || !enabled;
                }
            }

            if (hideOrbitLinesInScaledMode && entry.sourceOrbitLines != null)
            {
                for (int j = 0; j < entry.sourceOrbitLines.Length; j++)
                {
                    LineRenderer line = entry.sourceOrbitLines[j];
                    if (line == null) continue;
                    line.enabled = !enabled;
                }
            }
        }
    }

    private Renderer[] CollectSourceRenderers(Transform source)
    {
        if (source == null) return null;

        if (includeChildrenRenderers)
        {
            return source.GetComponentsInChildren<Renderer>(true);
        }

        Renderer sourceRenderer = source.GetComponent<Renderer>();
        if (sourceRenderer == null) return null;
        return new Renderer[] { sourceRenderer };
    }
}
