using System;
using UnityEngine;

[DefaultExecutionOrder(29000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(WorldPosition))]
[RequireComponent(typeof(GravityAffectedMover))]
[RequireComponent(typeof(SpaceshipFuel))]
[RequireComponent(typeof(SpaceshipFlightController))]
public class Spaceship : MonoBehaviour
{
    [SerializeField] private bool logWorldPositionEveryFrame = false;

    private WorldPosition worldPosition;
    private GravityAffectedMover gravityMover;
    private SpaceshipFuel fuel;
    private SpaceshipFlightController flightController;

    public event Action<SpaceshipState> StateChanged;
    public SpaceshipState CurrentState { get; private set; } = SpaceshipState.Idle;

    private void Awake()
    {
        CacheRequiredComponents();
        SetState(SpaceshipState.Idle);
    }

    public void Launch(Vector3 velocity)
    {
        if (CurrentState == SpaceshipState.Destroyed) return;

        CacheRequiredComponents();
        if (gravityMover == null)
        {
            Debug.LogError("Spaceship: GravityAffectedMover is missing.");
            return;
        }

        gravityMover.Launch(new Vector3(velocity.x, velocity.y, 0f));
        SetState(SpaceshipState.Launched);
    }

    private void LateUpdate()
    {
        if (!logWorldPositionEveryFrame) return;
        if (gravityMover == null || !gravityMover.IsLaunched) return;
        if (worldPosition == null) CacheRequiredComponents();
        if (worldPosition == null) return;

        Vector3 localPosition = transform.position;
        Debug.Log($"[SpaceshipFrame] local={localPosition} world={worldPosition.worldPosition} vel={gravityMover.CurrentVelocity}");
    }

    private void CacheRequiredComponents()
    {
        if (worldPosition == null) worldPosition = GetComponent<WorldPosition>();
        if (gravityMover == null) gravityMover = GetComponent<GravityAffectedMover>();
        if (fuel == null) fuel = GetComponent<SpaceshipFuel>();
        if (flightController == null) flightController = GetComponent<SpaceshipFlightController>();

        if (worldPosition == null || gravityMover == null || fuel == null || flightController == null)
        {
            Debug.LogError("Spaceship: Required components are missing on prefab.");
        }
    }

    private void OnDestroy()
    {
        SetState(SpaceshipState.Destroyed);
    }

    public void DestroyByCelestialCollision(Collider otherCollider)
    {
        if (CurrentState == SpaceshipState.Destroyed) return;

        string targetName = otherCollider != null ? otherCollider.name : "<null>";
        string rootName = (otherCollider != null && otherCollider.transform != null && otherCollider.transform.root != null)
            ? otherCollider.transform.root.name
            : "<null>";

        Debug.Log($"[SpaceshipDestroyed] ship={name} cause=CelestialCollision target={targetName} root={rootName}");
        SetState(SpaceshipState.Destroyed);
        Destroy(gameObject);
    }

    private void SetState(SpaceshipState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        StateChanged?.Invoke(newState);
    }

}
