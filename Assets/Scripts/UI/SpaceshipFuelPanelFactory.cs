using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class SpaceshipFuelPanelFactory
{
    public struct Handles
    {
        public RectTransform panelRect;
        public Slider fuelSlider;
        public TextMeshProUGUI fuelText;

        public bool IsValid => panelRect != null && fuelSlider != null && fuelText != null;
    }

    public static Handles Ensure(
        Canvas hostCanvas,
        Vector2 panelSize,
        Vector2 anchoredPosition,
        Color panelColor,
        Color fillColor,
        TMP_FontAsset font)
    {
        if (hostCanvas == null) return default;

        Transform existing = hostCanvas.transform.Find("SpaceshipFuelPanel");
        if (existing != null)
        {
            return new Handles
            {
                panelRect = existing as RectTransform,
                fuelSlider = existing.GetComponentInChildren<Slider>(true),
                fuelText = existing.GetComponentInChildren<TextMeshProUGUI>(true),
            };
        }

        GameObject panel = new GameObject("SpaceshipFuelPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(hostCanvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = anchoredPosition;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = panelColor;

        Slider slider = CreateFuelSlider(panel.transform, fillColor);
        TextMeshProUGUI text = CreateFuelText(panel.transform, font);

        return new Handles
        {
            panelRect = panelRect,
            fuelSlider = slider,
            fuelText = text,
        };
    }

    private static Slider CreateFuelSlider(Transform parent, Color fillColor)
    {
        GameObject sliderObject = new GameObject("FuelSlider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.08f, 0.18f);
        sliderRect.anchorMax = new Vector2(0.92f, 0.48f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderObject.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImage = background.GetComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 0.2f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;

        Slider fuelSlider = sliderObject.GetComponent<Slider>();
        fuelSlider.targetGraphic = fillImage;
        fuelSlider.fillRect = fillRect;
        fuelSlider.handleRect = null;
        fuelSlider.direction = Slider.Direction.LeftToRight;
        fuelSlider.minValue = 0f;
        fuelSlider.maxValue = 1f;
        fuelSlider.value = 1f;
        return fuelSlider;
    }

    private static TextMeshProUGUI CreateFuelText(Transform parent, TMP_FontAsset font)
    {
        GameObject textObject = new GameObject("FuelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.58f);
        textRect.anchorMax = new Vector2(0.92f, 0.94f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI fuelText = textObject.GetComponent<TextMeshProUGUI>();
        fuelText.SetText("Fuel: 0 / 0");
        fuelText.color = Color.white;
        fuelText.alignment = TextAlignmentOptions.MidlineLeft;
        fuelText.enableAutoSizing = true;
        fuelText.fontSizeMin = 11f;
        fuelText.fontSizeMax = 20f;
        if (font != null)
        {
            fuelText.font = font;
        }

        return fuelText;
    }
}
