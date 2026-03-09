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
    [SerializeField] private bool debugPipePlacement = true;
    [SerializeField] private bool debugHoverCellPortMapping = true;
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

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsPipePart(part))
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
        outputPortTemplate = FindOutputPortTemplate();
        RequestRefreshOutputPorts();

        Debug.Log("Assembly activated.");
    }

    public void Deactivate()
    {
        ExitAssemblyCameraMode();

        Cancel();
        artificialSatellite = null;
        IsAssembling = false;
        assemblyPlaneCreated = false;
        DestroyGridPlane();
        occupiedCells.Clear();

        Debug.Log("Assembly deactivated.");
    }

    public void SelectPart(Part part)
    {
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

        if (IsPipePart(part) && pipePathStartSelected)
        {
            UpdatePipePathPreview();
            return;
        }

        bool areaFree = IsCurrentGhostPlacementAreaFree();
        if (!areaFree)
        {
            if (IsPipePart(part) && !pipePathStartSelected && partGhost != null)
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
    }

    private void Apply()
    {
        if (!isSnapped || !canPlace || artificialSatellite == null || part == null || partGhost == null) return;

        Vector3 finalLocalPos = partGhost.transform.localPosition;
        Quaternion finalLocalRot = partGhost.transform.localRotation;
        GameObject placedObject = AddPartToSatellite(artificialSatellite, part, finalLocalPos, finalLocalRot);

        ArtificialSatellite sourceSatellite = ResolveSourceSatelliteForAssemblyTarget(artificialSatellite);

        if (ShouldMirrorToSourceSatellite(artificialSatellite, sourceSatellite))
        {
            AddPartToSatellite(sourceSatellite, part, finalLocalPos, finalLocalRot);
        }

        RebuildMeshesForAssemblyTargets(artificialSatellite, sourceSatellite);

        Debug.Log($"Applied part {part.partName} to {artificialSatellite.name}.");

        Vector2Int gridPos = GetCurrentGhostCenterCell();
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
            || partName.IndexOf("DropPort", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

    private static void RebuildMeshesForAssemblyTargets(ArtificialSatellite assemblyTarget, ArtificialSatellite sourceSatellite)
    {
        RebuildCombinedMesh(assemblyTarget);

        bool isSandboxTarget = IsSandboxTarget(assemblyTarget);
        if (!isSandboxTarget && ShouldMirrorToSourceSatellite(assemblyTarget, sourceSatellite))
        {
            RebuildCombinedMesh(sourceSatellite);
        }
    }


    private void Cancel()
    {
        SetOutputPortsPulsing(false);
        ClearPipePathGhosts();

        DestroyPartGhost(true);

        ResetSelectedPartState();
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
        if (!isPipe || !pipePathStartSelected)
        {
            Quaternion originalRotation = partGhost.transform.localRotation;
            if (!enableInputPortAutoMapping)
            {
                if (TryGetOutputPortMatchForCurrentRotationWithoutPreferredSide(
                    centerCell,
                    out AssemblyPort candidatePort,
                    logPipeDebug: isPipe,
                    requirePipeSource: !isPipe))
                {
                    matchedOutputPort = candidatePort;
                    return true;
                }

                partGhost.transform.localRotation = originalRotation;
                return false;
            }

            if (BuildAvailableOutputSideOrder(centerCell, requirePipeSource: !isPipe))
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
                            requirePipeSource: !isPipe))
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

        HashSet<AssemblyPort> usedPorts = new HashSet<AssemblyPort>();
        AssemblyPort firstMatchedPort = null;

        for (int i = 0; i < inputRequirements.Count; i++)
        {
            (Vector2Int cell, CellSideMask side) requirement = inputRequirements[i];
            CellSideMask inputSide = requirement.side;
            if (inputSide == CellSideMask.None) return false;

            if (!TryGetAvailableOutputPort(requirement.cell, inputSide, out AssemblyPort outputPort))
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

        if (requirePipeSource &&
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

        AssemblyPort preferredSidePort = null;
        HashSet<AssemblyPort> usedPorts = new HashSet<AssemblyPort>();
        bool hasAnyValidInputMatch = false;

        for (int i = 0; i < inputRequirements.Count; i++)
        {
            (Vector2Int cell, CellSideMask side) requirement = inputRequirements[i];
            CellSideMask inputSide = requirement.side;
            if (inputSide == CellSideMask.None) return false;

            if (!TryGetAvailableOutputPort(requirement.cell, inputSide, out AssemblyPort outputPort))
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

        if (requirePipeSource &&
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
                if (entry == null || entry.portType != AssemblyPortType.Input) continue;

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
            Vector3 inputDirOnSatellite = partGhost.transform.localRotation * inputLocal;
            CellSideMask inputSide = DirectionToSideMask(inputDirOnSatellite);
            if (inputSide == CellSideMask.None) continue;
            inputs.Add((centerCell, inputSide));
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
                if (entry == null || entry.portType != AssemblyPortType.Output) continue;

                Vector2Int rotatedCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
                CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
                if (rotatedSide == CellSideMask.None) continue;
                outputs.Add((centerCell + rotatedCell, rotatedSide));
            }

            return outputs.Count > 0;
        }

        HashSet<CellSideMask> sides = new HashSet<CellSideMask>();
        if (artificialSatellite == null) return false;

        Transform[] all = partGhost.GetComponentsInChildren<Transform>(true);
        Vector3 centerLocalOnSatellite = partGhost.transform.localPosition;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == partGhost.transform) continue;
            if (!IsOutputPortTransform(t)) continue;

            Vector3 portLocalOnSatellite = artificialSatellite.transform.InverseTransformPoint(t.position);
            Vector3 direction = portLocalOnSatellite - centerLocalOnSatellite;
            CellSideMask side = DirectionToSideMask(direction);
            if (side == CellSideMask.None) continue;
            sides.Add(side);
        }

        foreach (CellSideMask side in sides)
        {
            outputs.Add((centerCell, side));
        }

        return outputs.Count > 0;
    }

    private bool TryResolveGhostEntryFromTransform(
        AssemblyPartPortLayout.PortEntry entry,
        CellSideMask fallbackSide,
        Vector2 snapOffset,
        out Vector2Int sourceCell,
        out CellSideMask side)
    {
        sourceCell = Vector2Int.zero;
        side = CellSideMask.None;
        if (entry == null || entry.portTransform == null) return false;
        if (artificialSatellite == null) return false;

        side = ResolveGhostPortSide(entry.portTransform, fallbackSide);
        if (side == CellSideMask.None) return false;

        Vector2Int sideOffset = SideToCellOffset(side);
        if (sideOffset == Vector2Int.zero) return false;

        Vector3 portLocalOnSatellite = artificialSatellite.transform.InverseTransformPoint(entry.portTransform.position);
        Vector3 sourceCellCenterLocal = portLocalOnSatellite - new Vector3(sideOffset.x * cellSize * 0.5f, sideOffset.y * cellSize * 0.5f, 0f);
        sourceCell = LocalPositionToGrid(sourceCellCenterLocal, snapOffset);
        return IsInsideGrid(sourceCell);
    }

    private CellSideMask ResolveGhostPortSide(Transform portTransform, CellSideMask fallbackSide)
    {
        if (portTransform == null || artificialSatellite == null) return fallbackSide;

        AssemblyPort port = portTransform.GetComponent<AssemblyPort>();
        if (port != null && port.LocalDirection.sqrMagnitude > DirectionEpsilonSqr)
        {
            Vector3 worldDirection = portTransform.TransformDirection(port.LocalDirection.normalized);
            Vector3 localDirection = artificialSatellite.transform.InverseTransformDirection(worldDirection);
            CellSideMask side = DirectionToSideMask(localDirection);
            if (side != CellSideMask.None) return side;
        }

        return fallbackSide;
    }

    private static bool IsPipeOwnedOutputPort(AssemblyPort outputPort)
    {
        if (outputPort == null) return false;

        AssemblyPartFocus partFocus = outputPort.GetComponentInParent<AssemblyPartFocus>(true);
        if (partFocus != null && partFocus.SourcePart != null)
        {
            string partName = partFocus.SourcePart.partName;
            if (!string.IsNullOrWhiteSpace(partName) &&
                string.Equals(partName, "Pipe", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
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

        for (int i = 0; i < OutputSidePriorityOrder.Length; i++)
        {
            CellSideMask side = OutputSidePriorityOrder[i];
            if (!TryGetAvailableOutputPort(centerCell, side, out AssemblyPort outputPort)) continue;
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
        if (hasLastPipeHoverCell && hoverCell == lastPipeHoverCell) return;
        hasLastPipeHoverCell = true;
        lastPipeHoverCell = hoverCell;

        if (!TryFindPipePath(pipePathStartCell, hoverCell, out List<Vector2Int> path))
        {
            pipePreviewPath.Clear();
            canPlace = false;
            BuildPipePathGhosts(pipePreviewPath, false);
            return;
        }

        pipePreviewPath.Clear();
        pipePreviewPath.AddRange(path);
        canPlace = pipePreviewPath.Count > 0;
        BuildPipePathGhosts(pipePreviewPath, canPlace);
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
            Vector2Int nextDir = GetPathDirection(pipePreviewPath, i, i + 1);
            Vector3 localPos = GridToLocalPosition(cell, GetCurrentPartSnapOffset());
            bool isCorner = IsCornerSegment(previousDir, nextDir);
            Quaternion localRot = isCorner
                ? Quaternion.identity
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
