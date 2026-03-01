using System.Collections.Generic;
using UnityEngine;

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

        combinedMeshFilter = combinedObject.GetComponent<MeshFilter>();
        if (combinedMeshFilter == null)
        {
            combinedMeshFilter = combinedObject.AddComponent<MeshFilter>();
        }

        combinedMeshRenderer = combinedObject.GetComponent<MeshRenderer>();
        if (combinedMeshRenderer == null)
        {
            combinedMeshRenderer = combinedObject.AddComponent<MeshRenderer>();
        }

        SmallScaleLayerUtility.ApplyRecursively(combinedObject.transform);
    }

    private bool ShouldExcludeTransform(Transform t)
    {
        if (t == null) return true;
        if (t == transform) return true;
        if (t.GetComponent<ArtificialSatellite>() != null) return true;
        if (t == transform.Find(combinedObjectName)) return true;
        if (t.GetComponentInParent<AssemblyGhostMarker>(true) != null) return true;
        if (t.name == "AssemblyGridPlane") return true;
        if (t.CompareTag("Icon")) return true;
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
