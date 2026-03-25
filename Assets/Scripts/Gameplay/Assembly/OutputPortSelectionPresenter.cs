using UnityEngine;

[DefaultExecutionOrder(33996)]
[DisallowMultipleComponent]
public sealed class OutputPortSelectionPresenter : FocusEventSubscriber
{
    // Legacy selector is intentionally inert. Output-port selection now uses
    // ModuleOutputPortSelectorPresenter's cell-anchored overlay buttons.
    public static bool IsWorldInputBlockedByPanel => false;

    private void Awake()
    {
        enabled = false;
    }

    protected override void OnEnable()
    {
        enabled = false;
    }

    protected override void HandleFocusChanged(Transform focused)
    {
    }
}