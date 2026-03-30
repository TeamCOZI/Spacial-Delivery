using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

[DefaultExecutionOrder(34005)]
[DisallowMultipleComponent]
public class MergePipePresenter : FocusEventSubscriber
{
    private sealed class DirectionView
    {
        public RectTransform container;
        public Image background;
        public Outline outline;
        public TextMeshProUGUI label;
    }

    private const string PanelObjectName = "MergePipeUIPanel";
    private const int PanelSortingOrderOffset = 207;
    private const float CellSize = 0.1f;
    private const int GridSize = 99;
    private const string TitleText = "합류관";
    private const string RecoverText = "아이템 회수";
    private const string DefaultTransportText = "수송\n아이템";
    private const string EmptyTransportText = "없음";
    private const string NoRouteText = "연결 경로 없음";
    private const string InputText = "Input";
    private const string OutputText = "Output";

    private static readonly string[] KoreanFontCandidates =
    {
        "Malgun Gothic",
        "맑은 고딕",
        "Noto Sans KR",
        "Noto Sans CJK KR",
        "Arial Unicode MS"
    };

    private static readonly AssemblyPartPortLayout.PortSide[] MergeInputLocalSides =
    {
        AssemblyPartPortLayout.PortSide.Left,
        AssemblyPartPortLayout.PortSide.Top,
        AssemblyPartPortLayout.PortSide.Bottom
    };

    private static readonly Color PanelBackgroundColor = new Color(0.53f, 0.53f, 0.53f, 0.96f);
    private static readonly Color PanelBorderColor = new Color(0.92f, 0.92f, 0.92f, 0.28f);
    private static readonly Color OutputArrowColor = new Color(0.30f, 0.64f, 0.89f, 0.98f);
    private static readonly Color InputArrowColor = new Color(0.96f, 0.17f, 0.12f, 0.98f);
    private static readonly Color InputArrowSelectedColor = new Color(1f, 0.36f, 0.28f, 1f);
    private static readonly Color TransportCircleColor = new Color(0.90f, 0.77f, 0.91f, 1f);
    private static readonly Color ActionButtonColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color ActionButtonActiveColor = new Color(0.16f, 0.60f, 0.48f, 0.98f);
    private static readonly Color CloseButtonColor = new Color(0.97f, 0.20f, 0.13f, 1f);
    private static readonly Color DirectionOutlineColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color SelectedDirectionOutlineColor = new Color(1f, 1f, 1f, 0.38f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);

    private static MergePipePresenter instance;
    private static Sprite cachedCircularSprite;
    private static Sprite cachedTriangleSprite;
    private static TMP_FontAsset cachedDisplayFont;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private GraphicRaycaster panelRaycaster;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI transportItemLabel;
    private Button recoverButton;
    private Image recoverButtonBackground;
    private Button onButton;
    private Image onButtonBackground;
    private Button offButton;
    private Image offButtonBackground;
    private DirectionView topDirectionView;
    private DirectionView rightDirectionView;
    private DirectionView bottomDirectionView;
    private DirectionView leftDirectionView;
    private readonly List<Vector3> routePointBuffer = new List<Vector3>();
    private readonly HashSet<AssemblyPartPortLayout.PortSide> inputSidesBuffer = new HashSet<AssemblyPartPortLayout.PortSide>();
    private AssemblyPartFocus focusedMergePipePart;
    private MergePipeState focusedMergePipeState;
    private OutputPortProductionState selectedProductionState;

    private void Awake()
    {
        if (instance == null || instance == this)
        {
            instance = this;
        }

        EnsurePanel();
        SetPanelVisible(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsurePanel();
    }

    protected override void OnDisable()
    {
        SetPanelVisible(false);
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (panelRect != null)
        {
            Destroy(panelRect.gameObject);
            panelRect = null;
        }
    }

    protected override void LateUpdate()
    {
        if (panelRect != null && panelRect.gameObject.activeInHierarchy)
        {
            RefreshPanelContent();
        }
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        focusedMergePipePart = TryResolveFocusedMergePipe(focused, out AssemblyPartFocus partFocus) ? partFocus : null;
        focusedMergePipeState = focusedMergePipePart != null ? MergePipeUtility.ResolveState(focusedMergePipePart.gameObject) : null;
        UpdatePanelState();
    }

    private void EnsurePanel()
    {
        if (panelRect != null)
        {
            return;
        }

        hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        GameObject panelObject = CreateUiObject(PanelObjectName, hostCanvas.transform, typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.44f, 0.06f);
        panelRect.anchorMax = new Vector2(0.96f, 0.94f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, PanelBorderColor, new Vector2(2f, -2f));

        panelCanvas = panelObject.GetComponent<Canvas>();
        panelRaycaster = panelObject.GetComponent<GraphicRaycaster>();
        EnsurePanelDrawOrder();

        titleLabel = CreateLabel(panelRect, "MergePipeTitle", TitleText, new Vector2(0.03f, 0.93f), new Vector2(0.45f, 0.99f), 24f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        statusLabel = CreateLabel(panelRect, "MergePipeStatus", NoRouteText, new Vector2(0.03f, 0.88f), new Vector2(0.74f, 0.93f), 12f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        Button closeButton = CreateButton(panelRect, "MergePipeClose", "X", new Vector2(0.90f, 0.91f), new Vector2(0.985f, 0.995f), 18f, out _, CloseButtonColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);

        topDirectionView = CreateDirectionView(panelRect, "MergePipeDirectionTop", new Vector2(0.42f, 0.81f), new Vector2(0.58f, 0.96f), 0f);
        leftDirectionView = CreateDirectionView(panelRect, "MergePipeDirectionLeft", new Vector2(0.07f, 0.60f), new Vector2(0.25f, 0.78f), 90f);
        rightDirectionView = CreateDirectionView(panelRect, "MergePipeDirectionRight", new Vector2(0.75f, 0.60f), new Vector2(0.93f, 0.78f), -90f);
        bottomDirectionView = CreateDirectionView(panelRect, "MergePipeDirectionBottom", new Vector2(0.42f, 0.45f), new Vector2(0.58f, 0.60f), 180f);

        RectTransform transportCircle = CreateSection(panelRect, "MergePipeTransportCircle", new Vector2(0.37f, 0.57f), new Vector2(0.63f, 0.81f), TransportCircleColor, true);
        transportItemLabel = CreateLabel(transportCircle, "MergePipeTransportItem", DefaultTransportText, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), 28f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        recoverButton = CreateButton(panelRect, "MergePipeRecoverButton", RecoverText, new Vector2(0.31f, 0.07f), new Vector2(0.69f, 0.44f), 24f, out recoverButtonBackground, ActionButtonColor, WhiteText, false);
        recoverButton.onClick.AddListener(HandleRecoverClicked);

        onButton = CreateButton(panelRect, "MergePipeOnButton", "ON", new Vector2(0.80f, 0.25f), new Vector2(0.97f, 0.44f), 20f, out onButtonBackground, ActionButtonColor, WhiteText, false);
        onButton.onClick.AddListener(HandleOnClicked);

        offButton = CreateButton(panelRect, "MergePipeOffButton", "OFF", new Vector2(0.80f, 0.07f), new Vector2(0.97f, 0.24f), 20f, out offButtonBackground, ActionButtonColor, WhiteText, false);
        offButton.onClick.AddListener(HandleOffClicked);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveCloseFocus();
        focusedMergePipePart = null;
        focusedMergePipeState = null;
        selectedProductionState = null;
        SetPanelVisible(false);

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager))
        {
            if (nextFocus != null)
            {
                CameraManager.Instance?.RequestZoomResetOnNextFocusChange();
            }

            focusManager.SetFocus(nextFocus);
        }
    }

    private void HandleRecoverClicked()
    {
        if (selectedProductionState == null)
        {
            return;
        }

        _ = selectedProductionState.TryRecallPacketsToCoreLogistics(out _);
        RefreshPanelContent();
    }

    private void HandleOnClicked()
    {
        if (selectedProductionState == null)
        {
            return;
        }

        _ = selectedProductionState.RequestStart();
        RefreshPanelContent();
    }

    private void HandleOffClicked()
    {
        if (selectedProductionState == null || !selectedProductionState.IsRunning)
        {
            return;
        }

        selectedProductionState.ToggleRunning();
        RefreshPanelContent();
    }

    private Transform ResolveCloseFocus()
    {
        return focusedMergePipePart != null && focusedMergePipePart.OwnerSatellite != null ? focusedMergePipePart.OwnerSatellite.transform : null;
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        bool shouldShow = focusedMergePipePart != null && focusedMergePipeState != null;
        SetPanelVisible(shouldShow);
        if (shouldShow)
        {
            RefreshPanelContent();
        }
    }

    private void RefreshPanelContent()
    {
        if (focusedMergePipePart == null || focusedMergePipeState == null || !MergePipeUtility.IsMergePipePart(focusedMergePipePart.SourcePart))
        {
            SetPanelVisible(false);
            return;
        }

        titleLabel.SetText(TitleText);
        PopulateRouteContext();
        RefreshDirectionDisplay();
        UpdateButtons();
    }

    private void PopulateRouteContext()
    {
        selectedProductionState = null;
        string transportName = DefaultTransportText;
        string currentStatus = NoRouteText;

        if (focusedMergePipePart == null || focusedMergePipePart.OwnerSatellite == null)
        {
            ApplyRouteDisplay(transportName, currentStatus);
            return;
        }

        Vector2Int focusedCell = LocalPositionToGrid(focusedMergePipePart.transform.localPosition);
        OutputPortProductionState[] states = focusedMergePipePart.OwnerSatellite.GetComponentsInChildren<OutputPortProductionState>(true);
        int bestScore = int.MinValue;
        for (int i = 0; i < states.Length; i++)
        {
            OutputPortProductionState candidateState = states[i];
            if (candidateState == null || candidateState.OutputPortFocus == null)
            {
                continue;
            }

            routePointBuffer.Clear();
            if (!OutputPortTransferUtility.TryResolveTransferRoute(candidateState.OutputPortFocus, routePointBuffer, out int pipeCellCount, out _, out _))
            {
                continue;
            }

            if (!RouteContainsPipeCell(routePointBuffer, focusedCell))
            {
                continue;
            }

            int score = BuildCandidateScore(candidateState, pipeCellCount);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            selectedProductionState = candidateState;
            transportName = ResolveTransportItemLabel(candidateState);
            currentStatus = candidateState.StatusLabel;
        }

        ApplyRouteDisplay(transportName, currentStatus);
    }

    private void ApplyRouteDisplay(string transportName, string currentStatus)
    {
        transportItemLabel.SetText(string.IsNullOrWhiteSpace(transportName) ? DefaultTransportText : transportName);
        statusLabel.SetText(string.IsNullOrWhiteSpace(currentStatus) ? NoRouteText : currentStatus);
    }

    private void RefreshDirectionDisplay()
    {
        inputSidesBuffer.Clear();
        BuildWorldMergePortSides(focusedMergePipePart != null ? focusedMergePipePart.transform.localRotation : Quaternion.identity, inputSidesBuffer, out AssemblyPartPortLayout.PortSide outputSide);
        bool hasSelectedInput = TryResolveSelectedInputSide(inputSidesBuffer, out AssemblyPartPortLayout.PortSide selectedInputSide);
        ApplyDirectionView(topDirectionView, AssemblyPartPortLayout.PortSide.Top, outputSide, inputSidesBuffer, hasSelectedInput, selectedInputSide);
        ApplyDirectionView(rightDirectionView, AssemblyPartPortLayout.PortSide.Right, outputSide, inputSidesBuffer, hasSelectedInput, selectedInputSide);
        ApplyDirectionView(bottomDirectionView, AssemblyPartPortLayout.PortSide.Bottom, outputSide, inputSidesBuffer, hasSelectedInput, selectedInputSide);
        ApplyDirectionView(leftDirectionView, AssemblyPartPortLayout.PortSide.Left, outputSide, inputSidesBuffer, hasSelectedInput, selectedInputSide);
    }

    private void ApplyDirectionView(DirectionView view, AssemblyPartPortLayout.PortSide side, AssemblyPartPortLayout.PortSide outputSide, HashSet<AssemblyPartPortLayout.PortSide> inputSides, bool hasSelectedInput, AssemblyPartPortLayout.PortSide selectedInputSide)
    {
        if (view == null || view.container == null)
        {
            return;
        }

        bool isOutput = side == outputSide;
        bool isInput = inputSides != null && inputSides.Contains(side);
        bool visible = isInput || isOutput;
        view.container.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        bool isSelectedInput = isInput && hasSelectedInput && side == selectedInputSide;
        view.label.SetText(isOutput ? OutputText : InputText);
        view.background.color = isOutput ? OutputArrowColor : (isSelectedInput ? InputArrowSelectedColor : InputArrowColor);
        if (view.outline != null)
        {
            view.outline.effectColor = isSelectedInput ? SelectedDirectionOutlineColor : DirectionOutlineColor;
            view.outline.effectDistance = isSelectedInput ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        view.container.localScale = isSelectedInput ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
    }

    private bool TryResolveSelectedInputSide(HashSet<AssemblyPartPortLayout.PortSide> inputSides, out AssemblyPartPortLayout.PortSide selectedInputSide)
    {
        selectedInputSide = AssemblyPartPortLayout.PortSide.Left;
        if (focusedMergePipeState == null || inputSides == null || inputSides.Count <= 0)
        {
            return false;
        }

        List<AssemblyPartPortLayout.PortSide> previewSides = new List<AssemblyPartPortLayout.PortSide>(inputSides);
        return focusedMergePipeState.TrySelectPreviewInputSide(previewSides, out selectedInputSide);
    }

    private void UpdateButtons()
    {
        bool hasState = selectedProductionState != null;
        bool canRecover = hasState && selectedProductionState.InTransitCount > 0;
        bool canTurnOn = hasState && !selectedProductionState.IsRunning && selectedProductionState.HasAssignedResource;
        bool canTurnOff = hasState && selectedProductionState.IsRunning;

        if (recoverButton != null) recoverButton.interactable = canRecover;
        if (onButton != null) onButton.interactable = canTurnOn;
        if (offButton != null) offButton.interactable = canTurnOff;
        if (recoverButtonBackground != null) recoverButtonBackground.color = canRecover ? ActionButtonActiveColor : ActionButtonColor;
        if (onButtonBackground != null) onButtonBackground.color = canTurnOff ? ActionButtonColor : (canTurnOn ? ActionButtonActiveColor : ActionButtonColor);
        if (offButtonBackground != null) offButtonBackground.color = canTurnOff ? ActionButtonActiveColor : ActionButtonColor;
    }

    private bool IsPointerOverVisiblePanel()
    {
        if (panelRect == null || !panelRect.gameObject.activeInHierarchy || Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Camera uiCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? panelCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPosition, uiCamera);
    }

    private void EnsurePanelDrawOrder()
    {
        if (panelCanvas == null || hostCanvas == null)
        {
            return;
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingLayerID = hostCanvas.sortingLayerID;
        panelCanvas.sortingOrder = hostCanvas.sortingOrder + PanelSortingOrderOffset;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect == null)
        {
            return;
        }

        if (panelRect.gameObject.activeSelf != visible)
        {
            panelRect.gameObject.SetActive(visible);
        }

        if (panelRaycaster != null)
        {
            panelRaycaster.enabled = visible;
        }
    }

    private static bool TryResolveFocusedMergePipe(Transform focused, out AssemblyPartFocus partFocus)
    {
        partFocus = null;
        if (focused == null || focused.GetComponentInParent<AssemblyOutputPortFocus>() != null)
        {
            return false;
        }

        partFocus = focused.GetComponent<AssemblyPartFocus>() ?? focused.GetComponentInParent<AssemblyPartFocus>();
        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null)
        {
            partFocus = null;
            return false;
        }

        if (!MergePipeUtility.IsMergePipePart(partFocus.SourcePart))
        {
            partFocus = null;
            return false;
        }

        return true;
    }

    private static int BuildCandidateScore(OutputPortProductionState state, int pipeCellCount)
    {
        int score = state.IsRunning ? 1000 : 0;
        score += state.InTransitCount * 100;
        if (state.HasAssignedResource)
        {
            score += 10;
        }

        return score - Mathf.Max(0, pipeCellCount);
    }

    private static bool RouteContainsPipeCell(List<Vector3> localPoints, Vector2Int focusedCell)
    {
        if (localPoints == null)
        {
            return false;
        }

        for (int i = 0; i < localPoints.Count; i++)
        {
            if (LocalPositionToGrid(localPoints[i]) == focusedCell)
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveTransportItemLabel(OutputPortProductionState state)
    {
        if (state == null)
        {
            return DefaultTransportText;
        }

        InventoryResourceType resourceType = state.HasAssignedResource ? state.AssignedResourceType : state.ProducedResourceType;
        string resourceName = InventoryResourceCatalog.GetDisplayName(resourceType).Replace("_", " ");
        return string.IsNullOrWhiteSpace(resourceName) ? EmptyTransportText : resourceName;
    }

    private static void BuildWorldMergePortSides(Quaternion localRotation, HashSet<AssemblyPartPortLayout.PortSide> inputSides, out AssemblyPartPortLayout.PortSide outputSide)
    {
        inputSides?.Clear();
        int quarterTurns = AssemblyMathUtility.GetQuarterTurns(localRotation);
        outputSide = RotateSide(AssemblyPartPortLayout.PortSide.Right, quarterTurns);
        if (inputSides == null)
        {
            return;
        }

        for (int i = 0; i < MergeInputLocalSides.Length; i++)
        {
            inputSides.Add(RotateSide(MergeInputLocalSides[i], quarterTurns));
        }
    }

    private static AssemblyPartPortLayout.PortSide RotateSide(AssemblyPartPortLayout.PortSide side, int quarterTurns)
    {
        return ConvertDirectionToSide(AssemblyMathUtility.RotateCellOffset(ConvertSideToDirection(side), quarterTurns));
    }

    private static Vector2Int ConvertSideToDirection(AssemblyPartPortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top: return Vector2Int.up;
            case AssemblyPartPortLayout.PortSide.Bottom: return Vector2Int.down;
            case AssemblyPartPortLayout.PortSide.Left: return Vector2Int.left;
            case AssemblyPartPortLayout.PortSide.Right: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private static AssemblyPartPortLayout.PortSide ConvertDirectionToSide(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return AssemblyPartPortLayout.PortSide.Top;
        if (direction == Vector2Int.down) return AssemblyPartPortLayout.PortSide.Bottom;
        if (direction == Vector2Int.left) return AssemblyPartPortLayout.PortSide.Left;
        return AssemblyPartPortLayout.PortSide.Right;
    }

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition)
    {
        int center = GridSize / 2;
        return new Vector2Int(Mathf.RoundToInt(localPosition.x / CellSize) + center, Mathf.RoundToInt(localPosition.y / CellSize) + center);
    }

    private static RectTransform CreateSection(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color backgroundColor, bool circular)
    {
        GameObject sectionObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image));
        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        ConfigureStretchRect(sectionRect, anchorMin, anchorMax);
        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = backgroundColor;
        if (circular)
        {
            sectionImage.sprite = ResolveCircularSprite();
            sectionImage.preserveAspect = true;
        }
        else
        {
            ApplyOutline(sectionObject, DirectionOutlineColor, Vector2.zero);
        }

        return sectionRect;
    }

    private static DirectionView CreateDirectionView(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float rotationZ)
    {
        GameObject containerObject = CreateUiObject(name, parent, typeof(RectTransform));
        RectTransform containerRect = containerObject.GetComponent<RectTransform>();
        ConfigureStretchRect(containerRect, anchorMin, anchorMax);

        GameObject backgroundObject = CreateUiObject(name + "Background", containerRect, typeof(RectTransform), typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        ConfigureStretchRect(backgroundRect, Vector2.zero, Vector2.one);
        backgroundRect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = ResolveTriangleSprite();
        backgroundImage.preserveAspect = true;
        backgroundImage.color = InputArrowColor;

        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(backgroundObject);
        outline.effectColor = DirectionOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        return new DirectionView
        {
            container = containerRect,
            background = backgroundImage,
            outline = outline,
            label = CreateLabel(containerRect, name + "Label", InputText, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText)
        };
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
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
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(7f, fontSize * 0.5f);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(label);
        return label;
    }

    private static void ApplyFont(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        TMP_FontAsset font = ResolveDisplayFont();
        if (font != null)
        {
            label.font = font;
        }
    }

    private static TMP_FontAsset ResolveDisplayFont()
    {
        if (cachedDisplayFont != null)
        {
            return cachedDisplayFont;
        }

        Font osFont = Font.CreateDynamicFontFromOSFont(KoreanFontCandidates, 32);
        if (osFont != null)
        {
            cachedDisplayFont = TMP_FontAsset.CreateFontAsset(
                osFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (cachedDisplayFont != null)
            {
                return cachedDisplayFont;
            }
        }

        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
        {
            cachedDisplayFont = focusManager.focusInfoText.font;
            return cachedDisplayFont;
        }

        cachedDisplayFont = TMP_Settings.defaultFontAsset;
        if (cachedDisplayFont != null)
        {
            return cachedDisplayFont;
        }

        cachedDisplayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return cachedDisplayFont;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, float fontSize, out Image backgroundImage, Color backgroundColor, Color textColor, bool circular)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);
        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        if (circular)
        {
            backgroundImage.sprite = ResolveCircularSprite();
            backgroundImage.preserveAspect = true;
        }
        else
        {
            ApplyOutline(buttonObject, DirectionOutlineColor, Vector2.zero);
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;
        _ = CreateLabel(buttonRect, name + "Label", labelText, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        return button;
    }

    private static Sprite ResolveCircularSprite()
    {
        if (cachedCircularSprite != null)
        {
            return cachedCircularSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }

        texture.Apply();
        cachedCircularSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircularSprite;
    }

    private static Sprite ResolveTriangleSprite()
    {
        if (cachedTriangleSprite != null)
        {
            return cachedTriangleSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 a = new Vector2(size * 0.5f, size - 6f);
        Vector2 b = new Vector2(6f, 8f);
        Vector2 c = new Vector2(size - 6f, 8f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = !(Sign(point, a, b) < 0f || Sign(point, b, c) < 0f || Sign(point, c, a) < 0f) || !(Sign(point, a, b) > 0f || Sign(point, b, c) > 0f || Sign(point, c, a) > 0f);
                texture.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }

        texture.Apply();
        cachedTriangleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedTriangleSprite;
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 effectDistance)
    {
        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(target);
        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = true;
    }

    private static void ConfigureStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
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
}
