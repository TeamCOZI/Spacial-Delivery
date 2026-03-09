using UnityEngine;

[DisallowMultipleComponent]
public class SimulationTierTarget : MonoBehaviour
{
    [Header("Orbit Step Interval")]
    [Min(1)] public int nearStepInterval = 1;
    [Min(1)] public int midStepInterval = 2;
    [Min(1)] public int farStepInterval = 4;

    [Header("Optional Tier Toggles")]
    public bool disableVisualHelpersInFarTier = false;
    public bool disableCollidersInFarTier = false;

    private SimulationTier currentTier = SimulationTier.Near;
    private OrbitRevolution orbitRevolution;
    private Collider[] cachedColliders;
    private OrbitVisualizer[] orbitVisualizers;
    private GravityField[] gravityFields;

    private void Awake()
    {
        orbitRevolution = GetComponent<OrbitRevolution>();
        cachedColliders = GetComponentsInChildren<Collider>(true);
        orbitVisualizers = GetComponentsInChildren<OrbitVisualizer>(true);
        gravityFields = GetComponentsInChildren<GravityField>(true);
    }

    private void OnEnable()
    {
        SimulationTierManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        SimulationTierManager.Instance?.Deregister(this);
    }

    public void ApplyTier(SimulationTier tier)
    {
        if (currentTier == tier) return;
        currentTier = tier;

        if (orbitRevolution != null)
        {
            int stepInterval = tier == SimulationTier.Near
                ? nearStepInterval
                : tier == SimulationTier.Mid ? midStepInterval : farStepInterval;
            orbitRevolution.SetSimulationStepInterval(stepInterval);
        }

        bool enableVisualHelpers = !disableVisualHelpersInFarTier || tier != SimulationTier.Far;
        for (int i = 0; i < orbitVisualizers.Length; i++)
        {
            if (orbitVisualizers[i] != null) orbitVisualizers[i].enabled = enableVisualHelpers;
        }
        for (int i = 0; i < gravityFields.Length; i++)
        {
            if (gravityFields[i] != null) gravityFields[i].enabled = enableVisualHelpers;
        }

        bool enableColliders = !disableCollidersInFarTier || tier != SimulationTier.Far;
        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null) cachedColliders[i].enabled = enableColliders;
        }
    }

    public SimulationTier CurrentTier => currentTier;
}
