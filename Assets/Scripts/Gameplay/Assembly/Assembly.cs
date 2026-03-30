using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(33000)]
public partial class Assembly : MonoBehaviour
{
    [System.Flags]
    private enum CellSideMask
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3
    }

    [System.Flags]
    private enum CellPortTypeMask
    {
        None = 0,
        Input = 1 << 0,
        Output = 1 << 1
    }

    public static Assembly Instance { get; private set; }

    public bool IsAssembling { get; private set; } = false;

    private bool isSnapped = false;
    private bool canPlace = false;

    private ArtificialSatellite artificialSatellite;
    private Part part;
    private GameObject partGhost;
    private Quaternion initialGhostRotation = Quaternion.identity;
    private AssemblyPort matchedOutputPort;
    private AssemblyPartPortProfile ghostPortProfile;
    private GameObject outputPortTemplate;
    private AssemblyPort pipeStartOutputPort;
    private bool pipePathStartSelected = false;
    private Vector2Int pipePathStartCell;
    private Vector2Int pipePathStartIncomingDir;
    private readonly List<Vector2Int> pipePreviewPath = new List<Vector2Int>();
    private Vector2Int lastPipeHoverCell;
    private bool hasLastPipeHoverCell = false;
    private Vector2Int pipePathTerminalDirection;
    private GameObject pipePathGhostRoot;

    private Plane assemblyPlane;
    private bool assemblyPlaneCreated = false;
    private GameObject gridPlane;

    private Camera mainCamera;

    private int gridSize = 99;
    private float cellSize = 0.1f;

    private Dictionary<Vector2Int, GameObject> occupiedCells = new Dictionary<Vector2Int, GameObject>();
    private readonly List<AssemblyPort> outputPorts = new List<AssemblyPort>();
    private readonly List<AssemblyPortPulse> outputPortPulses = new List<AssemblyPortPulse>();
    private readonly List<Renderer> ghostRenderers = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> ghostPropertyBlocks = new List<MaterialPropertyBlock>();
    private readonly Dictionary<Vector2Int, CellSideMask> inputMaskByCell = new Dictionary<Vector2Int, CellSideMask>();
    private readonly Dictionary<Vector2Int, CellSideMask> outputMaskByCell = new Dictionary<Vector2Int, CellSideMask>();
    private readonly Dictionary<Vector2Int, Dictionary<CellSideMask, CellPortTypeMask>> portTypesByCellAndSide =
        new Dictionary<Vector2Int, Dictionary<CellSideMask, CellPortTypeMask>>();
    private readonly Dictionary<Vector2Int, Dictionary<CellSideMask, string>> portOwnerByCellAndSide =
        new Dictionary<Vector2Int, Dictionary<CellSideMask, string>>();
    private readonly Dictionary<Vector2Int, Dictionary<CellSideMask, AssemblyPort>> outputPortsByCellAndSide =
        new Dictionary<Vector2Int, Dictionary<CellSideMask, AssemblyPort>>();
    private readonly Dictionary<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)> coreLayoutByPort =
        new Dictionary<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)>();
    private bool outputPortRefreshQueued = false;
    private Coroutine outputPortRefreshCoroutine;
    [Header("Removal")]
    [Tooltip("If enabled, deleting selected parts also removes parts that become disconnected from the Core.")]
    [SerializeField] private bool removeDisconnectedPartsAfterDeletion = false;
    [SerializeField] private bool debugPipePlacement = true;
    [SerializeField] private bool debugHoverCellPortMapping = true;
    [SerializeField] private bool debugConnectionGraph = true;
    [SerializeField] private bool debugActiveSplitGraph = true;
    [SerializeField] private bool enableInputPortAutoMapping = false;
    private bool hasPipeDebugState = false;
    private Vector2Int lastPipeDebugCell;
    private float lastPipeDebugRotZ;
    private bool lastPipeDebugMatch;
    private CellSideMask lastPipeDebugInputSide;
    private CellSideMask lastPipeDebugRequiredSide;
    private bool hasHoverDebugCell = false;
    private Vector2Int lastHoverDebugCell;
    private int outputSideCycleOffset = 0;
    private bool hasLastOutputCycleCell = false;
    private Vector2Int lastOutputCycleCell;
    private readonly List<CellSideMask> availableOutputSides = new List<CellSideMask>(4);
    private bool isSelectionMode = false;
    private GameObject selectionStartOwner;
    private readonly HashSet<GameObject> selectedOwners = new HashSet<GameObject>();
    private readonly List<GameObject> selectionPreviewOwners = new List<GameObject>();
    private readonly List<GameObject> selectionPathOwners = new List<GameObject>();
    private bool isRemoveSelectionPreviewActive = false;
    private readonly List<GameObject> removeCascadePreviewOwners = new List<GameObject>();
    private SelectionHighlightMode currentSelectionHighlightMode = SelectionHighlightMode.Valid;
    private readonly HashSet<GameObject> removeHoverOwners = new HashSet<GameObject>();
    private readonly List<Renderer> removeHoverRenderers = new List<Renderer>();
    private readonly List<RemoveHoverRendererState> removeHoverRendererStates =
        new List<RemoveHoverRendererState>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color GhostValidColor = new Color(0.2f, 0.55f, 1f, 0.5f);
    private static readonly Color GhostInvalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    private static readonly Color InputPortColor = new Color(0.15f, 0.9f, 0.95f, 1f);
    private const float DirectionEpsilonSqr = 0.000001f;
    private const float MinCameraSpaceDepth = 0.01f;
    private static readonly Vector3 DefaultPortDirection = Vector3.right;
    private static readonly Vector2 ZeroSnapOffset = Vector2.zero;
    private const string RuntimePortsRootName = "__RuntimePorts";
    private const string AssemblyGridPlaneName = "AssemblyGridPlane";
    private const string OutputPortNameToken = "OutputPort";
    private static readonly Quaternion[] PipeAutoRotations =
    {
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(0f, 0f, 180f),
        Quaternion.Euler(0f, 0f, 270f)
    };
    private static readonly CellSideMask[] OutputSidePriorityOrder =
    {
        CellSideMask.Left,
        CellSideMask.Top,
        CellSideMask.Right,
        CellSideMask.Bottom
    };

    private sealed class RemoveHoverRendererState
    {
        public Renderer renderer;
        public bool wasEnabled;
        public MaterialPropertyBlock originalPropertyBlock;
    }

    private enum SelectionHighlightMode
    {
        Valid,
        Invalid
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        DisposeSelectionDragVisual();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (artificialSatellite != null && assemblyPlaneCreated)
        {
            DebugLogHoveredCellPortMapping();
            DrawDebugConnectionGraph();
        }

        if (isSelectionMode)
        {
            UpdateSelectionMode();
            return;
        }

        if (!IsAssembling) return;

        bool cancelByMouse = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool cancelByEsc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (cancelByMouse || cancelByEsc)
        {
            Cancel();
            return;
        }

        UpdateGhost();

        if (UserInput.IsWorldInputBlockedByUiPanels())
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (HasActiveStructurePlacement())
            {
                ApplyStructure();
            }
            else if (UsesPipePathPlacement(part))
            {
                HandlePipeLeftClick();
            }
            else
            {
                Apply();
            }
        }
    }

    public void Activate(ArtificialSatellite artificialSatellite)
    {
        this.artificialSatellite = artificialSatellite;
        IsAssembling = false;

        TryEnsureMainCamera();
        Vector3 planeNormal = mainCamera != null ? -mainCamera.transform.forward : artificialSatellite.transform.forward;
        Vector3 planePoint = artificialSatellite.transform.position;
        assemblyPlane = new Plane(planeNormal, planePoint);
        assemblyPlaneCreated = true;

        CreateGridPlane();

        EnterAssemblyCameraMode();

        occupiedCells.Clear();
        int centerIndex = gridSize / 2;
        RegisterCoreOccupiedCells(new Vector2Int(centerIndex, centerIndex));
        PrepareStructurePlacementContext();
        outputPortTemplate = FindOutputPortTemplate();
        RequestRefreshOutputPorts();

        Debug.Log("Assembly activated.");
    }

    public void Deactivate()
    {
        ExitAssemblyCameraMode();

        StopSelectionMode();
        Cancel();
        artificialSatellite = null;
        IsAssembling = false;
        assemblyPlaneCreated = false;
        DestroyGridPlane();
        occupiedCells.Clear();
        ResetStructurePlacementSession();

        Debug.Log("Assembly deactivated.");
    }

    public void SelectPart(Part part)
    {
        StopSelectionMode();
        ClearStructurePlacementSelection();
        DestroyPartGhost(false);

        if (part == null || part.ghostPrefab == null)
        {
            Debug.LogError($"Part : {part}, Part Ghost : {partGhost}");
            return;
        }

        RefreshOutputPortsNow();
        this.part = part;
        ResetPipePathSelectionState();
        ClearPipePathGhosts();
        ResetOutputCycleSelectionState();

        partGhost = Instantiate(part.ghostPrefab, artificialSatellite.transform);
        initialGhostRotation = partGhost.transform.localRotation;
        _ = ComponentUtility.GetOrAddComponent<AssemblyGhostMarker>(partGhost);

        ghostPortProfile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(partGhost);
        ConfigureRuntimePorts(partGhost, part, ghostPortProfile, true);

        CacheGhostRenderers();
        SetOutputPortsPulsing(true);
        SetGhostPlacementVisual(false);

        SmallScaleLayerUtility.ApplyRecursively(partGhost.transform);
        IsAssembling = true;
        Debug.Log($"Assembling {part.partName}.");
    }

    private void UpdateGhost()
    {
        if (HasActiveStructurePlacement())
        {
            UpdateStructureGhost();
            return;
        }

        if (partGhost == null || artificialSatellite == null || !assemblyPlaneCreated) return;

        if (!TryEnsureMainCamera()) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        float cameraSpaceDepth = Vector3.Dot(
            artificialSatellite.transform.position - mainCamera.transform.position,
            mainCamera.transform.forward
        );
        cameraSpaceDepth = Mathf.Max(MinCameraSpaceDepth, cameraSpaceDepth);

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cameraSpaceDepth));
        Vector3 hitPoint = artificialSatellite.transform.InverseTransformPoint(worldPoint);

        Vector2 snapOffset = GetCurrentPartSnapOffset();
        Vector2 pivotOffset = GetCurrentPartPivotOffset();
        Vector2Int centerCell = LocalPositionToGrid(hitPoint, snapOffset);
        Vector3 finalPosition = GridToLocalPosition(centerCell, snapOffset) + (Vector3)pivotOffset;
        partGhost.transform.localPosition = finalPosition;
        isSnapped = true;
        UpdateOutputCycleSelection(centerCell);

        if (UsesPipePathPlacement(part) && pipePathStartSelected)
        {
            UpdatePipePathPreview();
            return;
        }

        bool areaFree = IsCurrentGhostPlacementAreaFree();
        if (!areaFree)
        {
            if (UsesPipePathPlacement(part) && !pipePathStartSelected && partGhost != null)
            {
                partGhost.transform.localRotation = initialGhostRotation;
            }
            canPlace = false;
        }
        else
        {
            canPlace = TryMatchGhostToOutputPort();
        }
        SetGhostPlacementVisual(canPlace);

        if (UsesPipePathPlacement(part) && !pipePathStartSelected)
        {
            RefreshCurrentPipePlacementGhostEnds(canPlace ? GhostValidColor : GhostInvalidColor);
        }
    }

    private void Apply()
    {
        if (!isSnapped || !canPlace || artificialSatellite == null || part == null || partGhost == null) return;

        Vector3 finalLocalPos = partGhost.transform.localPosition;
        Quaternion finalLocalRot = partGhost.transform.localRotation;
        Vector2Int gridPos = GetCurrentGhostCenterCell();
        ArtificialSatellite sourceSatellite = ResolveSourceSatelliteForAssemblyTarget(artificialSatellite);

        if (TryCollectPipeReplacementRoots(gridPos, finalLocalRot, out List<GameObject> replacementRoots))
        {
            RemoveReplacementPipeRoots(artificialSatellite, sourceSatellite, replacementRoots);
        }

        GameObject placedObject = AddPartToSatellite(artificialSatellite, part, finalLocalPos, finalLocalRot);

        if (ShouldMirrorToSourceSatellite(artificialSatellite, sourceSatellite))
        {
            AddPartToSatellite(sourceSatellite, part, finalLocalPos, finalLocalRot);
        }

        RebuildMeshesForAssemblyTargets(artificialSatellite, sourceSatellite);

        Debug.Log($"Applied part {part.partName} to {artificialSatellite.name}.");

        if (placedObject != null)
        {
            if (partGhost != null)
            {
                AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
                if (layout != null &&
                    TryGetFootprintCellsFromLayout(layout, gridPos, finalLocalRot, out List<Vector2Int> occupiedFootprint))
                {
                    for (int i = 0; i < occupiedFootprint.Count; i++)
                    {
                        Vector2Int cell = occupiedFootprint[i];
                        if (!IsInsideGrid(cell)) continue;
                        occupiedCells[cell] = placedObject;
                    }
                }
                else
                {
                    RegisterOccupiedRect(gridPos, GetCellSpanForRotation(finalLocalRot), placedObject);
                }
            }
            else
            {
                RegisterOccupiedRect(gridPos, GetCellSpanForRotation(finalLocalRot), placedObject);
            }
        }

        if (matchedOutputPort != null)
        {
            matchedOutputPort.SetOccupied(true);
        }

        Cancel();
        RequestRefreshOutputPorts();
    }

    private GameObject AddPartToSatellite(ArtificialSatellite targetSatellite, Part targetPart, Vector3 localPos, Quaternion localRot)
    {
        if (targetSatellite == null || targetPart == null || targetPart.partPrefab == null) return null;

        GameObject gameObject = Instantiate(targetPart.partPrefab);
        return AddPartObjectToSatellite(targetSatellite, targetPart, gameObject, localPos, localRot, false, Vector3.zero, Vector3.zero);
    }

    private GameObject AddPartObjectToSatellite(
        ArtificialSatellite targetSatellite,
        Part targetPart,
        GameObject gameObject,
        Vector3 localPos,
        Quaternion localRot,
        bool usePipePortOverride,
        Vector3 overrideInputLocal,
        Vector3 overrideOutputLocal)
    {
        if (targetSatellite == null || targetPart == null || gameObject == null) return null;

        gameObject.transform.SetParent(targetSatellite.transform, false);
        SmallScaleLayerUtility.ApplyRecursively(gameObject.transform);
        EnsurePartCollider(gameObject);

        AssemblyPartPortProfile targetProfile = ComponentUtility.GetOrAddComponent<AssemblyPartPortProfile>(gameObject);

        if (IsPipePart(targetPart) && usePipePortOverride)
        {
            ConfigureRuntimePipePorts(gameObject, targetProfile, false, overrideInputLocal, overrideOutputLocal);
        }
        else
        {
            ConfigureRuntimePorts(gameObject, targetPart, targetProfile, false);
        }

        AssemblyAttachmentHub attachmentHub = ComponentUtility.GetOrAddComponent<AssemblyAttachmentHub>(targetSatellite.gameObject);

        attachmentHub.RegisterOrUpdate(gameObject.transform, localPos, localRot, gameObject.transform.localScale);
        attachmentHub.SyncAttachments();

        AssemblyPartFocus partFocus = ComponentUtility.GetOrAddComponent<AssemblyPartFocus>(gameObject);
        partFocus.Initialize(targetPart, targetSatellite);
        _ = SplitPipeUtility.ResolveState(gameObject);
        _ = MergePipeUtility.ResolveState(gameObject);

        ModulePartInventoryUtility.EnsurePartInventories(gameObject, targetPart);
        ApplyPartVisualConfiguration(gameObject, targetPart);

        AssemblyMeshCombiner combiner = targetSatellite.GetComponent<AssemblyMeshCombiner>();
        if (combiner != null && combiner.combinedOnlyMode &&
            !ShouldKeepPartSourceRenderersVisible(targetPart) &&
            !HasAlwaysVisiblePortVisuals(gameObject))
        {
            MeshRenderer[] meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null) meshRenderers[i].enabled = false;
            }
        }

        return gameObject;
    }

    private static bool ShouldKeepPartSourceRenderersVisible(Part targetPart)
    {
        if (targetPart == null || string.IsNullOrWhiteSpace(targetPart.partName)) return false;

        string partName = targetPart.partName;
        return partName.IndexOf("Launcher", System.StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("DropPort", System.StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("Pipe", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasAlwaysVisiblePortVisuals(GameObject root)
    {
        if (root == null) return false;
        if (root.GetComponentInChildren<AssemblyPortVisualMarker>(true) != null) return true;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Transform t = renderers[i] != null ? renderers[i].transform : null;
            if (t == null) continue;
            if (ContainsNameTokenInHierarchy(t, "Port")) return true;
            if (ContainsNameTokenInHierarchy(t, "Dock")) return true;
            if (ContainsNameTokenInHierarchy(t, "Drop")) return true;
        }

        return false;
    }

    private static void ApplyPartVisualConfiguration(GameObject root, Part targetPart)
    {
        if (root == null || targetPart == null) return;
        if (targetPart.partType != PartType.Pipe) return;

        SetRendererColorRecursiveStatic(root.transform, targetPart.partColor);
    }

    private static bool ContainsNameTokenInHierarchy(Transform t, string token)
    {
        if (t == null || string.IsNullOrWhiteSpace(token)) return false;

        Transform current = t;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.name) &&
                current.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void EnsurePartCollider(GameObject root)
    {
        if (root == null) return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0) return;

        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null) box = root.AddComponent<BoxCollider>();

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = root.transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        if (localSize.sqrMagnitude < 1e-8f) localSize = Vector3.one;

        box.center = localCenter;
        box.size = localSize;
    }

    private static void RebuildCombinedMesh(ArtificialSatellite targetSatellite)
    {
        if (targetSatellite == null) return;

        AssemblyMeshCombiner combiner = ComponentUtility.GetOrAddComponent<AssemblyMeshCombiner>(targetSatellite.gameObject);
        combiner.RebuildCombinedMesh();
    }

    private static ArtificialSatellite ResolveSourceSatelliteForAssemblyTarget(ArtificialSatellite assemblyTarget)
    {
        if (!TryGetAssemblyManager(out AssemblyManager manager)) return null;
        return manager.GetSourceSatelliteFor(assemblyTarget);
    }

    private static bool IsSandboxTarget(ArtificialSatellite assemblyTarget)
    {
        if (!TryGetAssemblyManager(out AssemblyManager manager)) return false;
        return manager.IsSandboxTarget(assemblyTarget);
    }

    private static bool ShouldMirrorToSourceSatellite(ArtificialSatellite assemblyTarget, ArtificialSatellite sourceSatellite)
    {
        return sourceSatellite != null && sourceSatellite != assemblyTarget;
    }

    private void RebuildMeshesForAssemblyTargets(ArtificialSatellite assemblyTarget, ArtificialSatellite sourceSatellite)
    {
        RebuildCombinedMesh(assemblyTarget);
        RefreshPipeEndVisualsForSatellite(assemblyTarget);

        bool isSandboxTarget = IsSandboxTarget(assemblyTarget);
        if (!isSandboxTarget && ShouldMirrorToSourceSatellite(assemblyTarget, sourceSatellite))
        {
            RebuildCombinedMesh(sourceSatellite);
            RefreshPipeEndVisualsForSatellite(sourceSatellite);
        }
    }


    private void Cancel()
    {
        SetOutputPortsPulsing(false);
        ClearPipePathGhosts();

        DestroyPartGhost(true);
        DestroyStructureGhost(true);

        ResetSelectedPartState();
        ResetSelectedStructureState();
        IsAssembling = false;
        ResetGhostPlacementState();
        ResetPipePathSelectionState();
        ResetHoverDebugState();

        Debug.Log("Assembly cancelled.");
    }

    private void ResetPipePathSelectionState()
    {
        pipePathStartSelected = false;
        pipeStartOutputPort = null;
        pipePreviewPath.Clear();
        pipePathStartIncomingDir = Vector2Int.zero;
        hasLastPipeHoverCell = false;
        pipePathTerminalDirection = Vector2Int.zero;
        ResetPipeDebugState();
    }

    private void ResetPipeDebugState()
    {
        hasPipeDebugState = false;
        lastPipeDebugCell = default;
        lastPipeDebugRotZ = 0f;
        lastPipeDebugMatch = false;
        lastPipeDebugInputSide = CellSideMask.None;
        lastPipeDebugRequiredSide = CellSideMask.None;
    }

    private void ResetHoverDebugState()
    {
        hasHoverDebugCell = false;
        lastHoverDebugCell = default;
    }

    private void ResetGhostPlacementState()
    {
        matchedOutputPort = null;
        ghostPortProfile = null;
        isSnapped = false;
        canPlace = false;
    }

    private void ResetSelectedPartState()
    {
        partGhost = null;
        part = null;
        ghostRenderers.Clear();
        ghostPropertyBlocks.Clear();
        ResetOutputCycleSelectionState();
    }

    private void DestroyPartGhost(bool hideBeforeDestroy)
    {
        if (partGhost == null) return;

        if (hideBeforeDestroy)
        {
            partGhost.SetActive(false);
        }

        Destroy(partGhost);
    }

    private void ResetOutputCycleSelectionState()
    {
        outputSideCycleOffset = 0;
        hasLastOutputCycleCell = false;
        lastOutputCycleCell = default;
        availableOutputSides.Clear();
    }

    public bool IsSelectionModeActive => isSelectionMode;
    public bool HasSelection => selectedOwners.Count > 0;

    public bool ToggleSelectionMode()
    {
        if (isSelectionMode)
        {
            StopSelectionMode();
            return false;
        }

        StartSelectionMode();
        return true;
    }

    public void StartSelectionMode()
    {
        Cancel();
        RefreshOutputPortsNow();
        isSelectionMode = true;
        TryEnsureSelectionDragVisual();
        ResetSelectionDragState();
        selectionStartOwner = null;
        isRemoveSelectionPreviewActive = false;
        selectedOwners.Clear();
        selectionPreviewOwners.Clear();
        selectionPathOwners.Clear();
        removeCascadePreviewOwners.Clear();
        ClearRemoveHoverVisual();
    }

    public void StopSelectionMode()
    {
        if (!isSelectionMode && removeHoverOwners.Count == 0) return;

        isSelectionMode = false;
        ResetSelectionDragState();
        selectionStartOwner = null;
        isRemoveSelectionPreviewActive = false;
        selectedOwners.Clear();
        selectionPreviewOwners.Clear();
        selectionPathOwners.Clear();
        removeCascadePreviewOwners.Clear();
        ClearRemoveHoverVisual();
    }

    public void RemoveSelectedParts()
    {
        if (!isSelectionMode || selectedOwners.Count == 0) return;
        List<GameObject> removableRoots = new List<GameObject>(selectedOwners.Count);
        BuildRemovalSetForSelectedParts(selectedOwners, removableRoots);
        if (!TryRemoveParts(removableRoots)) return;

        ResetSelectionDragState();
        selectionStartOwner = null;
        isRemoveSelectionPreviewActive = false;
        selectedOwners.Clear();
        selectionPreviewOwners.Clear();
        selectionPathOwners.Clear();
        removeCascadePreviewOwners.Clear();
        SetRemoveHoverTargets(null);
    }

    public void SetRemoveSelectionPreviewActive(bool active)
    {
        isRemoveSelectionPreviewActive = active;
    }

    private void UpdateSelectionMode()
    {
        if (artificialSatellite == null || !assemblyPlaneCreated)
        {
            ResetSelectionDragState();
            ClearRemoveHoverVisual();
            return;
        }

        bool cancelByMouse = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        bool cancelByEsc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (cancelByMouse || cancelByEsc)
        {
            StopSelectionMode();
            return;
        }

        if (isRemoveSelectionPreviewActive && selectedOwners.Count > 0)
        {
            ResetSelectionDragState();
            removeCascadePreviewOwners.Clear();
            BuildRemovalSetForSelectedParts(selectedOwners, removeCascadePreviewOwners);
            currentSelectionHighlightMode = SelectionHighlightMode.Invalid;
            SetRemoveHoverTargets(removeCascadePreviewOwners);
            RefreshRemoveHoverVisual();
            return;
        }

        HandleSelectionRectangleInput();
        currentSelectionHighlightMode = SelectionHighlightMode.Valid;
        PopulateSelectionPreviewOwners();
        SetRemoveHoverTargets(selectionPreviewOwners);
        RefreshRemoveHoverVisual();
    }

    private bool TryGetHoveredGridCell(out Vector2Int cell)
    {
        cell = default;
        if (!TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)) return false;

        cell = LocalPositionToGrid(localPoint, ZeroSnapOffset);
        return IsInsideGrid(cell);
    }

    private bool TryRemoveParts(List<GameObject> removableRoots)
    {
        if (removableRoots == null || removableRoots.Count == 0) return false;

        List<(Vector2Int centerCell, string partName, bool hasCenterCell)> removedPartInfos =
            new List<(Vector2Int centerCell, string partName, bool hasCenterCell)>();
        bool removedAny = false;

        for (int i = 0; i < removableRoots.Count; i++)
        {
            GameObject removableRoot = removableRoots[i];
            if (!CanSelectPart(removableRoot)) continue;

            bool hasCenterCell = TryGetPartCenterCell(removableRoot, out Vector2Int centerCell);
            removedPartInfos.Add((centerCell, GetPartName(removableRoot), hasCenterCell));
            RemovePartObjectFromSatellite(artificialSatellite, removableRoot);
            removedAny = true;
        }

        if (!removedAny) return false;

        ArtificialSatellite sourceSatellite = ResolveSourceSatelliteForAssemblyTarget(artificialSatellite);
        if (ShouldMirrorToSourceSatellite(artificialSatellite, sourceSatellite))
        {
            HashSet<GameObject> removedMirroredParts = new HashSet<GameObject>();
            for (int i = 0; i < removedPartInfos.Count; i++)
            {
                (Vector2Int centerCell, string partName, bool hasCenterCell) partInfo = removedPartInfos[i];
                if (!partInfo.hasCenterCell) continue;

                GameObject mirrored = FindMatchingPartOnSatellite(sourceSatellite, partInfo.centerCell, partInfo.partName);
                if (mirrored == null) continue;
                if (!removedMirroredParts.Add(mirrored)) continue;
                RemovePartObjectFromSatellite(sourceSatellite, mirrored);
            }
        }

        StartCoroutine(RefreshAfterPartRemovalEndOfFrame(artificialSatellite, sourceSatellite));
        return true;
    }

    private IEnumerator RefreshAfterPartRemovalEndOfFrame(
        ArtificialSatellite assemblyTarget,
        ArtificialSatellite sourceSatellite)
    {
        yield return new WaitForEndOfFrame();

        if (assemblyTarget == null) yield break;

        RebuildMeshesForAssemblyTargets(assemblyTarget, sourceSatellite);
        RequestRefreshOutputPorts();
    }

    private bool TryGetSelectablePartUnderMouse(out GameObject removableRoot)
    {
        removableRoot = null;
        if (!TryEnsureMainCamera()) return false;
        if (Mouse.current == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f))
        {
            AssemblyPartFocus hitFocus = hit.collider != null
                ? hit.collider.GetComponentInParent<AssemblyPartFocus>(true)
                : null;
            if (hitFocus != null && CanSelectPart(hitFocus.gameObject))
            {
                removableRoot = hitFocus.gameObject;
                return true;
            }
        }

        if (!TryGetHoveredGridCell(out Vector2Int hoveredCell)) return false;
        if (!occupiedCells.TryGetValue(hoveredCell, out GameObject owner) || owner == null) return false;

        GameObject byCell = ResolveRemovablePartRoot(owner);
        if (!CanSelectPart(byCell)) return false;

        removableRoot = byCell;
        return true;
    }

    private void RemovePartObjectFromSatellite(ArtificialSatellite satellite, GameObject partRoot)
    {
        if (satellite == null || partRoot == null) return;

        AssemblyAttachmentHub hub = satellite.GetComponent<AssemblyAttachmentHub>();
        if (hub != null)
        {
            hub.Unregister(partRoot.transform);
        }

        RemoveOccupiedCellsOwnedBy(partRoot);
        Destroy(partRoot);
    }

    private void RemoveOccupiedCellsOwnedBy(GameObject owner)
    {
        if (owner == null) return;

        List<Vector2Int> cellsToRemove = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, GameObject> pair in occupiedCells)
        {
            if (pair.Value == owner)
            {
                cellsToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < cellsToRemove.Count; i++)
        {
            occupiedCells.Remove(cellsToRemove[i]);
        }
    }

    private GameObject ResolveRemovablePartRoot(GameObject owner)
    {
        if (owner == null) return null;
        if (owner == artificialSatellite.gameObject) return null;

        AssemblyPartFocus focus = owner.GetComponentInParent<AssemblyPartFocus>(true);
        if (focus != null) return focus.gameObject;
        return owner;
    }

    private bool CanSelectPart(GameObject owner)
    {
        if (owner == null) return false;
        if (owner == artificialSatellite.gameObject) return false;
        if (IsCoreLikeProtectedPart(owner)) return false;

        AssemblyPartFocus focus = owner.GetComponent<AssemblyPartFocus>();
        if (focus == null) return false;

        if (focus.SourcePart == null)
        {
            string ownerName = owner.name;
            if (string.IsNullOrWhiteSpace(ownerName)) return true;
            return ownerName.IndexOf("Core", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        return focus.SourcePart.partType != PartType.Core;
    }

    private bool IsCoreLikeProtectedPart(GameObject owner)
    {
        if (owner == null) return false;

        AssemblyPartFocus focus = owner.GetComponent<AssemblyPartFocus>();
        if (focus != null && focus.SourcePart != null)
        {
            string partName = focus.SourcePart.partName;
            if (string.IsNullOrWhiteSpace(partName)) return false;

            if (string.Equals(partName, "Core", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(partName, "Launcher", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (partName.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        string ownerName = owner.name;
        if (string.IsNullOrWhiteSpace(ownerName)) return false;
        if (ownerName.IndexOf("Core", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (ownerName.IndexOf("Launcher", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (ownerName.IndexOf("Drop", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private void RemoveReplacementPipeRoots(
        ArtificialSatellite assemblyTarget,
        ArtificialSatellite sourceSatellite,
        List<GameObject> replacementRoots)
    {
        if (assemblyTarget == null || replacementRoots == null || replacementRoots.Count == 0)
        {
            return;
        }

        List<(Vector2Int centerCell, string partName, bool hasCenterCell)> removedPartInfos =
            new List<(Vector2Int centerCell, string partName, bool hasCenterCell)>();
        HashSet<GameObject> removedRoots = new HashSet<GameObject>();
        for (int i = 0; i < replacementRoots.Count; i++)
        {
            GameObject removableRoot = replacementRoots[i];
            if (removableRoot == null || !removedRoots.Add(removableRoot))
            {
                continue;
            }

            bool hasCenterCell = TryGetPartCenterCell(removableRoot, out Vector2Int centerCell);
            removedPartInfos.Add((centerCell, GetPartName(removableRoot), hasCenterCell));
            RemovePartObjectFromSatellite(assemblyTarget, removableRoot);
        }

        if (!ShouldMirrorToSourceSatellite(assemblyTarget, sourceSatellite))
        {
            return;
        }

        HashSet<GameObject> removedMirroredParts = new HashSet<GameObject>();
        for (int i = 0; i < removedPartInfos.Count; i++)
        {
            (Vector2Int centerCell, string partName, bool hasCenterCell) partInfo = removedPartInfos[i];
            if (!partInfo.hasCenterCell)
            {
                continue;
            }

            GameObject mirrored = FindMatchingPartOnSatellite(sourceSatellite, partInfo.centerCell, partInfo.partName);
            if (mirrored == null || !removedMirroredParts.Add(mirrored))
            {
                continue;
            }

            RemovePartObjectFromSatellite(sourceSatellite, mirrored);
        }
    }

    private GameObject FindMatchingPartOnSatellite(ArtificialSatellite satellite, Vector2Int centerCell, string partName)
    {
        if (satellite == null || !IsInsideGrid(centerCell)) return null;

        AssemblyPartFocus[] parts = satellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            AssemblyPartFocus partFocus = parts[i];
            if (partFocus == null || partFocus.SourcePart == null) continue;

            if (!string.IsNullOrWhiteSpace(partName) &&
                !string.Equals(partFocus.SourcePart.partName, partName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryGetPartCenterCell(partFocus.gameObject, out Vector2Int candidateCell)) continue;
            if (candidateCell != centerCell) continue;
            return partFocus.gameObject;
        }

        return null;
    }

    private bool TryGetPartCenterCell(GameObject partRoot, out Vector2Int centerCell)
    {
        centerCell = Vector2Int.zero;
        if (partRoot == null) return false;

        Transform rootTransform = partRoot.transform;
        AssemblyPartPortLayout layout = partRoot.GetComponent<AssemblyPartPortLayout>();
        AssemblyPartFocus focus = partRoot.GetComponent<AssemblyPartFocus>();
        Vector2 snapOffset = GetSnapOffsetFromPartFocus(focus);

        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            Vector2 pivotOffset = GetPartPivotOffset(layout, rootTransform.localRotation);
            centerCell = LocalPositionToGrid(rootTransform.localPosition - (Vector3)pivotOffset, snapOffset);
            return IsInsideGrid(centerCell);
        }

        centerCell = LocalPositionToGrid(rootTransform.localPosition, snapOffset);
        return IsInsideGrid(centerCell);
    }

    private static string GetPartName(GameObject root)
    {
        if (root == null) return string.Empty;
        AssemblyPartFocus focus = root.GetComponent<AssemblyPartFocus>();
        if (focus == null || focus.SourcePart == null) return string.Empty;
        return focus.SourcePart.partName;
    }

    private void SetRemoveHoverTargets(List<GameObject> owners)
    {
        if (owners == null || owners.Count == 0)
        {
            ClearRemoveHoverVisual();
            return;
        }

        if (owners.Count == removeHoverOwners.Count)
        {
            bool same = true;
            for (int i = 0; i < owners.Count; i++)
            {
                if (!removeHoverOwners.Contains(owners[i]))
                {
                    same = false;
                    break;
                }
            }

            if (same) return;
        }

        ClearRemoveHoverVisual();
        for (int i = 0; i < owners.Count; i++)
        {
            GameObject owner = owners[i];
            if (owner == null) continue;
            if (!removeHoverOwners.Add(owner)) continue;
            ApplyRemoveHoverVisual(owner);
        }
    }

    private void ApplyRemoveHoverVisual(GameObject owner)
    {
        if (owner == null) return;
        Color highlightColor = ResolveSelectionHighlightColor(currentSelectionHighlightMode);

        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;
            if (!renderer.sharedMaterial.HasProperty(BaseColorId) && !renderer.sharedMaterial.HasProperty(ColorId)) continue;

            MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalBlock);
            removeHoverRendererStates.Add(new RemoveHoverRendererState
            {
                renderer = renderer,
                wasEnabled = renderer.enabled,
                originalPropertyBlock = originalBlock
            });
            renderer.enabled = true;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, highlightColor);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, highlightColor);
            renderer.SetPropertyBlock(block);
            removeHoverRenderers.Add(renderer);
        }
    }

    private void RefreshRemoveHoverVisual()
    {
        if (removeHoverOwners.Count == 0) return;
        Color highlightColor = ResolveSelectionHighlightColor(currentSelectionHighlightMode);

        for (int i = 0; i < removeHoverRenderers.Count; i++)
        {
            Renderer renderer = removeHoverRenderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;
            if (!renderer.sharedMaterial.HasProperty(BaseColorId) && !renderer.sharedMaterial.HasProperty(ColorId)) continue;

            renderer.enabled = true;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, highlightColor);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, highlightColor);
            renderer.SetPropertyBlock(block);
        }
    }

    private Color ResolveSelectionHighlightColor(SelectionHighlightMode mode)
    {
        return mode == SelectionHighlightMode.Invalid ? GhostInvalidColor : SelectionValidColor;
    }

    private void ClearRemoveHoverVisual()
    {
        for (int i = 0; i < removeHoverRenderers.Count; i++)
        {
            Renderer renderer = removeHoverRenderers[i];
            if (renderer == null) continue;
        }

        for (int i = 0; i < removeHoverRendererStates.Count; i++)
        {
            RemoveHoverRendererState state = removeHoverRendererStates[i];
            if (state == null || state.renderer == null) continue;
            state.renderer.SetPropertyBlock(state.originalPropertyBlock);
            state.renderer.enabled = state.wasEnabled;
        }

        removeHoverRendererStates.Clear();
        removeHoverRenderers.Clear();
        removeHoverOwners.Clear();
    }

    private bool TryFindPartPath(GameObject startOwner, GameObject endOwner, List<GameObject> pathOwners)
    {
        if (pathOwners == null) return false;
        pathOwners.Clear();
        if (!CanSelectPart(startOwner) || !CanSelectPart(endOwner)) return false;

        if (startOwner == endOwner)
        {
            pathOwners.Add(startOwner);
            return true;
        }

        Dictionary<GameObject, HashSet<GameObject>> adjacency = new Dictionary<GameObject, HashSet<GameObject>>();
        BuildPartAdjacencyGraph(adjacency);
        if (!adjacency.ContainsKey(startOwner) || !adjacency.ContainsKey(endOwner)) return false;

        Queue<GameObject> open = new Queue<GameObject>();
        HashSet<GameObject> visited = new HashSet<GameObject>();
        Dictionary<GameObject, GameObject> cameFrom = new Dictionary<GameObject, GameObject>();

        visited.Add(startOwner);
        open.Enqueue(startOwner);

        while (open.Count > 0)
        {
            GameObject current = open.Dequeue();
            if (current == endOwner) break;

            if (!adjacency.TryGetValue(current, out HashSet<GameObject> neighbors)) continue;
            foreach (GameObject next in neighbors)
            {
                if (next == null) continue;
                if (!visited.Add(next)) continue;
                cameFrom[next] = current;
                open.Enqueue(next);
            }
        }

        if (!visited.Contains(endOwner)) return false;

        GameObject trace = endOwner;
        pathOwners.Add(trace);
        while (trace != startOwner)
        {
            if (!cameFrom.TryGetValue(trace, out GameObject prev)) return false;
            trace = prev;
            pathOwners.Add(trace);
        }

        pathOwners.Reverse();
        return pathOwners.Count > 0;
    }

    private void BuildPartAdjacencyGraph(
        Dictionary<GameObject, HashSet<GameObject>> adjacency,
        HashSet<GameObject> coreConnectedSeeds = null)
    {
        if (adjacency == null) return;
        adjacency.Clear();
        coreConnectedSeeds?.Clear();
        RefreshOutputPortsNow();

        AssemblyPartFocus[] partFocuses = artificialSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
        for (int i = 0; i < partFocuses.Length; i++)
        {
            AssemblyPartFocus focus = partFocuses[i];
            if (focus == null) continue;
            GameObject partRoot = focus.gameObject;
            if (!CanSelectPart(partRoot)) continue;
            if (!adjacency.ContainsKey(partRoot))
            {
                adjacency[partRoot] = new HashSet<GameObject>();
            }
        }

        BuildSourcePortOwnerMaps(
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
            out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
            out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys);

        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        foreach (KeyValuePair<Vector2Int, GameObject> pair in occupiedCells)
        {
            Vector2Int cell = pair.Key;
            if (!IsInsideGrid(cell)) continue;

            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2Int neighbor = cell + dirs[i];
                if (!IsInsideGrid(neighbor)) continue;
                if (!occupiedCells.ContainsKey(neighbor)) continue;

                if (!TryGetSideBetweenCells(cell, neighbor, out CellSideMask sideFromCell)) continue;
                CellSideMask sideFromNeighbor = OppositeSide(sideFromCell);
                if (sideFromNeighbor == CellSideMask.None) continue;

                AddConnectionsForFacingCells(
                    inputCell: cell,
                    inputSide: sideFromCell,
                    outputCell: neighbor,
                    outputSide: sideFromNeighbor,
                    inputOwnersByKey,
                    outputOwnersByKey,
                    coreOutputKeys,
                    adjacency,
                    coreConnectedSeeds);
            }
        }
    }

    private void BuildSourcePortOwnerMaps(
        out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        out Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        out HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys)
    {
        inputOwnersByKey = new Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>>();
        outputOwnersByKey = new Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>>();
        coreOutputKeys = new HashSet<(Vector2Int cell, CellSideMask side)>();

        if (artificialSatellite == null) return;

        AssemblyPartPortLayout[] layouts = artificialSatellite.GetComponentsInChildren<AssemblyPartPortLayout>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            AssemblyPartPortLayout layout = layouts[i];
            if (layout == null) continue;
            if (layout.GetComponentInParent<AssemblyGhostMarker>() != null) continue;

            AssemblyPartFocus focus = layout.GetComponent<AssemblyPartFocus>();
            GameObject owner = focus != null ? focus.gameObject : null;
            bool ownerSelectable = CanSelectPart(owner);
            bool ownerAsCoreLikeRoot = IsCoreLikeProtectedPart(owner);

            List<AssemblyPartPortLayout.PortEntry> entries = layout.Ports;
            if (entries == null || entries.Count == 0) continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AssemblyPartPortLayout.PortEntry entry = entries[j];
                if (entry == null) continue;
                if (!AssemblyPortTypeUtility.IsInputCompatible(entry.portType) && !AssemblyPortTypeUtility.IsOutputCompatible(entry.portType)) continue;
                if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) continue;
                if (!IsInsideGrid(sourceCell) || portSide == CellSideMask.None) continue;

                (Vector2Int cell, CellSideMask side) key = (sourceCell, portSide);
                if (AssemblyPortTypeUtility.IsInputCompatible(entry.portType) && ownerSelectable)
                {
                    AddOwnerToPortMap(inputOwnersByKey, key, owner);
                }

                if (AssemblyPortTypeUtility.IsOutputCompatible(entry.portType))
                {
                    if (ownerSelectable)
                    {
                        AddOwnerToPortMap(outputOwnersByKey, key, owner);
                    }
                    else if (ownerAsCoreLikeRoot)
                    {
                        coreOutputKeys.Add(key);
                    }
                }
            }
        }

        foreach (KeyValuePair<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)> pair in coreLayoutByPort)
        {
            Vector2Int sourceCell = pair.Value.sourceCell;
            CellSideMask side = pair.Value.outputSide;
            if (!IsInsideGrid(sourceCell) || side == CellSideMask.None) continue;
            coreOutputKeys.Add((sourceCell, side));
        }
    }

    private static void AddOwnerToPortMap(
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> map,
        (Vector2Int cell, CellSideMask side) key,
        GameObject owner)
    {
        if (map == null || owner == null) return;

        if (!map.TryGetValue(key, out HashSet<GameObject> owners))
        {
            owners = new HashSet<GameObject>();
            map[key] = owners;
        }

        owners.Add(owner);
    }

    private void AddConnectionsForFacingCells(
        Vector2Int inputCell,
        CellSideMask inputSide,
        Vector2Int outputCell,
        CellSideMask outputSide,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys,
        Dictionary<GameObject, HashSet<GameObject>> adjacency,
        HashSet<GameObject> coreConnectedSeeds)
    {
        if (!TryGetOwnersByPortKey(inputOwnersByKey, (inputCell, inputSide), out HashSet<GameObject> inputOwners)) return;

        bool hasCoreOutput = coreOutputKeys != null && coreOutputKeys.Contains((outputCell, outputSide));
        bool hasPartOutput = TryGetOwnersByPortKey(outputOwnersByKey, (outputCell, outputSide), out HashSet<GameObject> outputOwners);
        if (!hasCoreOutput && !hasPartOutput) return;

        foreach (GameObject inputOwner in inputOwners)
        {
            if (!CanSelectPart(inputOwner)) continue;

            if (hasCoreOutput)
            {
                coreConnectedSeeds?.Add(inputOwner);
            }

            if (!hasPartOutput) continue;
            foreach (GameObject outputOwner in outputOwners)
            {
                if (!CanSelectPart(outputOwner)) continue;
                if (inputOwner == outputOwner) continue;
                if (!adjacency.TryGetValue(inputOwner, out HashSet<GameObject> inputNeighbors)) continue;
                if (!adjacency.TryGetValue(outputOwner, out HashSet<GameObject> outputNeighbors)) continue;

                inputNeighbors.Add(outputOwner);
                outputNeighbors.Add(inputOwner);
            }
        }
    }

    private static bool TryGetOwnersByPortKey(
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> map,
        (Vector2Int cell, CellSideMask side) key,
        out HashSet<GameObject> owners)
    {
        owners = null;
        if (map == null) return false;
        if (!map.TryGetValue(key, out HashSet<GameObject> value) || value == null || value.Count == 0) return false;
        owners = value;
        return true;
    }

    private static bool TryGetSideBetweenCells(Vector2Int fromCell, Vector2Int toCell, out CellSideMask side)
    {
        side = CellSideMask.None;
        Vector2Int delta = toCell - fromCell;
        if (delta == Vector2Int.right) { side = CellSideMask.Right; return true; }
        if (delta == Vector2Int.left) { side = CellSideMask.Left; return true; }
        if (delta == Vector2Int.up) { side = CellSideMask.Top; return true; }
        if (delta == Vector2Int.down) { side = CellSideMask.Bottom; return true; }
        return false;
    }

    private bool AreAdjacentCellsConnectedByOppositePorts(
        Vector2Int cellA,
        Vector2Int cellB,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> inputOwnersByKey,
        Dictionary<(Vector2Int cell, CellSideMask side), HashSet<GameObject>> outputOwnersByKey,
        HashSet<(Vector2Int cell, CellSideMask side)> coreOutputKeys)
    {
        if (!TryGetSideBetweenCells(cellA, cellB, out CellSideMask sideFromA)) return false;
        CellSideMask sideFromB = OppositeSide(sideFromA);
        if (sideFromB == CellSideMask.None) return false;

        bool aInputFromBOutput =
            TryGetOwnersByPortKey(inputOwnersByKey, (cellA, sideFromA), out HashSet<GameObject> aInputs) &&
            (
                (coreOutputKeys != null && coreOutputKeys.Contains((cellB, sideFromB))) ||
                TryGetOwnersByPortKey(outputOwnersByKey, (cellB, sideFromB), out HashSet<GameObject> bOutputs)
            ) &&
            (aInputs.Count > 0);

        bool bInputFromAOutput =
            TryGetOwnersByPortKey(inputOwnersByKey, (cellB, sideFromB), out HashSet<GameObject> bInputs) &&
            (
                (coreOutputKeys != null && coreOutputKeys.Contains((cellA, sideFromA))) ||
                TryGetOwnersByPortKey(outputOwnersByKey, (cellA, sideFromA), out HashSet<GameObject> aOutputs)
            ) &&
            (bInputs.Count > 0);

        return aInputFromBOutput || bInputFromAOutput;
    }

    private bool TryCollectPartInputMappedKeys(
        GameObject partRoot,
        List<(Vector2Int cell, CellSideMask side)> mappedInputs)
    {
        if (partRoot == null || mappedInputs == null) return false;
        mappedInputs.Clear();

        AssemblyPartPortLayout layout = partRoot.GetComponent<AssemblyPartPortLayout>();
        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            for (int i = 0; i < layout.Ports.Count; i++)
            {
                AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
                if (entry == null || !AssemblyPortTypeUtility.IsInputCompatible(entry.portType)) continue;
                if (!TryGetPartLayoutEntryMapping(layout, entry, out Vector2Int sourceCell, out CellSideMask portSide)) continue;

                Vector2Int outputDir = SideToCellOffset(portSide);
                if (outputDir == Vector2Int.zero) continue;

                Vector2Int mappedCell = sourceCell + outputDir;
                if (!IsInsideGrid(mappedCell)) continue;

                CellSideMask mappedSide = OppositeSide(portSide);
                if (mappedSide == CellSideMask.None) continue;
                mappedInputs.Add((mappedCell, mappedSide));
            }

            if (mappedInputs.Count > 0) return true;
        }

        AssemblyPartFocus focus = partRoot.GetComponent<AssemblyPartFocus>();
        AssemblyPartPortProfile profile = partRoot.GetComponent<AssemblyPartPortProfile>();
        if (focus == null || profile == null) return false;

        Vector2 snapOffset = GetSnapOffsetFromPartFocus(focus);
        Vector2Int centerCell = LocalPositionToGrid(partRoot.transform.localPosition, snapOffset);
        if (!IsInsideGrid(centerCell)) return false;

        HashSet<CellSideMask> uniqueSides = new HashSet<CellSideMask>();
        int inputCount = profile.InputPortCount;
        for (int i = 0; i < inputCount; i++)
        {
            Vector3 inputLocal = profile.GetInputPortLocalPosition(i);
            Vector3 inputDirOnSatellite = partRoot.transform.localRotation * inputLocal;
            CellSideMask inputSide = DirectionToSideMask(inputDirOnSatellite);
            if (inputSide == CellSideMask.None) continue;
            if (!uniqueSides.Add(inputSide)) continue;

            mappedInputs.Add((centerCell, inputSide));
        }

        return mappedInputs.Count > 0;
    }

    private void BuildRemovalSetForSelectedParts(
        IEnumerable<GameObject> initiallyRemovedOwners,
        List<GameObject> result)
    {
        if (result == null) return;
        result.Clear();
        if (initiallyRemovedOwners == null || artificialSatellite == null) return;

        HashSet<GameObject> removed = new HashSet<GameObject>();
        foreach (GameObject owner in initiallyRemovedOwners)
        {
            if (!CanSelectPart(owner)) continue;
            removed.Add(owner);
        }

        if (removed.Count == 0) return;

        if (removeDisconnectedPartsAfterDeletion)
        {
            Dictionary<GameObject, HashSet<GameObject>> adjacency = new Dictionary<GameObject, HashSet<GameObject>>();
            HashSet<GameObject> coreConnectedSeeds = new HashSet<GameObject>();
            BuildPartAdjacencyGraph(adjacency, coreConnectedSeeds);

            HashSet<GameObject> allParts = new HashSet<GameObject>(adjacency.Keys);
            HashSet<GameObject> reachableBefore = ComputeReachableFromCore(adjacency, coreConnectedSeeds, null);
            HashSet<GameObject> reachableAfter = ComputeReachableFromCore(adjacency, coreConnectedSeeds, removed);

            foreach (GameObject part in allParts)
            {
                if (part == null) continue;
                if (!reachableBefore.Contains(part)) continue;
                if (!reachableAfter.Contains(part)) removed.Add(part);
            }
        }

        foreach (GameObject owner in removed)
        {
            if (owner != null) result.Add(owner);
        }
    }

    private static HashSet<GameObject> ComputeReachableFromCore(
        Dictionary<GameObject, HashSet<GameObject>> adjacency,
        HashSet<GameObject> coreConnectedSeeds,
        HashSet<GameObject> removed)
    {
        HashSet<GameObject> reachable = new HashSet<GameObject>();
        if (adjacency == null || coreConnectedSeeds == null) return reachable;

        Queue<GameObject> open = new Queue<GameObject>();
        foreach (GameObject seed in coreConnectedSeeds)
        {
            if (seed == null) continue;
            if (removed != null && removed.Contains(seed)) continue;
            if (!reachable.Add(seed)) continue;
            open.Enqueue(seed);
        }

        while (open.Count > 0)
        {
            GameObject current = open.Dequeue();
            if (!adjacency.TryGetValue(current, out HashSet<GameObject> neighbors)) continue;

            foreach (GameObject next in neighbors)
            {
                if (next == null) continue;
                if (removed != null && removed.Contains(next)) continue;
                if (!reachable.Add(next)) continue;
                open.Enqueue(next);
            }
        }

        return reachable;
    }

    private void CreateGridPlane()
    {
        if (gridPlane != null) Destroy(gridPlane);

        gridPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gridPlane.name = AssemblyGridPlaneName;
        Destroy(gridPlane.GetComponent<Collider>());
        SmallScaleLayerUtility.ApplyRecursively(gridPlane.transform);

        float planeSize = gridSize * cellSize;
        gridPlane.transform.localScale = new Vector3(planeSize, planeSize, 1f);

        AssemblyAttachmentHub attachmentHub = ComponentUtility.GetOrAddComponent<AssemblyAttachmentHub>(artificialSatellite.gameObject);
        attachmentHub.RegisterOrUpdate(gridPlane.transform, Vector3.zero, Quaternion.identity, gridPlane.transform.localScale);
        attachmentHub.SyncAttachments();

        MeshRenderer renderer = gridPlane.GetComponent<MeshRenderer>();
        Shader gridShader = Shader.Find("Unlit/Grid");

        if (gridShader == null)
        {
            Debug.LogError("Shader is missing.");
            return;
        }

        renderer.material = new Material(gridShader);
        renderer.material.SetFloat("_GridSize", gridSize);
        renderer.material.SetColor("_GridColor", new Color(1f, 1f, 1f, 0.3f));
    }

    private void DestroyGridPlane()
    {
        if (gridPlane != null)
        {
            if (artificialSatellite != null)
            {
                AssemblyAttachmentHub attachmentHub = artificialSatellite.GetComponent<AssemblyAttachmentHub>();
                if (attachmentHub != null)
                {
                    attachmentHub.Unregister(gridPlane.transform);
                }
            }

            Destroy(gridPlane);
        }
    }


    private bool TryMatchGhostToOutputPort()
    {
        matchedOutputPort = null;
        if (partGhost == null || ghostPortProfile == null || outputPorts.Count == 0) return false;

        Vector2Int centerCell = GetCurrentGhostCenterCell();
        bool isPipe = IsPipePart(part);
        bool usesPipePathPlacement = UsesPipePathPlacement(part);
        if (!usesPipePathPlacement || !pipePathStartSelected)
        {
            Quaternion originalRotation = partGhost.transform.localRotation;
            if (!enableInputPortAutoMapping)
            {
                if (TryGetOutputPortMatchForCurrentRotationWithoutPreferredSide(
                    centerCell,
                    out AssemblyPort candidatePort,
                    logPipeDebug: isPipe,
                    requirePipeSource: !isPipe && !SolarPanelUtility.IsSolarPanelPart(part) && !SolarTurbineUtility.IsSolarTurbinePart(part)))
                {
                    matchedOutputPort = candidatePort;
                    return true;
                }

                partGhost.transform.localRotation = originalRotation;
                return false;
            }

            if (BuildAvailableOutputSideOrder(centerCell, requirePipeSource: !isPipe && !SolarPanelUtility.IsSolarPanelPart(part) && !SolarTurbineUtility.IsSolarTurbinePart(part)))
            {
                int startIndex = PositiveModulo(outputSideCycleOffset, availableOutputSides.Count);
                for (int sidePass = 0; sidePass < availableOutputSides.Count; sidePass++)
                {
                    CellSideMask preferredSide = availableOutputSides[(startIndex + sidePass) % availableOutputSides.Count];
                    for (int i = 0; i < PipeAutoRotations.Length; i++)
                    {
                        partGhost.transform.localRotation = PipeAutoRotations[i];
                        if (TryGetOutputPortMatchForCurrentRotation(
                            centerCell,
                            preferredSide,
                            out AssemblyPort candidatePort,
                            logPipeDebug: isPipe,
                            requirePipeSource: !isPipe && !SolarPanelUtility.IsSolarPanelPart(part) && !SolarTurbineUtility.IsSolarTurbinePart(part)))
                        {
                            matchedOutputPort = candidatePort;
                            return true;
                        }
                    }
                }
            }

            partGhost.transform.localRotation = originalRotation;
            // Non-pipe parts must connect only from Pipe outputs.
            // If no valid Pipe output mapping is found, placement is invalid.
            return false;
        }

        // Keep pipe matching cell/side based as well.
        return TryGetOutputPortMatchForCurrentRotationWithoutPreferredSide(
            centerCell,
            out matchedOutputPort,
            logPipeDebug: true,
            requirePipeSource: false);
    }

    private bool TryGetOutputPortMatchForCurrentRotationWithoutPreferredSide(
        Vector2Int centerCell,
        out AssemblyPort matchedPort,
        bool logPipeDebug,
        bool requirePipeSource)
    {
        matchedPort = null;
        if (partGhost == null || ghostPortProfile == null) return false;

        if (!TryCollectGhostInputRequirements(centerCell, out List<(Vector2Int cell, CellSideMask side)> inputRequirements))
        {
            return false;
        }
        if (inputRequirements.Count == 0) return false;

        bool ignoreOccupiedOutputs = CanReplaceInstalledPipe(part)
            && TryCollectPipeReplacementRoots(centerCell, partGhost.transform.localRotation, out _);
        HashSet<AssemblyPort> usedPorts = new HashSet<AssemblyPort>();
        AssemblyPort firstMatchedPort = null;

        for (int i = 0; i < inputRequirements.Count; i++)
        {
            (Vector2Int cell, CellSideMask side) requirement = inputRequirements[i];
            CellSideMask inputSide = requirement.side;
            if (inputSide == CellSideMask.None) return false;

            if (!TryGetAvailableOutputPort(requirement.cell, inputSide, out AssemblyPort outputPort, ignoreOccupiedOutputs))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, CellSideMask.None, null);
                continue;
            }

            if (requirePipeSource && !IsPipeOwnedOutputPort(outputPort))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, CellSideMask.None, null);
                continue;
            }

            if (!usedPorts.Add(outputPort))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, CellSideMask.None, null);
                continue;
            }

            if (firstMatchedPort == null) firstMatchedPort = outputPort;
            if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, CellSideMask.None, outputPort);
        }

        if (firstMatchedPort == null) return false;

        if (!ignoreOccupiedOutputs &&
            requirePipeSource &&
            TryCollectGhostOutputRequirements(centerCell, out List<(Vector2Int cell, CellSideMask side)> outputRequirements) &&
            HasPipeOutputConflictForPlacement(inputRequirements, outputRequirements))
        {
            return false;
        }

        matchedPort = firstMatchedPort;
        return true;
    }

    private bool TryGetOutputPortMatchForCurrentRotation(
        Vector2Int centerCell,
        CellSideMask requiredOutputSide,
        out AssemblyPort matchedPort,
        bool logPipeDebug,
        bool requirePipeSource)
    {
        matchedPort = null;
        if (partGhost == null || ghostPortProfile == null) return false;
        if (requiredOutputSide == CellSideMask.None) return false;

        if (!TryCollectGhostInputRequirements(centerCell, out List<(Vector2Int cell, CellSideMask side)> inputRequirements))
        {
            return false;
        }
        if (inputRequirements.Count == 0) return false;

        bool ignoreOccupiedOutputs = CanReplaceInstalledPipe(part)
            && TryCollectPipeReplacementRoots(centerCell, partGhost.transform.localRotation, out _);
        AssemblyPort preferredSidePort = null;
        HashSet<AssemblyPort> usedPorts = new HashSet<AssemblyPort>();
        bool hasAnyValidInputMatch = false;

        for (int i = 0; i < inputRequirements.Count; i++)
        {
            (Vector2Int cell, CellSideMask side) requirement = inputRequirements[i];
            CellSideMask inputSide = requirement.side;
            if (inputSide == CellSideMask.None) return false;

            if (!TryGetAvailableOutputPort(requirement.cell, inputSide, out AssemblyPort outputPort, ignoreOccupiedOutputs))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, requiredOutputSide, null);
                continue;
            }

            if (requirePipeSource && !IsPipeOwnedOutputPort(outputPort))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, requiredOutputSide, null);
                continue;
            }

            if (!usedPorts.Add(outputPort))
            {
                if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, requiredOutputSide, null);
                continue;
            }

            hasAnyValidInputMatch = true;

            if (inputSide == requiredOutputSide && preferredSidePort == null)
            {
                preferredSidePort = outputPort;
            }

            if (logPipeDebug) DebugLogPipeStartMatch(requirement.cell, inputSide, requiredOutputSide, outputPort);
        }

        if (!hasAnyValidInputMatch) return false;
        if (preferredSidePort == null) return false;

        if (!ignoreOccupiedOutputs &&
            requirePipeSource &&
            TryCollectGhostOutputRequirements(centerCell, out List<(Vector2Int cell, CellSideMask side)> outputRequirements) &&
            HasPipeOutputConflictForPlacement(inputRequirements, outputRequirements))
        {
            return false;
        }

        matchedPort = preferredSidePort;
        return true;
    }

    private bool HasPipeOutputConflictForPlacement(
        List<(Vector2Int cell, CellSideMask side)> inputRequirements,
        List<(Vector2Int cell, CellSideMask side)> outputRequirements)
    {
        if (outputRequirements == null || outputRequirements.Count == 0) return false;

        HashSet<(Vector2Int, CellSideMask)> inputSet = new HashSet<(Vector2Int, CellSideMask)>();
        if (inputRequirements != null)
        {
            for (int i = 0; i < inputRequirements.Count; i++)
            {
                inputSet.Add(inputRequirements[i]);
            }
        }

        for (int i = 0; i < outputRequirements.Count; i++)
        {
            (Vector2Int cell, CellSideMask side) outputReq = outputRequirements[i];
            if (outputReq.side == CellSideMask.None) continue;

            if (inputSet.Contains(outputReq)) continue;
            if (!TryGetMappedOutputPort(outputReq.cell, outputReq.side, out AssemblyPort existingOutput)) continue;
            if (!IsPipeOwnedOutputPort(existingOutput)) continue;
            return true;
        }

        return false;
    }

    private bool TryCollectGhostInputRequirements(
        Vector2Int centerCell,
        out List<(Vector2Int cell, CellSideMask side)> inputs)
    {
        inputs = new List<(Vector2Int cell, CellSideMask side)>();
        if (partGhost == null) return false;

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            int quarterTurns = GetQuarterTurns(partGhost.transform.localRotation);
            for (int i = 0; i < layout.Ports.Count; i++)
            {
                AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
                if (entry == null || !AssemblyPortTypeUtility.IsInputCompatible(entry.portType)) continue;

                Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
                CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
                if (rotatedSide == CellSideMask.None) continue;
                inputs.Add((centerCell + rotatedCell, rotatedSide));
            }

            return inputs.Count > 0;
        }

        int inputCount = ghostPortProfile != null ? ghostPortProfile.InputPortCount : 0;
        for (int i = 0; i < inputCount; i++)
        {
            Vector3 inputLocal = ghostPortProfile.GetInputPortLocalPosition(i);
            if (!TryResolveGhostBoundaryFromLocalPosition(inputLocal, out Vector2Int sourceCell, out CellSideMask inputSide))
            {
                continue;
            }

            inputs.Add((sourceCell, inputSide));
        }

        return inputs.Count > 0;
    }

    private bool TryCollectGhostOutputRequirements(
        Vector2Int centerCell,
        out List<(Vector2Int cell, CellSideMask side)> outputs)
    {
        outputs = new List<(Vector2Int cell, CellSideMask side)>();
        if (partGhost == null) return false;

        AssemblyPartPortLayout layout = partGhost.GetComponent<AssemblyPartPortLayout>();
        if (layout != null && layout.Ports != null && layout.Ports.Count > 0)
        {
            int quarterTurns = GetQuarterTurns(partGhost.transform.localRotation);
            for (int i = 0; i < layout.Ports.Count; i++)
            {
                AssemblyPartPortLayout.PortEntry entry = layout.Ports[i];
                if (entry == null || !AssemblyPortTypeUtility.IsOutputCompatible(entry.portType)) continue;

                Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
                CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
                if (rotatedSide == CellSideMask.None) continue;
                outputs.Add((centerCell + rotatedCell, rotatedSide));
            }

            return outputs.Count > 0;
        }

        HashSet<(Vector2Int cell, CellSideMask side)> uniqueOutputs = new HashSet<(Vector2Int cell, CellSideMask side)>();
        Transform[] all = partGhost.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == partGhost.transform) continue;
            if (!IsOutputPortTransform(t)) continue;

            Vector3 outputLocal = partGhost.transform.InverseTransformPoint(t.position);
            if (!TryResolveGhostBoundaryFromLocalPosition(outputLocal, out Vector2Int sourceCell, out CellSideMask outputSide))
            {
                continue;
            }

            uniqueOutputs.Add((sourceCell, outputSide));
        }

        outputs.AddRange(uniqueOutputs);
        return outputs.Count > 0;
    }

    private bool TryResolveGhostBoundaryFromLocalPosition(
        Vector3 portLocal,
        out Vector2Int sourceCell,
        out CellSideMask side)
    {
        sourceCell = Vector2Int.zero;
        side = CellSideMask.None;
        if (partGhost == null) return false;

        Quaternion ghostRotation = partGhost.transform.localRotation;
        Vector3 scaledLocal = Vector3.Scale(portLocal, partGhost.transform.localScale);
        Vector3 rotatedLocal = ghostRotation * scaledLocal;
        side = DirectionToSideMask(rotatedLocal);
        if (side == CellSideMask.None) return false;

        Vector2Int sideOffset = SideToCellOffset(side);
        if (sideOffset == Vector2Int.zero) return false;

        Vector3 portLocalOnSatellite = partGhost.transform.localPosition + rotatedLocal;
        Vector3 sourceCellCenterLocal = portLocalOnSatellite - new Vector3(sideOffset.x * cellSize * 0.5f, sideOffset.y * cellSize * 0.5f, 0f);
        sourceCell = LocalPositionToGrid(sourceCellCenterLocal, GetCurrentPartSnapOffset());
        return IsInsideGrid(sourceCell);
    }

    private static bool IsPipeOwnedOutputPort(AssemblyPort outputPort)
    {
        if (outputPort == null) return false;

        AssemblyPartFocus partFocus = outputPort.GetComponentInParent<AssemblyPartFocus>(true);
        if (partFocus != null && PipePartUtility.IsPipePart(partFocus.SourcePart))
        {
            return true;
        }

        // Fallback for runtime objects where SourcePart binding can be delayed/missing.
        return ContainsNameTokenInHierarchy(outputPort.transform, "Pipe");
    }

    private bool BuildAvailableOutputSideOrder(Vector2Int centerCell, bool requirePipeSource)
    {
        availableOutputSides.Clear();
        if (!IsInsideGrid(centerCell)) return false;

        if (requirePipeSource)
        {
            for (int i = 0; i < OutputSidePriorityOrder.Length; i++)
            {
                availableOutputSides.Add(OutputSidePriorityOrder[i]);
            }
            return true;
        }

        bool ignoreOccupiedOutputs = CanReplaceInstalledPipe(part)
            && TryCollectPipeReplacementRoots(centerCell, partGhost != null ? partGhost.transform.localRotation : Quaternion.identity, out _);
        for (int i = 0; i < OutputSidePriorityOrder.Length; i++)
        {
            CellSideMask side = OutputSidePriorityOrder[i];
            if (!TryGetAvailableOutputPort(centerCell, side, out AssemblyPort outputPort, ignoreOccupiedOutputs)) continue;
            if (requirePipeSource && !IsPipeOwnedOutputPort(outputPort)) continue;
            availableOutputSides.Add(side);
        }

        return availableOutputSides.Count > 0;
    }

    private void UpdateOutputCycleSelection(Vector2Int centerCell)
    {
        bool isNewCell = !hasLastOutputCycleCell || centerCell != lastOutputCycleCell;
        if (isNewCell)
        {
            lastOutputCycleCell = centerCell;
            hasLastOutputCycleCell = true;
            outputSideCycleOffset = 0;
        }

        if (Keyboard.current == null) return;
        bool rotateClockwise = Keyboard.current.eKey.wasPressedThisFrame;
        bool rotateCounterClockwise = Keyboard.current.qKey.wasPressedThisFrame;
        if (!rotateClockwise && !rotateCounterClockwise) return;

        if (!enableInputPortAutoMapping)
        {
            int deltaQuarterTurns = rotateClockwise ? 1 : -1;
            RotateGhostByQuarterTurns(deltaQuarterTurns);
            return;
        }

        if (rotateClockwise)
        {
            outputSideCycleOffset++;
        }
        else if (rotateCounterClockwise)
        {
            outputSideCycleOffset--;
        }
    }

    private void RotateGhostByQuarterTurns(int deltaQuarterTurns)
    {
        if (partGhost == null || !partGhost.activeInHierarchy) return;

        int currentQuarterTurns = GetQuarterTurns(partGhost.transform.localRotation);
        int nextQuarterTurns = PositiveModulo(currentQuarterTurns + deltaQuarterTurns, PipeAutoRotations.Length);
        partGhost.transform.localRotation = PipeAutoRotations[nextQuarterTurns];
    }

    private static int PositiveModulo(int value, int modulo)
    {
        if (modulo <= 0) return 0;
        int result = value % modulo;
        if (result < 0) result += modulo;
        return result;
    }

    private void HandlePipeLeftClick()
    {
        if (part == null || partGhost == null || artificialSatellite == null) return;

        if (!pipePathStartSelected)
        {
            if (!isSnapped || !canPlace || matchedOutputPort == null) return;

            pipePathStartSelected = true;
            pipeStartOutputPort = matchedOutputPort;
            pipePathStartCell = GetCurrentGhostCenterCell();
            pipePreviewPath.Clear();
            pipePreviewPath.Add(pipePathStartCell);
            pipePathStartIncomingDir = GetPipeStartIncomingDirection();
            hasLastPipeHoverCell = false;
            pipePathTerminalDirection = Vector2Int.zero;

            partGhost.SetActive(false);
            UpdatePipePathPreview();
            return;
        }

        if (!canPlace || pipePreviewPath.Count == 0) return;

        ApplyPipePath();
    }

    private void UpdatePipePathPreview()
    {
        if (partGhost == null || part == null || part.partType != PartType.Pipe) return;

        Vector2Int hoverCell = GetCurrentGhostCenterCell();
        Vector2Int previousHoverCell = lastPipeHoverCell;
        bool hadPreviousHoverCell = hasLastPipeHoverCell;
        if (hadPreviousHoverCell && hoverCell == previousHoverCell) return;
        hasLastPipeHoverCell = true;
        lastPipeHoverCell = hoverCell;

        if (!TryResolvePipePreviewPath(
            hoverCell,
            hadPreviousHoverCell,
            previousHoverCell,
            out List<Vector2Int> path,
            out Vector2Int terminalDirection))
        {
            pipePreviewPath.Clear();
            pipePathTerminalDirection = Vector2Int.zero;
            canPlace = false;
            BuildPipePathGhosts(pipePreviewPath, false, pipePathTerminalDirection);
            return;
        }

        pipePreviewPath.Clear();
        pipePreviewPath.AddRange(path);
        pipePathTerminalDirection = terminalDirection;
        canPlace = pipePreviewPath.Count > 0;
        BuildPipePathGhosts(pipePreviewPath, canPlace, pipePathTerminalDirection);
    }

    private void ApplyPipePath()
    {
        if (artificialSatellite == null || part == null || part.partType != PartType.Pipe) return;
        if (pipePreviewPath.Count == 0) return;

        ArtificialSatellite sourceSatellite = ResolveSourceSatelliteForAssemblyTarget(artificialSatellite);

        for (int i = 0; i < pipePreviewPath.Count; i++)
        {
            Vector2Int cell = pipePreviewPath[i];
            Vector2Int previousDir = GetPathSegmentPreviousDir(pipePreviewPath, i);
            Vector2Int nextDir = GetPathSegmentNextDir(pipePreviewPath, i, pipePathTerminalDirection);
            Vector3 localPos = GridToLocalPosition(cell, GetCurrentPartSnapOffset());
            bool isCorner = IsCornerSegment(previousDir, nextDir);
            Quaternion localRot = isCorner
                ? GetPipeCornerRotation(previousDir, nextDir)
                : GetPipeSegmentRotation(previousDir, nextDir);
            GameObject placed = isCorner
                ? AddPipeCornerToSatellite(artificialSatellite, part, localPos, localRot, previousDir, nextDir)
                : AddPartToSatellite(artificialSatellite, part, localPos, localRot);

            occupiedCells[cell] = placed != null ? placed : artificialSatellite.gameObject;

            if (ShouldMirrorToSourceSatellite(artificialSatellite, sourceSatellite))
            {
                if (isCorner)
                {
                    AddPipeCornerToSatellite(sourceSatellite, part, localPos, localRot, previousDir, nextDir);
                }
                else
                {
                    AddPartToSatellite(sourceSatellite, part, localPos, localRot);
                }
            }
        }

        if (pipeStartOutputPort != null) pipeStartOutputPort.SetOccupied(true);

        RebuildMeshesForAssemblyTargets(artificialSatellite, sourceSatellite);

        Debug.Log($"Applied pipe path with {pipePreviewPath.Count} segments.");

        Cancel();
        RequestRefreshOutputPorts();
    }


}


