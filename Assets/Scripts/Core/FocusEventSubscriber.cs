using UnityEngine;

public abstract class FocusEventSubscriber : MonoBehaviour
{
    private bool isFocusSubscribed;
    private IFocusService subscribedFocusService;

    protected virtual void OnEnable()
    {
        FocusManager.InstanceChanged += HandleFocusManagerInstanceChanged;
        TrySubscribeFocusEvent();
    }

    protected virtual void OnDisable()
    {
        FocusManager.InstanceChanged -= HandleFocusManagerInstanceChanged;
        TryUnsubscribeFocusEvent();
    }

    protected virtual void LateUpdate()
    {
    }

    protected abstract void HandleFocusChanged(Transform focused);

    private void HandleFocusManagerInstanceChanged(FocusManager _)
    {
        RebindFocusEvent();
    }

    private void TrySubscribeFocusEvent()
    {
        if (isFocusSubscribed) return;
        if (!CoreRuntimeAccess.TryGetFocusService(out IFocusService focusService)) return;

        focusService.RegisterFocusListener(HandleFocusChanged, true);
        subscribedFocusService = focusService;
        isFocusSubscribed = true;
    }

    private void TryUnsubscribeFocusEvent()
    {
        if (!isFocusSubscribed) return;
        if (subscribedFocusService != null)
        {
            subscribedFocusService.DeregisterFocusListener(HandleFocusChanged);
        }
        subscribedFocusService = null;
        isFocusSubscribed = false;
    }

    private void RebindFocusEvent()
    {
        if (isFocusSubscribed)
        {
            TryUnsubscribeFocusEvent();
        }
        TrySubscribeFocusEvent();
    }
}
