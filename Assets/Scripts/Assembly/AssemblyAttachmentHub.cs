using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(20000)]
public class AssemblyAttachmentHub : MonoBehaviour
{
    [System.Serializable]
    private struct AttachmentEntry
    {
        public Transform target;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [SerializeField] private bool destroyAttachmentsOnOwnerDestroy = true;
    [SerializeField] private bool autoRegisterExistingChildren = true;
    [SerializeField] private bool stabilizeAttachmentRigidbodies = true;

    private readonly List<AttachmentEntry> attachments = new List<AttachmentEntry>();

    private void Start()
    {
        if (!autoRegisterExistingChildren) return;
        AutoRegisterExistingChildren();
        SyncAttachments();
    }

    private void OnEnable()
    {
        WorldOriginManager.worldShifted += HandleWorldShift;
    }

    private void OnDisable()
    {
        WorldOriginManager.worldShifted -= HandleWorldShift;
    }

    public void RegisterOrUpdate(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        if (target == null) return;
        if (target.parent != transform)
        {
            target.SetParent(transform, false);
        }
        if (stabilizeAttachmentRigidbodies)
        {
            StabilizeRigidbodies(target);
        }

        for (int i = 0; i < attachments.Count; i++)
        {
            if (attachments[i].target != target) continue;

            AttachmentEntry updated = attachments[i];
            updated.localPosition = localPosition;
            updated.localRotation = localRotation;
            updated.localScale = localScale;
            attachments[i] = updated;
            return;
        }

        attachments.Add(new AttachmentEntry
        {
            target = target,
            localPosition = localPosition,
            localRotation = localRotation,
            localScale = localScale
        });
    }

    public void Unregister(Transform target)
    {
        if (target == null) return;

        for (int i = attachments.Count - 1; i >= 0; i--)
        {
            if (attachments[i].target == target)
            {
                attachments.RemoveAt(i);
            }
        }
    }

    private void LateUpdate()
    {
        SyncAttachments();
    }

    public void SyncAttachments()
    {
        for (int i = attachments.Count - 1; i >= 0; i--)
        {
            AttachmentEntry entry = attachments[i];
            if (entry.target == null)
            {
                attachments.RemoveAt(i);
                continue;
            }

            entry.target.localPosition = entry.localPosition;
            entry.target.localRotation = entry.localRotation;
            entry.target.localScale = entry.localScale;
        }
    }

    private void AutoRegisterExistingChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            if (!ShouldAutoRegister(child)) continue;

            RegisterOrUpdate(child, child.localPosition, child.localRotation, child.localScale);
        }
    }

    private static bool ShouldAutoRegister(Transform child)
    {
        if (child == null) return false;
        if (child.CompareTag("Icon")) return false;
        if (child.name == "AssemblyGridPlane") return true;
        if (child.GetComponent<AssemblyPartFocus>() != null) return true;
        if (child.CompareTag("Part")) return true;
        return false;
    }

    private void OnDestroy()
    {
        if (!destroyAttachmentsOnOwnerDestroy) return;

        for (int i = attachments.Count - 1; i >= 0; i--)
        {
            Transform target = attachments[i].target;
            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }

        attachments.Clear();
    }

    private void HandleWorldShift(Vector3 shiftDelta)
    {
        SyncAttachments();
    }

    private static void StabilizeRigidbodies(Transform targetRoot)
    {
        if (targetRoot == null) return;

        Rigidbody[] rigidbodies = targetRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null) continue;

            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
