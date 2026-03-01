using UnityEngine;

public class AssemblyManager : MonoBehaviour
{
    public static AssemblyManager Instance { get; private set; }
    public bool isAssembling = false;

    private ArtificialSatellite sourceSatellite;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void StartAssemblyMode(ArtificialSatellite artificialSatellite)
    {
        if (isAssembling)
        {
            Debug.LogWarning("Already in assembly mode.");
            return;
        }
        
        if (artificialSatellite == null)
        {
            Debug.LogError("Satellite is missing.");
            return;
        }

        isAssembling = true;
        sourceSatellite = artificialSatellite;

        AssemblyUI.Instance?.Show();
        Assembly.Instance?.Activate(sourceSatellite);
        FocusManager.Instance?.SetFocus(sourceSatellite.transform);

        Debug.Log($"Start assembly mode : {sourceSatellite.name}");
    }

    public void EndAssemblyMode()
    {
        if (!isAssembling)
        {
            Debug.LogWarning("Not in assembly mode.");
            return;
        }

        isAssembling = false;

        AssemblyUI.Instance?.Hide();
        Assembly.Instance?.Deactivate();

        if (sourceSatellite != null)
        {
            FocusManager.Instance?.SetFocus(sourceSatellite.transform);
            Debug.Log($"End assembly mode : {sourceSatellite.name}");
        }

        CleanUp();
    }

    private void CleanUp()
    {
        isAssembling = false;
        sourceSatellite = null;

        Debug.Log("Assembly mode cleaned up.");
    }

    public ArtificialSatellite GetSourceSatelliteFor(ArtificialSatellite assemblyTarget)
    {
        if (assemblyTarget == null) return null;
        if (sourceSatellite != null && assemblyTarget == sourceSatellite) return sourceSatellite;
        return assemblyTarget;
    }

    public bool IsSandboxTarget(ArtificialSatellite assemblyTarget)
    {
        return false;
    }
}
