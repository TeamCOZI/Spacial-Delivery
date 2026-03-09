using UnityEngine;

public class AssemblyManager : MonoBehaviour
{
    private const string StartPhase = "Start";
    private const string EndPhase = "End";

    public static AssemblyManager Instance { get; private set; }
    [SerializeField] private bool isAssembling = false;

    private ArtificialSatellite sourceSatellite;

    public bool IsAssembling => isAssembling;
    public ArtificialSatellite SourceSatellite => sourceSatellite;

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

    public void StartAssemblyMode(ArtificialSatellite artificialSatellite)
    {
        if (!CanStartAssemblyMode(artificialSatellite)) return;

        SetAssemblyState(true, artificialSatellite);

        SetAssemblyUIVisible(true);
        ActivateAssembly(sourceSatellite);
        FocusAndLogSourceSatellite(StartPhase);
    }

    public void EndAssemblyMode()
    {
        if (!CanEndAssemblyMode()) return;

        SetAssemblyUIVisible(false);
        DeactivateAssembly();

        if (sourceSatellite != null)
        {
            FocusAndLogSourceSatellite(EndPhase);
        }

        ResetAssemblyState();
    }

    private void ResetAssemblyState()
    {
        SetAssemblyState(false, null);

        Debug.Log("Assembly mode cleaned up.");
    }

    private void FocusSourceSatellite()
    {
        if (sourceSatellite == null) return;
        if (!TryGetFocusManager(out FocusManager focusManager)) return;
        focusManager.SetFocus(sourceSatellite.transform);
    }

    private void FocusAndLogSourceSatellite(string phase)
    {
        if (sourceSatellite == null) return;
        FocusSourceSatellite();
        Debug.Log($"{phase} assembly mode : {sourceSatellite.name}");
    }

    private static void SetAssemblyUIVisible(bool visible)
    {
        if (!TryGetAssemblyUI(out AssemblyUI ui)) return;

        if (visible) ui.Show();
        else ui.Hide();
    }

    private static void ActivateAssembly(ArtificialSatellite satellite)
    {
        if (satellite == null) return;
        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.Activate(satellite);
    }

    private static void DeactivateAssembly()
    {
        if (!TryGetAssembly(out Assembly assembly)) return;
        assembly.Deactivate();
    }

    private static bool TryGetAssemblyUI(out AssemblyUI ui)
    {
        return GameplayRuntimeAccess.TryGetAssemblyUi(out ui);
    }

    private static bool TryGetAssembly(out Assembly assembly)
    {
        return GameplayRuntimeAccess.TryGetAssembly(out assembly);
    }

    private static bool TryGetFocusManager(out FocusManager focusManager)
    {
        return CoreRuntimeAccess.TryGetFocusManager(out focusManager);
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

    private bool CanStartAssemblyMode(ArtificialSatellite artificialSatellite)
    {
        if (isAssembling)
        {
            Debug.LogWarning("Already in assembly mode.");
            return false;
        }

        if (artificialSatellite == null)
        {
            Debug.LogError("Satellite is missing.");
            return false;
        }

        return true;
    }

    private bool CanEndAssemblyMode()
    {
        if (!isAssembling)
        {
            Debug.LogWarning("Not in assembly mode.");
            return false;
        }

        return true;
    }

    private void SetAssemblyState(bool assembling, ArtificialSatellite satellite)
    {
        isAssembling = assembling;
        sourceSatellite = satellite;
    }
}
