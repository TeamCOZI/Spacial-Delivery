using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class PeriodUI : FocusEventSubscriber
{
    private VisualElement root;
    private VisualElement rootUI;
    private VisualElement uiContainer;
    private ProgressBar period;

    private readonly List<ProgressBar> progressBars = new List<ProgressBar>();
    private readonly List<VisualElement> visualElements = new List<VisualElement>();

    private void Awake()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        rootUI = root.Q<VisualElement>("RootUI");
        uiContainer = root.Q<VisualElement>("UI");

        period = root.Q<ProgressBar>("Period");
        if (period != null) period.value = 0f;

    }

    private void FixedUpdate()
    {
        if (period == null) return;

        period.value += Time.fixedDeltaTime * 100f / 18f;
        period.value %= 100f;

        SyncValues(period.value);
    }

    protected override void HandleFocusChanged(Transform transform)
    {
        if (root == null || uiContainer == null) return;

        var chain = BuildChainToStar(transform);
        int desiredCount = 1 + chain.Count;

        SyncProgressBars(desiredCount);
        SyncValues(period != null ? period.value : 0f);
        ApplyHierarchyColors(chain);
    }

    private List<Transform> BuildChainToStar(Transform focused)
    {
        var stack = new List<Transform>();
        var current = focused;

        while (current != null)
        {
            if (current.GetComponent<Star>() != null) break;
            stack.Add(current);
            current = current.parent;
        }

        stack.Reverse();
        return stack;
    }

    private void SyncProgressBars(int desiredTotalCount)
    {
        int targetExtraCount = Mathf.Max(0, desiredTotalCount - 1);

        while (progressBars.Count < targetExtraCount) AddExtraProgressBar();

        for (int i = progressBars.Count - 1; i >= targetExtraCount; i--)
        {
            var bar = progressBars[i];
            if (bar != null) bar.RemoveFromHierarchy();
            progressBars.RemoveAt(i);

            var wrapper = visualElements[i];
            if (wrapper != null) wrapper.RemoveFromHierarchy();
            visualElements.RemoveAt(i);
        }
    }

    private void AddExtraProgressBar()
    {
        if (rootUI == null || uiContainer == null || period == null) return;

        var wrapper = new VisualElement();
        wrapper.style.flexGrow = 1;
        wrapper.style.flexDirection = FlexDirection.Column;
        wrapper.style.alignItems = Align.Center;
        wrapper.style.justifyContent = Justify.Center;

        var bar = new ProgressBar();
        bar.value = period.value;

        foreach (var className in period.GetClasses()) bar.AddToClassList(className);

        bar.style.width = Length.Percent(80);

        wrapper.Add(bar);
        rootUI.Add(wrapper);

        visualElements.Add(wrapper);
        progressBars.Add(bar);
    }

    private void SyncValues(float value)
    {
        if (period != null) period.value = value;

        for (int i = 0; i < progressBars.Count; i++)
        {
            var bar = progressBars[i];
            if (bar != null) bar.value = value;
        }
    }

    private void ApplyHierarchyColors(List<Transform> chain)
    {
        ApplyBarColor(period, Color.white);

        for (int i = 0; i < progressBars.Count; i++)
        {
            var bar = progressBars[i];
            var color = (i < chain.Count) ? GetRendererColor(chain[i]) : Color.white;
            ApplyBarColor(bar, color);
        }
    }

    private Color GetRendererColor(Transform transform)
    {
        if (transform == null) return Color.white;

        var renderer = transform.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return Color.white;

        return renderer.sharedMaterial.color;
    }

    private void ApplyBarColor(ProgressBar bar, Color baseColor)
    {
        if (bar == null) return;

        var progress = bar.Q<VisualElement>(className: "unity-progress-bar__progress");
        if (progress != null) progress.style.backgroundColor = baseColor;

        var titleContainer = bar.Q<VisualElement>(className: "unity-progress-bar__title-container");
        if (titleContainer != null)
        {
            var faded = baseColor;
            faded.a *= 0.5f;
            titleContainer.style.backgroundColor = faded;
        }
    }
}
