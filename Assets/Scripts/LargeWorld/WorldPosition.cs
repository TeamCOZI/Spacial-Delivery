using UnityEngine;

[DisallowMultipleComponent]
public class WorldPosition : MonoBehaviour
{
    [Header("Absolute World Position (double)")]
    public Double3 worldPosition;

    [Tooltip("If true, initialize worldPosition from current Transform on Awake.")]
    public bool initializeFromTransform = true;

    private void Awake()
    {
        if (initializeFromTransform)
        {
            worldPosition = transform.position;
        }

        LargeWorldCoordinator.Instance?.Register(this);
    }

    private void OnEnable()
    {
        LargeWorldCoordinator.Instance?.Register(this);
    }

    private void OnDisable()
    {
        LargeWorldCoordinator.Instance?.Deregister(this);
    }

    public void SetWorldPosition(Double3 position)
    {
        worldPosition = position;
    }
}
