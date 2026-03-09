using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FocusFeatureBootstrap))]
public class FocusManager : MonoBehaviour, IFocusService
{
    public static FocusManager Instance { get; private set; }
    public static event Action<FocusManager> InstanceChanged;
    public static Transform currentFocus { get; private set; }

    private event Action<Transform> focusChanged;

    [Header("UI Settings")]
    public GameObject focusInfoPanel;
    public TextMeshProUGUI focusInfoText;

    private bool isUserInputSubscribed;
    private UserInput subscribedUserInput;
    private Icon currentHoveredIcon;

    public Transform CurrentFocus => currentFocus;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InstanceChanged?.Invoke(this);
    }

    private void OnEnable()
    {
        UserInput.InstanceChanged += HandleUserInputInstanceChanged;
        TrySubscribeUserInputEvents();
    }

    private void OnDisable()
    {
        UserInput.InstanceChanged -= HandleUserInputInstanceChanged;
        TryUnsubscribeUserInputEvents();
    }

    private void HandleUserInputInstanceChanged(UserInput _)
    {
        RebindUserInputEvents();
    }

    private void OnDestroy()
    {
        currentHoveredIcon = null;
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
            if (currentFocus != null)
            {
                currentFocus = null;
            }
        }
    }

    private void UpdateFocus(Transform focusedTransform)
    {
        if (currentFocus == focusedTransform) return;
        currentFocus = focusedTransform;

        focusChanged?.Invoke(currentFocus);

        SetFocusInfoText(string.Empty);

        if (currentFocus != null)
        {
            UpdateFocusInfo provider = ResolveFocusInfoProvider(currentFocus);
            if (provider == null) return;

            Dictionary<string, string> focusInfo = provider.UpdateFocusInfo();
            if (focusInfo == null) return;
            SetFocusInfoText(BuildFocusInfoText(focusInfo));
        }
    }

    private void UpdateHover(Icon hover)
    {
        NormalizeHoveredIconReference();

        if (currentHoveredIcon == hover) return;

        if (currentHoveredIcon != null)
        {
            currentHoveredIcon.IsHover = false;
        }

        if (hover != null)
        {
            hover.IsHover = true;
        }

        currentHoveredIcon = hover;
    }

    private void NormalizeHoveredIconReference()
    {
        if (!ReferenceEquals(currentHoveredIcon, null) && currentHoveredIcon == null)
        {
            currentHoveredIcon = null;
        }
    }

    private static UpdateFocusInfo ResolveFocusInfoProvider(Transform focusedTransform)
    {
        if (focusedTransform == null) return null;

        UpdateFocusInfo provider = focusedTransform.GetComponent<UpdateFocusInfo>();
        if (provider != null) return provider;
        return focusedTransform.GetComponentInParent<UpdateFocusInfo>();
    }

    private static string BuildFocusInfoText(Dictionary<string, string> focusInfo)
    {
        if (focusInfo == null || focusInfo.Count == 0) return string.Empty;

        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, string> info in focusInfo)
        {
            builder.Append(info.Key).Append(" : ").Append(info.Value).Append("\n\n");
        }
        return builder.ToString();
    }

    private static bool IsAssemblyModeActive()
    {
        AssemblyManager assemblyManager = AssemblyManager.Instance;
        return assemblyManager != null && assemblyManager.IsAssembling;
    }

    public void SetFocus(Transform focusedTransform)
    {
        UpdateFocus(focusedTransform);
    }

    public void RegisterFocusListener(Action<Transform> listener, bool invokeImmediately = true)
    {
        if (listener == null) return;
        focusChanged -= listener;
        focusChanged += listener;
        if (invokeImmediately)
        {
            listener.Invoke(currentFocus);
        }
    }

    public void DeregisterFocusListener(Action<Transform> listener)
    {
        if (listener == null) return;
        focusChanged -= listener;
    }

    private void TrySubscribeUserInputEvents()
    {
        if (isUserInputSubscribed) return;
        UserInput userInput = UserInput.Instance;
        if (userInput == null) return;

        userInput.focusEvent += UpdateFocus;
        userInput.hoverEvent += UpdateHover;
        subscribedUserInput = userInput;
        isUserInputSubscribed = true;
    }

    private void TryUnsubscribeUserInputEvents()
    {
        if (!isUserInputSubscribed) return;
        if (subscribedUserInput != null)
        {
            subscribedUserInput.focusEvent -= UpdateFocus;
            subscribedUserInput.hoverEvent -= UpdateHover;
        }
        subscribedUserInput = null;
        isUserInputSubscribed = false;
    }

    private void RebindUserInputEvents()
    {
        if (isUserInputSubscribed)
        {
            TryUnsubscribeUserInputEvents();
        }
        TrySubscribeUserInputEvents();
    }

    private void SetFocusInfoText(string value)
    {
        if (focusInfoText == null) return;
        focusInfoText.SetText(value);
    }
}
