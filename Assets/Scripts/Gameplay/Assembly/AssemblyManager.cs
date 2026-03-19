using UnityEngine;

public class AssemblyManager : MonoBehaviour
{
    private const string StartPhase = "Start";
    private const string EndPhase = "End";

    public static AssemblyManager Instance { get; private set; }
    [SerializeField] private bool isAssembling = false;

    private ArtificialSatellite sourceSatellite;
    private bool isFocusSubscribed;
    private IFocusService subscribedFocusService;

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
        TryUnsubscribeFocusEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        FocusManager.InstanceChanged += HandleFocusManagerInstanceChanged;
        TrySubscribeFocusEvents();
    }

    private void OnDisable()
    {
        FocusManager.InstanceChanged -= HandleFocusManagerInstanceChanged;
        TryUnsubscribeFocusEvents();
    }

    public void StartAssemblyMode(ArtificialSatellite artificialSatellite)
    {
        if (artificialSatellite == null)
        {
            Debug.LogError("Satellite is missing.");
            return;
        }

        if (isAssembling && sourceSatellite == artificialSatellite)
        {
            return;
        }

        if (isAssembling)
        {
            StopAssemblyModeInternal();
        }

        StartAssemblyModeInternal(artificialSatellite);
    }

    public void EndAssemblyMode()
    {
        if (!isAssembling)
        {
            return;
        }

        StopAssemblyModeInternal();
    }

    private void StartAssemblyModeInternal(ArtificialSatellite artificialSatellite)
    {
        if (artificialSatellite == null)
        {
            return;
        }

        SetAssemblyState(true, artificialSatellite);
        SetAssemblyUIVisible(true);
        ActivateAssembly(artificialSatellite);
        Debug.Log($"{StartPhase} assembly mode : {artificialSatellite.name}");
    }

    private void StopAssemblyModeInternal()
    {
        ArtificialSatellite previousSourceSatellite = sourceSatellite;

        SetAssemblyUIVisible(false);
        DeactivateAssembly();
        SetAssemblyState(false, null);

        if (previousSourceSatellite != null)
        {
            Debug.Log($"{EndPhase} assembly mode : {previousSourceSatellite.name}");
        }

        Debug.Log("Assembly mode cleaned up.");
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

    private void SetAssemblyState(bool assembling, ArtificialSatellite satellite)
    {
        isAssembling = assembling;
        sourceSatellite = satellite;
    }

    private void HandleFocusChanged(Transform focused)
    {
        ArtificialSatellite focusedSatellite = ResolveAssemblyFocusSatellite(focused);

        if (!isAssembling)
        {
            if (focusedSatellite != null)
            {
                StartAssemblyModeInternal(focusedSatellite);
            }

            return;
        }

        if (focusedSatellite == sourceSatellite)
        {
            return;
        }

        StopAssemblyModeInternal();

        if (focusedSatellite != null)
        {
            StartAssemblyModeInternal(focusedSatellite);
        }
    }

    private static ArtificialSatellite ResolveAssemblyFocusSatellite(Transform focused)
    {
        if (focused == null) return null;

        ArtificialSatellite focusedSatellite = focused.GetComponent<ArtificialSatellite>();
        if (focusedSatellite != null) return focusedSatellite;

        AssemblyPartFocus partFocus = focused.GetComponent<AssemblyPartFocus>();
        if (partFocus != null)
        {
            return partFocus.OwnerSatellite;
        }

        StructureFocus structureFocus = focused.GetComponent<StructureFocus>();
        if (structureFocus != null)
        {
            return structureFocus.OwnerSatellite;
        }

        return null;
    }

    private void HandleFocusManagerInstanceChanged(FocusManager _)
    {
        RebindFocusEvents();
    }

    private void TrySubscribeFocusEvents()
    {
        if (isFocusSubscribed) return;
        if (!CoreRuntimeAccess.TryGetFocusService(out IFocusService focusService)) return;

        focusService.RegisterFocusListener(HandleFocusChanged, true);
        subscribedFocusService = focusService;
        isFocusSubscribed = true;
    }

    private void TryUnsubscribeFocusEvents()
    {
        if (!isFocusSubscribed) return;
        if (subscribedFocusService != null)
        {
            subscribedFocusService.DeregisterFocusListener(HandleFocusChanged);
        }

        subscribedFocusService = null;
        isFocusSubscribed = false;
    }

    private void RebindFocusEvents()
    {
        if (isFocusSubscribed)
        {
            TryUnsubscribeFocusEvents();
        }

        TrySubscribeFocusEvents();
    }
}

