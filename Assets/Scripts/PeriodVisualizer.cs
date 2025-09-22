using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PeriodVisualizer : MonoBehaviour
{
    public static PeriodVisualizer Instance { get; private set; }
    [Header("UI References")]
    public GameObject periodVisualizerContainer;
    public GameObject periodBarPrefab;
    public float rowHeight = 30f;
    public float labelWidth = 150f;

    private readonly List<Orbiter> registeredOrbiters = new List<Orbiter>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        Orbiter[] allOrbiters = FindObjectsByType<Orbiter>(FindObjectsSortMode.None);
        foreach (Orbiter orbiter in allOrbiters)
        {
            RegisterOrbiter(orbiter);
        }

        UpdatePeriodVisualizer();
    }

    public void RegisterOrbiter(Orbiter orbiter)
    {
        if (!registeredOrbiters.Contains(orbiter))
        {
            registeredOrbiters.Add(orbiter);
        }
        UpdatePeriodVisualizer();
    }

    public void DeregisterOrbiter(Orbiter orbiter)
    {
        if (registeredOrbiters.Contains(orbiter))
        {
            registeredOrbiters.Remove(orbiter);
        }
        UpdatePeriodVisualizer();
    }

    public void UpdatePeriodVisualizer()
    {
        if (periodVisualizerContainer == null || periodBarPrefab == null)
        {
            return;
        }

        var activeOrbiters = registeredOrbiters.Where(o => o.orbitSpeed > 0).ToList();

        if (activeOrbiters.Count == 0)
        {
            periodVisualizerContainer.SetActive(false);
            return;
        }

        periodVisualizerContainer.SetActive(true);

        List<int> periods = activeOrbiters.Select(o => Mathf.RoundToInt(360f / o.orbitSpeed)).ToList();

        foreach (Transform child in periodVisualizerContainer.transform)
        {
            Destroy(child.gameObject);
        }

        int totalPeriodLcm = CalculateLCM(periods);

        CreatePeriodRow($"전체 주기: {totalPeriodLcm}", totalPeriodLcm, totalPeriodLcm, periodVisualizerContainer.transform);

        var SortedOrbiters = activeOrbiters.OrderByDescending(o => 360f / o.orbitSpeed);
        foreach (var orbiter in SortedOrbiters)
        {
            int period = Mathf.RoundToInt(360f / orbiter.orbitSpeed);
            CreatePeriodRow($"{orbiter.gameObject.name} - 주기: {orbiter}", period, totalPeriodLcm, periodVisualizerContainer.transform);
        }
    }

    private void CreatePeriodRow(string labelText, int period, int totalPeriodLcm, Transform parent)
    {
        GameObject rowGO = new GameObject(labelText.Replace(":", ""), typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        HorizontalLayoutGroup rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10;
        rowLayout.childAlignment = TextAnchor.MiddleRight;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        LayoutElement rowLayoutElement = rowGO.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = rowHeight;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        Text label = labelGO.AddComponent<Text>();
        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleRight;

        LayoutElement labelLayout = labelGO.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = labelWidth;

        GameObject barsContainer = new GameObject("Bars", typeof(RectTransform));
        barsContainer.transform.SetParent(rowGO.transform, false);

        LayoutElement barsContainerLayout = barsContainer.AddComponent<LayoutElement>();
        barsContainerLayout.flexibleWidth = 1;

        HorizontalLayoutGroup barsLayout = barsContainer.AddComponent<HorizontalLayoutGroup>();
        barsLayout.spacing = 2;

        int numBars = (period > 0) ? totalPeriodLcm / period : 0;
        for (int i = 0; i < numBars; i++)
        {
            GameObject bar = Instantiate(periodBarPrefab, barsContainer.transform);
            LayoutElement barLayout = bar.AddComponent<LayoutElement>();
            barLayout.flexibleWidth = 1;
        }
    }

    #region LCM_HELPERS
    private static int GCD(int a, int b)
    {
        while (b != 0) { int temp = b; b = a % b; a = temp; }
        return a;
    }

    private static int LCM(int a, int b)
    {
        if (a == 0 || b == 0) return 0;
        return Mathf.Abs(a / GCD(a, b)) * b;
    }

    private static int CalculateLCM(List<int> numbers)
    {
        if (numbers == null || numbers.Count == 0) return 0;
        if (numbers.Count == 1) return numbers[0];

        int result = numbers[0];
        for (int i = 1; i < numbers.Count; i++) { result = LCM(result, numbers[i]); }
        return result;
    }
    #endregion
}