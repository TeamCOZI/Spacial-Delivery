using UnityEngine;
using UnityEngine.InputSystem;

public class TimeControllerUI : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        ExecuteIfTimeManagerExists(static manager =>
        {
            if (manager.IsPaused) manager.Play();
            else manager.Pause();
        });
    }

    public void OnRewind()
    {
        ExecuteIfTimeManagerExists(static manager => manager.StartRewind());
    }

    public void OnPlay()
    {
        ExecuteIfTimeManagerExists(static manager => manager.Play());
    }

    public void OnPause()
    {
        ExecuteIfTimeManagerExists(static manager => manager.Pause());
    }

    public void OnFastForward2x()
    {
        ExecuteIfTimeManagerExists(static manager => manager.FastForward(2f));
    }

    public void OnFastForward4x()
    {
        ExecuteIfTimeManagerExists(static manager => manager.FastForward(4f));
    }

    public void OnFastForward8x()
    {
        ExecuteIfTimeManagerExists(static manager => manager.FastForward(8f));
    }

    private static void ExecuteIfTimeManagerExists(System.Action<TimeManager> command)
    {
        if (command == null) return;
        if (!CoreRuntimeAccess.TryGetTimeManager(out TimeManager manager)) return;
        command.Invoke(manager);
    }
}
