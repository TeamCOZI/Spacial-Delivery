using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Assembly
{
    private const float SelectionDragThresholdPixels = 6f;
    private static readonly Color SelectionValidColor = new Color32(255, 255, 255, 63);
    private static readonly Color SelectionBoxFillColor = new Color32(255, 255, 255, 24);
    private static readonly Color SelectionBoxOutlineColor = new Color32(255, 255, 255, 160);

    private bool isSelectionPointerDown = false;
    private bool isSelectionDragActive = false;
    private Vector2 selectionDragStartScreen = Vector2.zero;
    private Vector2 selectionDragCurrentScreen = Vector2.zero;
    private Vector2Int selectionDragStartCell = default;
    private Vector2Int selectionDragCurrentCell = default;
    private readonly HashSet<GameObject> selectionRectOwners = new HashSet<GameObject>();
    private Canvas selectionDragCanvas;
    private RectTransform selectionDragBoxRect;
    private Image selectionDragBoxImage;
    private Outline selectionDragBoxOutline;

    private void HandleSelectionRectangleInput()
    {
        if (Mouse.current == null)
        {
            ResetSelectionDragState();
            return;
        }

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        if (UserInput.IsWorldInputBlockedByUiPanels() && !isSelectionPointerDown)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryEnsureSelectionDragVisual();
            if (!TryGetHoveredGridCell(out Vector2Int startCell))
            {
                ResetSelectionDragState();
                return;
            }

            isSelectionPointerDown = true;
            isSelectionDragActive = false;
            selectionDragStartScreen = mouseScreen;
            selectionDragCurrentScreen = mouseScreen;
            selectionDragStartCell = startCell;
            selectionDragCurrentCell = startCell;
            UpdateSelectionDragVisual();
        }

        if (isSelectionPointerDown)
        {
            selectionDragCurrentScreen = mouseScreen;
            if (TryGetClampedSelectionDragCell(out Vector2Int currentCell))
            {
                selectionDragCurrentCell = currentCell;
            }

            if (!isSelectionDragActive)
            {
                float dragThresholdSqr = SelectionDragThresholdPixels * SelectionDragThresholdPixels;
                bool movedAcrossCells = selectionDragCurrentCell != selectionDragStartCell;
                bool movedEnoughOnScreen = (selectionDragCurrentScreen - selectionDragStartScreen).sqrMagnitude >= dragThresholdSqr;
                if (movedAcrossCells || movedEnoughOnScreen)
                {
                    isSelectionDragActive = true;
                }
            }

            UpdateSelectionDragVisual();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isSelectionPointerDown)
        {
            selectionDragCurrentScreen = mouseScreen;
            if (TryGetClampedSelectionDragCell(out Vector2Int releasedCell))
            {
                selectionDragCurrentCell = releasedCell;
            }

            CommitCurrentRectangleSelection();
            ResetSelectionDragState();
        }
    }

    private void PopulateSelectionPreviewOwners()
    {
        selectionPreviewOwners.Clear();

        if (isSelectionDragActive)
        {
            BuildSelectionOwnersFromCurrentRectangle(selectionPreviewOwners);
            return;
        }

        foreach (GameObject selected in selectedOwners)
        {
            if (selected != null)
            {
                selectionPreviewOwners.Add(selected);
            }
        }
    }

    private void CommitCurrentRectangleSelection()
    {
        selectedOwners.Clear();

        if (isSelectionDragActive)
        {
            selectionPreviewOwners.Clear();
            BuildSelectionOwnersFromCurrentRectangle(selectionPreviewOwners);
            for (int i = 0; i < selectionPreviewOwners.Count; i++)
            {
                GameObject owner = selectionPreviewOwners[i];
                if (owner != null)
                {
                    selectedOwners.Add(owner);
                }
            }
            return;
        }

        if (TryGetSelectablePartUnderMouse(out GameObject clickedOwner) && clickedOwner != null)
        {
            selectedOwners.Add(clickedOwner);
        }
    }

    private void BuildSelectionOwnersFromCurrentRectangle(List<GameObject> results)
    {
        if (results == null) return;

        results.Clear();
        selectionRectOwners.Clear();
        if (!TryGetCurrentSelectionGridBounds(out Vector2Int minCell, out Vector2Int maxCell)) return;

        foreach (KeyValuePair<Vector2Int, GameObject> pair in occupiedCells)
        {
            if (pair.Key.x < minCell.x || pair.Key.x > maxCell.x) continue;
            if (pair.Key.y < minCell.y || pair.Key.y > maxCell.y) continue;

            GameObject owner = ResolveRemovablePartRoot(pair.Value);
            if (!CanSelectPart(owner)) continue;
            if (selectionRectOwners.Contains(owner)) continue;

            if (selectionRectOwners.Add(owner))
            {
                results.Add(owner);
            }
        }
    }

    private bool TryGetCurrentSelectionGridBounds(out Vector2Int minCell, out Vector2Int maxCell)
    {
        minCell = default;
        maxCell = default;
        if (!isSelectionPointerDown && !isSelectionDragActive) return false;

        minCell = new Vector2Int(
            Mathf.Min(selectionDragStartCell.x, selectionDragCurrentCell.x),
            Mathf.Min(selectionDragStartCell.y, selectionDragCurrentCell.y));
        maxCell = new Vector2Int(
            Mathf.Max(selectionDragStartCell.x, selectionDragCurrentCell.x),
            Mathf.Max(selectionDragStartCell.y, selectionDragCurrentCell.y));
        return IsInsideGrid(minCell) && IsInsideGrid(maxCell);
    }

    private bool TryGetCurrentSelectionScreenRect(out Rect screenRect)
    {
        screenRect = default;
        if (artificialSatellite == null) return false;
        if (!TryEnsureMainCamera()) return false;
        if (!TryGetCurrentSelectionGridBounds(out Vector2Int minCell, out Vector2Int maxCell)) return false;

        float halfCell = cellSize * 0.5f;
        Vector3 minCenter = GridToLocalPosition(minCell, ZeroSnapOffset);
        Vector3 maxCenter = GridToLocalPosition(maxCell, ZeroSnapOffset);
        Vector3[] localCorners =
        {
            new Vector3(minCenter.x - halfCell, minCenter.y - halfCell, minCenter.z),
            new Vector3(minCenter.x - halfCell, maxCenter.y + halfCell, minCenter.z),
            new Vector3(maxCenter.x + halfCell, maxCenter.y + halfCell, minCenter.z),
            new Vector3(maxCenter.x + halfCell, minCenter.y - halfCell, minCenter.z)
        };

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(
                artificialSatellite.transform.TransformPoint(localCorners[i]));
            if (screenPoint.z < MinCameraSpaceDepth) return false;

            minX = Mathf.Min(minX, screenPoint.x);
            minY = Mathf.Min(minY, screenPoint.y);
            maxX = Mathf.Max(maxX, screenPoint.x);
            maxY = Mathf.Max(maxY, screenPoint.y);
        }

        screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return screenRect.width > 0.001f || screenRect.height > 0.001f;
    }

    private bool TryGetClampedSelectionDragCell(out Vector2Int cell)
    {
        cell = default;
        if (!TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)) return false;

        cell = LocalPositionToGrid(localPoint, ZeroSnapOffset);
        cell.x = Mathf.Clamp(cell.x, 0, gridSize - 1);
        cell.y = Mathf.Clamp(cell.y, 0, gridSize - 1);
        return true;
    }

    private void ResetSelectionDragState()
    {
        isSelectionPointerDown = false;
        isSelectionDragActive = false;
        selectionDragStartScreen = Vector2.zero;
        selectionDragCurrentScreen = Vector2.zero;
        selectionDragStartCell = default;
        selectionDragCurrentCell = default;
        selectionRectOwners.Clear();
        SetSelectionDragVisualVisible(false);
    }

    private void UpdateSelectionDragVisual()
    {
        if (!isSelectionDragActive || !TryGetCurrentSelectionScreenRect(out Rect screenRect))
        {
            SetSelectionDragVisualVisible(false);
            return;
        }

        if (!TryEnsureSelectionDragVisual())
        {
            return;
        }

        RectTransform canvasRect = selectionDragCanvas != null ? selectionDragCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            SetSelectionDragVisualVisible(false);
            return;
        }

        Camera uiCamera = selectionDragCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : selectionDragCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(screenRect.xMin, screenRect.yMin), uiCamera, out Vector2 minLocal))
        {
            SetSelectionDragVisualVisible(false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(screenRect.xMax, screenRect.yMax), uiCamera, out Vector2 maxLocal))
        {
            SetSelectionDragVisualVisible(false);
            return;
        }

        Vector2 lowerLeft = Vector2.Min(minLocal, maxLocal);
        Vector2 upperRight = Vector2.Max(minLocal, maxLocal);
        selectionDragBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        selectionDragBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        selectionDragBoxRect.pivot = new Vector2(0.5f, 0.5f);
        selectionDragBoxRect.anchoredPosition = (lowerLeft + upperRight) * 0.5f;
        selectionDragBoxRect.sizeDelta = upperRight - lowerLeft;
        SetSelectionDragVisualVisible(true);
    }

    private bool TryEnsureSelectionDragVisual()
    {
        if (selectionDragBoxRect != null && selectionDragCanvas != null)
        {
            return true;
        }

        Canvas canvas = UIRootLocator.ResolvePrimaryCanvas();
        if (canvas == null) return false;

        selectionDragCanvas = canvas;

        Transform existing = canvas.transform.Find("AssemblySelectionDragBox");
        GameObject boxObject = existing != null ? existing.gameObject : null;
        if (boxObject == null)
        {
            boxObject = new GameObject("AssemblySelectionDragBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            boxObject.transform.SetParent(canvas.transform, false);
        }

        selectionDragBoxRect = boxObject.GetComponent<RectTransform>();
        if (selectionDragBoxRect == null) selectionDragBoxRect = boxObject.AddComponent<RectTransform>();

        selectionDragBoxImage = boxObject.GetComponent<Image>();
        if (selectionDragBoxImage == null) selectionDragBoxImage = boxObject.AddComponent<Image>();
        selectionDragBoxImage.color = SelectionBoxFillColor;
        selectionDragBoxImage.raycastTarget = false;

        selectionDragBoxOutline = boxObject.GetComponent<Outline>();
        if (selectionDragBoxOutline == null) selectionDragBoxOutline = boxObject.AddComponent<Outline>();
        selectionDragBoxOutline.effectColor = SelectionBoxOutlineColor;
        selectionDragBoxOutline.effectDistance = new Vector2(1f, -1f);
        selectionDragBoxOutline.useGraphicAlpha = true;

        boxObject.transform.SetAsLastSibling();
        SetSelectionDragVisualVisible(false);
        return true;
    }

    private void SetSelectionDragVisualVisible(bool visible)
    {
        if (selectionDragBoxRect == null) return;
        if (selectionDragBoxRect.gameObject.activeSelf == visible) return;
        selectionDragBoxRect.gameObject.SetActive(visible);
    }

    private void DisposeSelectionDragVisual()
    {
        if (selectionDragBoxRect != null)
        {
            Object.Destroy(selectionDragBoxRect.gameObject);
        }

        selectionDragBoxRect = null;
        selectionDragBoxImage = null;
        selectionDragBoxOutline = null;
        selectionDragCanvas = null;
    }
}
