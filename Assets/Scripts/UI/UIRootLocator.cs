using UnityEngine;

public static class UIRootLocator
{
    private static Canvas cachedPrimaryCanvas;

    public static Canvas ResolvePrimaryCanvas()
    {
        if (CoreRuntimeAccess.TryGetFocusManager(out FocusManager focusManager) &&
            focusManager.focusInfoPanel != null)
        {
            Canvas panelCanvas = focusManager.focusInfoPanel.GetComponentInParent<Canvas>();
            if (panelCanvas != null)
            {
                cachedPrimaryCanvas = panelCanvas;
                return panelCanvas;
            }
        }

        if (cachedPrimaryCanvas != null)
        {
            return cachedPrimaryCanvas;
        }

        if (!ReferenceEquals(cachedPrimaryCanvas, null) && cachedPrimaryCanvas == null)
        {
            cachedPrimaryCanvas = null;
        }

        cachedPrimaryCanvas = Object.FindFirstObjectByType<Canvas>();
        return cachedPrimaryCanvas;
    }
}
