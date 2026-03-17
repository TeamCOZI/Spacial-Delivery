using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(28850)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Spaceship))]
[RequireComponent(typeof(SpaceshipFlightController))]
[RequireComponent(typeof(GravityAffectedMover))]
[RequireComponent(typeof(WorldPosition))]
public class SpaceshipRendezvousController : MonoBehaviour
{
    [Header("Formation")]
    [SerializeField, Min(0f)] private float targetOffsetPadding = 2f;
    [SerializeField, Min(0.5f)] private float minimumTargetOffset = 6f;

    [Header("Assist")]
    [SerializeField, Min(0f)] private float positionGain = 0.8f;
    [SerializeField, Min(0f)] private float velocityGain = 1.6f;
    [SerializeField, Min(0f)] private float maxPositionError = 200f;

    private Spaceship spaceship;
    private SpaceshipFlightController flightController;
    private GravityAffectedMover mover;
    private WorldPosition worldPosition;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        CacheComponents();
    }

    private void Update()
    {
        CacheComponents();

        if (!IsActivationInputHeld())
        {
            IsActive = false;
            return;
        }

        if (!TryBuildContext(out SpaceshipRendezvousUtility.Context context))
        {
            IsActive = false;
            return;
        }

        Vector3 requestedAcceleration = ComputeRequestedAcceleration(context);
        flightController.RequestAutomatedAcceleration(requestedAcceleration);
        IsActive = true;
    }

    private void CacheComponents()
    {
        if (spaceship == null) spaceship = GetComponent<Spaceship>();
        if (flightController == null) flightController = GetComponent<SpaceshipFlightController>();
        if (mover == null) mover = GetComponent<GravityAffectedMover>();
        if (worldPosition == null) worldPosition = GetComponent<WorldPosition>();
    }

    private bool TryBuildContext(out SpaceshipRendezvousUtility.Context context)
    {
        context = default;
        if (spaceship == null || flightController == null || mover == null || worldPosition == null) return false;
        if (!SpaceshipRendezvousUtility.TryResolveContext(spaceship, out context)) return false;
        if (!IsFocusedSpaceship()) return false;
        if (!flightController.CanApplyAutomatedThrust()) return false;
        return true;
    }

    private bool IsFocusedSpaceship()
    {
        return SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out Spaceship focusedSpaceship)
            && focusedSpaceship == spaceship;
    }

    private static bool IsActivationInputHeld()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    }

    private Vector3 ComputeRequestedAcceleration(SpaceshipRendezvousUtility.Context context)
    {
        Double3 shipWorld = worldPosition.worldPosition;
        Double3 targetWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(context.target);
        Double3 parentWorld = SpaceshipRendezvousUtility.ResolveWorldPosition(context.parent);
        Vector3 shipToTarget = (targetWorld - shipWorld).ToVector3();
        Vector3 parentToTarget = (targetWorld - parentWorld).ToVector3();
        Vector3 shipVelocity = mover != null ? mover.CurrentVelocity : Vector3.zero;
        Vector3 targetVelocity = SpaceshipRendezvousUtility.ResolvePlanarVelocity(context.target);

        shipToTarget.z = 0f;
        parentToTarget.z = 0f;

        Vector3 radialDirection = parentToTarget;
        if (radialDirection.sqrMagnitude <= 0.000001f)
        {
            radialDirection = shipToTarget;
        }
        if (radialDirection.sqrMagnitude <= 0.000001f)
        {
            radialDirection = Vector3.up;
        }
        radialDirection.Normalize();

        float followDistance = ComputeFollowDistance(context.target);
        Vector3 positionError = shipToTarget + radialDirection * followDistance;
        positionError.z = 0f;
        if (maxPositionError > 0f && positionError.sqrMagnitude > maxPositionError * maxPositionError)
        {
            positionError = positionError.normalized * maxPositionError;
        }

        Vector3 velocityError = targetVelocity - shipVelocity;
        velocityError.z = 0f;

        return (positionError * positionGain) + (velocityError * velocityGain);
    }

    private float ComputeFollowDistance(Transform target)
    {
        float shipRadius = EstimatePlanarRadius(transform);
        float targetRadius = EstimatePlanarRadius(target);
        float offset = shipRadius + targetRadius + targetOffsetPadding;
        return Mathf.Max(minimumTargetOffset, offset);
    }

    private static float EstimatePlanarRadius(Transform root)
    {
        if (root == null) return 0.5f;

        if (TryGetCombinedBounds(root.GetComponentsInChildren<Collider>(true), out Bounds colliderBounds))
        {
            return Mathf.Max(0.5f, new Vector2(colliderBounds.extents.x, colliderBounds.extents.y).magnitude);
        }

        if (TryGetCombinedBounds(root.GetComponentsInChildren<Renderer>(true), out Bounds rendererBounds))
        {
            return Mathf.Max(0.5f, new Vector2(rendererBounds.extents.x, rendererBounds.extents.y).magnitude);
        }

        return 0.5f;
    }

    private static bool TryGetCombinedBounds<T>(T[] components, out Bounds bounds) where T : Component
    {
        bounds = default;
        if (components == null || components.Length == 0) return false;

        bool foundAny = false;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null) continue;

            Bounds candidateBounds;
            if (component is Collider collider)
            {
                if (!collider.enabled) continue;
                candidateBounds = collider.bounds;
            }
            else if (component is Renderer renderer)
            {
                if (!renderer.enabled) continue;
                candidateBounds = renderer.bounds;
            }
            else
            {
                continue;
            }

            if (!foundAny)
            {
                bounds = candidateBounds;
                foundAny = true;
            }
            else
            {
                bounds.Encapsulate(candidateBounds);
            }
        }

        return foundAny;
    }
}