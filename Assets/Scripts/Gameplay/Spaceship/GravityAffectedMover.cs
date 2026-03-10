using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(28950)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(WorldPosition))]
public class GravityAffectedMover : MonoBehaviour
{
    private const bool DisableAllSpaceshipCollisions = false;
    private const bool DisableAllSpaceshipGravity = false;

    [Header("Gravity")]
    [SerializeField, Min(0f)] private float gravityAcceleration = 80f;
    [SerializeField, Min(0f)] private float maxAcceleration = 250f;

    [Header("Collision")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private bool logCollisionTarget = true;
    [SerializeField, Min(0f)] private float collisionLogDelayAfterLaunchSeconds = 0.2f;
    [SerializeField, Min(0f)] private float collisionLogIntervalSeconds = 0.2f;

    [Header("Launch Diagnostics")]
    [SerializeField] private bool debugLaunchDiagnostics = false;
    [SerializeField, Min(0f)] private float launchDiagnosticsDurationSeconds = 1f;
    [SerializeField, Min(0f)] private float launchDiagnosticsLogIntervalSeconds = 0.05f;

    [Header("Facing")]
    [SerializeField, Min(0f)] private float minFacingSpeed = 0.01f;

    private Rigidbody rb;
    private WorldPosition worldPosition;

    private bool launched;
    private Vector3 velocity;
    private Vector3 queuedVelocityDelta;
    private Collider lastLoggedCollider;
    private float lastCollisionLogTime = float.NegativeInfinity;
    private float launchedAtUnscaledTime = float.NegativeInfinity;
    private bool wasKinematicBeforePause;
    private bool pauseKinematicApplied;
    private Vector3 pausedLinearVelocity;
    private Vector3 pausedAngularVelocity;
    private Collider lastRawLoggedCollider;
    private float lastRawCollisionLogTime = float.NegativeInfinity;
    private float lastLaunchDynamicsLogTime = float.NegativeInfinity;
    private Spaceship spaceship;

    public bool IsLaunched => launched;
    public Vector3 CurrentVelocity => velocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        worldPosition = GetComponent<WorldPosition>();
        spaceship = GetComponent<Spaceship>();
    }

    public void Launch(Vector3 initialVelocity)
    {
        EnsureDependencies();

        Vector3 launchVelocity = new Vector3(initialVelocity.x, initialVelocity.y, 0f);

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // This ship is moved explicitly in FixedUpdate and queried by screen-space raycasts in LateUpdate.
        // Interpolation makes the rendered pose diverge from the physics query pose by a frame, which
        // shows up as forward-stretched hit areas and hover flicker on fast-moving ships.
        rb.interpolation = RigidbodyInterpolation.None;
        rb.detectCollisions = enableCollision && !DisableAllSpaceshipCollisions;

        Vector3 startLocal = transform.position;
        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        Double3 startWorld = coordinator != null
            ? coordinator.ToWorld(startLocal)
            : (Double3)startLocal;
        worldPosition.SetWorldPosition(startWorld);

        velocity = launchVelocity;
        queuedVelocityDelta = Vector3.zero;
        rb.position = startLocal;
        launchedAtUnscaledTime = Time.unscaledTime;
        lastRawLoggedCollider = null;
        lastRawCollisionLogTime = float.NegativeInfinity;
        lastLaunchDynamicsLogTime = float.NegativeInfinity;

        if (IsMovementPaused())
        {
            // Launch is accepted while paused, but motion starts when unpaused.
            pausedLinearVelocity = launchVelocity;
            pausedAngularVelocity = Vector3.zero;
            velocity = Vector3.zero;
            pauseKinematicApplied = true;
        }

        launched = true;
    }

    public void AddAcceleration(Vector3 acceleration, float deltaTime)
    {
        if (!launched) return;
        if (deltaTime <= 0f) return;

        Vector3 planar = new Vector3(acceleration.x, acceleration.y, 0f);
        queuedVelocityDelta += planar * deltaTime;
    }

    private void FixedUpdate()
    {
        if (!launched || worldPosition == null) return;
        if (IsMovementPaused())
        {
            ApplyPauseKinematicState();
            return;
        }

        RestoreKinematicAfterPause();

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        rb.detectCollisions = enableCollision && !DisableAllSpaceshipCollisions;
        rb.isKinematic = true;
        Vector3 velocityBeforeStep = velocity;
        velocityBeforeStep.z = 0f;
        velocity = velocityBeforeStep;

        Vector3 queuedDeltaApplied = Vector3.zero;
        if (queuedVelocityDelta.sqrMagnitude > 0f)
        {
            queuedDeltaApplied = queuedVelocityDelta;
            velocity += queuedVelocityDelta;
            queuedVelocityDelta = Vector3.zero;
        }

        Vector3 acceleration = ComputeGravityAcceleration();
        velocity += acceleration * dt;
        velocity.z = 0f;

        Double3 nextWorld = worldPosition.worldPosition + (Double3)(velocity * dt);
        worldPosition.SetWorldPosition(nextWorld);
        Vector3 localPosition = ToLocal(nextWorld);
        localPosition.z = 0f;
        rb.MovePosition(localPosition);

        LogLaunchDynamicsIfNeeded(
            velocityBeforeStep,
            velocity,
            queuedDeltaApplied,
            acceleration,
            dt);
        UpdateFacingFromVelocity();
    }

    private Vector3 ComputeGravityAcceleration()
    {
        if (DisableAllSpaceshipGravity) return Vector3.zero;

        IReadOnlyList<Gravity> gravities = Gravity.ActiveGravities;
        if (gravities == null || gravities.Count == 0) return Vector3.zero;

        Double3 self = worldPosition.worldPosition;
        Vector3 acceleration = Vector3.zero;

        for (int i = 0; i < gravities.Count; i++)
        {
            Gravity source = gravities[i];
            if (source == null || source.gameObject == gameObject) continue;

            int radiusInt = source.GravityRadius;
            if (radiusInt <= 0) continue;
            double radius = radiusInt;

            Double3 sourceWorld = ResolveWorldPosition(source.transform);
            double dx = sourceWorld.x - self.x;
            double dy = sourceWorld.y - self.y;
            double distSq = (dx * dx) + (dy * dy);
            if (distSq < 1e-12d) continue;

            double dist = System.Math.Sqrt(distSq);
            if (dist > radius) continue;

            float distF = (float)dist;
            float radiusF = (float)radius;
            float falloff = 1f - Mathf.Clamp01(distF / Mathf.Max(0.0001f, radiusF));
            float accelMag = gravityAcceleration * falloff;
            if (accelMag <= 0f) continue;

            Vector3 dir = new Vector3((float)(dx / dist), (float)(dy / dist), 0f);
            acceleration += dir * accelMag;
        }

        if (maxAcceleration > 0f && acceleration.sqrMagnitude > maxAcceleration * maxAcceleration)
        {
            acceleration = acceleration.normalized * maxAcceleration;
        }
        acceleration.z = 0f;
        return acceleration;
    }

    private void LogCollisionTargetIfNeeded(Collider other, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!logCollisionTarget) return;
        if (!launched) return;
        if ((Time.unscaledTime - launchedAtUnscaledTime) < collisionLogDelayAfterLaunchSeconds) return;
        if (other == null) return;
        if (ShouldIgnoreCollisionLog(other)) return;

        float now = Time.unscaledTime;
        bool sameCollider = (other == lastLoggedCollider);
        if (sameCollider && (now - lastCollisionLogTime) < collisionLogIntervalSeconds) return;

        lastLoggedCollider = other;
        lastCollisionLogTime = now;

        GameObject target = other.gameObject;
        string targetName = target != null ? target.name : "<null>";
        string rootName = (target != null && target.transform.root != null) ? target.transform.root.name : "<null>";

        Debug.Log(
            $"[SpaceshipCollision] ship={name} target={targetName} root={rootName} point={hitPoint} normal={hitNormal} speed={velocity.magnitude:F3}"
        );
    }

    private void SyncLocalFromWorld()
    {
        EnsureDependencies();
        if (worldPosition == null) return;

        Vector3 local = ToLocal(worldPosition.worldPosition);
        local.z = 0f;
        if (rb != null)
        {
            rb.MovePosition(local);
        }
        else
        {
            transform.position = local;
        }
    }

    private void UpdateFacingFromVelocity()
    {
        Vector2 planarVelocity = new Vector2(velocity.x, velocity.y);
        if (planarVelocity.sqrMagnitude < minFacingSpeed * minFacingSpeed) return;

        // Spaceship forward axis is +Y, so subtract 90 degrees from atan2 heading.
        float z = Mathf.Atan2(planarVelocity.y, planarVelocity.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, z);
        if (rb != null)
        {
            rb.MoveRotation(rotation);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

    private Vector3 ToLocal(Double3 world)
    {
        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        if (coordinator != null)
        {
            return coordinator.ToLocal(world);
        }
        return world.ToVector3();
    }

    private Double3 ToWorld(Vector3 local)
    {
        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        if (coordinator != null)
        {
            return coordinator.ToWorld(local);
        }
        return (Double3)local;
    }

    private static Double3 ResolveWorldPosition(Transform source)
    {
        if (source == null) return Double3.Zero;

        WorldPosition wp = source.GetComponent<WorldPosition>();
        if (wp != null) return wp.worldPosition;

        CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator);
        if (coordinator != null)
        {
            return coordinator.ToWorld(source.position);
        }
        return (Double3)source.position;
    }

    private bool ShouldIgnoreCollisionLog(Collider collider)
    {
        if (collider == null) return true;
        return collider.CompareTag("Icon");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        if (TryHandleCelestialCollision(collision)) return;
        LogRawCollisionIfNeeded(collision, "Enter");
        ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        LogCollisionTargetIfNeeded(ResolveOtherCollider(collision), contact.point, contact.normal);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision == null) return;
        if (TryHandleCelestialCollision(collision)) return;
        LogRawCollisionIfNeeded(collision, "Stay");
        ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        LogCollisionTargetIfNeeded(ResolveOtherCollider(collision), contact.point, contact.normal);
    }

    private void EnsureDependencies()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (worldPosition == null)
        {
            worldPosition = GetComponent<WorldPosition>();
            if (worldPosition == null) Debug.LogError("GravityAffectedMover: WorldPosition is missing.");
        }

        if (spaceship == null)
        {
            spaceship = GetComponent<Spaceship>();
        }
    }

    private bool IsMovementPaused()
    {
        return CoreRuntimeAccess.TryGetTimeManager(out TimeManager timeManager) && timeManager.IsPaused;
    }

    private void ApplyPauseKinematicState()
    {
        EnsureDependencies();
        if (rb == null || worldPosition == null) return;

        if (!pauseKinematicApplied)
        {
            pausedLinearVelocity = velocity;
            pausedAngularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            pauseKinematicApplied = true;
        }

        velocity = Vector3.zero;
        queuedVelocityDelta = Vector3.zero;
        SyncLocalFromWorld();
    }

    private void RestoreKinematicAfterPause()
    {
        if (!pauseKinematicApplied) return;
        if (rb == null) return;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        velocity = new Vector3(pausedLinearVelocity.x, pausedLinearVelocity.y, 0f);
        pauseKinematicApplied = false;
    }

    private bool IsWithinLaunchDiagnosticsWindow()
    {
        if (!IsLaunchDiagnosticsEnabled()) return false;
        if (!launched) return false;
        return (Time.unscaledTime - launchedAtUnscaledTime) <= launchDiagnosticsDurationSeconds;
    }

    private bool IsLaunchDiagnosticsEnabled()
    {
        return debugLaunchDiagnostics;
    }

    private bool CanEmitLaunchDiagnosticsNow()
    {
        float now = Time.unscaledTime;
        if ((now - lastLaunchDynamicsLogTime) < launchDiagnosticsLogIntervalSeconds) return false;
        lastLaunchDynamicsLogTime = now;
        return true;
    }

    private void LogLaunchDynamicsIfNeeded(
        Vector3 velocityBeforeStep,
        Vector3 velocityAfterStep,
        Vector3 queuedDeltaApplied,
        Vector3 gravityAcceleration,
        float dt)
    {
        if (!IsWithinLaunchDiagnosticsWindow()) return;
        if (!CanEmitLaunchDiagnosticsNow()) return;

        Vector3 gravityDelta = gravityAcceleration * dt;
        Vector3 netDelta = velocityAfterStep - velocityBeforeStep;
        Debug.Log(
            $"[LaunchDynamics] ship={name} dt={dt:F4} speedBefore={velocityBeforeStep.magnitude:F3} speedAfter={velocityAfterStep.magnitude:F3} " +
            $"queuedDv={queuedDeltaApplied} gravityA={gravityAcceleration} gravityDv={gravityDelta} netDv={netDelta}"
        );
    }

    private void LogRawCollisionIfNeeded(Collision collision, string phase)
    {
        if (!IsWithinLaunchDiagnosticsWindow()) return;
        if (collision == null) return;

        Collider other = ResolveOtherCollider(collision);
        if (other == null) return;

        float now = Time.unscaledTime;
        bool sameCollider = other == lastRawLoggedCollider;
        if (sameCollider && (now - lastRawCollisionLogTime) < launchDiagnosticsLogIntervalSeconds) return;

        lastRawLoggedCollider = other;
        lastRawCollisionLogTime = now;

        ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
        Vector3 relativeVelocity = collision.relativeVelocity;
        string otherName = other.gameObject != null ? other.gameObject.name : "<null>";
        string otherRoot = (other.transform != null && other.transform.root != null) ? other.transform.root.name : "<null>";

        Debug.Log(
            $"[LaunchCollisionRaw] ship={name} phase={phase} target={otherName} root={otherRoot} contacts={collision.contactCount} " +
            $"point={contact.point} normal={contact.normal} relVel={relativeVelocity} relSpeed={relativeVelocity.magnitude:F3}"
        );
    }

    private Collider ResolveOtherCollider(Collision collision)
    {
        if (collision == null) return null;

        Collider collider = collision.collider;
        if (collider != null && collider.transform != null && !collider.transform.IsChildOf(transform))
        {
            return collider;
        }

        if (collision.gameObject == null) return null;
        return collision.gameObject.GetComponent<Collider>();
    }

    private bool TryHandleCelestialCollision(Collision collision)
    {
        if (!launched || collision == null) return false;

        Collider other = ResolveOtherCollider(collision);
        if (other == null) return false;
        if (!IsCelestialCollider(other)) return false;

        EnsureDependencies();
        if (spaceship != null)
        {
            spaceship.DestroyByCelestialCollision(other);
        }
        else
        {
            Destroy(gameObject);
        }
        return true;
    }

    private static bool IsCelestialCollider(Collider collider)
    {
        if (collider == null) return false;
        if (collider.CompareTag("Icon")) return false;
        return collider.GetComponentInParent<Gravity>() != null;
    }

}
