using UnityEngine;

public abstract class BaseTimeRecorder : MonoBehaviour
{
    protected Rigidbody rb;
    private bool isTimeManagerRegistered;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        TryRegisterToTimeManager();
    }

    protected virtual void LateUpdate()
    {
        if (isTimeManagerRegistered && !CoreRuntimeAccess.TryGetTimeManager(out _))
        {
            isTimeManagerRegistered = false;
        }

        TryRegisterToTimeManager();
    }

    protected virtual void OnDestroy()
    {
        TryDeregisterFromTimeManager();
    }

    public abstract void UpdateVisibility(int globalFrame);
    
    public abstract void Record();
    public abstract void Rewind();
    public abstract void Replay();
    public abstract bool isDoneReplaying();
    public abstract void ClearFutureHistory();

    public virtual void SetKinematic(bool isKinematic)
    {
        if (rb != null)
        {
            rb.isKinematic = isKinematic;
        }
    }

    public virtual void ApplyFinalState() {}

    private void TryRegisterToTimeManager()
    {
        if (isTimeManagerRegistered) return;
        if (!CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager)) return;

        timeManager.Register(this);
        isTimeManagerRegistered = true;
    }

    private void TryDeregisterFromTimeManager()
    {
        if (!isTimeManagerRegistered) return;
        if (CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager))
        {
            timeManager.Deregister(this);
        }
        isTimeManagerRegistered = false;
    }
}
