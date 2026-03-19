using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Assembly
{
    private const float StructureSurfaceLocalZ = -0.502f;
    private const int InstalledStructureSortingOrder = 20;
    private const int StructureGhostSortingOrder = 30;
    private const string RuntimeStructureObjectPrefix = "Structure_";
    private const string StructureLabelObjectName = "Label";
    private const float StructureLabelLocalY = 0f;
    private const float StructureLabelLocalZ = -0.08f;
    private const float StructureLabelScaleMultiplier = 0.42f;
    private const float StructureLabelFontSize = 5f;

    private static Sprite runtimeStructureSprite;
    private static Texture2D runtimeStructureTexture;

    private readonly Dictionary<Vector2Int, GameObject> occupiedStructureCells = new Dictionary<Vector2Int, GameObject>();

    private Structure structure;
    private GameObject structureGhost;
    private Transform activeCoreStructureHost;

    public void SelectStructure(Structure selectedStructure)
    {
        StopSelectionMode();
        SetOutputPortsPulsing(false);
        ClearPipePathGhosts();
        ResetPipePathSelectionState();
        ResetOutputCycleSelectionState();
        DestroyPartGhost(false);
        ResetSelectedPartState();
        ClearStructurePlacementSelection();

        if (selectedStructure == null)
        {
            IsAssembling = false;
            return;
        }

        activeCoreStructureHost = ResolveFocusedCoreTransform();
        if (activeCoreStructureHost == null)
        {
            activeCoreStructureHost = ResolveCoreTransform(artificialSatellite);
        }

        if (activeCoreStructureHost == null)
        {
            Debug.LogWarning("Assembly: Core transform is missing. Structure placement cannot start.");
            IsAssembling = false;
            return;
        }

        RebuildOccupiedStructureCells();

        structure = selectedStructure;
        structureGhost = CreateStructureVisual(structure, activeCoreStructureHost, StructureGhostSortingOrder, isGhost: true);
        CacheGhostRenderers();
        SetGhostPlacementVisual(false);
        IsAssembling = true;

        Debug.Log($"Selected structure for placement: {structure.structureName}");
    }

    private bool HasActiveStructurePlacement()
    {
        return structure != null && structureGhost != null;
    }

    private void UpdateStructureGhost()
    {
        if (structureGhost == null || structure == null || activeCoreStructureHost == null || !assemblyPlaneCreated)
        {
            isSnapped = false;
            canPlace = false;
            return;
        }

        if (!TryEnsureMainCamera() || Mouse.current == null)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 surfaceWorldPosition = ResolveStructureSurfaceWorldPosition(activeCoreStructureHost);
        float cameraSpaceDepth = Vector3.Dot(surfaceWorldPosition - mainCamera.transform.position, mainCamera.transform.forward);
        cameraSpaceDepth = Mathf.Max(MinCameraSpaceDepth, cameraSpaceDepth);

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cameraSpaceDepth));
        Vector3 localPoint = activeCoreStructureHost.InverseTransformPoint(worldPoint);

        Vector2 snapOffset = GetStructureSnapOffset(structure);
        Vector2Int centerCell = LocalPositionToStructureGrid(localPoint, snapOffset);
        centerCell = ClampStructureCenterCell(centerCell, structure);

        Vector3 finalLocalPosition = StructureGridToLocalPosition(centerCell, snapOffset);
        finalLocalPosition.z = StructureSurfaceLocalZ;
        structureGhost.transform.localPosition = finalLocalPosition;
        structureGhost.transform.localRotation = Quaternion.identity;

        isSnapped = true;
        canPlace = IsStructurePlacementAreaFree(centerCell, structure);
        SetGhostPlacementVisual(canPlace);
    }

    private void ApplyStructure()
    {
        if (!HasActiveStructurePlacement() || !isSnapped || !canPlace || activeCoreStructureHost == null)
        {
            return;
        }

        Vector2Int centerCell = GetCurrentStructureGhostCenterCell();
        Vector3 installedLocalPosition = structureGhost.transform.localPosition;

        GameObject placedObject = CreateInstalledStructure(activeCoreStructureHost, structure, centerCell, installedLocalPosition);
        if (placedObject != null)
        {
            RegisterStructureOccupiedCells(centerCell, structure, placedObject);
        }

        ArtificialSatellite sourceSatellite = ResolveSourceSatelliteForAssemblyTarget(artificialSatellite);
        if (ShouldMirrorToSourceSatellite(artificialSatellite, sourceSatellite))
        {
            Transform sourceCoreTransform = ResolveCoreTransform(sourceSatellite);
            if (sourceCoreTransform != null)
            {
                _ = CreateInstalledStructure(sourceCoreTransform, structure, centerCell, installedLocalPosition);
            }
        }

        Debug.Log($"Installed structure {structure.structureName} at {centerCell}.");
        Cancel();
    }

    private GameObject CreateInstalledStructure(Transform coreTransform, Structure selectedStructure, Vector2Int centerCell, Vector3 localPosition)
    {
        if (coreTransform == null || selectedStructure == null)
        {
            return null;
        }

        GameObject installedObject = CreateStructureVisual(selectedStructure, coreTransform, InstalledStructureSortingOrder, isGhost: false);
        installedObject.transform.localPosition = localPosition;
        installedObject.transform.localRotation = Quaternion.identity;
        AttachStructureLabel(installedObject, selectedStructure, InstalledStructureSortingOrder + 1);

        StructureInstance structureInstance = ComponentUtility.GetOrAddComponent<StructureInstance>(installedObject);
        structureInstance.Initialize(selectedStructure, centerCell);
        ConfigureInstalledStructureFocus(installedObject);
        return installedObject;
    }

    private static void ConfigureInstalledStructureFocus(GameObject installedObject)
    {
        if (installedObject == null)
        {
            return;
        }

        BoxCollider boxCollider = ComponentUtility.GetOrAddComponent<BoxCollider>(installedObject);
        boxCollider.center = Vector3.zero;
        boxCollider.size = new Vector3(1f, 1f, 0.2f);

        _ = ComponentUtility.GetOrAddComponent<StructureFocus>(installedObject);
    }

    private GameObject CreateStructureVisual(Structure selectedStructure, Transform parent, int sortingOrder, bool isGhost)
    {
        GameObject structureObject = new GameObject(RuntimeStructureObjectPrefix + selectedStructure.structureName);
        structureObject.transform.SetParent(parent, false);
        structureObject.transform.localPosition = new Vector3(0f, 0f, StructureSurfaceLocalZ);
        structureObject.transform.localRotation = Quaternion.identity;
        structureObject.transform.localScale = new Vector3(
            Mathf.Max(cellSize, selectedStructure.gridWidth * cellSize),
            Mathf.Max(cellSize, selectedStructure.gridHeight * cellSize),
            1f);

        SmallScaleLayerUtility.ApplyRecursively(structureObject.transform);

        SpriteRenderer spriteRenderer = structureObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = ResolveStructureSprite(selectedStructure);
        spriteRenderer.color = isGhost ? GhostInvalidColor : selectedStructure.iconTint;
        spriteRenderer.sortingOrder = sortingOrder;

        if (isGhost)
        {
            _ = ComponentUtility.GetOrAddComponent<AssemblyGhostMarker>(structureObject);
        }

        return structureObject;
    }

    private void AttachStructureLabel(GameObject structureObject, Structure selectedStructure, int sortingOrder)
    {
        if (structureObject == null || selectedStructure == null)
        {
            return;
        }

        GameObject labelObject = new GameObject(StructureLabelObjectName);
        labelObject.transform.SetParent(structureObject.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, StructureLabelLocalY, StructureLabelLocalZ);
        labelObject.transform.localRotation = Quaternion.identity;

        Vector3 structureScale = structureObject.transform.localScale;
        float inverseScaleX = Mathf.Approximately(structureScale.x, 0f) ? 1f : 1f / structureScale.x;
        float inverseScaleY = Mathf.Approximately(structureScale.y, 0f) ? 1f : 1f / structureScale.y;
        float labelScale = cellSize * StructureLabelScaleMultiplier;
        labelObject.transform.localScale = new Vector3(
            inverseScaleX * labelScale,
            inverseScaleY * labelScale,
            labelScale);

        SmallScaleLayerUtility.ApplyRecursively(labelObject.transform);

        TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
        TMP_FontAsset font = ResolveStructureLabelFont();
        if (font != null)
        {
            text.font = font;
        }

        text.text = selectedStructure.structureName;
        text.fontSize = StructureLabelFontSize;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.sortingOrder = sortingOrder;
        text.ForceMeshUpdate();

        Renderer textRenderer = text.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            textRenderer.sortingOrder = sortingOrder;
        }
    }

    private static TMP_FontAsset ResolveStructureLabelFont()
    {
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
        {
            return focusManager.focusInfoText.font;
        }

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            return defaultFont;
        }

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static Sprite ResolveStructureSprite(Structure selectedStructure)
    {
        if (runtimeStructureSprite != null)
        {
            return runtimeStructureSprite;
        }

        runtimeStructureTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "RuntimeStructureTexture"
        };
        runtimeStructureTexture.SetPixel(0, 0, Color.white);
        runtimeStructureTexture.Apply(false, true);

        runtimeStructureSprite = Sprite.Create(
            runtimeStructureTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        runtimeStructureSprite.name = "RuntimeStructureSprite";
        return runtimeStructureSprite;
    }

    private Vector2Int GetCurrentStructureGhostCenterCell()
    {
        if (structureGhost == null || structure == null)
        {
            return Vector2Int.zero;
        }

        Vector2 snapOffset = GetStructureSnapOffset(structure);
        return LocalPositionToStructureGrid(structureGhost.transform.localPosition, snapOffset);
    }

    private Vector2Int LocalPositionToStructureGrid(Vector3 localPosition, Vector2 snapOffset)
    {
        Vector2Int gridDimensions = GetStructureGridDimensions();
        int gridCenterX = gridDimensions.x / 2;
        int gridCenterY = gridDimensions.y / 2;

        return new Vector2Int(
            QuantizeToCellIndex((localPosition.x - snapOffset.x) / cellSize) + gridCenterX,
            QuantizeToCellIndex((localPosition.y - snapOffset.y) / cellSize) + gridCenterY);
    }

    private Vector3 StructureGridToLocalPosition(Vector2Int gridPosition, Vector2 snapOffset)
    {
        Vector2Int gridDimensions = GetStructureGridDimensions();
        int gridCenterX = gridDimensions.x / 2;
        int gridCenterY = gridDimensions.y / 2;

        float x = (gridPosition.x - gridCenterX) * cellSize + snapOffset.x;
        float y = (gridPosition.y - gridCenterY) * cellSize + snapOffset.y;
        return new Vector3(x, y, StructureSurfaceLocalZ);
    }

    private Vector2Int ClampStructureCenterCell(Vector2Int centerCell, Structure selectedStructure)
    {
        Vector2Int span = GetStructureCellSpan(selectedStructure);
        Vector2Int gridDimensions = GetStructureGridDimensions();

        int minX = span.x / 2;
        int maxX = gridDimensions.x - (span.x - (span.x / 2));
        int minY = span.y / 2;
        int maxY = gridDimensions.y - (span.y - (span.y / 2));

        return new Vector2Int(
            Mathf.Clamp(centerCell.x, minX, Mathf.Max(minX, maxX)),
            Mathf.Clamp(centerCell.y, minY, Mathf.Max(minY, maxY)));
    }

    private bool IsStructurePlacementAreaFree(Vector2Int centerCell, Structure selectedStructure)
    {
        if (!TryGetStructureFootprintCells(centerCell, selectedStructure, out List<Vector2Int> footprintCells))
        {
            return false;
        }

        for (int i = 0; i < footprintCells.Count; i++)
        {
            Vector2Int cell = footprintCells[i];
            if (!IsInsideStructureGrid(cell)) return false;
            if (occupiedStructureCells.ContainsKey(cell)) return false;
        }

        return footprintCells.Count > 0;
    }

    private bool TryGetStructureFootprintCells(Vector2Int centerCell, Structure selectedStructure, out List<Vector2Int> cells)
    {
        cells = new List<Vector2Int>();
        if (selectedStructure == null) return false;

        Vector2Int span = GetStructureCellSpan(selectedStructure);
        int width = Mathf.Max(1, span.x);
        int height = Mathf.Max(1, span.y);

        int minX = -(width / 2);
        int maxX = width - (width / 2) - 1;
        int minY = -(height / 2);
        int maxY = height - (height / 2) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                cells.Add(new Vector2Int(centerCell.x + x, centerCell.y + y));
            }
        }

        return cells.Count > 0;
    }

    private void RegisterStructureOccupiedCells(Vector2Int centerCell, Structure selectedStructure, GameObject owner)
    {
        if (!TryGetStructureFootprintCells(centerCell, selectedStructure, out List<Vector2Int> footprintCells))
        {
            return;
        }

        for (int i = 0; i < footprintCells.Count; i++)
        {
            Vector2Int cell = footprintCells[i];
            if (!IsInsideStructureGrid(cell)) continue;
            occupiedStructureCells[cell] = owner;
        }
    }

    private void PrepareStructurePlacementContext()
    {
        activeCoreStructureHost = ResolveCoreTransform(artificialSatellite);
        RebuildOccupiedStructureCells();
    }

    private void RebuildOccupiedStructureCells()
    {
        occupiedStructureCells.Clear();
        if (activeCoreStructureHost == null)
        {
            return;
        }

        StructureInstance[] installedStructures = activeCoreStructureHost.GetComponentsInChildren<StructureInstance>(true);
        for (int i = 0; i < installedStructures.Length; i++)
        {
            StructureInstance instance = installedStructures[i];
            if (instance == null || instance.SourceStructure == null) continue;
            RegisterStructureOccupiedCells(instance.InstalledCenterCell, instance.SourceStructure, instance.gameObject);
        }
    }

    private void ClearStructurePlacementSelection()
    {
        DestroyStructureGhost(false);
        ResetSelectedStructureState();
        ResetGhostPlacementState();
    }

    private void ResetStructurePlacementSession()
    {
        ClearStructurePlacementSelection();
        occupiedStructureCells.Clear();
    }

    private void ResetSelectedStructureState()
    {
        structureGhost = null;
        structure = null;
        activeCoreStructureHost = null;
    }

    private void DestroyStructureGhost(bool hideBeforeDestroy)
    {
        if (structureGhost == null) return;

        if (hideBeforeDestroy)
        {
            structureGhost.SetActive(false);
        }

        Destroy(structureGhost);
    }

    private Transform ResolveFocusedCoreTransform()
    {
        Transform focused = FocusManager.currentFocus;
        if (focused == null) return null;

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null) return null;
        if (!string.Equals(partFocus.SourcePart.partName, "Core", StringComparison.OrdinalIgnoreCase)) return null;

        if (artificialSatellite != null && partFocus.OwnerSatellite != artificialSatellite) return null;
        return focused;
    }

    private static Transform ResolveCoreTransform(ArtificialSatellite targetSatellite)
    {
        if (targetSatellite == null) return null;

        AssemblyPartFocus[] partFocuses = targetSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus partFocus = partFocuses[i];
            if (partFocus == null || partFocus.SourcePart == null) continue;
            if (string.Equals(partFocus.SourcePart.partName, "Core", StringComparison.OrdinalIgnoreCase))
            {
                return partFocus.transform;
            }
        }

        Transform[] children = targetSatellite.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == targetSatellite.transform) continue;

            string normalizedName = (child.name ?? string.Empty).Replace("(Clone)", string.Empty).Trim();
            if (string.Equals(normalizedName, "Core", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedName, "CorePrefab", StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Vector2Int GetStructureGridDimensions()
    {
        Vector2Int coreSpan = GetCoreCellSpan();
        return new Vector2Int(Mathf.Max(1, coreSpan.x), Mathf.Max(1, coreSpan.y));
    }

    private static Vector2Int GetStructureCellSpan(Structure selectedStructure)
    {
        if (selectedStructure == null)
        {
            return Vector2Int.one;
        }

        return new Vector2Int(Mathf.Max(1, selectedStructure.gridWidth), Mathf.Max(1, selectedStructure.gridHeight));
    }

    private Vector2 GetStructureSnapOffset(Structure selectedStructure)
    {
        Vector2Int span = GetStructureCellSpan(selectedStructure);
        float offsetX = (span.x % 2 == 0) ? (-cellSize * 0.5f) : 0f;
        float offsetY = (span.y % 2 == 0) ? (-cellSize * 0.5f) : 0f;
        return new Vector2(offsetX, offsetY);
    }

    private bool IsInsideStructureGrid(Vector2Int cell)
    {
        Vector2Int gridDimensions = GetStructureGridDimensions();
        return cell.x >= 0 && cell.x < gridDimensions.x && cell.y >= 0 && cell.y < gridDimensions.y;
    }

    private static Vector3 ResolveStructureSurfaceWorldPosition(Transform coreTransform)
    {
        if (coreTransform == null)
        {
            return Vector3.zero;
        }

        return coreTransform.TransformPoint(new Vector3(0f, 0f, StructureSurfaceLocalZ));
    }

    private GameObject ResolveActiveGhostRoot()
    {
        if (structureGhost != null)
        {
            return structureGhost;
        }

        return partGhost;
    }
}









