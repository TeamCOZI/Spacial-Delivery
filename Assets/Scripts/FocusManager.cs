using System;
using UnityEngine;

public class FocusManager : MonoBehaviour
{
    public static FocusManager Instance { get; private set; }

    public Transform CurrentFocus { get; private set; }
    public event Action<Transform> OnFocusChanged;

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

    public void SetFocus(Transform newFocus)
    {
        if (CurrentFocus == newFocus) return;

        CurrentFocus = newFocus;
        OnFocusChanged?.Invoke(CurrentFocus);
        Debug.Log($"Focus changed to: {(newFocus != null ? newFocus.name : "None")}");
    }
}