using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class AssemblyMeshCombiner : MonoBehaviour
{
    [Header("Combine Settings")]
    public bool hideSourceRenderers = true;
    public bool combinedOnlyMode = true;
    public bool enforceCombinedOnlyEachFrame = true;
    public bool includeInactive = true;
    public string combinedObjectName = "CombinedPartsMesh";

    private GameObject combinedObject;
    private MeshFilter combinedMeshFilter;
    private MeshRenderer combinedMeshRenderer;
    private Mesh combinedMesh;
    private readonly List<Renderer> hiddenSourceRenderers = new List<Renderer>();

    public void RebuildCombinedMesh()
    {
        EnsureCombinedObject();
        EnforceAlwaysVisibleSourceRenderers();
        if (!combinedOnlyMode)
        {
            RestoreSourceRenderers();
        }
        else
        {
            hiddenSourceRenderers.Clear();
        }

        Dictionary<Material, List<CombineInstance>> combinesByMaterial = new Dictionary<Material, List<CombineInstance>>();
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;
            if (combinedMeshFilter != null && mf == combinedMeshFilter) continue;

            Transform t = mf.transform;
            if (t == null) continue;
            if (ShouldExcludeTransform(t)) continue;

            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Material[] materials = mr.sharedMaterials;
            if (materials == null || materials.Length == 0) continue;

            int subMeshCount = Mathf.Min(mf.sharedMesh.subMeshCount, materials.Length);
            Matrix4x4 matrix = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials[subMeshIndex];
                if (material == null) continue;

                if (!combinesByMaterial.TryGetValue(material, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    combinesByMaterial[material] = list;
                }

                CombineInstance ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = subMeshIndex,
                    transform = matrix
                };
                list.Add(ci);
            }

            if (hideSourceRenderers || combinedOnlyMode)
            {
                if (!hiddenSourceRenderers.Contains(mr))
                {
                    hiddenSourceRenderers.Add(mr);
                }
                mr.enabled = false;
            }
        }

        if (combinesByMaterial.Count == 0)
        {
            if (combinedObject != null) combinedObject.SetActive(false);
            ClearCombinedMesh();
            return;
        }

        combinedObject.SetActive(true);

        Material[] finalMaterials = new Material[combinesByMaterial.Count];
        List<CombineInstance> finalCombiners = new List<CombineInstance>(combinesByMaterial.Count);
        List<Mesh> tempSubMeshes = new List<Mesh>(combinesByMaterial.Count);

        int matIndex = 0;
        foreach (KeyValuePair<Material, List<CombineInstance>> pair in combinesByMaterial)
        {
            Mesh materialMesh = new Mesh
            {
                name = $"{combinedObjectName}_Sub_{matIndex}"
            };
            materialMesh.CombineMeshes(pair.Value.ToArray(), true, true, false);

            finalMaterials[matIndex] = pair.Key;
            finalCombiners.Add(new CombineInstance
            {
                mesh = materialMesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
            tempSubMeshes.Add(materialMesh);
            matIndex++;
        }

        if (combinedMesh == null)
        {
            combinedMesh = new Mesh
            {
                name = combinedObjectName
            };
        }
        else
        {
            combinedMesh.Clear();
        }

        combinedMesh.CombineMeshes(finalCombiners.ToArray(), false, false, false);
        combinedMeshFilter.sharedMesh = combinedMesh;
        combinedMeshRenderer.sharedMaterials = finalMaterials;
        ApplyCombinedOnlyMode();

        for (int i = 0; i < tempSubMeshes.Count; i++)
        {
            if (tempSubMeshes[i] != null)
            {
                Destroy(tempSubMeshes[i]);
            }
        }
    }

    public void RestoreSourceRenderers()
    {
        for (int i = 0; i < hiddenSourceRenderers.Count; i++)
        {
            if (hiddenSourceRenderers[i] != null)
            {
                hiddenSourceRenderers[i].enabled = true;
            }
        }
        hiddenSourceRenderers.Clear();
    }

    private void LateUpdate()
    {
        if (!combinedOnlyMode || !enforceCombinedOnlyEachFrame) return;
        ApplyCombinedOnlyMode();
    }

    private void EnsureCombinedObject()
    {
        if (combinedObject == null)
        {
            Transform existing = transform.Find(combinedObjectName);
            if (existing != null)
            {
                combinedObject = existing.gameObject;
            }
        }

        if (combinedObject == null)
        {
            combinedObject = new GameObject(combinedObjectName);
            combinedObject.transform.SetParent(transform, false);
        }

        combinedMeshFilter = ComponentUtility.GetOrAddComponent<MeshFilter>(combinedObject);
        combinedMeshRenderer = ComponentUtility.GetOrAddComponent<MeshRenderer>(combinedObject);

        SmallScaleLayerUtility.ApplyRecursively(combinedObject.transform);
    }

    private bool ShouldExcludeTransform(Transform t)
    {
        if (t == null) return true;
        if (t == transform) return true;
        if (t.GetComponent<ArtificialSatellite>() != null) return true;
        if (t.GetComponentInParent<AssemblyGhostMarker>(true) != null) return true;
        if (ShouldKeepRendererVisible(t)) return true;
        if (t == transform.Find(combinedObjectName)) return true;
        if (t.name == "AssemblyGridPlane") return true;
        if (t.name == "CoreFocusGridPlane") return true;
        if (t.CompareTag("Icon")) return true;
        return false;
    }

    private static bool ShouldKeepRendererVisible(Transform t)
    {
        if (t == null) return false;

        if (t.GetComponentInParent<AssemblyPortVisualMarker>(true) != null) return true;

        // Keep launcher and drop-port visuals as source renderers so they are always visible.
        if (ContainsNameTokenInHierarchy(t, "Launcher")) return true;
        if (ContainsNameTokenInHierarchy(t, "Drop")) return true;
        if (ContainsNameTokenInHierarchy(t, "DropPort")) return true;
        if (ContainsNameTokenInHierarchy(t, "Port")) return true;
        if (ContainsNameTokenInHierarchy(t, "Dock")) return true;

        AssemblyPartFocus partFocus = t.GetComponentInParent<AssemblyPartFocus>(true);
        if (partFocus == null || partFocus.SourcePart == null) return false;

        string partName = partFocus.SourcePart.partName;
        if (string.IsNullOrWhiteSpace(partName)) return false;

        return partName.IndexOf("Launcher", StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("DropPort", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsNameTokenInHierarchy(Transform t, string token)
    {
        if (t == null || string.IsNullOrWhiteSpace(token)) return false;

        Transform current = t;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.name) &&
                current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ApplyCombinedOnlyMode()
    {
        if (combinedObject == null) return;

        combinedObject.SetActive(true);

        if (!combinedOnlyMode) return;

        MeshRenderer[] sourceRenderers = GetComponentsInChildren<MeshRenderer>(includeInactive);
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            MeshRenderer mr = sourceRenderers[i];
            if (mr == null) continue;
            if (combinedMeshRenderer != null && mr == combinedMeshRenderer) continue;
            if (ShouldExcludeTransform(mr.transform)) continue;
            mr.enabled = false;
        }

        EnforceAlwaysVisibleSourceRenderers();
    }

    private void EnforceAlwaysVisibleSourceRenderers()
    {
        MeshRenderer[] sourceRenderers = GetComponentsInChildren<MeshRenderer>(includeInactive);
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            MeshRenderer mr = sourceRenderers[i];
            if (mr == null) continue;
            if (mr.GetComponentInParent<AssemblyGhostMarker>(true) != null) continue;
            if (!ShouldKeepRendererVisible(mr.transform)) continue;

            EnsureHierarchyActive(mr.transform);

            if (!mr.enabled)
            {
                mr.enabled = true;
            }
        }
    }

    private static void EnsureHierarchyActive(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }
            current = current.parent;
        }
    }

    private void ClearCombinedMesh()
    {
        if (combinedMesh != null)
        {
            combinedMesh.Clear();
        }
        if (combinedMeshFilter != null)
        {
            combinedMeshFilter.sharedMesh = null;
        }
    }

    private void OnDestroy()
    {
        RestoreSourceRenderers();
        if (combinedMesh != null)
        {
            Destroy(combinedMesh);
            combinedMesh = null;
        }
    }

}
