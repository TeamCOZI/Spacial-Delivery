using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

[DefaultExecutionOrder(34003)]
[DisallowMultipleComponent]
public class PipePresenter : FocusEventSubscriber
{
    private const string PanelObjectName = "PipeUIPanel";
    private const int PanelSortingOrderOffset = 207;
    private const float CellSize = 0.1f;
    private const int GridSize = 99;

    private const string TitleText = "\uC218\uC1A1\uAD00";
    private const string RecoverText = "\uC544\uC774\uD15C \uD68C\uC218";
    private const string DefaultTransportText = "\uC218\uC1A1 \uC544\uC774\uD15C";
    private const string EmptyTransportText = "\uC5C6\uC74C";
    private const string NoRouteText = "\uC5F0\uACB0 \uACBD\uB85C \uC5C6\uC74C";
    private const string SenderPlaceholderText = "OUTPUT\nMODULE";
    private const string ReceiverPlaceholderText = "INPUT\nMODULE";
    private const string SenderCapacityFormat = "OUTPUT {0} / {1}";
    private const string ReceiverCapacityFormat = "INPUT {0} / {1}";

    private static readonly string[] KoreanFontCandidates =
    {
        "Malgun Gothic",
        "\uB9D1\uC740 \uACE0\uB515",
        "Noto Sans KR",
        "Noto Sans CJK KR",
        "Arial Unicode MS"
    };

    private static readonly Color PanelBackgroundColor = new Color(0.53f, 0.53f, 0.53f, 0.96f);
    private static readonly Color PanelBorderColor = new Color(0.92f, 0.92f, 0.92f, 0.28f);
    private static readonly Color SenderCardColor = new Color(0.45f, 0.73f, 0.91f, 0.98f);
    private static readonly Color ReceiverCardColor = new Color(0.96f, 0.17f, 0.12f, 0.98f);
    private static readonly Color TransportCircleColor = new Color(0.88f, 0.76f, 0.90f, 1f);
    private static readonly Color ActionButtonColor = new Color(0.22f, 0.43f, 0.58f, 0.98f);
    private static readonly Color ActionButtonActiveColor = new Color(0.16f, 0.60f, 0.48f, 0.98f);
    private static readonly Color CloseButtonColor = new Color(0.97f, 0.20f, 0.13f, 1f);
    private static readonly Color WhiteText = Color.white;
    private static readonly Color DarkText = new Color(0.08f, 0.07f, 0.07f, 1f);

    private static PipePresenter instance;
    private static Sprite cachedCircularSprite;
    private static TMP_FontAsset cachedDisplayFont;

    public static bool IsWorldInputBlockedByPanel => instance != null && instance.IsPointerOverVisiblePanel();

    private Canvas hostCanvas;
    private Canvas panelCanvas;
    private RectTransform panelRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI senderModuleLabel;
    private TextMeshProUGUI senderCapacityLabel;
    private TextMeshProUGUI transportItemLabel;
    private TextMeshProUGUI receiverModuleLabel;
    private TextMeshProUGUI receiverCapacityLabel;
    private Button recoverButton;
    private Image recoverButtonBackground;
    private TextMeshProUGUI recoverButtonLabel;
    private Button onButton;
    private Image onButtonBackground;
    private TextMeshProUGUI onButtonLabel;
    private Button offButton;
    private Image offButtonBackground;
    private TextMeshProUGUI offButtonLabel;

    private readonly List<Vector3> routePointBuffer = new List<Vector3>();
    private AssemblyPartFocus focusedPipePart;
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
        focusedPipePart = TryResolveFocusedPipe(focused, out AssemblyPartFocus partFocus)
            ? partFocus
            : null;
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
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = PanelBackgroundColor;
        ApplyOutline(panelObject, PanelBorderColor, new Vector2(2f, -2f));

        panelCanvas = panelObject.GetComponent<Canvas>();
        EnsurePanelDrawOrder();

        CreateHeader(panelObject.transform);
        CreateContent(panelObject.transform);
    }

    private void CreateHeader(Transform parent)
    {
        titleLabel = CreateLabel(parent, "PipeTitle", TitleText, new Vector2(0.03f, 0.93f), new Vector2(0.70f, 0.99f), 24f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        statusLabel = CreateLabel(parent, "PipeStatus", NoRouteText, new Vector2(0.03f, 0.88f), new Vector2(0.70f, 0.93f), 12f, FontStyles.Bold, TextAlignmentOptions.Left, WhiteText);
        Button closeButton = CreateButton(parent, "PipeClose", "X", new Vector2(0.90f, 0.91f), new Vector2(0.985f, 0.995f), 18f, out _, out _, CloseButtonColor, WhiteText, true);
        closeButton.onClick.AddListener(HandleCloseClicked);
    }

    private void CreateContent(Transform parent)
    {
        RectTransform senderCard = CreateSection(parent, "PipeSenderCard", new Vector2(0.03f, 0.60f), new Vector2(0.25f, 0.81f), SenderCardColor, false);
        senderModuleLabel = CreateLabel(senderCard, "PipeSenderModule", SenderPlaceholderText, new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.84f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
        senderCapacityLabel = CreateLabel(senderCard, "PipeSenderCapacity", "-", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.42f), 15f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        RectTransform transportCircle = CreateSection(parent, "PipeTransportCircle", new Vector2(0.35f, 0.56f), new Vector2(0.65f, 0.85f), TransportCircleColor, true);
        transportItemLabel = CreateLabel(transportCircle, "PipeTransportItem", DefaultTransportText, new Vector2(0.16f, 0.20f), new Vector2(0.84f, 0.80f), 20f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        RectTransform receiverCard = CreateSection(parent, "PipeReceiverCard", new Vector2(0.75f, 0.60f), new Vector2(0.97f, 0.81f), ReceiverCardColor, false);
        receiverModuleLabel = CreateLabel(receiverCard, "PipeReceiverModule", ReceiverPlaceholderText, new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.84f), 16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);
        receiverCapacityLabel = CreateLabel(receiverCard, "PipeReceiverCapacity", "-", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.42f), 15f, FontStyles.Bold, TextAlignmentOptions.Center, DarkText);

        recoverButton = CreateButton(parent, "PipeRecoverButton", RecoverText, new Vector2(0.31f, 0.07f), new Vector2(0.69f, 0.44f), 24f, out recoverButtonBackground, out recoverButtonLabel, ActionButtonColor, WhiteText, false);
        recoverButton.onClick.AddListener(HandleRecoverClicked);

        onButton = CreateButton(parent, "PipeOnButton", "ON", new Vector2(0.80f, 0.25f), new Vector2(0.97f, 0.44f), 20f, out onButtonBackground, out onButtonLabel, ActionButtonColor, WhiteText, false);
        onButton.onClick.AddListener(HandleOnClicked);

        offButton = CreateButton(parent, "PipeOffButton", "OFF", new Vector2(0.80f, 0.07f), new Vector2(0.97f, 0.24f), 20f, out offButtonBackground, out offButtonLabel, ActionButtonColor, WhiteText, false);
        offButton.onClick.AddListener(HandleOffClicked);
    }

    private void HandleCloseClicked()
    {
        Transform nextFocus = ResolveCloseFocus();
        focusedPipePart = null;
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
        return focusedPipePart != null && focusedPipePart.OwnerSatellite != null
            ? focusedPipePart.OwnerSatellite.transform
            : null;
    }

    private void UpdatePanelState()
    {
        EnsurePanel();
        if (panelRect == null)
        {
            return;
        }

        bool shouldShow = focusedPipePart != null;
        SetPanelVisible(shouldShow);
        if (shouldShow)
        {
            RefreshPanelContent();
        }
    }

    private void RefreshPanelContent()
    {
        if (focusedPipePart == null || focusedPipePart.SourcePart == null || focusedPipePart.SourcePart.partType != PartType.Pipe)
        {
            SetPanelVisible(false);
            return;
        }

        titleLabel.SetText(TitleText);
        PopulateRouteContext();
        UpdateButtons();
    }

    private void PopulateRouteContext()
    {
        selectedProductionState = null;
        string senderName = SenderPlaceholderText;
        string senderCapacity = "-";
        string receiverName = ReceiverPlaceholderText;
        string receiverCapacity = "-";
        string transportName = EmptyTransportText;
        string currentStatus = NoRouteText;

        if (focusedPipePart == null || focusedPipePart.OwnerSatellite == null)
        {
            ApplyRouteDisplay(senderName, senderCapacity, transportName, receiverName, receiverCapacity, currentStatus);
            return;
        }

        Vector2Int focusedCell = LocalPositionToGrid(focusedPipePart.transform.localPosition);
        OutputPortProductionState[] states = focusedPipePart.OwnerSatellite.GetComponentsInChildren<OutputPortProductionState>(true);
        int bestScore = int.MinValue;
        for (int i = 0; i < states.Length; i++)
        {
            OutputPortProductionState candidateState = states[i];
            if (candidateState == null)
            {
                continue;
            }

            AssemblyOutputPortFocus outputPortFocus = candidateState.OutputPortFocus;
            if (outputPortFocus == null)
            {
                continue;
            }

            routePointBuffer.Clear();
            if (!OutputPortTransferUtility.TryResolveTransferRoute(
                outputPortFocus,
                routePointBuffer,
                out int pipeCellCount,
                out StructureResourceInventory receiverInputInventory,
                out AssemblyPartFocus receiverPartFocus))
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
            senderName = OutputPortTransferUtility.ResolveModuleLabel(outputPortFocus).ToUpperInvariant();
            senderCapacity = BuildSenderCapacityLabel(outputPortFocus);
            receiverName = ResolvePartLabel(receiverPartFocus).ToUpperInvariant();
            receiverCapacity = BuildReceiverCapacityLabel(receiverInputInventory);
            transportName = ResolveTransportItemLabel(candidateState);
            currentStatus = candidateState.StatusLabel;
        }

        ApplyRouteDisplay(senderName, senderCapacity, transportName, receiverName, receiverCapacity, currentStatus);
    }

    private void ApplyRouteDisplay(
        string senderName,
        string senderCapacity,
        string transportName,
        string receiverName,
        string receiverCapacity,
        string currentStatus)
    {
        senderModuleLabel.SetText(string.IsNullOrWhiteSpace(senderName) ? SenderPlaceholderText : senderName);
        senderCapacityLabel.SetText(string.IsNullOrWhiteSpace(senderCapacity) ? "-" : senderCapacity);
        transportItemLabel.SetText(string.IsNullOrWhiteSpace(transportName) ? EmptyTransportText : transportName);
        receiverModuleLabel.SetText(string.IsNullOrWhiteSpace(receiverName) ? ReceiverPlaceholderText : receiverName);
        receiverCapacityLabel.SetText(string.IsNullOrWhiteSpace(receiverCapacity) ? "-" : receiverCapacity);
        statusLabel.SetText(string.IsNullOrWhiteSpace(currentStatus) ? NoRouteText : currentStatus);
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

        if (recoverButtonBackground != null)
        {
            recoverButtonBackground.color = canRecover ? ActionButtonActiveColor : ActionButtonColor;
        }

        if (onButtonBackground != null)
        {
            onButtonBackground.color = canTurnOff ? ActionButtonColor : (canTurnOn ? ActionButtonActiveColor : ActionButtonColor);
        }

        if (offButtonBackground != null)
        {
            offButtonBackground.color = canTurnOff ? ActionButtonActiveColor : ActionButtonColor;
        }
    }

    private static int BuildCandidateScore(OutputPortProductionState state, int pipeCellCount)
    {
        int score = 0;
        if (state.IsRunning)
        {
            score += 1000;
        }

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

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition)
    {
        int center = GridSize / 2;
        int x = Mathf.RoundToInt(localPosition.x / CellSize) + center;
        int y = Mathf.RoundToInt(localPosition.y / CellSize) + center;
        return new Vector2Int(x, y);
    }

    private static string BuildSenderCapacityLabel(AssemblyOutputPortFocus outputPortFocus)
    {
        if (!OutputPortTransferUtility.TryResolveSenderOutputInventory(outputPortFocus, out StructureResourceInventory outputInventory) || outputInventory == null)
        {
            return "-";
        }

        return string.Format(SenderCapacityFormat, outputInventory.TotalAmount, outputInventory.Capacity);
    }

    private static string BuildReceiverCapacityLabel(StructureResourceInventory receiverInputInventory)
    {
        if (receiverInputInventory == null)
        {
            return "-";
        }

        return string.Format(ReceiverCapacityFormat, receiverInputInventory.TotalAmount, receiverInputInventory.Capacity);
    }

    private static string ResolveTransportItemLabel(OutputPortProductionState state)
    {
        if (state == null)
        {
            return EmptyTransportText;
        }

        InventoryResourceType resourceType = state.HasAssignedResource
            ? state.AssignedResourceType
            : state.ProducedResourceType;
        string resourceName = InventoryResourceCatalog.GetDisplayName(resourceType).Replace("_", " ");
        return string.IsNullOrWhiteSpace(resourceName) ? EmptyTransportText : resourceName;
    }

    private static string ResolvePartLabel(AssemblyPartFocus partFocus)
    {
        return partFocus != null && partFocus.SourcePart != null && !string.IsNullOrWhiteSpace(partFocus.SourcePart.partName)
            ? partFocus.SourcePart.partName
            : ReceiverPlaceholderText;
    }

    private bool IsPointerOverVisiblePanel()
    {
        if (panelRect == null || !panelRect.gameObject.activeInHierarchy || Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Camera uiCamera = panelCanvas != null && panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? panelCanvas.worldCamera
            : null;
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
    }

    private static bool TryResolveFocusedPipe(Transform focused, out AssemblyPartFocus partFocus)
    {
        partFocus = null;
        if (focused == null)
        {
            return false;
        }

        if (focused.GetComponentInParent<AssemblyOutputPortFocus>() != null)
        {
            return false;
        }

        partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = focused.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus == null || partFocus.SourcePart == null || partFocus.OwnerSatellite == null)
        {
            partFocus = null;
            return false;
        }

        if (partFocus.SourcePart.partType != PartType.Pipe || SplitPipeUtility.IsSplitPipePart(partFocus.SourcePart) || MergePipeUtility.IsMergePipePart(partFocus.SourcePart) || FilterPipeUtility.IsFilterPipePart(partFocus.SourcePart))
        {
            partFocus = null;
            return false;
        }

        return true;
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
            sectionImage.type = Image.Type.Simple;
            sectionImage.preserveAspect = true;
        }
        else
        {
            ApplyOutline(sectionObject, new Color(1f, 1f, 1f, 0.12f), Vector2.zero);
        }

        return sectionRect;
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
        label.fontSizeMin = Mathf.Max(7f, fontSize * 0.55f);
        label.fontSizeMax = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        ApplyFont(label);
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, float fontSize, out Image backgroundImage, out TextMeshProUGUI label, Color backgroundColor, Color textColor, bool circular)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        ConfigureStretchRect(buttonRect, anchorMin, anchorMax);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        if (circular)
        {
            backgroundImage.sprite = ResolveCircularSprite();
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = true;
        }
        else
        {
            ApplyOutline(buttonObject, new Color(1f, 1f, 1f, 0.12f), Vector2.zero);
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 0.94f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;

        label = CreateLabel(buttonRect, name + "Label", labelText, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        return button;
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
            cachedDisplayFont = TMP_FontAsset.CreateFontAsset(osFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
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

    private static Sprite ResolveCircularSprite()
    {
        if (cachedCircularSprite != null)
        {
            return cachedCircularSprite;
        }

        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            name = "PipePresenterCircleTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2(31.5f, 31.5f);
        float radius = 30f;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? fill : clear);
            }
        }

        texture.Apply();
        cachedCircularSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        return cachedCircularSprite;
    }

    private static void ApplyOutline(GameObject target, Color color, Vector2 effectDistance)
    {
        if (target == null)
        {
            return;
        }

        Outline outline = ComponentUtility.GetOrAddComponent<Outline>(target);
        outline.effectColor = color;
        outline.effectDistance = effectDistance;
        outline.useGraphicAlpha = true;
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

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
