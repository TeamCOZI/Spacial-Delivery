using UnityEngine;

[DisallowMultipleComponent]
public class CoreFocusGridUI : MonoBehaviour
{
    [Header("Grid Plane")]
    [SerializeField] private Vector2 planeSize = new Vector2(0.9f, 0.9f);
    [SerializeField] private float faceOffset = -0.501f;
    [SerializeField] private int cellsPerAxis = 9;
    [SerializeField, Range(0f, 0.1f)] private float lineWidth = 0.02f;
    [SerializeField] private Color gridColor = new Color(0.2f, 0.95f, 1f, 0.7f);

    private const string GridPlaneName = "CoreFocusGridPlane";
    private GameObject gridPlane;
    private Material gridMaterial;
    private AssemblyPartFocus partFocus;

    private void Awake()
    {
        EnsurePartFocus();
        EnsureGridPlane();
    }

    private void OnEnable()
    {
        EnsureGridPlane();
        UpdateVisibility();
    }

    private void OnDisable()
    {
        if (gridPlane != null) gridPlane.SetActive(false);
    }

    private void OnDestroy()
    {
        if (gridMaterial != null)
        {
            Destroy(gridMaterial);
            gridMaterial = null;
        }
    }

    private void LateUpdate()
    {
        UpdateVisibility();
    }

    private void EnsureGridPlane()
    {
        if (gridPlane == null)
        {
            Transform existing = transform.Find(GridPlaneName);
            if (existing != null)
            {
                gridPlane = existing.gameObject;
            }
            else
            {
                gridPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                gridPlane.name = GridPlaneName;
                gridPlane.transform.SetParent(transform, false);

                Collider collider = gridPlane.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }
        }

        ConfigureGridPlaneTransform();
        SmallScaleLayerUtility.ApplyRecursively(gridPlane.transform);
        ConfigureGridPlaneMaterial();
    }

    private void ConfigureGridPlaneTransform()
    {
        if (gridPlane == null) return;

        gridPlane.transform.localPosition = new Vector3(0f, 0f, faceOffset);
        gridPlane.transform.localRotation = Quaternion.identity;
        gridPlane.transform.localScale = new Vector3(
            Mathf.Max(0.01f, planeSize.x),
            Mathf.Max(0.01f, planeSize.y),
            1f);
    }

    private void ConfigureGridPlaneMaterial()
    {
        if (!TryGetGridRenderer(out MeshRenderer renderer)) return;

        Shader gridShader = Shader.Find("Unlit/Grid");
        if (gridShader == null)
        {
            Debug.LogError("CoreFocusGridUI: Shader 'Unlit/Grid' is missing.");
            return;
        }

        if (gridMaterial == null || gridMaterial.shader != gridShader)
        {
            if (gridMaterial != null) Destroy(gridMaterial);
            gridMaterial = new Material(gridShader);
        }

        gridMaterial.SetColor("_GridColor", gridColor);
        gridMaterial.SetFloat("_GridSize", Mathf.Max(1, cellsPerAxis));
        gridMaterial.SetFloat("_LineWidth", lineWidth);
        renderer.material = gridMaterial;
    }

    private void UpdateVisibility()
    {
        if (gridPlane == null) return;
        bool isCoreFocused = IsCorePart() && FocusManager.currentFocus == transform;
        if (gridPlane.activeSelf != isCoreFocused)
        {
            gridPlane.SetActive(isCoreFocused);
        }
    }

    private bool IsCorePart()
    {
        EnsurePartFocus();
        if (partFocus == null || partFocus.SourcePart == null) return false;
        if (!string.Equals(partFocus.SourcePart.partName, "Core", System.StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private void EnsurePartFocus()
    {
        if (partFocus == null)
        {
            partFocus = GetComponent<AssemblyPartFocus>();
        }
    }

    private bool TryGetGridRenderer(out MeshRenderer renderer)
    {
        renderer = null;
        if (gridPlane == null) return false;

        renderer = gridPlane.GetComponent<MeshRenderer>();
        return renderer != null;
    }
}
