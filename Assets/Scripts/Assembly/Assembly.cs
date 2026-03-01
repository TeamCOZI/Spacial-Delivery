using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(33000)]
public class Assembly : MonoBehaviour
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
    private readonly Dictionary<Vector2Int, CellSideMask> outputMaskByCell = new Dictionary<Vector2Int, CellSideMask>();
    private readonly Dictionary<Vector2Int, Dictionary<CellSideMask, AssemblyPort>> outputPortsByCellAndSide =
        new Dictionary<Vector2Int, Dictionary<CellSideMask, AssemblyPort>>();
    private readonly Dictionary<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)> coreLayoutByPort =
        new Dictionary<AssemblyPort, (Vector2Int sourceCell, CellSideMask outputSide)>();
    private bool outputPortRefreshQueued = false;
    private Coroutine outputPortRefreshCoroutine;
    [SerializeField] private bool debugPipePlacement = true;
    [SerializeField] private bool debugHoverCellPortMapping = true;
    private bool hasPipeDebugState = false;
    private Vector2Int lastPipeDebugCell;
    private float lastPipeDebugRotZ;
    private bool lastPipeDebugMatch;
    private CellSideMask lastPipeDebugInputSide;
    private CellSideMask lastPipeDebugRequiredSide;
    private bool hasHoverDebugCell = false;
    private Vector2Int lastHoverDebugCell;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color GhostValidColor = new Color(0.2f, 0.55f, 1f, 0.5f);
    private static readonly Color GhostInvalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    private static readonly Color InputPortColor = new Color(0.15f, 0.9f, 0.95f, 1f);
    private const float PortMatchDistance = 0.055f;
    private const string RuntimePortsRootName = "__RuntimePorts";
    private static readonly Quaternion[] PipeAutoRotations =
    {
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(0f, 0f, 180f),
        Quaternion.Euler(0f, 0f, 270f)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (artificialSatellite != null && assemblyPlaneCreated)
        {
            DebugLogHoveredCellPortMapping();
        }

        if (!IsAssembling) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        UpdateGhost();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (part != null && part.partType == PartType.Pipe)
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

        if (mainCamera == null) mainCamera = Camera.main;
        Vector3 planeNormal = mainCamera != null ? -mainCamera.transform.forward : artificialSatellite.transform.forward;
        Vector3 planePoint = artificialSatellite.transform.position;
        assemblyPlane = new Plane(planeNormal, planePoint);
        assemblyPlaneCreated = true;

        CreateGridPlane();

        CameraManager.Instance.EnterAssemblyMode();

        occupiedCells.Clear();
        int centerIndex = gridSize / 2;
        RegisterCoreOccupiedCells(new Vector2Int(centerIndex, centerIndex));
        outputPortTemplate = FindOutputPortTemplate();
        RequestRefreshOutputPorts();

        Debug.Log("Assembly activated.");
    }

    public void Deactivate()
    {
        CameraManager.Instance.ExitAssemblyMode();
        
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
        if (partGhost != null)
        {
            Destroy(partGhost);
        }

        if (part == null || part.ghostPrefab == null)
        {
            Debug.LogError($"Part : {part}, Part Ghost : {partGhost}");
            return;
        }

        RefreshOutputPortsNow();
        this.part = part;
        pipePathStartSelected = false;
        pipeStartOutputPort = null;
        pipePreviewPath.Clear();
        hasLastPipeHoverCell = false;
        hasPipeDebugState = false;
        ClearPipePathGhosts();

        partGhost = Instantiate(part.ghostPrefab, artificialSatellite.transform);
        initialGhostRotation = partGhost.transform.localRotation;
        if (partGhost.GetComponent<AssemblyGhostMarker>() == null)
        {
            partGhost.AddComponent<AssemblyGhostMarker>();
        }

        ghostPortProfile = partGhost.GetComponent<AssemblyPartPortProfile>();
        if (ghostPortProfile == null)
        {
            ghostPortProfile = partGhost.AddComponent<AssemblyPartPortProfile>();
        }
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

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        float cameraSpaceDepth = Vector3.Dot(
            artificialSatellite.transform.position - mainCamera.transform.position,
            mainCamera.transform.forward
        );
        cameraSpaceDepth = Mathf.Max(0.01f, cameraSpaceDepth);

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cameraSpaceDepth));
        Vector3 hitPoint = artificialSatellite.transform.InverseTransformPoint(worldPoint);

        Vector2 snapOffset = GetCurrentPartSnapOffset();

        float snappedX = Mathf.Round((hitPoint.x - snapOffset.x) / cellSize) * cellSize + snapOffset.x;
        float snappedY = Mathf.Round((hitPoint.y - snapOffset.y) / cellSize) * cellSize + snapOffset.y;

        Vector3 finalPosition = new Vector3(snappedX, snappedY, 0);
        partGhost.transform.localPosition = finalPosition;
        isSnapped = true;

        if (part != null && part.partType == PartType.Pipe && pipePathStartSelected)
        {
            UpdatePipePathPreview();
            return;
        }

        bool areaFree = IsCurrentGhostPlacementAreaFree();
        if (!areaFree)
        {
            if (part != null && part.partType == PartType.Pipe && !pipePathStartSelected && partGhost != null)
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

        ArtificialSatellite sourceSatellite = AssemblyManager.Instance != null
            ? AssemblyManager.Instance.GetSourceSatelliteFor(artificialSatellite)
            : null;

        if (sourceSatellite != null && sourceSatellite != artificialSatellite)
        {
            AddPartToSatellite(sourceSatellite, part, finalLocalPos, finalLocalRot);
        }

        RebuildCombinedMesh(artificialSatellite);
        bool isSandboxTarget = AssemblyManager.Instance != null && AssemblyManager.Instance.IsSandboxTarget(artificialSatellite);
        if (!isSandboxTarget && sourceSatellite != null && sourceSatellite != artificialSatellite)
        {
            RebuildCombinedMesh(sourceSatellite);
        }

        Debug.Log($"Applied part {part.partName} to {artificialSatellite.name}.");

        Vector2 snapOffset = GetCurrentPartSnapOffset();
        int gridCenter = gridSize / 2;
        Vector2Int gridPos = new Vector2Int
        (
            Mathf.RoundToInt((finalLocalPos.x - snapOffset.x) / cellSize) + gridCenter,
            Mathf.RoundToInt((finalLocalPos.y - snapOffset.y) / cellSize) + gridCenter
        );
        if (placedObject != null)
        {
            RegisterOccupiedRect(gridPos, GetCurrentPartCellSpan(), placedObject);
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

        AssemblyPartPortProfile targetProfile = gameObject.GetComponent<AssemblyPartPortProfile>();
        if (targetProfile == null)
        {
            targetProfile = gameObject.AddComponent<AssemblyPartPortProfile>();
        }

        if (targetPart.partType == PartType.Pipe && usePipePortOverride)
        {
            ConfigureRuntimePipePorts(gameObject, targetProfile, false, overrideInputLocal, overrideOutputLocal);
        }
        else
        {
            ConfigureRuntimePorts(gameObject, targetPart, targetProfile, false);
        }

        AssemblyAttachmentHub attachmentHub = targetSatellite.GetComponent<AssemblyAttachmentHub>();
        if (attachmentHub == null)
        {
            attachmentHub = targetSatellite.gameObject.AddComponent<AssemblyAttachmentHub>();
        }

        attachmentHub.RegisterOrUpdate(gameObject.transform, localPos, localRot, gameObject.transform.localScale);
        attachmentHub.SyncAttachments();

        AssemblyPartFocus partFocus = gameObject.GetComponent<AssemblyPartFocus>();
        if (partFocus == null) partFocus = gameObject.AddComponent<AssemblyPartFocus>();
        partFocus.Initialize(targetPart, targetSatellite);

        AssemblyMeshCombiner combiner = targetSatellite.GetComponent<AssemblyMeshCombiner>();
        if (combiner != null && combiner.combinedOnlyMode)
        {
            MeshRenderer[] meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null) meshRenderers[i].enabled = false;
            }
        }

        return gameObject;
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

        AssemblyMeshCombiner combiner = targetSatellite.GetComponent<AssemblyMeshCombiner>();
        if (combiner == null)
        {
            combiner = targetSatellite.gameObject.AddComponent<AssemblyMeshCombiner>();
        }
        combiner.RebuildCombinedMesh();
    }

    private Vector2 GetCurrentPartSnapOffset()
    {
        Vector2Int span = GetCurrentPartCellSpan();
        int width = span.x;
        int height = span.y;

        float offsetX = (width % 2 == 0) ? (cellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (cellSize * 0.5f) : 0f;

        return new Vector2(offsetX, offsetY);
    }

    private Vector2Int GetCurrentPartCellSpan()
    {
        if (part != null)
        {
            return new Vector2Int(Mathf.Max(1, part.gridWidth), Mathf.Max(1, part.gridHeight));
        }

        if (partGhost != null)
        {
            Vector3 scale = partGhost.transform.localScale;
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) / cellSize));
            int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.y) / cellSize));
            return new Vector2Int(width, height);
        }

        return Vector2Int.one;
    }

    private void Cancel()
    {
        SetOutputPortsPulsing(false);
        ClearPipePathGhosts();

        if (partGhost != null)
        {
            partGhost.SetActive(false);
            Destroy(partGhost);
        }

        partGhost = null;
        part = null;
        matchedOutputPort = null;
        ghostPortProfile = null;
        ghostRenderers.Clear();
        ghostPropertyBlocks.Clear();
        IsAssembling = false;
        isSnapped = false;
        canPlace = false;
        pipePathStartSelected = false;
        pipeStartOutputPort = null;
        pipePreviewPath.Clear();
        hasLastPipeHoverCell = false;
        hasPipeDebugState = false;
        hasHoverDebugCell = false;

        Debug.Log("Assembly cancelled.");
    }

    private void CreateGridPlane()
    {
        if (gridPlane != null) Destroy(gridPlane);

        gridPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gridPlane.name = "AssemblyGridPlane";
        Destroy(gridPlane.GetComponent<Collider>());
        SmallScaleLayerUtility.ApplyRecursively(gridPlane.transform);

        float planeSize = gridSize * cellSize;
        gridPlane.transform.localScale = new Vector3(planeSize, planeSize, 1f);

        AssemblyAttachmentHub attachmentHub = artificialSatellite.GetComponent<AssemblyAttachmentHub>();
        if (attachmentHub == null)
        {
            attachmentHub = artificialSatellite.gameObject.AddComponent<AssemblyAttachmentHub>();
        }
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

    private void RequestRefreshOutputPorts()
    {
        outputPortRefreshQueued = true;
        if (outputPortRefreshCoroutine == null)
        {
            outputPortRefreshCoroutine = StartCoroutine(RefreshOutputPortsAtEndOfFrame());
        }
    }

    private IEnumerator RefreshOutputPortsAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        outputPortRefreshCoroutine = null;
        if (!outputPortRefreshQueued) yield break;

        outputPortRefreshQueued = false;
        RefreshOutputPortsNow();
    }

    private void RefreshOutputPortsNow()
    {
        outputPorts.Clear();
        outputPortPulses.Clear();
        outputMaskByCell.Clear();
        outputPortsByCellAndSide.Clear();
        coreLayoutByPort.Clear();

        if (artificialSatellite == null) return;

        Transform[] allTransforms = artificialSatellite.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null || current == artificialSatellite.transform) continue;
            if (current.GetComponentInParent<AssemblyGhostMarker>() != null) continue;
            if (!current.name.Contains("OutputPort")) continue;

            AssemblyPort outputPort = current.GetComponent<AssemblyPort>();
            if (outputPort == null) outputPort = current.gameObject.AddComponent<AssemblyPort>();
            outputPort.portType = AssemblyPortType.Output;
            if (outputPort.GetComponentInParent<AssemblyPartFocus>(true) == null)
            {
                // Core/output prefab ports: infer side once and cache as a direction vector.
                CellSideMask inferredSide = GetOutputPortSideMask(outputPort);
                if (inferredSide != CellSideMask.None)
                {
                    outputPort.localDirection = SideToVector3(inferredSide);
                }
            }
            outputPorts.Add(outputPort);

            AssemblyPortPulse pulse = current.GetComponent<AssemblyPortPulse>();
            if (pulse == null) pulse = current.gameObject.AddComponent<AssemblyPortPulse>();
            outputPortPulses.Add(pulse);
        }

        BuildCoreLayoutMapping();

        // Second phase: assign mappings only after all output ports are discovered.
        for (int i = 0; i < outputPorts.Count; i++)
        {
            RegisterOutputPortCellInfo(outputPorts[i]);
        }
    }

    private void BuildCoreLayoutMapping()
    {
        if (artificialSatellite == null) return;

        AssemblyCorePortLayout coreLayout = artificialSatellite.GetComponentInChildren<AssemblyCorePortLayout>(true);
        if (coreLayout == null)
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning("[CorePortMap] AssemblyCorePortLayout missing on core object.");
            }
            return;
        }

        List<AssemblyCorePortLayout.OutputPortEntry> entries = coreLayout.OutputPorts;
        if (entries == null || entries.Count == 0) return;

        Vector2Int coreCenterCell = LocalPositionToGrid(coreLayout.transform.localPosition, Vector2.zero);
        if (!IsInsideGrid(coreCenterCell))
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning($"[CorePortMap] coreCenterCell out of range: {coreCenterCell}");
            }
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AssemblyCorePortLayout.OutputPortEntry entry = entries[i];
            if (entry == null || entry.portTransform == null) continue;

            AssemblyPort port = entry.portTransform.GetComponent<AssemblyPort>();
            if (port == null) continue;

            CellSideMask side = ConvertCoreLayoutSide(entry.outputSide);
            if (side == CellSideMask.None) continue;

            Vector2Int sourceCell = coreCenterCell + entry.relativeSourceCell;
            coreLayoutByPort[port] = (sourceCell, side);
        }
    }

    private static CellSideMask ConvertCoreLayoutSide(AssemblyCorePortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyCorePortLayout.PortSide.Top: return CellSideMask.Top;
            case AssemblyCorePortLayout.PortSide.Bottom: return CellSideMask.Bottom;
            case AssemblyCorePortLayout.PortSide.Left: return CellSideMask.Left;
            case AssemblyCorePortLayout.PortSide.Right: return CellSideMask.Right;
            default: return CellSideMask.None;
        }
    }

    private static CellSideMask ConvertPartLayoutSide(AssemblyPartPortLayout.PortSide side)
    {
        switch (side)
        {
            case AssemblyPartPortLayout.PortSide.Top: return CellSideMask.Top;
            case AssemblyPartPortLayout.PortSide.Bottom: return CellSideMask.Bottom;
            case AssemblyPartPortLayout.PortSide.Left: return CellSideMask.Left;
            case AssemblyPartPortLayout.PortSide.Right: return CellSideMask.Right;
            default: return CellSideMask.None;
        }
    }

    private static AssemblyPartPortLayout.PortSide ConvertMaskToPartLayoutSide(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return AssemblyPartPortLayout.PortSide.Top;
            case CellSideMask.Bottom: return AssemblyPartPortLayout.PortSide.Bottom;
            case CellSideMask.Left: return AssemblyPartPortLayout.PortSide.Left;
            case CellSideMask.Right: return AssemblyPartPortLayout.PortSide.Right;
            default: return AssemblyPartPortLayout.PortSide.Right;
        }
    }

    private void SetOutputPortsPulsing(bool isPulsing)
    {
        for (int i = 0; i < outputPortPulses.Count; i++)
        {
            if (outputPortPulses[i] != null) outputPortPulses[i].SetPulsing(isPulsing);
        }
    }

    private bool TryMatchGhostToOutputPort()
    {
        matchedOutputPort = null;
        if (partGhost == null || ghostPortProfile == null || outputPorts.Count == 0) return false;

        if (part != null && part.partType == PartType.Pipe && !pipePathStartSelected)
        {
            Quaternion originalRotation = partGhost.transform.localRotation;
            for (int i = 0; i < PipeAutoRotations.Length; i++)
            {
                partGhost.transform.localRotation = PipeAutoRotations[i];
                if (TryGetPipeStartOutputPortMatch(out AssemblyPort candidatePort))
                {
                    matchedOutputPort = candidatePort;
                    return true;
                }
            }

            partGhost.transform.localRotation = originalRotation;
            return false;
        }

        return TryGetBestOutputPortMatchForCurrentGhostPose(out matchedOutputPort, out _);
    }

    private bool TryGetPipeStartOutputPortMatch(out AssemblyPort matchedPort)
    {
        matchedPort = null;
        if (partGhost == null || ghostPortProfile == null) return false;

        Vector2Int centerCell = LocalPositionToGrid(partGhost.transform.localPosition, GetCurrentPartSnapOffset());
        int inputCount = ghostPortProfile.InputPortCount;
        for (int i = 0; i < inputCount; i++)
        {
            Vector3 inputLocal = ghostPortProfile.GetInputPortLocalPosition(i);
            Vector3 inputDirOnSatellite = partGhost.transform.localRotation * inputLocal;
            CellSideMask inputSide = DirectionToSideMask(inputDirOnSatellite);
            if (inputSide == CellSideMask.None) continue;

            // Output mapping is stored on the destination cell side where the connection enters.
            CellSideMask requiredOutputSide = inputSide;
            if (TryGetAvailableOutputPort(centerCell, requiredOutputSide, out AssemblyPort outputPort))
            {
                matchedPort = outputPort;
                DebugLogPipeStartMatch(centerCell, inputSide, requiredOutputSide, matchedPort);
                return true;
            }

            DebugLogPipeStartMatch(centerCell, inputSide, requiredOutputSide, null);
        }

        return false;
    }

    private bool TryGetBestOutputPortMatchForCurrentGhostPose(out AssemblyPort bestPort, out float bestDistance)
    {
        bestPort = null;
        bestDistance = float.MaxValue;

        int inputCount = ghostPortProfile.InputPortCount;
        for (int inputIndex = 0; inputIndex < inputCount; inputIndex++)
        {
            Vector3 inputLocal = ghostPortProfile.GetInputPortLocalPosition(inputIndex);
            Vector3 currentInputWorld = partGhost.transform.TransformPoint(inputLocal);
            Vector3 currentInputLocalOnSatellite =
                artificialSatellite.transform.InverseTransformPoint(currentInputWorld);

            for (int outputIndex = 0; outputIndex < outputPorts.Count; outputIndex++)
            {
                AssemblyPort outputPort = outputPorts[outputIndex];
                if (outputPort == null || outputPort.IsOccupied) continue;

                Vector3 outputWorldAnchor = GetBestOutputPortWorldAnchorForInput(outputPort, currentInputWorld);
                Vector3 outputLocalOnSatellite =
                    artificialSatellite.transform.InverseTransformPoint(outputWorldAnchor);
                float distance = (outputLocalOnSatellite - currentInputLocalOnSatellite).magnitude;
                if (distance > PortMatchDistance || distance >= bestDistance) continue;

                bestDistance = distance;
                bestPort = outputPort;
            }
        }

        return bestPort != null;
    }

    private void CacheGhostRenderers()
    {
        ghostRenderers.Clear();
        ghostPropertyBlocks.Clear();

        if (partGhost == null) return;

        Renderer[] renderers = partGhost.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            ghostRenderers.Add(renderers[i]);
            ghostPropertyBlocks.Add(new MaterialPropertyBlock());
        }
    }

    private void SetGhostPlacementVisual(bool isValidPlacement)
    {
        Color targetColor = isValidPlacement ? GhostValidColor : GhostInvalidColor;

        for (int i = 0; i < ghostRenderers.Count; i++)
        {
            Renderer renderer = ghostRenderers[i];
            if (renderer == null) continue;
            if (renderer.GetComponentInParent<AssemblyPortVisualMarker>() != null) continue;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null) continue;

            bool hasBaseColor = sharedMaterial.HasProperty(BaseColorId);
            bool hasColor = sharedMaterial.HasProperty(ColorId);
            if (!hasBaseColor && !hasColor) continue;

            MaterialPropertyBlock block = ghostPropertyBlocks[i];
            renderer.GetPropertyBlock(block);
            if (hasBaseColor) block.SetColor(BaseColorId, targetColor);
            if (hasColor) block.SetColor(ColorId, targetColor);
            renderer.SetPropertyBlock(block);
        }
    }

    private void HandlePipeLeftClick()
    {
        if (part == null || partGhost == null || artificialSatellite == null) return;

        if (!pipePathStartSelected)
        {
            if (!isSnapped || !canPlace || matchedOutputPort == null) return;

            pipePathStartSelected = true;
            pipeStartOutputPort = matchedOutputPort;
            pipePathStartCell = LocalPositionToGrid(partGhost.transform.localPosition, GetCurrentPartSnapOffset());
            pipePreviewPath.Clear();
            pipePreviewPath.Add(pipePathStartCell);
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

        Vector2Int hoverCell = LocalPositionToGrid(partGhost.transform.localPosition, GetCurrentPartSnapOffset());
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

        ArtificialSatellite sourceSatellite = AssemblyManager.Instance != null
            ? AssemblyManager.Instance.GetSourceSatelliteFor(artificialSatellite)
            : null;

        for (int i = 0; i < pipePreviewPath.Count; i++)
        {
            Vector2Int cell = pipePreviewPath[i];
            Vector2Int previousDir = GetPathDirection(pipePreviewPath, i - 1, i);
            Vector2Int nextDir = GetPathDirection(pipePreviewPath, i, i + 1);
            Vector3 localPos = GridToLocalPosition(cell, GetCurrentPartSnapOffset());
            bool isCorner = IsCornerSegment(previousDir, nextDir, i, pipePreviewPath.Count);
            Quaternion localRot = isCorner
                ? Quaternion.identity
                : GetPipeSegmentRotation(previousDir, nextDir);
            GameObject placed = isCorner
                ? AddPipeCornerToSatellite(artificialSatellite, part, localPos, localRot, previousDir, nextDir)
                : AddPartToSatellite(artificialSatellite, part, localPos, localRot);

            occupiedCells[cell] = placed != null ? placed : artificialSatellite.gameObject;

            if (sourceSatellite != null && sourceSatellite != artificialSatellite)
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

        RebuildCombinedMesh(artificialSatellite);
        bool isSandboxTarget = AssemblyManager.Instance != null && AssemblyManager.Instance.IsSandboxTarget(artificialSatellite);
        if (!isSandboxTarget && sourceSatellite != null && sourceSatellite != artificialSatellite)
        {
            RebuildCombinedMesh(sourceSatellite);
        }

        Debug.Log($"Applied pipe path with {pipePreviewPath.Count} segments.");

        Cancel();
        RequestRefreshOutputPorts();
    }

    private void ConfigureRuntimePorts(GameObject root, Part targetPart, AssemblyPartPortProfile profile, bool isGhost)
    {
        if (root == null || targetPart == null || profile == null) return;

        Transform runtimePortsRoot = root.transform.Find(RuntimePortsRootName);
        if (runtimePortsRoot != null)
        {
            Destroy(runtimePortsRoot.gameObject);
        }

        profile.SetInputPortLocalPositions(new[] { Vector3.zero });
        if (targetPart.partType != PartType.Pipe) return;

        float widthWorld = Mathf.Max(cellSize, targetPart.gridWidth * cellSize);
        float halfWidthWorld = widthWorld * 0.5f;
        float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(root.transform.localScale.x));
        float halfWidthLocal = halfWidthWorld / rootScaleX;

        Vector3 inputLocal = new Vector3(-halfWidthLocal, 0f, 0f);
        Vector3 outputLocal = new Vector3(halfWidthLocal, 0f, 0f);
        ConfigureRuntimePipePorts(root, profile, isGhost, inputLocal, outputLocal);
    }

    private void ConfigureRuntimePipePorts(
        GameObject root,
        AssemblyPartPortProfile profile,
        bool isGhost,
        Vector3 inputLocal,
        Vector3 outputLocal)
    {
        if (root == null || profile == null) return;

        Transform runtimePortsRoot = root.transform.Find(RuntimePortsRootName);
        if (runtimePortsRoot != null)
        {
            Destroy(runtimePortsRoot.gameObject);
        }

        profile.SetInputPortLocalPositions(new[] { inputLocal });

        GameObject portsRootObject = new GameObject(RuntimePortsRootName);
        portsRootObject.transform.SetParent(root.transform, false);

        AssemblyPort inputPort = CreateRuntimePortMarker(
            portsRootObject.transform,
            "InputPort",
            AssemblyPortType.Input,
            inputLocal,
            isGhost);
        AssemblyPort outputPort = CreateRuntimePortMarker(
            portsRootObject.transform,
            "OutputPort",
            AssemblyPortType.Output,
            outputLocal,
            isGhost);
        ConfigureRuntimePartPortLayout(root, inputPort, outputPort, inputLocal, outputLocal);
    }

    private AssemblyPort CreateRuntimePortMarker(
        Transform parent,
        string name,
        AssemblyPortType portType,
        Vector3 localPosition,
        bool isGhost)
    {
        GameObject marker = CreatePortVisualClone();
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        MatchWorldScale(marker.transform, GetPortVisualWorldScale());

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        AssemblyPortVisualMarker visualMarker = marker.GetComponent<AssemblyPortVisualMarker>();
        if (visualMarker == null)
        {
            marker.AddComponent<AssemblyPortVisualMarker>();
        }

        AssemblyPort assemblyPort = marker.GetComponent<AssemblyPort>();
        if (assemblyPort == null)
        {
            assemblyPort = marker.AddComponent<AssemblyPort>();
        }
        assemblyPort.portType = portType;
        assemblyPort.SetOccupied(false);
        assemblyPort.localDirection = localPosition.sqrMagnitude > 0.000001f
            ? localPosition.normalized
            : Vector3.right;
        SetDockingDirection(marker.transform, localPosition);

        Renderer renderer = marker.GetComponent<Renderer>();
        if (portType == AssemblyPortType.Input && renderer != null)
        {
            Color inputColor = InputPortColor;
            if (isGhost) inputColor.a = GhostValidColor.a;
            if (isGhost) EnsureRendererTransparencyRecursive(marker.transform);
            SetRendererColorRecursive(marker.transform, inputColor);
        }
        else if (isGhost)
        {
            EnsureRendererTransparencyRecursive(marker.transform);
            SetRendererAlphaRecursive(marker.transform, GhostValidColor.a);
        }

        return assemblyPort;
    }

    private void ConfigureRuntimePartPortLayout(
        GameObject root,
        AssemblyPort inputPort,
        AssemblyPort outputPort,
        Vector3 inputLocal,
        Vector3 outputLocal)
    {
        if (root == null || inputPort == null || outputPort == null) return;

        AssemblyPartPortLayout layout = root.GetComponent<AssemblyPartPortLayout>();
        if (layout == null) layout = root.AddComponent<AssemblyPartPortLayout>();

        List<AssemblyPartPortLayout.PortEntry> entries = new List<AssemblyPartPortLayout.PortEntry>(2)
        {
            new AssemblyPartPortLayout.PortEntry
            {
                portTransform = inputPort.transform,
                relativeSourceCell = Vector2Int.zero,
                side = ConvertMaskToPartLayoutSide(DirectionToSideMask(inputLocal)),
                portType = AssemblyPortType.Input
            },
            new AssemblyPartPortLayout.PortEntry
            {
                portTransform = outputPort.transform,
                relativeSourceCell = Vector2Int.zero,
                side = ConvertMaskToPartLayoutSide(DirectionToSideMask(outputLocal)),
                portType = AssemblyPortType.Output
            }
        };
        layout.SetPorts(entries);
    }

    private GameObject CreatePortVisualClone()
    {
        GameObject template = GetOutputPortTemplate();
        GameObject marker;

        if (template != null)
        {
            marker = Instantiate(template);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        Collider[] colliders = marker.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) Destroy(colliders[i]);
        }

        // Template can come from combined-only renderers, so force visibility on cloned port visuals.
        Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].enabled = true;
        }

        // Runtime port visuals should not inherit pulse behavior from source output ports.
        AssemblyPortPulse[] pulses = marker.GetComponentsInChildren<AssemblyPortPulse>(true);
        for (int i = 0; i < pulses.Length; i++)
        {
            if (pulses[i] != null) Destroy(pulses[i]);
        }

        return marker;
    }

    private Vector3 GetBestOutputPortWorldAnchorForInput(AssemblyPort outputPort, Vector3 inputWorldPoint)
    {
        if (outputPort == null) return Vector3.zero;

        Docking[] dockings = outputPort.GetComponentsInChildren<Docking>(true);
        if (dockings != null && dockings.Length > 0)
        {
            float bestDistanceSq = float.MaxValue;
            Vector3 bestAnchor = outputPort.transform.position;

            for (int i = 0; i < dockings.Length; i++)
            {
                Docking docking = dockings[i];
                if (docking == null) continue;

                float distanceSq = (docking.transform.position - inputWorldPoint).sqrMagnitude;
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                bestAnchor = docking.transform.position;
            }

            return bestAnchor;
        }

        return outputPort.transform.position;
    }

    private void SetDockingDirection(Transform portRoot, Vector3 localDirectionHint)
    {
        if (portRoot == null) return;

        Docking[] dockings = portRoot.GetComponentsInChildren<Docking>(true);
        if (dockings == null || dockings.Length == 0) return;

        int bestIndex = -1;
        float bestDot = float.NegativeInfinity;

        Vector3 hint = localDirectionHint;
        if (hint.sqrMagnitude < 0.000001f) hint = Vector3.right;
        hint.Normalize();

        for (int i = 0; i < dockings.Length; i++)
        {
            Docking docking = dockings[i];
            if (docking == null) continue;

            Transform dTransform = docking.transform;
            Vector3 candidateLocalDir = (dTransform.localPosition.sqrMagnitude < 0.000001f)
                ? Vector3.right
                : dTransform.localPosition.normalized;
            float dot = Vector3.Dot(candidateLocalDir, hint);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        for (int i = 0; i < dockings.Length; i++)
        {
            if (dockings[i] != null) dockings[i].isDocking = (i == bestIndex);
        }
    }

    private Vector3 GetPortVisualWorldScale()
    {
        GameObject template = GetOutputPortTemplate();
        if (template != null) return template.transform.lossyScale;
        return Vector3.one * 0.03f;
    }

    private GameObject GetOutputPortTemplate()
    {
        if (outputPortTemplate != null) return outputPortTemplate;

        outputPortTemplate = FindOutputPortTemplate();
        if (outputPortTemplate != null) return outputPortTemplate;

        for (int i = 0; i < outputPorts.Count; i++)
        {
            if (outputPorts[i] != null) return outputPorts[i].gameObject;
        }

        return null;
    }

    private GameObject FindOutputPortTemplate()
    {
        if (artificialSatellite == null) return null;

        Transform[] allTransforms = artificialSatellite.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform current = allTransforms[i];
            if (current == null) continue;
            if (current.GetComponentInParent<AssemblyGhostMarker>() != null) continue;
            if (current.name.Contains("OutputPort")) return current.gameObject;
        }

        return null;
    }

    private static void MatchWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        Transform parent = target.parent;
        Vector3 parentLossyScale = parent != null ? parent.lossyScale : Vector3.one;

        float scaleX = Mathf.Abs(parentLossyScale.x) < 0.0001f ? desiredWorldScale.x : desiredWorldScale.x / parentLossyScale.x;
        float scaleY = Mathf.Abs(parentLossyScale.y) < 0.0001f ? desiredWorldScale.y : desiredWorldScale.y / parentLossyScale.y;
        float scaleZ = Mathf.Abs(parentLossyScale.z) < 0.0001f ? desiredWorldScale.z : desiredWorldScale.z / parentLossyScale.z;
        target.localScale = new Vector3(scaleX, scaleY, scaleZ);
    }

    private void SetRendererColorRecursive(Transform root, Color color)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, color);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    private void SetRendererAlphaRecursive(Transform root, float alpha)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Color baseColor = Color.white;
            if (renderer.sharedMaterial.HasProperty(BaseColorId))
            {
                baseColor = renderer.sharedMaterial.GetColor(BaseColorId);
            }
            else if (renderer.sharedMaterial.HasProperty(ColorId))
            {
                baseColor = renderer.sharedMaterial.GetColor(ColorId);
            }

            Color updated = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.HasProperty(BaseColorId)) block.SetColor(BaseColorId, updated);
            if (renderer.sharedMaterial.HasProperty(ColorId)) block.SetColor(ColorId, updated);
            renderer.SetPropertyBlock(block);
        }
    }

    private void EnsureRendererTransparencyRecursive(Transform root)
    {
        if (root == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Material runtimeMaterial = renderer.material;
            if (runtimeMaterial == null) continue;

            if (runtimeMaterial.HasProperty("_Surface")) runtimeMaterial.SetFloat("_Surface", 1f);
            if (runtimeMaterial.HasProperty("_ZWrite")) runtimeMaterial.SetFloat("_ZWrite", 0f);
            if (runtimeMaterial.HasProperty("_SrcBlend")) runtimeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (runtimeMaterial.HasProperty("_DstBlend")) runtimeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (runtimeMaterial.HasProperty("_AlphaClip")) runtimeMaterial.SetFloat("_AlphaClip", 0f);

            runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            runtimeMaterial.DisableKeyword("_ALPHATEST_ON");
            runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            runtimeMaterial.SetOverrideTag("RenderType", "Transparent");
        }
    }

    private void RegisterOutputPortCellInfo(AssemblyPort outputPort)
    {
        if (outputPort == null || artificialSatellite == null) return;

        Vector2Int sourceCell;
        CellSideMask outputSide;

        if (coreLayoutByPort.TryGetValue(outputPort, out (Vector2Int sourceCell, CellSideMask outputSide) coreMapped))
        {
            sourceCell = coreMapped.sourceCell;
            outputSide = coreMapped.outputSide;
        }
        else if (TryGetPartLayoutPortMapping(outputPort, out sourceCell, out outputSide))
        {
            // Part layout mapping succeeded.
        }
        else
        {
            bool belongsToCoreLayout = outputPort.GetComponentInParent<AssemblyCorePortLayout>(true) != null;
            if (belongsToCoreLayout)
            {
                // Core ports must be mapped only from explicit core layout data.
                if (debugPipePlacement)
                {
                    Debug.LogWarning($"[CorePortMap] Missing explicit mapping for core port: {outputPort.name}");
                }
                return;
            }

            bool belongsToPartLayout = outputPort.GetComponentInParent<AssemblyPartPortLayout>(true) != null;
            if (belongsToPartLayout)
            {
                if (debugPipePlacement)
                {
                    Debug.LogWarning($"[PartPortMap] Missing explicit mapping for part port: {outputPort.name}");
                }
                return;
            }

            outputSide = ResolveOutputPortSide(outputPort);
            if (outputSide == CellSideMask.None) return;

            Vector2Int outputDirFallback = SideToCellOffset(outputSide);
            if (outputDirFallback == Vector2Int.zero) return;

            // Default runtime parts: estimate the source cell from the output marker location.
            Vector3 localPos = artificialSatellite.transform.InverseTransformPoint(outputPort.transform.position);
            Vector3 halfCellBack = new Vector3(outputDirFallback.x * cellSize * 0.5f, outputDirFallback.y * cellSize * 0.5f, 0f);
            sourceCell = LocalPositionToGrid(localPos - halfCellBack, Vector2.zero);
        }

        Vector2Int outputDir = SideToCellOffset(outputSide);
        if (outputDir == Vector2Int.zero) return;

        Vector2Int mappedCell = sourceCell + outputDir;
        if (!IsInsideGrid(mappedCell)) return;

        CellSideMask mappedSide = OppositeSide(outputSide);
        if (mappedSide == CellSideMask.None) return;

        if (outputMaskByCell.TryGetValue(mappedCell, out CellSideMask currentMask))
        {
            outputMaskByCell[mappedCell] = currentMask | mappedSide;
        }
        else
        {
            outputMaskByCell[mappedCell] = mappedSide;
        }

        if (!outputPortsByCellAndSide.TryGetValue(mappedCell, out Dictionary<CellSideMask, AssemblyPort> perSide))
        {
            perSide = new Dictionary<CellSideMask, AssemblyPort>();
            outputPortsByCellAndSide[mappedCell] = perSide;
        }

        if (perSide.TryGetValue(mappedSide, out AssemblyPort existing) && existing != null && existing != outputPort)
        {
            if (debugPipePlacement)
            {
                Debug.LogWarning(
                    $"[PipeStartDebug] duplicate output side mapping cell={mappedCell} side={mappedSide} " +
                    $"existing={existing.name} new={outputPort.name}");
            }
        }
        perSide[mappedSide] = outputPort;
    }

    private bool TryGetPartLayoutPortMapping(AssemblyPort port, out Vector2Int sourceCell, out CellSideMask outputSide)
    {
        sourceCell = Vector2Int.zero;
        outputSide = CellSideMask.None;
        if (port == null) return false;

        AssemblyPartPortLayout layout = port.GetComponentInParent<AssemblyPartPortLayout>(true);
        if (layout == null) return false;
        if (!layout.TryGetPortEntry(port, out AssemblyPartPortLayout.PortEntry entry)) return false;
        if (entry == null || entry.portType != AssemblyPortType.Output) return false;

        AssemblyPartFocus focus = layout.GetComponent<AssemblyPartFocus>();
        Vector2 snapOffset = GetSnapOffsetFromPartFocus(focus);
        Vector2Int partCenterCell = LocalPositionToGrid(layout.transform.localPosition, snapOffset);
        if (!IsInsideGrid(partCenterCell)) return false;

        int quarterTurns = GetQuarterTurns(layout.transform.localRotation);
        Vector2Int rotatedRelativeCell = RotateCellOffset(entry.relativeSourceCell, quarterTurns);
        CellSideMask rotatedSide = RotateSide(ConvertPartLayoutSide(entry.side), quarterTurns);
        if (rotatedSide == CellSideMask.None) return false;

        sourceCell = partCenterCell + rotatedRelativeCell;
        outputSide = rotatedSide;
        return true;
    }

    private Vector2 GetSnapOffsetFromPartFocus(AssemblyPartFocus focus)
    {
        if (focus == null || focus.SourcePart == null) return Vector2.zero;

        int width = Mathf.Max(1, focus.SourcePart.gridWidth);
        int height = Mathf.Max(1, focus.SourcePart.gridHeight);
        float offsetX = (width % 2 == 0) ? (cellSize * 0.5f) : 0f;
        float offsetY = (height % 2 == 0) ? (cellSize * 0.5f) : 0f;
        return new Vector2(offsetX, offsetY);
    }

    private static int GetQuarterTurns(Quaternion localRotation)
    {
        float z = Mathf.Repeat(localRotation.eulerAngles.z, 360f);
        int turns = Mathf.RoundToInt(z / 90f) % 4;
        if (turns < 0) turns += 4;
        return turns;
    }

    private static Vector2Int RotateCellOffset(Vector2Int value, int quarterTurns)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;
        Vector2Int result = value;
        for (int i = 0; i < turns; i++)
        {
            result = new Vector2Int(-result.y, result.x);
        }

        return result;
    }

    private static CellSideMask RotateSide(CellSideMask side, int quarterTurns)
    {
        Vector2Int dir = SideToCellOffset(side);
        if (dir == Vector2Int.zero) return CellSideMask.None;

        Vector2Int rotated = RotateCellOffset(dir, quarterTurns);
        if (rotated == Vector2Int.up) return CellSideMask.Top;
        if (rotated == Vector2Int.down) return CellSideMask.Bottom;
        if (rotated == Vector2Int.left) return CellSideMask.Left;
        if (rotated == Vector2Int.right) return CellSideMask.Right;
        return CellSideMask.None;
    }

    private CellSideMask ResolveOutputPortSide(AssemblyPort outputPort)
    {
        if (outputPort == null || artificialSatellite == null) return CellSideMask.None;

        if (outputPort.localDirection.sqrMagnitude > 0.000001f)
        {
            Vector3 worldDir = outputPort.transform.TransformDirection(outputPort.localDirection.normalized);
            Vector3 satelliteLocalDir = artificialSatellite.transform.InverseTransformDirection(worldDir);
            CellSideMask byDirection = DirectionToSideMask(satelliteLocalDir);
            if (byDirection != CellSideMask.None) return byDirection;
        }

        return GetOutputPortSideMask(outputPort);
    }

    private bool TryGetAvailableOutputPort(Vector2Int cell, CellSideMask side, out AssemblyPort port)
    {
        port = null;
        if (!outputPortsByCellAndSide.TryGetValue(cell, out Dictionary<CellSideMask, AssemblyPort> perSide)) return false;
        if (!perSide.TryGetValue(side, out AssemblyPort candidate)) return false;
        if (candidate == null || candidate.IsOccupied) return false;
        port = candidate;
        return true;
    }

    private static CellSideMask DirectionToSideMask(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.000001f) return CellSideMask.None;

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        if (absX >= absY)
        {
            return direction.x >= 0f ? CellSideMask.Right : CellSideMask.Left;
        }

        return direction.y >= 0f ? CellSideMask.Top : CellSideMask.Bottom;
    }

    private static CellSideMask GetOutputPortSideMask(AssemblyPort outputPort)
    {
        if (outputPort == null) return CellSideMask.None;

        Docking[] dockings = outputPort.GetComponentsInChildren<Docking>(true);
        for (int i = 0; i < dockings.Length; i++)
        {
            Docking docking = dockings[i];
            if (docking == null) continue;
            if (!docking.isDocking) continue;
            return DirectionToSideMask(docking.transform.localPosition);
        }

        return DirectionToSideMask(outputPort.transform.localPosition);
    }

    private static CellSideMask OppositeSide(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return CellSideMask.Bottom;
            case CellSideMask.Bottom: return CellSideMask.Top;
            case CellSideMask.Left: return CellSideMask.Right;
            case CellSideMask.Right: return CellSideMask.Left;
            default: return CellSideMask.None;
        }
    }

    private static Vector2Int SideToCellOffset(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return Vector2Int.up;
            case CellSideMask.Bottom: return Vector2Int.down;
            case CellSideMask.Left: return Vector2Int.left;
            case CellSideMask.Right: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private static Vector3 SideToVector3(CellSideMask side)
    {
        switch (side)
        {
            case CellSideMask.Top: return Vector3.up;
            case CellSideMask.Bottom: return Vector3.down;
            case CellSideMask.Left: return Vector3.left;
            case CellSideMask.Right: return Vector3.right;
            default: return Vector3.zero;
        }
    }

    private void DebugLogPipeStartMatch(
        Vector2Int centerCell,
        CellSideMask inputSide,
        CellSideMask requiredOutputSide,
        AssemblyPort matchedPort)
    {
        if (!debugPipePlacement || partGhost == null) return;

        float rotZ = Mathf.Repeat(partGhost.transform.localEulerAngles.z, 360f);
        bool isMatch = matchedPort != null;
        bool shouldLog = !hasPipeDebugState
            || centerCell != lastPipeDebugCell
            || Mathf.Abs(rotZ - lastPipeDebugRotZ) > 0.1f
            || isMatch != lastPipeDebugMatch
            || inputSide != lastPipeDebugInputSide
            || requiredOutputSide != lastPipeDebugRequiredSide;

        if (!shouldLog) return;

        CellSideMask mask = outputMaskByCell.TryGetValue(centerCell, out CellSideMask cellMask)
            ? cellMask
            : CellSideMask.None;

        string matchedName = matchedPort != null ? matchedPort.name : "none";
        string sides = GetCellOutputSideDetails(centerCell);
        Debug.Log(
            $"[PipeStartDebug] cell={centerCell} rotZ={rotZ:F1} input={inputSide} needs={requiredOutputSide} " +
            $"cellOutputMask={mask} sides={sides} matched={isMatch} matchedPort={matchedName}");

        hasPipeDebugState = true;
        lastPipeDebugCell = centerCell;
        lastPipeDebugRotZ = rotZ;
        lastPipeDebugMatch = isMatch;
        lastPipeDebugInputSide = inputSide;
        lastPipeDebugRequiredSide = requiredOutputSide;
    }

    private string GetCellOutputSideDetails(Vector2Int cell)
    {
        if (!outputPortsByCellAndSide.TryGetValue(cell, out Dictionary<CellSideMask, AssemblyPort> perSide))
        {
            return "T:none,B:none,L:none,R:none";
        }

        string top = perSide.TryGetValue(CellSideMask.Top, out AssemblyPort t) && t != null ? t.name : "none";
        string bottom = perSide.TryGetValue(CellSideMask.Bottom, out AssemblyPort b) && b != null ? b.name : "none";
        string left = perSide.TryGetValue(CellSideMask.Left, out AssemblyPort l) && l != null ? l.name : "none";
        string right = perSide.TryGetValue(CellSideMask.Right, out AssemblyPort r) && r != null ? r.name : "none";
        return $"T:{top},B:{bottom},L:{left},R:{right}";
    }

    private void DebugLogHoveredCellPortMapping()
    {
        if (!debugHoverCellPortMapping || !TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)) return;

        Vector2Int hoveredCell = LocalPositionToGrid(localPoint, Vector2.zero);
        if (!IsInsideGrid(hoveredCell)) return;

        if (hasHoverDebugCell && hoveredCell == lastHoverDebugCell) return;
        hasHoverDebugCell = true;
        lastHoverDebugCell = hoveredCell;

        CellSideMask mask = outputMaskByCell.TryGetValue(hoveredCell, out CellSideMask mappedMask)
            ? mappedMask
            : CellSideMask.None;
        string sides = GetCellOutputSideDetails(hoveredCell);
        bool occupied = occupiedCells.ContainsKey(hoveredCell);

        Debug.Log($"[CellPortMap] cell={hoveredCell} occupied={occupied} mask={mask} sides={sides}");
    }

    private bool TryGetMouseLocalOnSatellitePlane(out Vector3 localPoint)
    {
        localPoint = Vector3.zero;
        if (artificialSatellite == null) return false;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        float cameraSpaceDepth = Vector3.Dot(
            artificialSatellite.transform.position - mainCamera.transform.position,
            mainCamera.transform.forward
        );
        cameraSpaceDepth = Mathf.Max(0.01f, cameraSpaceDepth);

        Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cameraSpaceDepth));
        localPoint = artificialSatellite.transform.InverseTransformPoint(worldPoint);
        return true;
    }

    private static Vector2Int GetPathDirection(List<Vector2Int> path, int fromIndex, int toIndex)
    {
        if (path == null) return Vector2Int.zero;
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= path.Count || toIndex >= path.Count) return Vector2Int.zero;
        return path[toIndex] - path[fromIndex];
    }

    private static bool IsCornerSegment(Vector2Int previousDir, Vector2Int nextDir, int index, int count)
    {
        if (count < 3) return false;
        if (index <= 0 || index >= count - 1) return false;
        if (previousDir == Vector2Int.zero || nextDir == Vector2Int.zero) return false;
        return previousDir != nextDir;
    }

    private static Quaternion GetPipeSegmentRotation(Vector2Int previousDir, Vector2Int nextDir)
    {
        Vector2Int reference = nextDir != Vector2Int.zero ? nextDir : previousDir;
        if (reference == Vector2Int.up || reference == Vector2Int.down)
        {
            return Quaternion.Euler(0f, 0f, 90f);
        }

        return Quaternion.identity;
    }

    private void BuildPipePathGhosts(List<Vector2Int> path, bool isValid)
    {
        ClearPipePathGhosts();
        if (part == null || part.ghostPrefab == null || artificialSatellite == null) return;
        if (path == null || path.Count == 0) return;

        pipePathGhostRoot = new GameObject("PipePathGhostRoot");
        pipePathGhostRoot.transform.SetParent(artificialSatellite.transform, false);
        pipePathGhostRoot.AddComponent<AssemblyGhostMarker>();

        Color ghostColor = isValid ? GhostValidColor : GhostInvalidColor;
        Vector2 snapOffset = GetCurrentPartSnapOffset();

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int cell = path[i];
            Vector2Int previousDir = GetPathDirection(path, i - 1, i);
            Vector2Int nextDir = GetPathDirection(path, i, i + 1);
            bool isCorner = IsCornerSegment(previousDir, nextDir, i, path.Count);
            Quaternion rot = isCorner
                ? Quaternion.identity
                : GetPipeSegmentRotation(previousDir, nextDir);
            Vector3 localPos = GridToLocalPosition(cell, snapOffset);

            GameObject ghostSegment = isCorner
                ? CreatePipeCornerObject(part.ghostPrefab, true, previousDir, nextDir)
                : Instantiate(part.ghostPrefab);

            ghostSegment.name = isCorner ? "PipeCornerGhost" : "PipeGhost";
            ghostSegment.transform.SetParent(pipePathGhostRoot.transform, false);
            ghostSegment.transform.localPosition = localPos;
            ghostSegment.transform.localRotation = rot;

            if (ghostSegment.GetComponent<AssemblyGhostMarker>() == null)
            {
                ghostSegment.AddComponent<AssemblyGhostMarker>();
            }
            SmallScaleLayerUtility.ApplyRecursively(ghostSegment.transform);
            SetRendererColorRecursive(ghostSegment.transform, ghostColor);
            EnsureRendererTransparencyRecursive(ghostSegment.transform);
        }
    }

    private void ClearPipePathGhosts()
    {
        if (pipePathGhostRoot != null)
        {
            pipePathGhostRoot.SetActive(false);
            Destroy(pipePathGhostRoot);
            pipePathGhostRoot = null;
        }
    }

    private Vector2Int LocalPositionToGrid(Vector3 localPos, Vector2 snapOffset)
    {
        int gridCenter = gridSize / 2;
        return new Vector2Int
        (
            QuantizeToCellIndex((localPos.x - snapOffset.x) / cellSize) + gridCenter,
            QuantizeToCellIndex((localPos.y - snapOffset.y) / cellSize) + gridCenter
        );
    }

    private Vector3 GridToLocalPosition(Vector2Int gridPos, Vector2 snapOffset)
    {
        int gridCenter = gridSize / 2;
        float x = (gridPos.x - gridCenter) * cellSize + snapOffset.x;
        float y = (gridPos.y - gridCenter) * cellSize + snapOffset.y;
        return new Vector3(x, y, 0f);
    }

    private bool TryFindPipePath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (!IsInsideGrid(start) || !IsInsideGrid(goal)) return false;

        if (goal != start && occupiedCells.ContainsKey(goal)) return false;

        if (start == goal)
        {
            if (occupiedCells.ContainsKey(start)) return false;
            path.Add(start);
            return true;
        }

        List<Vector2Int> open = new List<Vector2Int> { start };
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, int> gScore = new Dictionary<Vector2Int, int> { [start] = 0 };
        Dictionary<Vector2Int, int> fScore = new Dictionary<Vector2Int, int> { [start] = Manhattan(start, goal) };

        while (open.Count > 0)
        {
            int currentIndex = 0;
            Vector2Int current = open[0];
            int currentF = fScore.TryGetValue(current, out int score) ? score : int.MaxValue;

            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                int candidateF = fScore.TryGetValue(candidate, out int f) ? f : int.MaxValue;
                if (candidateF < currentF)
                {
                    current = candidate;
                    currentF = candidateF;
                    currentIndex = i;
                }
            }

            if (current == goal)
            {
                ReconstructPath(cameFrom, current, path);
                return path.Count > 0;
            }

            open.RemoveAt(currentIndex);
            closed.Add(current);

            Vector2Int[] neighbors =
            {
                current + Vector2Int.right,
                current + Vector2Int.left,
                current + Vector2Int.up,
                current + Vector2Int.down
            };

            for (int i = 0; i < neighbors.Length; i++)
            {
                Vector2Int neighbor = neighbors[i];
                if (!IsInsideGrid(neighbor)) continue;
                if (closed.Contains(neighbor)) continue;

                bool isBlocked = occupiedCells.ContainsKey(neighbor) && neighbor != goal && neighbor != start;
                if (isBlocked) continue;

                int currentG = gScore.TryGetValue(current, out int cg) ? cg : int.MaxValue;
                int tentativeG = currentG + 1;
                int neighborG = gScore.TryGetValue(neighbor, out int ng) ? ng : int.MaxValue;

                if (tentativeG >= neighborG) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Manhattan(neighbor, goal);
                if (!open.Contains(neighbor)) open.Add(neighbor);
            }
        }

        return false;
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridSize && cell.y >= 0 && cell.y < gridSize;
    }

    private void RegisterCoreOccupiedCells(Vector2Int centerCell)
    {
        Vector2Int span = GetCoreCellSpan();
        RegisterOccupiedRect(centerCell, span, artificialSatellite != null ? artificialSatellite.gameObject : null);
    }

    private Vector2Int GetCoreCellSpan()
    {
        Part corePart = PartDB.Instance != null ? PartDB.Instance.GetPartByName("Core") : null;
        if (corePart != null)
        {
            return new Vector2Int(Mathf.Max(1, corePart.gridWidth), Mathf.Max(1, corePart.gridHeight));
        }

        if (artificialSatellite != null)
        {
            Vector3 scale = artificialSatellite.transform.localScale;
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.x) / cellSize));
            int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scale.y) / cellSize));
            return new Vector2Int(width, height);
        }

        return Vector2Int.one;
    }

    private void RegisterOccupiedRect(Vector2Int centerCell, Vector2Int span, GameObject owner)
    {
        int width = Mathf.Max(1, span.x);
        int height = Mathf.Max(1, span.y);

        int minX = -(width / 2);
        int maxX = width - (width / 2) - 1;
        int minY = -(height / 2);
        int maxY = height - (height / 2) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (!IsInsideGrid(cell)) continue;
                occupiedCells[cell] = owner;
            }
        }
    }

    private bool IsCurrentGhostPlacementAreaFree()
    {
        if (partGhost == null || part == null) return false;

        Vector2 snapOffset = GetCurrentPartSnapOffset();
        Vector2Int center = LocalPositionToGrid(partGhost.transform.localPosition, snapOffset);
        Vector2Int span = GetCurrentPartCellSpan();
        return IsAreaFree(center, span);
    }

    private bool IsAreaFree(Vector2Int centerCell, Vector2Int span)
    {
        int width = Mathf.Max(1, span.x);
        int height = Mathf.Max(1, span.y);

        int minX = -(width / 2);
        int maxX = width - (width / 2) - 1;
        int minY = -(height / 2);
        int maxY = height - (height / 2) - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (!IsInsideGrid(cell)) return false;
                if (occupiedCells.ContainsKey(cell)) return false;
            }
        }

        return true;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int QuantizeToCellIndex(float valueInCells)
    {
        // Avoid banker's rounding on .5 boundaries; always round away from zero.
        if (valueInCells >= 0f) return Mathf.FloorToInt(valueInCells + 0.5f);
        return Mathf.CeilToInt(valueInCells - 0.5f);
    }

    private static void ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int current,
        List<Vector2Int> path)
    {
        path.Clear();
        path.Add(current);

        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
    }

    private GameObject AddPipeCornerToSatellite(
        ArtificialSatellite targetSatellite,
        Part targetPart,
        Vector3 localPos,
        Quaternion localRot,
        Vector2Int previousDir,
        Vector2Int nextDir)
    {
        if (targetSatellite == null || targetPart == null) return null;

        GameObject cornerRoot = CreatePipeCornerObject(targetPart.partPrefab, false, previousDir, nextDir);
        Vector3 inputLocal = new Vector3(-previousDir.x, -previousDir.y, 0f) * (cellSize * 0.5f);
        Vector3 outputLocal = new Vector3(nextDir.x, nextDir.y, 0f) * (cellSize * 0.5f);
        return AddPartObjectToSatellite(
            targetSatellite,
            targetPart,
            cornerRoot,
            localPos,
            localRot,
            true,
            inputLocal,
            outputLocal);
    }

    private GameObject CreatePipeCornerObject(GameObject templatePrefab, bool isGhost, Vector2Int previousDir, Vector2Int nextDir)
    {
        if (templatePrefab == null) return new GameObject(isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime");

        GameObject root = new GameObject(isGhost ? "PipeCornerGhostRuntime" : "PipeCornerRuntime");
        GameObject armA = Instantiate(templatePrefab, root.transform);
        GameObject armB = Instantiate(templatePrefab, root.transform);

        // previousDir points from previous cell -> current cell,
        // so this arm must point back toward the previous cell.
        ConfigureCornerArm(armA.transform, -previousDir);
        ConfigureCornerArm(armB.transform, nextDir);

        if (isGhost)
        {
            EnsureRendererTransparencyRecursive(root.transform);
        }

        return root;
    }

    private void ConfigureCornerArm(Transform arm, Vector2Int dir)
    {
        if (arm == null) return;

        float length = cellSize * 0.5f;
        float thickness = cellSize * 0.5f;

        if (dir == Vector2Int.zero) dir = Vector2Int.right;
        Vector3 normalizedDir = new Vector3(dir.x, dir.y, 0f).normalized;

        arm.localRotation = (dir.x != 0) ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
        arm.localPosition = normalizedDir * (cellSize * 0.25f);
        arm.localScale = new Vector3(length, thickness, 1f);
    }

}
