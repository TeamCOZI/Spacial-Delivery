using System;
using UnityEngine;

[DefaultExecutionOrder(34000)]
[RequireComponent(typeof(LauncherLaunchFocusHandler))]
public class LauncherLaunchController : MonoBehaviour
{
    public event Action<LauncherSpawner.LaunchResult> launchSucceeded;

    [Header("Launch")]
    [SerializeField] private GameObject spaceshipPrefab;
    [SerializeField, Min(0f)] private float launchSpeed = 100f;

    [Header("Button")]
    [SerializeField] private Vector2 buttonSize = new Vector2(150f, 44f);
    [SerializeField] private Vector2 screenOffset = new Vector2(120f, -20f);
    [SerializeField] private string buttonLabel = "Launcher";
    [SerializeField] private Color buttonColor = new Color(0.18f, 0.6f, 0.95f, 0.95f);

    private LauncherSelectionPresenter selectionPresenter;

    private void Awake()
    {
        EnsureSelectionPresenter();
        if (selectionPresenter != null)
        {
            selectionPresenter.Configure(buttonSize, screenOffset, buttonLabel, buttonColor);
        }
        EnsureSpaceshipPrefab();
    }

    private void OnEnable()
    {
        EnsureSelectionPresenter();
        if (selectionPresenter != null)
        {
            selectionPresenter.launchRequested += OnLaunchRequested;
        }
    }

    private void OnDisable()
    {
        if (selectionPresenter != null)
        {
            selectionPresenter.launchRequested -= OnLaunchRequested;
        }
    }

    private void OnLaunchRequested(Transform launcher)
    {
        if (launcher == null) return;
        Vector3 carrierVelocity = LauncherLaunchUtility.GetLauncherCarrierVelocity(launcher);
        if (!TryLaunch(launcher, out LauncherSpawner.LaunchResult launchResult)) return;

        Vector3 relativeVelocity = launchResult.launchDirection * launchSpeed;
        Debug.Log(
            $"[LauncherLaunch] speed={launchSpeed:F2} start={launchResult.spawnPosition} dir={launchResult.launchDirection} " +
            $"carrierVel={carrierVelocity} carrierSpeed={carrierVelocity.magnitude:F2} relativeVel={relativeVelocity} finalVel={launchResult.launchVelocity} finalSpeed={launchResult.launchVelocity.magnitude:F2}"
        );

        launchSucceeded?.Invoke(launchResult);
    }

    private void EnsureSelectionPresenter()
    {
        if (selectionPresenter == null)
        {
            selectionPresenter = GetComponent<LauncherSelectionPresenter>();
        }
    }

    private void EnsureSpaceshipPrefab()
    {
        spaceshipPrefab = LauncherSpawner.ResolveSpaceshipPrefab(spaceshipPrefab);
    }

    private bool TryLaunch(Transform launcher, out LauncherSpawner.LaunchResult launchResult)
    {
        launchResult = default;

        EnsureSpaceshipPrefab();
        if (spaceshipPrefab == null)
        {
            Debug.LogWarning("LauncherLaunchController: Spaceship prefab is not assigned.");
            return false;
        }

        return LauncherSpawner.TryLaunch(
            launcher,
            spaceshipPrefab,
            launchSpeed,
            out launchResult);
    }
}


