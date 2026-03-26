using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OutputPortProductionState : MonoBehaviour
{
    private const float ConsumptionIntervalSeconds = 1f;
    private const float TravelSecondsPerSegment = 1f;
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
        public StructureResourceInventory destinationInventory;
        public AssemblyPartFocus destinationPartFocus;
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
            out int pipeCellCount))
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
            out int pipeCellCount))
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

    public bool RequestStart()
    {
        if (!CanSpawnNextPacket(
            out string failureReason,
            out _,
            out _,
            out AssemblyPartFocus receiverPartFocus,
            out _,
            out _,
            out int pipeCellCount))
        {
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
        if (!CanSpawnNextPacket(
            out string failureReason,
            out _,
            out _,
            out AssemblyPartFocus receiverPartFocus,
            out _,
            out _,
            out int pipeCellCount))
        {
            StopTransfer(failureReason);
            return;
        }

        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);
        lastResolvedPipeCellCount = pipeCellCount;

        spawnTimer += deltaTime;
        while (spawnTimer >= ConsumptionIntervalSeconds)
        {
            spawnTimer -= ConsumptionIntervalSeconds;
            if (!TrySpawnPacket())
            {
                spawnTimer = 0f;
                break;
            }
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
            out int pipeCellCount))
        {
            StopTransfer(failureReason);
            return false;
        }

        if (!OutputPortTransferUtility.TryConsumeOutputResource(outputPortFocus, transferResource, 1))
        {
            StopTransfer(NoMaterialStatus);
            return false;
        }

        TransitPacket packet = new TransitPacket
        {
            resourceType = transferResource,
            pathPointsLocal = new List<Vector3>(pathPoints),
            segmentIndex = 0,
            segmentProgress = 0f,
            waitingForDelivery = false,
            destinationInventory = receiverInputInventory,
            destinationPartFocus = receiverPartFocus
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
        out int pipeCellCount)
    {
        failureReason = ReadyStatus;
        senderOutputInventory = null;
        receiverInputInventory = null;
        receiverPartFocus = null;
        transferResource = assignedResourceType;
        pathPoints = pathBuffer;
        pipeCellCount = 0;
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

        if (!OutputPortTransferUtility.TryResolveTransferRoute(
            outputPortFocus,
            pathBuffer,
            out pipeCellCount,
            out receiverInputInventory,
            out receiverPartFocus) ||
            receiverInputInventory == null)
        {
            failureReason = NoRouteStatus;
            return false;
        }

        lastResolvedPipeCellCount = pipeCellCount;
        lastResolvedReceiverLabel = ResolvePartLabel(receiverPartFocus);

        int reservedAmount = CountReservedPackets(receiverInputInventory);
        if (receiverInputInventory.TotalAmount + reservedAmount + 1 > receiverInputInventory.Capacity)
        {
            failureReason = ReceiverFullStatus;
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
        for (int i = 0; i < activePackets.Count; i++)
        {
            TransitPacket packet = activePackets[i];
            if (packet != null && packet.destinationInventory == destinationInventory)
            {
                reservedAmount++;
            }
        }

        return reservedAmount;
    }

    private void UpdateTransitPackets(float deltaTime)
    {
        for (int i = activePackets.Count - 1; i >= 0; i--)
        {
            TransitPacket packet = activePackets[i];
            if (packet == null)
            {
                activePackets.RemoveAt(i);
                continue;
            }

            if (packet.waitingForDelivery)
            {
                if (TryDeliverPacket(packet))
                {
                    DestroyPacket(packet);
                    activePackets.RemoveAt(i);
                }
                else
                {
                    UpdatePacketVisual(packet);
                }

                continue;
            }

            if (packet.pathPointsLocal == null || packet.pathPointsLocal.Count < 2)
            {
                packet.waitingForDelivery = true;
                UpdatePacketVisual(packet);
                continue;
            }

            packet.segmentProgress += deltaTime / TravelSecondsPerSegment;
            while (packet.segmentProgress >= 1f && !packet.waitingForDelivery)
            {
                packet.segmentProgress -= 1f;
                packet.segmentIndex++;
                if (packet.segmentIndex >= packet.pathPointsLocal.Count - 1)
                {
                    packet.segmentIndex = packet.pathPointsLocal.Count - 1;
                    packet.segmentProgress = 0f;
                    packet.waitingForDelivery = true;
                }
            }

            if (packet.waitingForDelivery)
            {
                if (TryDeliverPacket(packet))
                {
                    DestroyPacket(packet);
                    activePackets.RemoveAt(i);
                    continue;
                }
            }

            UpdatePacketVisual(packet);
        }
    }

    private bool TryDeliverPacket(TransitPacket packet)
    {
        if (packet == null)
        {
            return true;
        }

        if (packet.destinationInventory == null)
        {
            StopTransfer(NoInventoryStatus);
            return false;
        }

        if (packet.destinationInventory.TryAdd(packet.resourceType, 1))
        {
            hasLastTransferredResource = true;
            lastTransferredResourceType = packet.resourceType;
            return true;
        }

        StopTransfer(ReceiverFullStatus);
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
        int receiverFreeSpace = receiverInputInventory != null
            ? Mathf.Max(0, receiverInputInventory.Capacity - (receiverInputInventory.TotalAmount + reserved))
            : 0;
        return $"Ready. {senderLabel} will send {FormatResourceName(transferResource)} to {receiverLabel}. Pipe route: {Mathf.Max(0, pipeCellCount)} cell(s). Sender stock: {senderAmount}. Receiver input free space: {receiverFreeSpace}.";
    }

    private string BuildRunningDescription(AssemblyPartFocus receiverPartFocus, InventoryResourceType transferResource, int pipeCellCount)
    {
        string senderLabel = ResolveModuleLabel();
        string receiverLabel = ResolvePartLabel(receiverPartFocus);
        int resolvedPipeCells = pipeCellCount > 0 ? pipeCellCount : Mathf.Max(0, lastResolvedPipeCellCount);
        return $"Running. {senderLabel} is sending {FormatResourceName(transferResource)} to {receiverLabel}. Pipe route: {resolvedPipeCells} cell(s). In transit: {activePackets.Count}.";
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
        int effectiveFreeSpace = Mathf.Max(0, receiverInputInventory.Capacity - (receiverInputInventory.TotalAmount + reserved));
        return $"{receiverLabel} input capacity is full. Free space: {effectiveFreeSpace}. Capacity {receiverInputInventory.Capacity}, stored {receiverInputInventory.TotalAmount}, reserved in pipe {reserved}.";
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

    private int ResolveSenderStockAmount(InventoryResourceType transferResource, StructureResourceInventory senderOutputInventory)
    {
        if (UsesCoreLogisticsSender())
        {
            return OutputPortTransferUtility.GetOutputResourceAmount(outputPortFocus, transferResource);
        }

        return senderOutputInventory != null ? senderOutputInventory.GetAmount(transferResource) : 0;
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

        if (packet.waitingForDelivery)
        {
            packet.visualTransform.localPosition = packet.pathPointsLocal[packet.pathPointsLocal.Count - 1];
            return;
        }

        int currentIndex = Mathf.Clamp(packet.segmentIndex, 0, packet.pathPointsLocal.Count - 1);
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

