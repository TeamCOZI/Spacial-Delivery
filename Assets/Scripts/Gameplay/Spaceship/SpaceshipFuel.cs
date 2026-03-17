using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SpaceshipFuel : MonoBehaviour
{
    [SerializeField, Min(0f)] private float maxFuel = 300f;
    [SerializeField] private bool refillOnAwake = true;

    private float currentFuel;

    public static event Action<SpaceshipFuel, float, float> OnFuelUpdated;

    public float CurrentFuel => currentFuel;
    public float MaxFuel => maxFuel;

    private void Awake()
    {
        if (refillOnAwake)
        {
            currentFuel = maxFuel;
        }
        else
        {
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
        }
    }

    private void OnEnable()
    {
        Publish();
    }

    private void Start()
    {
        Publish();
    }

    public bool HasFuel()
    {
        return currentFuel > 0f;
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f) return false;
        if (currentFuel <= 0f) return false;

        float previous = currentFuel;
        currentFuel = Mathf.Max(0f, currentFuel - amount);
        if (!Mathf.Approximately(previous, currentFuel))
        {
            Publish();
            return true;
        }

        return false;
    }

    public void Refill()
    {
        currentFuel = maxFuel;
        Publish();
    }

    private void Publish()
    {
        OnFuelUpdated?.Invoke(this, currentFuel, maxFuel);
    }
}

