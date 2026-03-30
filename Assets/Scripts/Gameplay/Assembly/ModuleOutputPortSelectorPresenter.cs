using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(33997)]
[DisallowMultipleComponent]
public class ModuleOutputPortSelectorPresenter : FocusEventSubscriber
{
    private sealed class PortButtonView
    {
        public RectTransform rect;
        public Button button;
        public Image background;
        public Outline outline;
        public TextMeshProUGUI label;
    }

    private const string LauncherPartName = "Launcher";
    private const string OverlayRootName = "ModuleOutputPortSelectorOverlay";
    private const int PanelSortingOrderOffset = 204;
    private const float DefaultButtonSize = 24f;
    private const float ButtonDiameterRatio = 2f / 3f;
    private const float SelectedButtonScale = 1.12f;
    private const float OverlapRadius = 18f;
    private const float CellSize = 0.1f;
    private const int GridSize = 99;

    private static readonly Color IdleButtonColor = new Color(0.24f, 0.47f, 0.63f, 0.96f);
    private static readonly Color ConnectedButtonColor = new Color(0.21f, 0.62f, 0.60f, 0.96f);
    private static readonly Color ActiveButtonColor = new Color(0.92f, 0.70f, 0.56f, 0.98f);
    private static readonly Color IdleText = Color.white;
    private static readonly Color ActiveText = new Color(0.08f, 0.07f, 0.07f, 1f);
    private static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.28f);
    private static readonly Color ActiveOutlineColor = new Color(1f, 0.93f, 0.80f, 0.92f);

    private static ModuleOutputPortSelectorPresenter instance;
    private static Sprite cachedCircularButtonSprite;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisibleButton();

    private Canvas hostCanvas;
    private Canvas overlayCanvas;
    private GraphicRaycaster overlayRaycaster;
    private RectTransform overlayRoot;
    private readonly List<PortButtonView> portButtons = new List<PortButtonView>();
    private readonly List<AssemblyOutputPortFocus> resolvedOutputPorts = new List<AssemblyOutputPortFocus>();
    private readonly Dictionary<Vector2Int, int> portGroupCounts = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, int> portGroupOrders = new Dictionary<Vector2Int, int>();
    private AssemblyPartFocus focusedPart;
    private AssemblyOutputPortFocus focusedOutputPort;
    private ArtificialSatellite focusedOwnerSatellite;
    private bool isCoreContext;

    private void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
        }

        EnsureOverlay();
        SetOverlayVisible(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureOverlay();
    }

    protected override void OnDisable()
    {
        SetOverlayVisible(false);
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (overlayRoot != null)
        {
            Destroy(overlayRoot.gameObject);
            overlayRoot = null;
        }
    }

    protected override void LateUpdate()
    {
        if (overlayRoot != null && overlayRoot.gameObject.activeInHierarchy)
        {
            RefreshOverlay();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        ResolveFocusedContext(focused);
        UpdateOverlayState();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
        {
            return;
        }

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        GameObject overlayObject = CreateUiObject(OverlayRootName, hostCanvas.transform, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        overlayRoot = overlayObject.GetComponent<RectTransform>();
        ConfigureStretchRect(overlayRoot, Vector2.zero, Vector2.one);

        overlayCanvas = overlayObject.GetComponent<Canvas>();
        overlayRaycaster = overlayObject.GetComponent<GraphicRaycaster>();
        EnsureOverlayDrawOrder();
    }

    private void ResolveFocusedContext(Transform focused)
    {
        focusedPart = null;
        focusedOutputPort = null;
        focusedOwnerSatellite = null;
        isCoreContext = false;

        if (focused == null)
        {
            return;
        }

        focusedOutputPort = focused.GetComponent<AssemblyOutputPortFocus>();
        if (focusedOutputPort == null)
        {
            focusedOutputPort = focused.GetComponentInParent<AssemblyOutputPortFocus>();
        }

        if (focusedOutputPort != null)
        {
            focusedOwnerSatellite = focusedOutputPort.OwnerSatellite;
            focusedPart = focusedOutputPort.ResolveOwnerModuleFocus();
            isCoreContext = IsCoreOutputPort(focusedOutputPort) || IsCorePart(focusedPart);
            if (focusedOwnerSatellite == null || (!isCoreContext && (focusedPart == null || IsIgnoredPart(focusedPart))))
            {
                focusedOutputPort = null;
                focusedPart = null;
                focusedOwnerSatellite = null;
                isCoreContext = false;
            }
            return;
        }

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = focused.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus == null || partFocus.OwnerSatellite == null || IsIgnoredPart(partFocus))
        {
            return;
        }

        focusedPart = partFocus;
        focusedOwnerSatellite = partFocus.OwnerSatellite;
        isCoreContext = IsCorePart(partFocus);
    }

    private void UpdateOverlayState()
    {
        EnsureOverlay();
        if (overlayRoot == null)
        {
            return;
        }

        if (!HasFocusedContext())
        {
            SetOverlayVisible(false);
            return;
        }

        RefreshResolvedOutputPorts();
        EnsureOverlayDrawOrder();
        SetOverlayVisible(resolvedOutputPorts.Count > 0);
        if (resolvedOutputPorts.Count > 0)
        {
            RefreshOverlay();
        }
    }

    private void RefreshOverlay()
    {
        if (!HasFocusedContext())
        {
            SetOverlayVisible(false);
            return;
        }

        RefreshResolvedOutputPorts();
        if (resolvedOutputPorts.Count <= 0)
        {
            SetOverlayVisible(false);
            return;
        }

        SetOverlayVisible(true);
        EnsurePortButtonCount(resolvedOutputPorts.Count);
        BuildPortGrouping();

        for (int i = 0; i < portButtons.Count; i++)
        {
            bool isActive = i < resolvedOutputPorts.Count;
            PortButtonView view = portButtons[i];
            if (view == null || view.rect == null)
            {
                continue;
            }

            view.rect.gameObject.SetActive(isActive);
            if (!isActive)
            {
                continue;
            }

            AssemblyOutputPortFocus port = resolvedOutputPorts[i];
            UpdatePortButton(view, port);
        }
    }

    private void RefreshResolvedOutputPorts()
    {
        resolvedOutputPorts.Clear();
        if (focusedOwnerSatellite == null)
        {
            return;
        }

        HashSet<string> seenPortKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AssemblyOutputPortFocus[] outputPorts = focusedOwnerSatellite.GetComponentsInChildren<AssemblyOutputPortFocus>(true);
        for (int i = 0; i < outputPorts.Length; i++)
        {
            AssemblyOutputPortFocus port = outputPorts[i];
            if (port == null || !port.HasCellMapping || !MatchesFocusedContext(port))
            {
                continue;
            }

            string portKey = $"{port.MappedCell.x}:{port.MappedCell.y}|{port.SideLabel}|{port.OwnerLabel}";
            if (!seenPortKeys.Add(portKey))
            {
                continue;
            }

            resolvedOutputPorts.Add(port);
        }

        resolvedOutputPorts.Sort(CompareOutputPorts);
    }

    private void BuildPortGrouping()
    {
        portGroupCounts.Clear();
        portGroupOrders.Clear();

        for (int i = 0; i < resolvedOutputPorts.Count; i++)
        {
            AssemblyOutputPortFocus port = resolvedOutputPorts[i];
            if (port == null)
            {
                continue;
            }

            Vector2Int key = port.HasCellMapping ? port.MappedCell : new Vector2Int(int.MinValue + i, int.MaxValue - i);
            if (portGroupCounts.TryGetValue(key, out int count))
            {
                portGroupCounts[key] = count + 1;
            }
            else
            {
                portGroupCounts[key] = 1;
            }
        }
    }

    private void UpdatePortButton(PortButtonView view, AssemblyOutputPortFocus port)
    {
        if (view == null || port == null)
        {
            return;
        }

        bool isSelected = focusedOutputPort != null && focusedOutputPort == port;
        if (view.label != null)
        {
            view.label.SetText(ResolveSideShortLabel(port.SideLabel));
            view.label.color = isSelected ? ActiveText : IdleText;
        }

        if (view.background != null)
        {
            view.background.color = isSelected
                ? ActiveButtonColor
                : port.IsOccupied
                    ? ConnectedButtonColor
                    : IdleButtonColor;
        }

        if (view.outline != null)
        {
            view.outline.effectColor = isSelected ? ActiveOutlineColor : OutlineColor;
        }

        view.rect.localScale = isSelected ? Vector3.one * SelectedButtonScale : Vector3.one;
        view.button.interactable = true;

        if (!TryResolveButtonAnchoredPosition(port, out Vector2 anchoredPosition, out float buttonDiameter))
        {
            view.rect.gameObject.SetActive(false);
            return;
        }

        view.rect.sizeDelta = new Vector2(buttonDiameter, buttonDiameter);
        view.rect.anchoredPosition = anchoredPosition + ResolveGroupedOffset(port);
    }
    private bool TryResolveButtonAnchoredPosition(AssemblyOutputPortFocus port, out Vector2 anchoredPosition, out float buttonDiameter)
    {
        anchoredPosition = Vector2.zero;
        buttonDiameter = DefaultButtonSize;
        if (port == null || overlayRoot == null)
        {
            return false;
        }
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return false;
        }
        if (!TryResolvePortButtonWorldPosition(port, out Vector3 worldPosition))
        {
            return false;
        }
        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            return false;
        }
        buttonDiameter = ResolveButtonDiameter(worldCamera, port, worldPosition, screenPoint);
        Camera uiCamera = overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? overlayCanvas.worldCamera
            : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screenPoint, uiCamera, out anchoredPosition);
    }

    private static bool TryResolvePortButtonWorldPosition(AssemblyOutputPortFocus port, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (port == null || port.OwnerSatellite == null)
        {
            return false;
        }

        ArtificialSatellite ownerSatellite = port.OwnerSatellite;
        Vector3 portLocalPosition = ownerSatellite.transform.InverseTransformPoint(port.transform.position);

        if (port.HasCellMapping)
        {
            Vector3 mappedLocalPosition = GridToLocalPosition(port.MappedCell);
            mappedLocalPosition.z = portLocalPosition.z;
            worldPosition = ownerSatellite.transform.TransformPoint(mappedLocalPosition);
            return true;
        }

        Vector3 direction = ResolveSatelliteLocalDirection(port);
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        worldPosition = ownerSatellite.transform.TransformPoint(portLocalPosition + direction.normalized * (CellSize * 0.5f));
        return true;
    }
    private static float ResolveButtonDiameter(Camera worldCamera, AssemblyOutputPortFocus port, Vector3 worldPosition, Vector3 screenPoint)
    {
        if (worldCamera == null || port == null || port.OwnerSatellite == null)
        {
            return DefaultButtonSize;
        }
        ArtificialSatellite ownerSatellite = port.OwnerSatellite;
        Vector3 localCenter = ownerSatellite.transform.InverseTransformPoint(worldPosition);
        Vector3 rightWorld = ownerSatellite.transform.TransformPoint(localCenter + (Vector3.right * CellSize));
        Vector3 upWorld = ownerSatellite.transform.TransformPoint(localCenter + (Vector3.up * CellSize));
        Vector3 rightScreenPoint = worldCamera.WorldToScreenPoint(rightWorld);
        Vector3 upScreenPoint = worldCamera.WorldToScreenPoint(upWorld);
        Vector2 centerScreen = new Vector2(screenPoint.x, screenPoint.y);
        float rightDistance = rightScreenPoint.z > 0f
            ? Vector2.Distance(centerScreen, new Vector2(rightScreenPoint.x, rightScreenPoint.y))
            : 0f;
        float upDistance = upScreenPoint.z > 0f
            ? Vector2.Distance(centerScreen, new Vector2(upScreenPoint.x, upScreenPoint.y))
            : 0f;
        float cellScreenSize = 0f;
        if (rightDistance > 0f && upDistance > 0f)
        {
            cellScreenSize = Mathf.Min(rightDistance, upDistance);
        }
        else
        {
            cellScreenSize = Mathf.Max(rightDistance, upDistance);
        }
        if (cellScreenSize <= 0.0001f)
        {
            return DefaultButtonSize;
        }
        return cellScreenSize * ButtonDiameterRatio;
    }

    private static Vector3 ResolveSatelliteLocalDirection(AssemblyOutputPortFocus port)
    {
        if (port == null)
        {
            return Vector3.zero;
        }

        if (port.HasCellMapping)
        {
            Vector2Int delta = port.MappedCell - port.SourceCell;
            if (delta.x > 0) return Vector3.right;
            if (delta.x < 0) return Vector3.left;
            if (delta.y > 0) return Vector3.up;
            if (delta.y < 0) return Vector3.down;
        }

        AssemblyPort assemblyPort = port.Port;
        if (assemblyPort == null || port.OwnerSatellite == null)
        {
            return Vector3.zero;
        }

        Vector3 worldDirection = assemblyPort.transform.TransformDirection(assemblyPort.LocalDirection);
        return port.OwnerSatellite.transform.InverseTransformDirection(worldDirection);
    }

    private static Vector3 GridToLocalPosition(Vector2Int gridPosition)
    {
        int gridCenter = GridSize / 2;
        float x = (gridPosition.x - gridCenter) * CellSize;
        float y = (gridPosition.y - gridCenter) * CellSize;
        return new Vector3(x, y, 0f);
    }

    private Vector2 ResolveGroupedOffset(AssemblyOutputPortFocus port)
    {
        if (port == null || !port.HasCellMapping)
        {
            return Vector2.zero;
        }

        Vector2Int key = port.MappedCell;
        int totalCount = portGroupCounts.TryGetValue(key, out int count) ? count : 1;
        int order = portGroupOrders.TryGetValue(key, out int currentOrder) ? currentOrder : 0;
        portGroupOrders[key] = order + 1;

        if (totalCount <= 1)
        {
            return Vector2.zero;
        }

        float angleStep = 360f / totalCount;
        float angle = (-90f + (angleStep * order)) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OverlapRadius;
    }

    private int CompareOutputPorts(AssemblyOutputPortFocus a, AssemblyOutputPortFocus b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        if (!isCoreContext)
        {
            int ownerComparison = string.Compare(a.OwnerLabel, b.OwnerLabel, StringComparison.OrdinalIgnoreCase);
            if (ownerComparison != 0)
            {
                return ownerComparison;
            }
        }

        int sideComparison = ResolveSideRank(a.SideLabel).CompareTo(ResolveSideRank(b.SideLabel));
        if (sideComparison != 0)
        {
            return sideComparison;
        }

        return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveSideRank(string sideLabel)
    {
        if (string.IsNullOrWhiteSpace(sideLabel))
        {
            return 99;
        }

        switch (sideLabel.Trim().ToUpperInvariant())
        {
            case "TOP": return 0;
            case "RIGHT": return 1;
            case "BOTTOM": return 2;
            case "LEFT": return 3;
            default: return 99;
        }
    }

    private bool MatchesFocusedContext(AssemblyOutputPortFocus port)
    {
        if (port == null || port.OwnerSatellite != focusedOwnerSatellite)
        {
            return false;
        }

        if (isCoreContext)
        {
            return IsCoreOutputPort(port);
        }

        return focusedPart != null && port.OwnerPartFocus == focusedPart;
    }

    private void EnsurePortButtonCount(int requiredCount)
    {
        while (portButtons.Count < requiredCount)
        {
            int buttonIndex = portButtons.Count;
            portButtons.Add(CreatePortButton(buttonIndex));
        }
    }

    private PortButtonView CreatePortButton(int buttonIndex)
    {
        GameObject buttonObject = CreateUiObject("PortButton_" + buttonIndex, overlayRoot, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(DefaultButtonSize, DefaultButtonSize);
        buttonRect.localScale = Vector3.one;

        Image backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.sprite = ResolveCircularButtonSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = true;
        backgroundImage.color = IdleButtonColor;
        backgroundImage.raycastTarget = true;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(1.3f, -1.3f);
        outline.useGraphicAlpha = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = backgroundImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        button.onClick.AddListener(() => HandlePortButtonClicked(buttonIndex));

        TextMeshProUGUI label = CreateLabel(buttonRect, "Label", string.Empty, Vector2.zero, Vector2.one, 17f, FontStyles.Bold, TextAlignmentOptions.Center, IdleText);
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        return new PortButtonView
        {
            rect = buttonRect,
            button = button,
            background = backgroundImage,
            outline = outline,
            label = label
        };
    }

    private void HandlePortButtonClicked(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= resolvedOutputPorts.Count)
        {
            return;
        }

        AssemblyOutputPortFocus port = resolvedOutputPorts[buttonIndex];
        if (port == null)
        {
            return;
        }

        CameraManager.Instance?.RequestZoomResetOnNextFocusChange();

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            focusManager.SetFocus(port.transform);
        }
    }

    private static string ResolveSideShortLabel(string sideLabel)
    {
        if (string.IsNullOrWhiteSpace(sideLabel))
        {
            return "O";
        }

        switch (sideLabel.Trim().ToUpperInvariant())
        {
            case "TOP": return "T";
            case "RIGHT": return "R";
            case "BOTTOM": return "B";
            case "LEFT": return "L";
            default: return sideLabel.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }

    private static bool IsIgnoredPart(AssemblyPartFocus partFocus)
    {
        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null)
        {
            return true;
        }

        if (partFocus.SourcePart.partType == PartType.Pipe)
        {
            return true;
        }

        if (SolarTurbineUtility.IsSolarTurbinePart(partFocus.SourcePart))
        {
            return true;
        }

        return string.Equals(partFocus.SourcePart.partName, LauncherPartName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCorePart(AssemblyPartFocus partFocus)
    {
        return partFocus != null && partFocus.SourcePart != null && partFocus.SourcePart.partType == PartType.Core;
    }

    private static bool IsCoreOutputPort(AssemblyOutputPortFocus outputPortFocus)
    {
        if (outputPortFocus == null)
        {
            return false;
        }

        if (IsCorePart(outputPortFocus.OwnerPartFocus))
        {
            return true;
        }

        return string.Equals(outputPortFocus.OwnerLabel, "Core", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasFocusedContext()
    {
        return focusedOwnerSatellite != null && (focusedPart != null || focusedOutputPort != null);
    }

    private void EnsureOverlayDrawOrder()
    {
        if (overlayCanvas == null || hostCanvas == null)
        {
            return;
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerID = hostCanvas.sortingLayerID;
        overlayCanvas.sortingOrder = hostCanvas.sortingOrder + PanelSortingOrderOffset;
        overlayCanvas.renderMode = hostCanvas.renderMode;
        overlayCanvas.worldCamera = hostCanvas.worldCamera;
        overlayCanvas.planeDistance = hostCanvas.planeDistance;
        overlayCanvas.additionalShaderChannels = hostCanvas.additionalShaderChannels;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayRoot == null)
        {
            return;
        }

        if (overlayRoot.gameObject.activeSelf != visible)
        {
            overlayRoot.gameObject.SetActive(visible);
        }

        if (overlayRaycaster != null)
        {
            overlayRaycaster.enabled = visible;
        }
    }

    private bool IsPointerOverVisibleButton()
    {
        if (overlayRoot == null || !overlayRoot.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Camera uiCamera = overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? overlayCanvas.worldCamera
            : null;

        for (int i = 0; i < portButtons.Count; i++)
        {
            PortButtonView view = portButtons[i];
            if (view == null || view.rect == null || !view.rect.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(view.rect, pointerPosition, uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject labelObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        ConfigureStretchRect(labelRect, anchorMin, anchorMax);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void ConfigureStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Sprite ResolveCircularButtonSprite()
    {
        if (cachedCircularButtonSprite != null)
        {
            return cachedCircularButtonSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "ModuleOutputPortSelectorCircle"
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[textureSize * textureSize];
        float radius = (textureSize - 4f) * 0.5f;
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                int index = y * textureSize + x;
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = distance <= radius ? (byte)255 : (byte)0;
                pixels[index] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        cachedCircularButtonSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        return cachedCircularButtonSprite;
    }
}
