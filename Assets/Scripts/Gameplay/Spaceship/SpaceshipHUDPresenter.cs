using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(34100)]
[DisallowMultipleComponent]
public class SpaceshipHUDPresenter : FocusEventSubscriber
{
    [Header("UI")]
    [SerializeField] private Vector2 panelSize = new Vector2(260f, 72f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(20f, -20f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.35f, 0.95f);

    private Canvas hostCanvas;
    private RectTransform panelRect;
    private Slider fuelSlider;
    private TextMeshProUGUI fuelText;
    private SpaceshipFuel focusedFuel;
    private bool hasFuelSample;
    private float sampledFuel;
    private float sampledMaxFuel;

    protected override void OnEnable()
    {
        base.OnEnable();
        SpaceshipFuel.OnFuelUpdated += HandleFuelUpdated;
        EnsureUI();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SpaceshipFuel.OnFuelUpdated -= HandleFuelUpdated;
    }

    protected override void LateUpdate()
    {
        if (focusedFuel == null)
        {
            hasFuelSample = false;
            SetPanelVisible(false);
            return;
        }

        float currentFuel = focusedFuel.CurrentFuel;
        float maxFuel = focusedFuel.MaxFuel;
        if (hasFuelSample &&
            Mathf.Approximately(sampledFuel, currentFuel) &&
            Mathf.Approximately(sampledMaxFuel, maxFuel))
        {
            return;
        }

        UpdateFuelVisual(currentFuel, maxFuel);
    }

    protected override void HandleFocusChanged(Transform focused)
    {
        if (SpaceshipFocusUtility.TryResolveSpaceship(focused, out Spaceship focusedSpaceship))
        {
            focusedFuel = focusedSpaceship.GetComponent<SpaceshipFuel>();
        }
        else
        {
            focusedFuel = null;
        }

        hasFuelSample = false;
        if (focusedFuel == null)
        {
            SetPanelVisible(false);
            return;
        }

        EnsureUI();
        UpdateFuelVisual(focusedFuel.CurrentFuel, focusedFuel.MaxFuel);
        SetPanelVisible(true);
    }

    private void HandleFuelUpdated(SpaceshipFuel owner, float currentFuel, float maxFuel)
    {
        if (owner == null || owner != focusedFuel) return;
        UpdateFuelVisual(currentFuel, maxFuel);
    }

    private void EnsureUI()
    {
        if (panelRect != null && fuelSlider != null && fuelText != null) return;

        if (hostCanvas == null)
        {
            hostCanvas = UIRootLocator.ResolvePrimaryCanvas();
        }
        if (hostCanvas == null) return;

        TMP_FontAsset font = ResolveReferenceFont();

        SpaceshipFuelPanelFactory.Handles handles = SpaceshipFuelPanelFactory.Ensure(
            hostCanvas,
            panelSize,
            anchoredPosition,
            panelColor,
            fillColor,
            font);

        if (!handles.IsValid) return;

        panelRect = handles.panelRect;
        fuelSlider = handles.fuelSlider;
        fuelText = handles.fuelText;
    }

    private static TMP_FontAsset ResolveReferenceFont()
    {
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoText != null &&
            focusManager.focusInfoText.font != null)
        {
            return focusManager.focusInfoText.font;
        }

        return null;
    }

    private void UpdateFuelVisual(float currentFuel, float maxFuel)
    {
        EnsureUI();
        if (fuelSlider == null || fuelText == null || panelRect == null) return;

        sampledFuel = currentFuel;
        sampledMaxFuel = maxFuel;
        hasFuelSample = true;

        float denom = Mathf.Max(0.0001f, maxFuel);
        float ratio = Mathf.Clamp01(currentFuel / denom);
        fuelSlider.value = ratio;
        fuelText.SetText("Fuel: {0} / {1}", Mathf.CeilToInt(currentFuel), Mathf.CeilToInt(maxFuel));

        SetPanelVisible(true);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf != visible)
        {
            panelRect.gameObject.SetActive(visible);
        }
    }
}
