using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FocusManager : MonoBehaviour
{
    public static FocusManager Instance { get; private set; }
    public static Transform currentFocus { get; private set; }

    public Action<Transform> focusEvent;

    [Header("UI Settings")]
    public GameObject focusInfoPanel;
    public TextMeshProUGUI focusInfoText;

    private readonly List<Icon> focuses = new List<Icon>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    private void Start()
    {
        UserInput.Instance.focusEvent += UpdateFocus;
        UserInput.Instance.hoverEvent += UpdateHover;
    }

    private void UpdateFocus(Transform transform)
    {
        if (currentFocus == transform) return;
        currentFocus = transform;

        focusEvent?.Invoke(currentFocus);

        focusInfoText.text = "";

        if (AssemblyManager.Instance != null && AssemblyManager.Instance.isAssembling)
        {
            return;
        }

        if (currentFocus != null)
        {
            UpdateFocusInfo provider = transform.GetComponent<UpdateFocusInfo>();
            if (provider == null)
            {
                provider = transform.GetComponentInParent<UpdateFocusInfo>();
            }
            if (provider == null) return;

            Dictionary<string, string> focusInfo = provider.UpdateFocusInfo();
            if (focusInfo == null) return;

            foreach(KeyValuePair<string, string> info in focusInfo) focusInfoText.text += info.Key + " : " + info.Value + "\n\n";
        }
    }

    private void UpdateHover(Icon hover)
    {
        foreach(Icon focus in focuses) focus.IsHover = false;
        if (hover != null) hover.IsHover = true;
    }

    public void RegisterIcon(Icon icon)
    {
        if (!focuses.Contains(icon)) focuses.Add(icon);
    }

    public void DeregisterIcon(Icon icon)
    {
        if(focuses.Contains(icon)) focuses.Remove(icon);
    }

    public void SetFocus(Transform transform)
    {
        UpdateFocus(transform);
    }
}
