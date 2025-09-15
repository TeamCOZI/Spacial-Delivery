using UnityEngine;

public abstract class BaseTimeRecorder : MonoBehaviour
{
    protected Rigidbody rb;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Register(this);
        }
    }

    protected virtual void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Deregister(this);
        }
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
}