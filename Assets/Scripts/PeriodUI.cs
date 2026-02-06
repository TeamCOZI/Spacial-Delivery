using UnityEngine;
using UnityEngine.UIElements;

public class PeriodUI : MonoBehaviour
{
    private Label focus;
    private ProgressBar period;

    private void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        focus = root.Q<Label>("Focus");
        period = root.Q<ProgressBar>("Period");

        FocusManager.Instance.focusEvent += UpdateLabel;

        UpdateLabel(FocusManager.currentFocus);
        period.value = 0f;
    }

    private void OnDisable()
    {
        FocusManager.Instance.focusEvent -= UpdateLabel;
    }

    private void FixedUpdate()
    {
        if (period == null) return;

        period.value += Time.fixedDeltaTime * 100f / 18f;
        period.value %= 100f;
    }

    private void UpdateLabel(Transform transform)
    {
        if (focus == null) return;

        if (transform != null) focus.text = transform.name;
        else focus.text = "None";
    }
}