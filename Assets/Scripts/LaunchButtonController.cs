using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LaunchButtonController : MonoBehaviour
{
    public CameraController cameraController;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        canvasGroup.interactable = false;
    }

    private void Update()
    {
        bool shouldBeInteractable = cameraController != null && cameraController.SelectedPrefab != null && cameraController.SelectedPrefab.gameObject.activeInHierarchy;

        if (canvasGroup.interactable != shouldBeInteractable)
        {
            if (shouldBeInteractable)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}