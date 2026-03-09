using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(30000)]
public class LargeWorldCoordinator : MonoBehaviour
{
    public static LargeWorldCoordinator Instance { get; private set; }

    [Header("Reference Origin (double)")]
    public Double3 worldOrigin = Double3.Zero;

    [Header("Sync")]
    public bool syncEveryLateUpdate = true;

    private readonly HashSet<WorldPosition> tracked = new HashSet<WorldPosition>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!syncEveryLateUpdate) return;
        // Keep Rigidbody-interpolated orbit bodies smooth during normal frames.
        SyncAllTransforms(includeOrbitDriven: false);
    }

    public void Register(WorldPosition wp)
    {
        if (wp == null) return;
        tracked.Add(wp);
    }

    public void Deregister(WorldPosition wp)
    {
        if (wp == null) return;
        tracked.Remove(wp);
    }

    public void SetWorldOrigin(Double3 newOrigin)
    {
        worldOrigin = newOrigin;
    }

    public Vector3 ToLocal(Double3 worldPos)
    {
        Double3 local = worldPos - worldOrigin;
        return local.ToVector3();
    }

    public Double3 ToWorld(Vector3 localPos)
    {
        return worldOrigin + (Double3)localPos;
    }

    public void SyncAllTransforms(bool includeOrbitDriven = true)
    {
        foreach (WorldPosition wp in tracked)
        {
            if (wp == null) continue;

            if (!includeOrbitDriven &&
                (wp.GetComponent<OrbitRevolution>() != null || wp.GetComponent<Spaceship>() != null))
            {
                continue;
            }

            Vector3 localPosition = ToLocal(wp.worldPosition);
            Rigidbody rb = wp.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.position = localPosition;
            }
            else
            {
                wp.transform.position = localPosition;
            }
        }
    }
}
