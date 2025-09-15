using UnityEngine;

public class TimeControllerUI : MonoBehaviour
{
    public void OnRewind()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.StartRewind();
    }

    public void OnPlay()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.Play();
    }

    public void OnPause()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.Pause();
    }

    public void OnFastForward2x()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.FastForward(2f);
    }

    public void OnFastForward4x()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.FastForward(4f);
    }

    public void OnFastForward8x()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.FastForward(8f);
    }
}