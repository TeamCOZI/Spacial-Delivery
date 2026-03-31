using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OutputPortProductionState : MonoBehaviour
{
    private const float ConsumptionIntervalSeconds = 1f;
    private const float DefaultTravelSecondsPerSegment = 1f;
    private const float TargetTokenWorldSize = 0.075f;
    private const int TokenSortingOrder = 80;
    private const string TokenRootName = "__ProductionTokens";
    private const string TokenObjectName = "ProductionToken";
    private const string IdleStatus = "SELECT RESOURCE";
    private const string ReadyStatus = "READY";
    private const string RunningStatus = "RUNNING";
    private const string StoppedStatus = "STOPPED";
    private const string UnsupportedStatus = "UNSUPPORTED";
    private const string NoRouteStatus = "NO ROUTE";
    private const string NoMaterialStatus = "NO MATERIAL";
    private const string ReceiverFullStatus = "INPUT FULL";
    private const string NoInventoryStatus = "NO INVENTORY";
    private const string PipeBlockedStatus = "PIPE BLOCKED";
    private const float PacketPointOccupancyToleranceSqr = 0.000001f;
    private const float CellSize = 0.1f;
    private const int GridSize = 99;
    private sealed class TransitPacket
    {
        public InventoryResourceType resourceType;
        public List<Vector3> pathPointsLocal = new List<Vector3>();
        public GameObject visualObject;
        public Transform visualTransform;
        public SpriteRenderer spriteRenderer;
        public int segmentIndex;
        public float segmentProgress;
        public bool waitingForDelivery;
        public bool holdsAtPoint;
        public bool waitingForFilterRecall;
        public int firstPipePointIndex;
        public int lastPipePointIndex;
        public List<Vector2Int> pipeCellPath = new List<Vector2Int>();
        public Vector2Int sourceTerminalDirection;
        public StructureResourceInventory destinationInventory;
        public AssemblyPartFocus destinationPartFocus;
        public Transform destinationPortTransform;
        public Vector2Int destinationReceiverTerminalDirection;
        public float segmentTravelSeconds = DefaultTravelSecondsPerSegment;
    }

    [SerializeField] private bool hasAssignedResource;
    [SerializeField] private InventoryResourceType assignedResourceType;
    [SerializeField] private bool isRunning;
    [SerializeField] private string statusLabel = IdleStatus;
    [SerializeField] private bool hasLastTransferredResource;
    [SerializeField] private InventoryResourceType lastTransferredResourceType;
    [SerializeField] private int lastResolvedPipeCellCount;
    [SerializeField] private string lastResolvedReceiverLabel = string.Empty;

    private readonly List<TransitPacket> activePackets = new List<TransitPacket>();
    private readonly List<Vector3> pathBuffer = new List<Vector3>();

    private AssemblyOutputPortFocus outputPortFocus;
    private Transform visualRoot;
    private float spawnTimer;

    public bool HasAssignedResource => hasAssignedResource;
    public InventoryResourceType AssignedResourceType => assignedResourceType;
    public bool IsRunning => isRunning;
    public int InTransitCount => activePackets.Count;
    public string StatusLabel => FormatStatusLabel(string.IsNullOrWhiteSpace(statusLabel) ? IdleStatus : statusLabel);
    public int PipeCellCount => Mathf.Max(0, lastResolvedPipeCellCount);
    public bool HasProducedResource => hasLastTransferredResource;
    public InventoryResourceType ProducedResourceType => hasLastTransferredResource ? lastTransferredResourceType : assignedResourceType;
    public AssemblyOutputPortFocus OutputPortFocus
    {
        get
        {
            BindReferences();
            return outputPortFocus;
        }
    }

    private void Awake()
    {
        BindReferences();
    }

    private void OnEnable()
    {
        BindReferences();
    }

    private void OnDisable()
    {
        isRunning = false;
        spawnTimer = 0f;
    }

    private void OnDestroy()
    {
        ClearAllPackets();
        if (visualRoot != null)
        {
            Destroy(visualRoot.gameObject);
            visualRoot = null;
        }
    }

    private void Update()
    {
        BindReferences();

        float deltaTime = Time.deltaTime;
        if (deltaTime < 0f)
        {
            deltaTime = 0f;
        }

        UpdateTransitPackets(deltaTime);
        if (isRunning)
        {
            UpdateTransfer(deltaTime);
        }
        else
        {
            RefreshIdleStatus();
        }
    }

    public void SetAssignedResource(InventoryResourceType resourceType)
    {
        if (hasAssignedResource && assignedResourceType == resourceType)
        {
            ClearAssignedResource();
            return;
        }

        hasAssignedResource = true;
        assignedResourceType = resourceType;
        if (!isRunning)
        {
            RefreshIdleStatus();
        }
    }

    public bool TryGetAssignedResource(out InventoryResourceType resourceType)
    {
        resourceType = assignedResourceType;
        return hasAssignedResource;
    }

    public bool CanStartProduction(out string disabledReason)
    {
        BindReferences();

        if (CanSpawnNextPacket(
            out string failureReason,
            out StructureResourceInventory senderOutputInventory,
            out StructureResourceInventory receiverInputInventory,
            out AssemblyPartFocus receiverPartFocus,
            out InventoryResourceType transferResource,
            out _,
            out int pipeCellCount,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _) || ShouldKeepRunningOnFlowBlock(failureReason))
        {
            disabledReason = string.Empty;
            return true;
        }

        disabledReason = BuildDetailedStatusDescription(
            failureReason,
            senderOutputInventory,
            receiverInputInventory,
            receiverPartFocus,
            transferResource,
            pipeCellCount);
        return false;
    }

    public string GetDetailedStatusDescription()
    {
        BindReferences();

        if (!isRunning && activePackets.Count > 0)
        {
            return activePackets.Count == 1
                ? "Stopped. 1 packet is still travelling through the pipe."
                : $"Stopped. {activePackets.Count} packets are still travelling through the pipe.";
        }

        if (CanSpawnNextPacket(
            out string failureReason,
            out StructureResourceInventory senderOutputInventory,
            out StructureResourceInventory receiverInputInventory,
            out AssemblyPartFocus receiverPartFocus,
            out InventoryResourceType transferResource,
            out _,
            out int pipeCellCount,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _))
        {
            return isRunning
                ? BuildRunningDescription(receiverPartFocus, transferResource, pipeCellCount)
                : BuildDetailedStatusDescription(
                    ReadyStatus,
                    senderOutputInventory,
                    receiverInputInventory,
                    receiverPartFocus,
                    transferResource,
                    pipeCellCount);
        }

        return BuildDetailedStatusDescription(
            failureReason,
            senderOutputInventory,
            receiverInputInventory,
            receiverPartFocus,
            transferResource,
            pipeCellCount);
    }

    public void ToggleRunning()
    {
        if (isRunning)
        {
            StopTransfer(StoppedStatus);
            return;
        }

        RequestStart();
    }


    public bool TryRecallPacketsToCoreLogistics(out int recalledCount)
    {
        recalledCount = 0;
        BindReferences();
        isRunning = false;
        spawnTimer = 0f;

        if (activePackets.Count <= 0)
        {
            RefreshIdleStatus();
            return false;
        }

        List<StructureResourceInventory> logisticsInventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(outputPortFocus != null ? outputPortFocus.OwnerSatellite : null, logisticsInventories);
        if (logisticsInventories.Count <= 0)
        {
            RefreshIdleStatus();
            return false;
        }

        for (int i = activePackets.Count - 1; i >= 0; i--)
        {
            TransitPacket packet = activePackets[i];
            if (packet == null)
            {
                activePackets.RemoveAt(i);
                continue;
            }

            if (!TryStorePacketInCoreLogistics(packet.resourceType, logisticsInventories))
            {
                continue;
            }

            DestroyPacket(packet);
            activePackets.RemoveAt(i);
            recalledCount++;
        }

        RefreshIdleStatus();
        return recalledCount > 0;
    }
    public bool RequestStart()
    {
        if (!CanSpawnNextPacket(
            out string failureReason,
            out _,
            out _,
            out AssemblyPartFocus receiverPartFocus,
            out _,
            out _,
            out int pipeCellCount,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _))
        {
            if (ShouldKeepRunningOnFlowBlock(failureReason))
            {
                isRunning = true;
                statusLabel = failureReason;
                return true;
            }

            StopTransfer(failureReason);
            return false;
        }

        isRunning = true;
        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);
        statusLabel = pipeCellCount > 0 ? $"{RunningStatus} / {pipeCellCount} CELLS" : RunningStatus;
        return true;
    }

    private void UpdateTransfer(float deltaTime)
    {
        spawnTimer = Mathf.Min(ConsumptionIntervalSeconds, spawnTimer + deltaTime);

        if (!CanSpawnNextPacket(
            out string failureReason,
            out _,
            out _,
            out AssemblyPartFocus receiverPartFocus,
            out _,
            out _,
            out int pipeCellCount,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _))
        {
            if (ShouldKeepRunningOnFlowBlock(failureReason))
            {
                statusLabel = failureReason;
                return;
            }

            StopTransfer(failureReason);
            return;
        }

        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);
        lastResolvedPipeCellCount = pipeCellCount;

        while (spawnTimer >= ConsumptionIntervalSeconds)
        {
            if (!TrySpawnPacket())
            {
                if (!isRunning)
                {
                    spawnTimer = 0f;
                }
                else
                {
                    spawnTimer = ConsumptionIntervalSeconds;
                }

                break;
            }

            spawnTimer -= ConsumptionIntervalSeconds;
        }
    }

    private bool TrySpawnPacket()
    {
        if (!CanSpawnNextPacket(
            out string failureReason,
            out StructureResourceInventory senderOutputInventory,
            out StructureResourceInventory receiverInputInventory,
            out AssemblyPartFocus receiverPartFocus,
            out InventoryResourceType transferResource,
            out List<Vector3> pathPoints,
            out int pipeCellCount,
            out OutputPortTransferUtility.TransferRouteSelection routeSelection,
            out int firstPipePointIndex,
            out int lastPipePointIndex,
            out Transform receiverPortTransform,
            out Vector2Int receiverTerminalDirection,
            out List<Vector2Int> pipeCellPath))
        {
            if (ShouldKeepRunningOnFlowBlock(failureReason))
            {
                statusLabel = failureReason;
                return false;
            }

            StopTransfer(failureReason);
            return false;
        }

        if (!OutputPortTransferUtility.TryConsumeOutputResource(outputPortFocus, transferResource, 1))
        {
            StopTransfer(NoMaterialStatus);
            return false;
        }

        routeSelection.Commit();

        TransitPacket packet = new TransitPacket
        {
            resourceType = transferResource,
            pathPointsLocal = new List<Vector3>(pathPoints),
            segmentIndex = Mathf.Max(0, firstPipePointIndex - 1),
            segmentProgress = 0f,
            waitingForDelivery = false,
            holdsAtPoint = false,
            waitingForFilterRecall = false,
            firstPipePointIndex = firstPipePointIndex,
            lastPipePointIndex = lastPipePointIndex,
            pipeCellPath = pipeCellPath != null ? new List<Vector2Int>(pipeCellPath) : new List<Vector2Int>(),
            sourceTerminalDirection = ResolveSenderTerminalDirection(),
            destinationInventory = receiverInputInventory,
            destinationPartFocus = receiverPartFocus,
            destinationPortTransform = receiverPortTransform,
            destinationReceiverTerminalDirection = receiverTerminalDirection,
            segmentTravelSeconds = ResolvePipeTransmitTimeSeconds()
        };

        CreatePacketVisual(packet);
        UpdatePacketVisual(packet);
        activePackets.Add(packet);

        lastResolvedPipeCellCount = pipeCellCount;
        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);
        hasLastTransferredResource = true;
        lastTransferredResourceType = transferResource;
        statusLabel = pipeCellCount > 0 ? $"{RunningStatus} / {pipeCellCount} CELLS" : RunningStatus;
        return true;
    }

    private bool CanSpawnNextPacket(
        out string failureReason,
        out StructureResourceInventory senderOutputInventory,
        out StructureResourceInventory receiverInputInventory,
        out AssemblyPartFocus receiverPartFocus,
        out InventoryResourceType transferResource,
        out List<Vector3> pathPoints,
        out int pipeCellCount,
        out OutputPortTransferUtility.TransferRouteSelection routeSelection,
            out int firstPipePointIndex,
            out int lastPipePointIndex,
            out Transform receiverPortTransform,
            out Vector2Int receiverTerminalDirection,
            out List<Vector2Int> pipeCellPath)
    {
        failureReason = ReadyStatus;
        senderOutputInventory = null;
        receiverInputInventory = null;
        receiverPartFocus = null;
        transferResource = assignedResourceType;
        pathPoints = pathBuffer;
        pipeCellCount = 0;
        routeSelection = default;
        firstPipePointIndex = -1;
        lastPipePointIndex = -1;
        receiverPortTransform = null;
        receiverTerminalDirection = Vector2Int.zero;
        pipeCellPath = null;
        pathBuffer.Clear();

        if (!OutputPortTransferUtility.SupportsTransfer(outputPortFocus))
        {
            failureReason = UnsupportedStatus;
            return false;
        }

        if (!hasAssignedResource)
        {
            failureReason = IdleStatus;
            return false;
        }

        if (!OutputPortTransferUtility.TryResolveSenderOutputInventory(outputPortFocus, out senderOutputInventory) || senderOutputInventory == null)
        {
            failureReason = NoInventoryStatus;
            return false;
        }

        int senderAmount = ResolveSenderStockAmount(transferResource, senderOutputInventory);
        if (senderAmount <= 0)
        {
            failureReason = NoMaterialStatus;
            return false;
        }

        if (!OutputPortTransferUtility.TryResolveTransferRouteDetailed(
            outputPortFocus,
            pathBuffer,
            out pipeCellCount,
            out receiverInputInventory,
            out receiverPartFocus,
            out routeSelection,
            out firstPipePointIndex,
            out lastPipePointIndex,
            out receiverPortTransform,
            out receiverTerminalDirection,
            out pipeCellPath) ||
            receiverInputInventory == null ||
            firstPipePointIndex < 0 ||
            lastPipePointIndex < firstPipePointIndex)
        {
            failureReason = NoRouteStatus;
            return false;
        }

        lastResolvedPipeCellCount = pipeCellCount;
        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);

        if (pipeCellCount <= 0 && receiverInputInventory.FreeCapacity <= 0)
        {
            failureReason = ReceiverFullStatus;
            return false;
        }

        if (IsRouteEntryOccupied(pathBuffer, firstPipePointIndex))
        {
            failureReason = PipeBlockedStatus;
            return false;
        }

        return true;
    }

    private int CountReservedPackets(StructureResourceInventory destinationInventory)
    {
        if (destinationInventory == null)
        {
            return 0;
        }

        int reservedAmount = 0;
        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket packet = state.activePackets[i];
                if (packet != null && packet.destinationInventory == destinationInventory)
                {
                    reservedAmount++;
                }
            }
        }

        return reservedAmount;
    }

    private bool IsRouteEntryOccupied(List<Vector3> pathPoints, int firstPipePointIndex)
    {
        if (pathPoints == null || pathPoints.Count <= 0 || firstPipePointIndex < 0 || firstPipePointIndex >= pathPoints.Count)
        {
            return false;
        }

        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        if (firstPipePointIndex > 0)
        {
            Vector3 entryStartPoint = pathPoints[firstPipePointIndex - 1];
            Vector3 entryEndPoint = pathPoints[firstPipePointIndex];
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                OutputPortProductionState state = states[stateIndex];
                if (state == null)
                {
                    continue;
                }

                for (int i = 0; i < state.activePackets.Count; i++)
                {
                    TransitPacket packet = state.activePackets[i];
                    if (packet == null)
                    {
                        continue;
                    }

                    if (TryGetOccupiedSegment(packet, out Vector3 occupiedSegmentStart, out Vector3 occupiedSegmentEnd) &&
                        ArePointsEquivalent(occupiedSegmentStart, entryStartPoint) &&
                        ArePointsEquivalent(occupiedSegmentEnd, entryEndPoint))
                    {
                        return true;
                    }
                }
            }
        }

        Vector3 firstPipePoint = pathPoints[firstPipePointIndex];
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket packet = state.activePackets[i];
                if (packet == null || !packet.holdsAtPoint)
                {
                    continue;
                }

                if (!TryGetHeldPoint(packet, out Vector3 occupiedPoint))
                {
                    continue;
                }

                if (ArePointsEquivalent(occupiedPoint, firstPipePoint))
                {
                    return true;
                }
            }
        }

        return false;
    }
    private void UpdateTransitPackets(float deltaTime)
    {
        for (int i = 0; i < activePackets.Count;)
        {
            TransitPacket packet = activePackets[i];
            if (packet == null)
            {
                activePackets.RemoveAt(i);
                continue;
            }

            if (UpdateTransitPacket(packet, deltaTime))
            {
                DestroyPacket(packet);
                activePackets.RemoveAt(i);
                continue;
            }

            UpdatePacketVisual(packet);
            i++;
        }
    }

    private bool UpdateTransitPacket(TransitPacket packet, float deltaTime)
    {
        if (packet == null)
        {
            return true;
        }

        if (packet.waitingForFilterRecall)
        {
            if (TryRecallPacketToCoreLogistics(packet))
            {
                packet.waitingForFilterRecall = false;
                return true;
            }

            packet.segmentProgress = 0f;
            packet.holdsAtPoint = true;
            statusLabel = NoInventoryStatus;
            return false;
        }

        if (packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            packet.segmentIndex = 0;
            packet.segmentProgress = 0f;
            packet.waitingForDelivery = false;
            packet.holdsAtPoint = true;
            return TryDeliverPacket(packet);
        }

        packet.segmentIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        packet.segmentProgress = Mathf.Clamp01(packet.segmentProgress);
        packet.waitingForDelivery = false;

        float remainingTime = Mathf.Max(0f, deltaTime);
        int safetyCounter = Mathf.Max(1, packet.pathPointsLocal.Count + activePackets.Count + 1);

        while (remainingTime > 0.0001f && safetyCounter-- > 0)
        {
            bool isFinalDeliverySegment = IsFinalDeliverySegment(packet);
            float segmentTravelSeconds = ResolveSegmentTravelSeconds(packet, isFinalDeliverySegment);
            if (!packet.holdsAtPoint && packet.segmentProgress > 0.0001f)
            {
                if (isFinalDeliverySegment)
                {
                    if (!CanPacketAdvanceToReceiver(packet))
                    {
                        packet.segmentProgress = 0f;
                        packet.holdsAtPoint = true;
                        return false;
                    }
                }
                else if (!CanPacketAdvanceToNextPoint(packet))
                {
                    packet.segmentProgress = 0f;
                    packet.holdsAtPoint = true;
                    return false;
                }
            }

            if (packet.holdsAtPoint)
            {
                if (isFinalDeliverySegment)
                {
                    if (!CanPacketAdvanceToReceiver(packet))
                    {
                        packet.segmentProgress = 0f;
                        return false;
                    }
                }
                else if (!CanPacketAdvanceToNextPoint(packet))
                {
                    packet.segmentProgress = 0f;
                    return false;
                }

                packet.holdsAtPoint = false;
            }

            if (packet.segmentProgress <= 0.0001f)
            {
                if (isFinalDeliverySegment)
                {
                    if (!CanPacketAdvanceToReceiver(packet))
                    {
                        packet.segmentProgress = 0f;
                        packet.holdsAtPoint = true;
                        return false;
                    }
                }
                else if (!CanPacketAdvanceToNextPoint(packet))
                {
                    packet.segmentProgress = 0f;
                    packet.holdsAtPoint = true;
                    return false;
                }
            }

            float remainingSegmentTime = Mathf.Max(0f, (1f - packet.segmentProgress) * segmentTravelSeconds);
            if (remainingTime + 0.0001f < remainingSegmentTime)
            {
                packet.segmentProgress += remainingTime / Mathf.Max(0.01f, segmentTravelSeconds);
                packet.segmentProgress = Mathf.Clamp01(packet.segmentProgress);
                return false;
            }

            remainingTime = Mathf.Max(0f, remainingTime - remainingSegmentTime);
            packet.segmentProgress = 1f;

            if (isFinalDeliverySegment)
            {
                if (TryDeliverPacket(packet))
                {
                    return true;
                }

                packet.segmentProgress = 0f;
                packet.holdsAtPoint = true;
                return false;
            }

            CommitMergeEntrySelectionIfNeeded(packet);
            CommitCrossEntrySelectionIfNeeded(packet);
            packet.segmentIndex = Mathf.Min(packet.segmentIndex + 1, GetLastPipePointIndex(packet));
            packet.segmentProgress = 0f;
            if (TryHandleFilterPipeArrival(packet, out bool recalledByFilter))
            {
                return recalledByFilter;
            }

            RefreshPacketOccupancyAfterArrival(packet);
        }

        return false;
    }

    private float ResolveSegmentTravelSeconds(TransitPacket packet, bool isFinalDeliverySegment)
    {
        float baseSeconds = Mathf.Max(0.01f, packet != null ? packet.segmentTravelSeconds : DefaultTravelSecondsPerSegment);
        return IsMergePipeTransitSegment(packet, isFinalDeliverySegment) || IsCrossPipeTransitSegment(packet, isFinalDeliverySegment)
            ? Mathf.Max(0.01f, baseSeconds * 0.5f)
            : baseSeconds;
    }

    private bool IsMergePipeTransitSegment(TransitPacket packet, bool isFinalDeliverySegment)
    {
        return IsSpecialPipeTransitSegment(packet, isFinalDeliverySegment, (ownerSatellite, point) =>
            MergePipeUtility.TryResolveStateForCell(ownerSatellite, LocalPositionToGrid(point), out _));
    }

    private bool IsCrossPipeTransitSegment(TransitPacket packet, bool isFinalDeliverySegment)
    {
        return IsSpecialPipeTransitSegment(packet, isFinalDeliverySegment, (ownerSatellite, point) =>
            CrossPipeUtility.TryResolveStateForCell(ownerSatellite, LocalPositionToGrid(point), out _));
    }

    private bool IsSpecialPipeTransitSegment(
        TransitPacket packet,
        bool isFinalDeliverySegment,
        System.Func<ArtificialSatellite, Vector3, bool> containsSpecialPipe)
    {
        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        if (packet == null || ownerSatellite == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2 || containsSpecialPipe == null)
        {
            return false;
        }

        int firstPipePointIndex = GetFirstPipePointIndex(packet);
        int lastPipePointIndex = GetLastPipePointIndex(packet);
        if (isFinalDeliverySegment)
        {
            return IsSpecialPipePathPoint(ownerSatellite, packet, lastPipePointIndex, containsSpecialPipe);
        }

        int currentPointIndex = Mathf.Clamp(packet.segmentIndex, 0, packet.pathPointsLocal.Count - 2);
        int nextPointIndex = currentPointIndex + 1;
        return (currentPointIndex >= firstPipePointIndex && currentPointIndex <= lastPipePointIndex && IsSpecialPipePathPoint(ownerSatellite, packet, currentPointIndex, containsSpecialPipe))
            || (nextPointIndex >= firstPipePointIndex && nextPointIndex <= lastPipePointIndex && IsSpecialPipePathPoint(ownerSatellite, packet, nextPointIndex, containsSpecialPipe));
    }

    private static bool IsSpecialPipePathPoint(
        ArtificialSatellite ownerSatellite,
        TransitPacket packet,
        int pointIndex,
        System.Func<ArtificialSatellite, Vector3, bool> containsSpecialPipe)
    {
        if (ownerSatellite == null || packet == null || containsSpecialPipe == null)
        {
            return false;
        }

        if (!TryGetPathPoint(packet, pointIndex, out Vector3 point))
        {
            return false;
        }

        return containsSpecialPipe(ownerSatellite, point);
    }

    private void RefreshPacketOccupancyAfterArrival(TransitPacket packet)
    {
        if (packet == null)
        {
            return;
        }

        if (IsFinalDeliverySegment(packet))
        {
            packet.holdsAtPoint = !CanPacketAdvanceToReceiver(packet);
            return;
        }

        packet.holdsAtPoint = !CanPacketAdvanceToNextPoint(packet);
    }

    private bool CanPacketAdvanceToNextPoint(TransitPacket packet)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        int currentSegmentIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = Mathf.Clamp(currentSegmentIndex + 1, 0, GetLastPipePointIndex(packet));
        return IsPacketSegmentPhysicallyLinked(packet, currentSegmentIndex)
            && !IsPathSegmentOccupiedByOtherPacket(packet, currentSegmentIndex)
            && !IsHoldingPointOccupiedByOtherPacket(packet, nextPointIndex)
            && IsMergeEntryAllowed(packet)
            && IsCrossEntryAllowed(packet);
    }

    private bool IsPacketSegmentPhysicallyLinked(TransitPacket packet, int segmentIndex)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        int currentPointIndex = Mathf.Clamp(segmentIndex, 0, packet.pathPointsLocal.Count - 2);
        int nextPointIndex = currentPointIndex + 1;
        if (currentPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return true;
        }

        if (!TryGetPathPoint(packet, currentPointIndex, out Vector3 currentPoint) || !TryGetPathPoint(packet, nextPointIndex, out Vector3 nextPoint))
        {
            return false;
        }

        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        if (ownerSatellite == null)
        {
            return false;
        }

        Vector2Int currentCell = LocalPositionToGrid(currentPoint);
        Vector2Int nextCell = LocalPositionToGrid(nextPoint);
        return PipeConnectivityUtility.CanTraversePhysicalDirection(ownerSatellite, currentCell, nextCell);
    }
    private bool IsPacketReceiverLinkStillAttached(TransitPacket packet)
    {
        if (packet == null || packet.destinationPartFocus == null || packet.destinationPortTransform == null || packet.destinationReceiverTerminalDirection == Vector2Int.zero)
        {
            return false;
        }

        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        if (ownerSatellite == null)
        {
            return false;
        }

        if (!TryGetPathPoint(packet, GetLastPipePointIndex(packet), out Vector3 lastPipePoint))
        {
            return false;
        }

        Vector2Int lastPipeCell = LocalPositionToGrid(lastPipePoint);
        return OutputPortTransferUtility.IsReceiverInputAttachedToPipeCell(
            ownerSatellite,
            packet.destinationPartFocus,
            packet.destinationPortTransform,
            lastPipeCell,
            packet.destinationReceiverTerminalDirection);
    }

    private bool CanPacketAdvanceToReceiver(TransitPacket packet)
    {
        if (packet == null || packet.destinationInventory == null || packet.destinationInventory.FreeCapacity <= 0)
        {
            return false;
        }

        if (!IsPacketReceiverLinkStillAttached(packet))
        {
            return false;
        }

        return !IsPathSegmentOccupiedByOtherPacket(packet, GetLastPipePointIndex(packet));
    }

    private bool IsPathSegmentOccupiedByOtherPacket(TransitPacket packet, int segmentIndex)
    {
        if (!TryGetPathSegment(packet, segmentIndex, out Vector3 targetStartPoint, out Vector3 targetEndPoint))
        {
            return false;
        }

        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket otherPacket = state.activePackets[i];
                if (otherPacket == null || otherPacket == packet)
                {
                    continue;
                }

                if (!TryGetOccupiedSegment(otherPacket, out Vector3 occupiedStartPoint, out Vector3 occupiedEndPoint))
                {
                    continue;
                }

                if (ArePointsEquivalent(occupiedStartPoint, targetStartPoint) &&
                    ArePointsEquivalent(occupiedEndPoint, targetEndPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsHoldingPointOccupiedByOtherPacket(TransitPacket packet, int pointIndex)
    {
        if (!TryGetPathPoint(packet, pointIndex, out Vector3 targetPoint))
        {
            return false;
        }

        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket otherPacket = state.activePackets[i];
                if (otherPacket == null || otherPacket == packet || !otherPacket.holdsAtPoint)
                {
                    continue;
                }

                if (!TryGetHeldPoint(otherPacket, out Vector3 occupiedPoint))
                {
                    continue;
                }

                if (ArePointsEquivalent(occupiedPoint, targetPoint))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsMergeEntryAllowed(TransitPacket packet)
    {
        if (!TryResolvePendingMergeEntry(packet, out MergePipeState mergePipeState, out Vector2Int mergeCell, out Vector2Int inputSideDirection))
        {
            return true;
        }

        if (!TryGetPendingMergePoint(packet, out Vector3 mergePoint) || IsMergeCellOccupiedByOtherPacket(packet, mergePoint))
        {
            return false;
        }

        List<Vector2Int> availableDirections = new List<Vector2Int>(3);
        CollectReadyMergeInputDirections(mergeCell, availableDirections);
        AddUniqueDirection(availableDirections, inputSideDirection);
        return mergePipeState.TrySelectPreviewInputDirection(availableDirections, out Vector2Int selectedDirection)
            && selectedDirection == inputSideDirection;
    }

    private void CommitMergeEntrySelectionIfNeeded(TransitPacket packet)
    {
        if (!TryResolveCommittedMergeEntry(packet, out MergePipeState mergePipeState, out _, out Vector2Int inputSideDirection))
        {
            return;
        }

        mergePipeState.CommitSelectedInputDirection(inputSideDirection);
    }

    private bool TryResolvePendingMergeEntry(TransitPacket packet, out MergePipeState mergePipeState, out Vector2Int mergeCell, out Vector2Int inputSideDirection)
    {
        mergePipeState = null;
        mergeCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (!TryGetPendingMergeEntry(packet, out mergeCell, out inputSideDirection))
        {
            return false;
        }

        return MergePipeUtility.TryResolveStateForCell(ResolveOwnerSatellite(), mergeCell, out mergePipeState);
    }
    private bool TryResolveCommittedMergeEntry(TransitPacket packet, out MergePipeState mergePipeState, out Vector2Int mergeCell, out Vector2Int inputSideDirection)
    {
        mergePipeState = null;
        mergeCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (!TryResolveCurrentMergePipeCell(packet, out int mergePipeCellIndex, out mergeCell) ||
            !TryResolveMergeInputSideDirection(packet, mergePipeCellIndex, out inputSideDirection))
        {
            return false;
        }

        return MergePipeUtility.TryResolveStateForCell(ResolveOwnerSatellite(), mergeCell, out mergePipeState);
    }

    private static bool TryResolveCurrentMergePipeCell(TransitPacket packet, out int mergePipeCellIndex, out Vector2Int mergeCell)
    {
        mergePipeCellIndex = -1;
        mergeCell = Vector2Int.zero;
        if (packet == null || packet.pipeCellPath == null || packet.pipeCellPath.Count <= 0)
        {
            return false;
        }

        int currentSegmentIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = currentSegmentIndex + 1;
        if (nextPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        mergePipeCellIndex = nextPointIndex - GetFirstPipePointIndex(packet);
        if (mergePipeCellIndex < 0 || mergePipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        mergeCell = packet.pipeCellPath[mergePipeCellIndex];
        return true;
    }

    private void CollectReadyMergeInputDirections(Vector2Int mergeCell, List<Vector2Int> availableDirections)
    {
        if (availableDirections == null)
        {
            return;
        }

        availableDirections.Clear();
        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket packet = state.activePackets[i];
                if (!TryGetPendingMergeEntry(packet, out Vector2Int otherMergeCell, out Vector2Int otherInputSideDirection) ||
                    otherMergeCell != mergeCell)
                {
                    continue;
                }

                AddUniqueDirection(availableDirections, otherInputSideDirection);
            }
        }
    }
    private bool TryGetPendingMergePoint(TransitPacket packet, out Vector3 mergePoint)
    {
        mergePoint = Vector3.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        if (!packet.holdsAtPoint && packet.segmentProgress > 0.0001f)
        {
            return false;
        }

        int currentPointIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = currentPointIndex + 1;
        if (nextPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        return TryGetPathPoint(packet, nextPointIndex, out mergePoint);
    }

    private bool IsMergeCellOccupiedByOtherPacket(TransitPacket packet, Vector3 mergePoint)
    {
        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket otherPacket = state.activePackets[i];
                if (otherPacket == null || otherPacket == packet)
                {
                    continue;
                }

                if (TryGetHeldPoint(otherPacket, out Vector3 heldPoint) && ArePointsEquivalent(heldPoint, mergePoint))
                {
                    return true;
                }

                if (TryGetOccupiedSegment(otherPacket, out Vector3 occupiedStartPoint, out Vector3 occupiedEndPoint) &&
                    (ArePointsEquivalent(occupiedStartPoint, mergePoint) || ArePointsEquivalent(occupiedEndPoint, mergePoint)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetPendingMergeEntry(TransitPacket packet, out Vector2Int mergeCell, out Vector2Int inputSideDirection)
    {
        mergeCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        if (!packet.holdsAtPoint && packet.segmentProgress > 0.0001f)
        {
            return false;
        }

        if (!TryResolveNextPipeCellIndex(packet, out int nextPipeCellIndex) ||
            packet.pipeCellPath == null ||
            nextPipeCellIndex < 0 ||
            nextPipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        mergeCell = packet.pipeCellPath[nextPipeCellIndex];
        return TryResolveMergeInputSideDirection(packet, nextPipeCellIndex, out inputSideDirection);
    }

    private static bool TryFindMergePipeCellIndex(TransitPacket packet, Vector2Int mergeCell, out int mergePipeCellIndex)
    {
        mergePipeCellIndex = -1;
        if (packet == null || packet.pipeCellPath == null)
        {
            return false;
        }

        for (int i = 0; i < packet.pipeCellPath.Count; i++)
        {
            if (packet.pipeCellPath[i] != mergeCell)
            {
                continue;
            }

            mergePipeCellIndex = i;
            return true;
        }

        return false;
    }

    private static bool TryResolveNextPipeCellIndex(TransitPacket packet, out int nextPipeCellIndex)
    {
        nextPipeCellIndex = -1;
        if (packet == null || packet.pipeCellPath == null || packet.pipeCellPath.Count <= 0)
        {
            return false;
        }

        int currentPointIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = currentPointIndex + 1;
        if (nextPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        nextPipeCellIndex = nextPointIndex - GetFirstPipePointIndex(packet);
        return nextPipeCellIndex >= 0 && nextPipeCellIndex < packet.pipeCellPath.Count;
    }

    private static int GetSegmentIndexBeforePipeCell(TransitPacket packet, int pipeCellIndex)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return 0;
        }

        int segmentIndex = GetFirstPipePointIndex(packet) + pipeCellIndex - 1;
        return Mathf.Clamp(segmentIndex, 0, packet.pathPointsLocal.Count - 2);
    }

    private static bool TryResolveMergeInputSideDirection(TransitPacket packet, int mergePipeCellIndex, out Vector2Int inputSideDirection)
    {
        inputSideDirection = Vector2Int.zero;
        if (packet == null || packet.pipeCellPath == null || mergePipeCellIndex < 0 || mergePipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        Vector2Int mergeCell = packet.pipeCellPath[mergePipeCellIndex];
        Vector2Int candidateDirection = mergePipeCellIndex > 0
            ? packet.pipeCellPath[mergePipeCellIndex - 1] - mergeCell
            : packet.sourceTerminalDirection;
        if (!IsCardinalCellDirection(candidateDirection))
        {
            return false;
        }

        inputSideDirection = candidateDirection;
        return true;
    }

    private static bool IsCardinalCellDirection(Vector2Int direction)
    {
        return Mathf.Abs(direction.x) + Mathf.Abs(direction.y) == 1;
    }

    private static void AddUniqueDirection(List<Vector2Int> directions, Vector2Int direction)
    {
        if (directions == null || direction == Vector2Int.zero || directions.Contains(direction))
        {
            return;
        }

        directions.Add(direction);
    }


    private bool IsCrossEntryAllowed(TransitPacket packet)
    {
        if (!TryResolvePendingCrossEntry(packet, out CrossPipeState crossPipeState, out Vector2Int crossCell, out Vector2Int inputSideDirection))
        {
            return true;
        }

        if (!TryGetPendingCrossPoint(packet, out Vector3 crossPoint) || IsCrossCellOccupiedByOtherPacket(packet, crossPoint))
        {
            return false;
        }

        List<Vector2Int> availableDirections = new List<Vector2Int>(2);
        CollectReadyCrossInputDirections(crossCell, availableDirections);
        AddUniqueDirection(availableDirections, inputSideDirection);
        return crossPipeState.TrySelectPreviewInputDirection(availableDirections, out Vector2Int selectedDirection)
            && selectedDirection == inputSideDirection;
    }

    private void CommitCrossEntrySelectionIfNeeded(TransitPacket packet)
    {
        if (!TryResolveCommittedCrossEntry(packet, out CrossPipeState crossPipeState, out _, out Vector2Int inputSideDirection))
        {
            return;
        }

        crossPipeState.CommitSelectedInputDirection(inputSideDirection);
    }

    private bool TryResolvePendingCrossEntry(TransitPacket packet, out CrossPipeState crossPipeState, out Vector2Int crossCell, out Vector2Int inputSideDirection)
    {
        crossPipeState = null;
        crossCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (!TryGetPendingCrossEntry(packet, out crossCell, out inputSideDirection))
        {
            return false;
        }

        return CrossPipeUtility.TryResolveStateForCell(ResolveOwnerSatellite(), crossCell, out crossPipeState);
    }

    private bool TryResolveCommittedCrossEntry(TransitPacket packet, out CrossPipeState crossPipeState, out Vector2Int crossCell, out Vector2Int inputSideDirection)
    {
        crossPipeState = null;
        crossCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (!TryResolveCurrentCrossPipeCell(packet, out int crossPipeCellIndex, out crossCell) ||
            !TryResolveCrossInputSideDirection(packet, crossPipeCellIndex, out inputSideDirection))
        {
            return false;
        }

        return CrossPipeUtility.TryResolveStateForCell(ResolveOwnerSatellite(), crossCell, out crossPipeState);
    }

    private static bool TryResolveCurrentCrossPipeCell(TransitPacket packet, out int crossPipeCellIndex, out Vector2Int crossCell)
    {
        crossPipeCellIndex = -1;
        crossCell = Vector2Int.zero;
        if (packet == null || packet.pipeCellPath == null || packet.pipeCellPath.Count <= 0)
        {
            return false;
        }

        int currentSegmentIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = currentSegmentIndex + 1;
        if (nextPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        crossPipeCellIndex = nextPointIndex - GetFirstPipePointIndex(packet);
        if (crossPipeCellIndex < 0 || crossPipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        crossCell = packet.pipeCellPath[crossPipeCellIndex];
        return true;
    }

    private void CollectReadyCrossInputDirections(Vector2Int crossCell, List<Vector2Int> availableDirections)
    {
        if (availableDirections == null)
        {
            return;
        }

        availableDirections.Clear();
        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket packet = state.activePackets[i];
                if (!TryGetPendingCrossEntry(packet, out Vector2Int otherCrossCell, out Vector2Int otherInputSideDirection) ||
                    otherCrossCell != crossCell)
                {
                    continue;
                }

                AddUniqueDirection(availableDirections, otherInputSideDirection);
            }
        }
    }

    private bool TryGetPendingCrossPoint(TransitPacket packet, out Vector3 crossPoint)
    {
        crossPoint = Vector3.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        if (!packet.holdsAtPoint && packet.segmentProgress > 0.0001f)
        {
            return false;
        }

        int currentPointIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextPointIndex = currentPointIndex + 1;
        if (nextPointIndex < GetFirstPipePointIndex(packet) || nextPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        return TryGetPathPoint(packet, nextPointIndex, out crossPoint);
    }

    private bool IsCrossCellOccupiedByOtherPacket(TransitPacket packet, Vector3 crossPoint)
    {
        OutputPortProductionState[] states = ResolveProductionStatesInOwnerSatellite();
        for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
        {
            OutputPortProductionState state = states[stateIndex];
            if (state == null)
            {
                continue;
            }

            for (int i = 0; i < state.activePackets.Count; i++)
            {
                TransitPacket otherPacket = state.activePackets[i];
                if (otherPacket == null || otherPacket == packet)
                {
                    continue;
                }

                if (TryGetHeldPoint(otherPacket, out Vector3 heldPoint) && ArePointsEquivalent(heldPoint, crossPoint))
                {
                    return true;
                }

                if (TryGetOccupiedSegment(otherPacket, out Vector3 occupiedStartPoint, out Vector3 occupiedEndPoint) &&
                    (ArePointsEquivalent(occupiedStartPoint, crossPoint) || ArePointsEquivalent(occupiedEndPoint, crossPoint)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetPendingCrossEntry(TransitPacket packet, out Vector2Int crossCell, out Vector2Int inputSideDirection)
    {
        crossCell = Vector2Int.zero;
        inputSideDirection = Vector2Int.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        if (!packet.holdsAtPoint && packet.segmentProgress > 0.0001f)
        {
            return false;
        }

        if (!TryResolveNextPipeCellIndex(packet, out int nextPipeCellIndex) ||
            packet.pipeCellPath == null ||
            nextPipeCellIndex < 0 ||
            nextPipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        crossCell = packet.pipeCellPath[nextPipeCellIndex];
        return TryResolveCrossInputSideDirection(packet, nextPipeCellIndex, out inputSideDirection);
    }

    private static bool TryResolveCrossInputSideDirection(TransitPacket packet, int crossPipeCellIndex, out Vector2Int inputSideDirection)
    {
        return TryResolveMergeInputSideDirection(packet, crossPipeCellIndex, out inputSideDirection);
    }

    private bool TryHandleFilterPipeArrival(TransitPacket packet, out bool recalledToCoreLogistics)
    {
        recalledToCoreLogistics = false;
        if (!TryResolveCurrentFilterPipeState(packet, out FilterPipeState filterPipeState, out _))
        {
            packet.waitingForFilterRecall = false;
            return false;
        }

        if (filterPipeState != null && filterPipeState.AllowsResource(packet.resourceType))
        {
            packet.waitingForFilterRecall = false;
            return false;
        }

        if (TryRecallPacketToCoreLogistics(packet))
        {
            packet.waitingForFilterRecall = false;
            recalledToCoreLogistics = true;
            return true;
        }

        packet.waitingForFilterRecall = true;
        packet.holdsAtPoint = true;
        packet.segmentProgress = 0f;
        statusLabel = NoInventoryStatus;
        return true;
    }

    private bool TryResolveCurrentFilterPipeState(TransitPacket packet, out FilterPipeState filterPipeState, out Vector2Int filterCell)
    {
        filterPipeState = null;
        filterCell = Vector2Int.zero;
        if (!TryResolveCurrentFilterPipeCell(packet, out _, out filterCell))
        {
            return false;
        }

        return FilterPipeUtility.TryResolveStateForCell(ResolveOwnerSatellite(), filterCell, out filterPipeState);
    }

    private static bool TryResolveCurrentFilterPipeCell(TransitPacket packet, out int filterPipeCellIndex, out Vector2Int filterCell)
    {
        filterPipeCellIndex = -1;
        filterCell = Vector2Int.zero;
        if (packet == null || packet.pipeCellPath == null || packet.pipeCellPath.Count <= 0)
        {
            return false;
        }

        int currentPointIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        if (currentPointIndex < GetFirstPipePointIndex(packet) || currentPointIndex > GetLastPipePointIndex(packet))
        {
            return false;
        }

        filterPipeCellIndex = currentPointIndex - GetFirstPipePointIndex(packet);
        if (filterPipeCellIndex < 0 || filterPipeCellIndex >= packet.pipeCellPath.Count)
        {
            return false;
        }

        filterCell = packet.pipeCellPath[filterPipeCellIndex];
        return true;
    }

    private bool TryRecallPacketToCoreLogistics(TransitPacket packet)
    {
        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        if (packet == null || ownerSatellite == null)
        {
            return false;
        }

        List<StructureResourceInventory> logisticsInventories = new List<StructureResourceInventory>();
        CollectCoreLogisticsInventories(ownerSatellite, logisticsInventories);
        return logisticsInventories.Count > 0 && TryStorePacketInCoreLogistics(packet.resourceType, logisticsInventories);
    }
    private ArtificialSatellite ResolveOwnerSatellite()
    {
        BindReferences();
        return outputPortFocus != null ? outputPortFocus.OwnerSatellite : null;
    }

    private OutputPortProductionState[] ResolveProductionStatesInOwnerSatellite()
    {
        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite();
        if (ownerSatellite == null)
        {
            return new[] { this };
        }

        return ownerSatellite.GetComponentsInChildren<OutputPortProductionState>(true);
    }
    private static bool TryGetOccupiedSegment(TransitPacket packet, out Vector3 startPoint, out Vector3 endPoint)
    {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        if (packet == null || packet.holdsAtPoint)
        {
            return false;
        }

        return TryGetPathSegment(packet, packet.segmentIndex, out startPoint, out endPoint);
    }

    private static bool TryGetHeldPoint(TransitPacket packet, out Vector3 point)
    {
        point = Vector3.zero;
        if (packet == null || !packet.holdsAtPoint)
        {
            return false;
        }

        return TryGetPathPoint(packet, GetOccupiedPointIndex(packet), out point);
    }

    private static bool TryGetPathSegment(TransitPacket packet, int segmentIndex, out Vector3 startPoint, out Vector3 endPoint)
    {
        startPoint = Vector3.zero;
        endPoint = Vector3.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return false;
        }

        int clampedIndex = Mathf.Clamp(segmentIndex, 0, packet.pathPointsLocal.Count - 2);
        startPoint = packet.pathPointsLocal[clampedIndex];
        endPoint = packet.pathPointsLocal[clampedIndex + 1];
        return true;
    }

    private static bool TryGetPathPoint(TransitPacket packet, int pointIndex, out Vector3 point)
    {
        point = Vector3.zero;
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count <= 0)
        {
            return false;
        }

        int clampedIndex = Mathf.Clamp(pointIndex, 0, packet.pathPointsLocal.Count - 1);
        point = packet.pathPointsLocal[clampedIndex];
        return true;
    }

    private static int GetOccupiedPointIndex(TransitPacket packet)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(packet.segmentIndex, GetFirstPipePointIndex(packet), GetLastPipePointIndex(packet));
    }

    private static int GetFirstPipePointIndex(TransitPacket packet)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(packet.firstPipePointIndex, 0, packet.pathPointsLocal.Count - 1);
    }

    private static int GetLastPipePointIndex(TransitPacket packet)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count <= 0)
        {
            return 0;
        }

        int firstPipePointIndex = GetFirstPipePointIndex(packet);
        int maxPointIndex = packet.pathPointsLocal.Count - 1;
        return Mathf.Clamp(packet.lastPipePointIndex, firstPipePointIndex, maxPointIndex);
    }

    private static bool IsFinalDeliverySegment(TransitPacket packet)
    {
        if (packet == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
        {
            return true;
        }

        return packet.segmentIndex >= GetLastPipePointIndex(packet);
    }

    private static bool ArePointsEquivalent(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= PacketPointOccupancyToleranceSqr;
    }

    private static Vector2Int LocalPositionToGrid(Vector3 localPosition)
    {
        int center = GridSize / 2;
        int x = Mathf.RoundToInt(localPosition.x / CellSize) + center;
        int y = Mathf.RoundToInt(localPosition.y / CellSize) + center;
        return new Vector2Int(x, y);
    }
    private bool TryDeliverPacket(TransitPacket packet)
    {
        if (packet == null)
        {
            return true;
        }

        packet.segmentIndex = GetLastPipePointIndex(packet);
        packet.segmentProgress = 0f;
        packet.waitingForDelivery = false;

        if (packet.destinationInventory == null)
        {
            packet.holdsAtPoint = true;
            statusLabel = NoInventoryStatus;
            return false;
        }

        if (packet.destinationInventory.TryAdd(packet.resourceType, 1))
        {
            hasLastTransferredResource = true;
            lastTransferredResourceType = packet.resourceType;
            return true;
        }

        statusLabel = ReceiverFullStatus;
        return false;
    }

    private void RefreshIdleStatus()
    {
        if (isRunning)
        {
            return;
        }

        if (!OutputPortTransferUtility.SupportsTransfer(outputPortFocus))
        {
            statusLabel = UnsupportedStatus;
            return;
        }

        if (!hasAssignedResource)
        {
            statusLabel = IdleStatus;
            return;
        }

        if (activePackets.Count > 0)
        {
            statusLabel = $"{StoppedStatus} / {activePackets.Count} IN PIPE";
            return;
        }

        statusLabel = CanSpawnNextPacket(
            out string failureReason,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _)
            ? ReadyStatus
            : failureReason;
    }

    private void ClearAssignedResource()
    {
        hasAssignedResource = false;
        assignedResourceType = default;
        if (isRunning)
        {
            StopTransfer(StoppedStatus);
        }
        else
        {
            RefreshIdleStatus();
        }
    }

    private string BuildDetailedStatusDescription(
        string rawStatus,
        StructureResourceInventory senderOutputInventory,
        StructureResourceInventory receiverInputInventory,
        AssemblyPartFocus receiverPartFocus,
        InventoryResourceType transferResource,
        int pipeCellCount)
    {
        switch (rawStatus)
        {
            case IdleStatus:
                return BuildSelectResourceDescription();

            case ReadyStatus:
                return BuildReadyDescription(senderOutputInventory, receiverInputInventory, receiverPartFocus, transferResource, pipeCellCount);

            case RunningStatus:
                return BuildRunningDescription(receiverPartFocus, transferResource, pipeCellCount);

            case StoppedStatus:
                return activePackets.Count > 0
                    ? (activePackets.Count == 1
                        ? "Stopped. 1 packet is still travelling through the pipe."
                        : $"Stopped. {activePackets.Count} packets are still travelling through the pipe.")
                    : "Stopped.";

            case UnsupportedStatus:
                return BuildUnsupportedDescription();

            case NoRouteStatus:
                return BuildNoRouteDescription();

            case NoMaterialStatus:
                return BuildNoMaterialDescription(senderOutputInventory, transferResource);

            case PipeBlockedStatus:
                return BuildPipeBlockedDescription(receiverInputInventory, receiverPartFocus, pipeCellCount);

            case ReceiverFullStatus:
                return BuildReceiverFullDescription(receiverInputInventory, receiverPartFocus);

            case NoInventoryStatus:
                return BuildNoInventoryDescription();
        }

        return FormatStatusLabel(rawStatus);
    }

    private string BuildSelectResourceDescription()
    {
        return "Select one resource from OUTPUT RESOURCES to choose what this port will send. Click the same resource again to clear it.";
    }

    private string BuildReadyDescription(
        StructureResourceInventory senderOutputInventory,
        StructureResourceInventory receiverInputInventory,
        AssemblyPartFocus receiverPartFocus,
        InventoryResourceType transferResource,
        int pipeCellCount)
    {
        string senderLabel = ResolveModuleLabel();
        string receiverLabel = ResolvePartLabel(receiverPartFocus);
        int senderAmount = ResolveSenderStockAmount(transferResource, senderOutputInventory);
        int reserved = CountReservedPackets(receiverInputInventory);
        int receiverFreeSpace = receiverInputInventory != null ? receiverInputInventory.FreeCapacity : 0;
        return $"Ready. {senderLabel} will send {FormatResourceName(transferResource)} to {receiverLabel}. Pipe route: {Mathf.Max(0, pipeCellCount)} cell(s). Sender stock: {senderAmount}. Receiver input free space: {receiverFreeSpace}. In pipe: {reserved}.";
    }

    private string BuildRunningDescription(AssemblyPartFocus receiverPartFocus, InventoryResourceType transferResource, int pipeCellCount)
    {
        string senderLabel = ResolveModuleLabel();
        string receiverLabel = ResolvePartLabel(receiverPartFocus);
        int resolvedPipeCells = pipeCellCount > 0 ? pipeCellCount : Mathf.Max(0, lastResolvedPipeCellCount);
        return $"Running. {senderLabel} is sending {FormatResourceName(transferResource)} to {receiverLabel}. Pipe route: {resolvedPipeCells} cell(s). In transit: {activePackets.Count}.";
    }

    private string BuildPipeBlockedDescription(StructureResourceInventory receiverInputInventory, AssemblyPartFocus receiverPartFocus, int pipeCellCount)
    {
        string receiverLabel = ResolvePartLabel(receiverPartFocus);
        int freeSpace = receiverInputInventory != null ? receiverInputInventory.FreeCapacity : 0;
        int resolvedPipeCells = pipeCellCount > 0 ? pipeCellCount : Mathf.Max(0, lastResolvedPipeCellCount);
        return $"Pipe is saturated. Packets are queued toward {receiverLabel}. Pipe route: {resolvedPipeCells} cell(s). Receiver input free space: {freeSpace}. Sending resumes automatically when the line opens.";
    }

    private string BuildUnsupportedDescription()
    {
        if (outputPortFocus == null)
        {
            return "This output port cannot send resources because its focus binding is missing.";
        }

        AssemblyPartFocus ownerPartFocus = outputPortFocus.OwnerPartFocus;
        if (ownerPartFocus == null || ownerPartFocus.SourcePart == null)
        {
            return "This output port cannot send resources because its owner module could not be resolved.";
        }

        Part sourcePart = ownerPartFocus.SourcePart;
        if (sourcePart.partType == PartType.Core)
        {
            return "Core output ports are not part of the new module-to-module transfer flow.";
        }

        if (sourcePart.partType == PartType.Pipe)
        {
            return "Pipe ports cannot send resources. Select a module output port instead.";
        }

        if (string.Equals(sourcePart.partName, "Launcher", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Launcher output ports cannot send module resources.";
        }

        return $"{sourcePart.partName} does not expose a transferable output inventory.";
    }

    private string BuildNoRouteDescription()
    {
        if (outputPortFocus == null)
        {
            return "No transfer route is available because the output port focus is missing.";
        }

        if (!outputPortFocus.HasCellMapping)
        {
            return $"No transfer route is available because {ResolveModuleLabel()} has no mapped output boundary cell yet.";
        }

        return $"No reachable module input port is connected to boundary cell {outputPortFocus.MappedCell} from {ResolveModuleLabel()} {outputPortFocus.SideLabel.ToLowerInvariant()} output.";
    }

    private string BuildNoMaterialDescription(StructureResourceInventory senderOutputInventory, InventoryResourceType transferResource)
    {
        int senderAmount = ResolveSenderStockAmount(transferResource, senderOutputInventory);
        return $"Selected resource {FormatResourceName(transferResource)} is not available in {ResolveSenderInventoryLabel()}. Sender stock: {senderAmount}.";
    }

    private string BuildReceiverFullDescription(StructureResourceInventory receiverInputInventory, AssemblyPartFocus receiverPartFocus)
    {
        string receiverLabel = ResolvePartLabel(receiverPartFocus);
        if (receiverInputInventory == null)
        {
            return $"{receiverLabel} input capacity is unavailable.";
        }

        int reserved = CountReservedPackets(receiverInputInventory);
        return $"{receiverLabel} input capacity is full. Free space: {receiverInputInventory.FreeCapacity}. Capacity {receiverInputInventory.Capacity}, stored {receiverInputInventory.TotalAmount}, in pipe {reserved}.";
    }

    private string BuildNoInventoryDescription()
    {
        return UsesCoreLogisticsSender()
            ? "Core Logistics Hub inventory could not be resolved for this Core output port."
            : $"{ResolveModuleLabel()} output inventory or the connected receiver input inventory could not be resolved.";
    }

    private string ResolveModuleLabel()
    {
        return OutputPortTransferUtility.ResolveModuleLabel(outputPortFocus);
    }
    private bool UsesCoreLogisticsSender()
    {
        if (outputPortFocus == null)
        {
            return false;
        }
        AssemblyPartFocus ownerPartFocus = outputPortFocus.ResolveOwnerModuleFocus();
        if (ownerPartFocus != null && ownerPartFocus.SourcePart != null && ownerPartFocus.SourcePart.partType == PartType.Core)
        {
            return true;
        }
        return string.Equals(outputPortFocus.OwnerLabel, "Core", System.StringComparison.OrdinalIgnoreCase);
    }
    private string ResolveSenderInventoryLabel()
    {
        return UsesCoreLogisticsSender()
            ? "Core Logistics Hub capacity"
            : $"{ResolveModuleLabel()} output capacity";
    }

    private float ResolvePipeTransmitTimeSeconds()
    {
        BindReferences();
        ArtificialSatellite ownerSatellite = outputPortFocus != null ? outputPortFocus.OwnerSatellite : null;
        if (ownerSatellite != null)
        {
            AssemblyPartFocus[] partFocuses = ownerSatellite.GetComponentsInChildren<AssemblyPartFocus>(true);
            for (int i = 0; i < partFocuses.Length; i++)
            {
                AssemblyPartFocus partFocus = partFocuses[i];
                if (partFocus == null || partFocus.SourcePart == null || partFocus.SourcePart.partType != PartType.Pipe)
                {
                    continue;
                }

                return Mathf.Max(0.01f, partFocus.SourcePart.transmitTime);
            }
        }

        return DefaultTravelSecondsPerSegment;
    }

    private Vector2Int ResolveSenderTerminalDirection()
    {
        BindReferences();
        if (outputPortFocus == null || !outputPortFocus.HasCellMapping)
        {
            return Vector2Int.zero;
        }

        Vector2Int direction = outputPortFocus.SourceCell - outputPortFocus.MappedCell;
        return IsCardinalCellDirection(direction) ? direction : Vector2Int.zero;
    }
    private int ResolveSenderStockAmount(InventoryResourceType transferResource, StructureResourceInventory senderOutputInventory)
    {
        if (UsesCoreLogisticsSender())
        {
            return OutputPortTransferUtility.GetOutputResourceAmount(outputPortFocus, transferResource);
        }

        return senderOutputInventory != null ? senderOutputInventory.GetAmount(transferResource) : 0;
    }

    private static bool TryStorePacketInCoreLogistics(
        InventoryResourceType resourceType,
        List<StructureResourceInventory> logisticsInventories)
    {
        if (logisticsInventories == null)
        {
            return false;
        }

        for (int i = 0; i < logisticsInventories.Count; i++)
        {
            StructureResourceInventory inventory = logisticsInventories[i];
            if (inventory != null && inventory.TryAdd(resourceType, 1))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectCoreLogisticsInventories(
        ArtificialSatellite ownerSatellite,
        List<StructureResourceInventory> inventories)
    {
        if (inventories == null)
        {
            return;
        }

        inventories.Clear();
        if (ownerSatellite == null)
        {
            return;
        }

        StructureInstance[] instances = ownerSatellite.GetComponentsInChildren<StructureInstance>(true);
        for (int i = 0; i < instances.Length; i++)
        {
            StructureInstance instance = instances[i];
            if (instance == null || instance.SourceStructure == null || !instance.SourceStructure.UsesLogisticsHubUi)
            {
                continue;
            }

            StructureResourceInventory inventory = instance.GetComponent<StructureResourceInventory>();
            if (inventory == null)
            {
                inventory = ComponentUtility.GetOrAddComponent<StructureResourceInventory>(instance.gameObject);
                inventory.InitializeForStructure(instance.SourceStructure);
            }

            if (inventory != null)
            {
                inventories.Add(inventory);
            }
        }
    }

    private static string ResolvePartLabel(AssemblyPartFocus partFocus)
    {
        return partFocus != null && partFocus.SourcePart != null && !string.IsNullOrWhiteSpace(partFocus.SourcePart.partName)
            ? partFocus.SourcePart.partName
            : "Receiver";
    }

    private static string FormatResourceName(InventoryResourceType resourceType)
    {
        return InventoryResourceCatalog.GetDisplayName(resourceType).Replace("_", " ");
    }

    private static string FormatStatusLabel(string rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return "Select a resource to enable run.";
        }

        switch (rawStatus)
        {
            case IdleStatus:
                return "Select a resource to enable run.";

            case ReadyStatus:
                return "Ready to send.";

            case RunningStatus:
                return "Sending.";

            case StoppedStatus:
                return "Stopped.";

            case UnsupportedStatus:
                return "This output port cannot send resources.";

            case NoRouteStatus:
                return "No connected receiver route is available.";

            case NoMaterialStatus:
                return "Selected resource is unavailable in output capacity.";

            case PipeBlockedStatus:
                return "Pipe is full; waiting for space.";

            case ReceiverFullStatus:
                return "Receiver input capacity is full.";

            case NoInventoryStatus:
                return "Sender or receiver inventory is unavailable.";
        }

        if (rawStatus.StartsWith(RunningStatus + " / ", System.StringComparison.Ordinal))
        {
            return rawStatus.Replace("CELLS", "cells");
        }

        if (rawStatus.StartsWith(StoppedStatus + " / ", System.StringComparison.Ordinal))
        {
            return rawStatus.Replace("IN PIPE", "in pipe");
        }

        return rawStatus;
    }

    private static bool ShouldKeepRunningOnFlowBlock(string failureReason)
    {
        return string.Equals(failureReason, PipeBlockedStatus, System.StringComparison.Ordinal)
            || string.Equals(failureReason, ReceiverFullStatus, System.StringComparison.Ordinal);
    }

    private void StopTransfer(string reason)
    {
        isRunning = false;
        spawnTimer = 0f;
        statusLabel = string.IsNullOrWhiteSpace(reason) ? StoppedStatus : reason;
    }

    private void BindReferences()
    {
        if (outputPortFocus == null)
        {
            outputPortFocus = GetComponent<AssemblyOutputPortFocus>();
        }
    }

    private void EnsureVisualRoot()
    {
        if (outputPortFocus == null || outputPortFocus.OwnerSatellite == null)
        {
            return;
        }

        Transform ownerTransform = outputPortFocus.OwnerSatellite.transform;
        if (visualRoot == null)
        {
            GameObject rootObject = new GameObject(TokenRootName);
            visualRoot = rootObject.transform;
            visualRoot.SetParent(ownerTransform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            SmallScaleLayerUtility.ApplyRecursively(visualRoot);
            return;
        }

        if (visualRoot.parent != ownerTransform)
        {
            visualRoot.SetParent(ownerTransform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            SmallScaleLayerUtility.ApplyRecursively(visualRoot);
        }
    }

    private void CreatePacketVisual(TransitPacket packet)
    {
        if (packet == null)
        {
            return;
        }

        EnsureVisualRoot();
        if (visualRoot == null)
        {
            return;
        }

        GameObject tokenObject = new GameObject(TokenObjectName, typeof(SpriteRenderer));
        tokenObject.transform.SetParent(visualRoot, false);
        tokenObject.transform.localRotation = Quaternion.identity;
        tokenObject.transform.localScale = Vector3.one;
        SmallScaleLayerUtility.ApplyRecursively(tokenObject.transform);

        SpriteRenderer spriteRenderer = tokenObject.GetComponent<SpriteRenderer>();
        Sprite resourceSprite = InventoryResourceCatalog.GetSlotSprite(packet.resourceType);
        spriteRenderer.sprite = resourceSprite;
        spriteRenderer.sortingOrder = TokenSortingOrder;
        spriteRenderer.color = Color.white;

        packet.visualObject = tokenObject;
        packet.visualTransform = tokenObject.transform;
        packet.spriteRenderer = spriteRenderer;

        if (resourceSprite != null)
        {
            Vector2 spriteSize = resourceSprite.bounds.size;
            float maxDimension = Mathf.Max(0.0001f, Mathf.Max(spriteSize.x, spriteSize.y));
            float uniformScale = TargetTokenWorldSize / maxDimension;
            packet.visualTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }
    }

    private void UpdatePacketVisual(TransitPacket packet)
    {
        if (packet == null || packet.visualTransform == null || packet.pathPointsLocal == null || packet.pathPointsLocal.Count <= 0)
        {
            return;
        }

        int currentIndex = Mathf.Clamp(packet.segmentIndex, 0, GetLastPipePointIndex(packet));
        int nextIndex = Mathf.Clamp(currentIndex + 1, 0, packet.pathPointsLocal.Count - 1);
        Vector3 currentPoint = packet.pathPointsLocal[currentIndex];
        Vector3 nextPoint = packet.pathPointsLocal[nextIndex];
        float t = Mathf.Clamp01(packet.segmentProgress);
        packet.visualTransform.localPosition = Vector3.Lerp(currentPoint, nextPoint, t);
    }

    private void ClearAllPackets()
    {
        for (int i = activePackets.Count - 1; i >= 0; i--)
        {
            DestroyPacket(activePackets[i]);
        }

        activePackets.Clear();
    }

    private static void DestroyPacket(TransitPacket packet)
    {
        if (packet == null || packet.visualObject == null)
        {
            return;
        }

        Object.Destroy(packet.visualObject);
    }
}
