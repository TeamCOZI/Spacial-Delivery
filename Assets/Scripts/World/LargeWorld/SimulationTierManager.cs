using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(29000)]
public class SimulationTierManager : MonoBehaviour
{
    public static SimulationTierManager Instance { get; private set; }

    [Header("Distance Thresholds")]
    [Min(1f)] public float nearDistance = 200f;
    [Min(1f)] public float farDistance = 5000f;
    [Min(0f)] public float tierHysteresis = 100f;

    [Header("Update Budget")]
    [Range(1, 60)] public int updateIntervalFrames = 5;

    private readonly HashSet<SimulationTierTarget> tracked = new HashSet<SimulationTierTarget>();
    private Camera mainCamera;
    private int frameCounter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mainCamera = Camera.main;
        updateIntervalFrames = Mathf.Max(1, updateIntervalFrames);
        farDistance = Mathf.Max(farDistance, nearDistance + 1f);
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
        frameCounter++;
        if (frameCounter < updateIntervalFrames) return;
        frameCounter = 0;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 cameraPos = mainCamera.transform.position;
        bool forceNearTier = FocusManager.currentFocus != null &&
            SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out _);

        foreach (SimulationTierTarget target in tracked)
        {
            if (target == null) continue;

            SimulationTier tier;
            if (forceNearTier)
            {
                tier = SimulationTier.Near;
            }
            else
            {
                float distance = Vector3.Distance(cameraPos, target.transform.position);
                tier = ResolveTier(distance, target.CurrentTier);
            }

            target.ApplyTier(tier);
        }
    }

    public void Register(SimulationTierTarget target)
    {
        if (target == null) return;
        tracked.Add(target);
    }

    public void Deregister(SimulationTierTarget target)
    {
        if (target == null) return;
        tracked.Remove(target);
    }

    private SimulationTier ResolveTier(float distance, SimulationTier currentTier)
    {
        float hysteresis = Mathf.Max(0f, tierHysteresis);

        switch (currentTier)
        {
            case SimulationTier.Near:
                if (distance > nearDistance + hysteresis) return SimulationTier.Mid;
                return SimulationTier.Near;

            case SimulationTier.Mid:
                if (distance < nearDistance - hysteresis) return SimulationTier.Near;
                if (distance > farDistance + hysteresis) return SimulationTier.Far;
                return SimulationTier.Mid;

            case SimulationTier.Far:
                if (distance < farDistance - hysteresis) return SimulationTier.Mid;
                return SimulationTier.Far;

            default:
                if (distance <= nearDistance) return SimulationTier.Near;
                if (distance >= farDistance) return SimulationTier.Far;
                return SimulationTier.Mid;
        }
    }
}
