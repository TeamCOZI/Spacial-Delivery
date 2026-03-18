using UnityEngine;

public static class SpaceshipRendezvousUtility
{
    public readonly struct Context
    {
        public readonly Spaceship spaceship;
        public readonly SpaceshipFlightController flightController;
        public readonly GravityAffectedMover mover;
        public readonly WorldPosition spaceshipWorldPosition;
        public readonly Transform target;
        public readonly OrbitRevolution targetOrbit;
        public readonly Gravity targetGravity;

        public Context(
            Spaceship spaceship,
            SpaceshipFlightController flightController,
            GravityAffectedMover mover,
            WorldPosition spaceshipWorldPosition,
            Transform target,
            OrbitRevolution targetOrbit,
            Gravity targetGravity)
        {
            this.spaceship = spaceship;
            this.flightController = flightController;
            this.mover = mover;
            this.spaceshipWorldPosition = spaceshipWorldPosition;
            this.target = target;
            this.targetOrbit = targetOrbit;
            this.targetGravity = targetGravity;
        }
    }

    public static bool ShouldReserveSpaceInput()
    {
        if (!SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out Spaceship spaceship) || spaceship == null)
        {
            return false;
        }

        SpaceshipRendezvousController rendezvousController = spaceship.GetComponent<SpaceshipRendezvousController>();
        if (rendezvousController != null)
        {
            return rendezvousController.ShouldReserveActivationInput();
        }

        if (!TryResolveContext(spaceship, out Context context)) return false;
        return context.flightController != null && context.flightController.CanApplyAutomatedThrust();
    }

    public static bool TryResolveFocusedContext(out Context context)
    {
        context = default;
        if (!SpaceshipFocusUtility.TryResolveSpaceship(FocusManager.currentFocus, out Spaceship spaceship) || spaceship == null)
        {
            return false;
        }

        return TryResolveContext(spaceship, out context);
    }

    public static bool TryResolveContext(Spaceship spaceship, out Context context)
    {
        context = default;
        if (spaceship == null) return false;

        Transform target = spaceship.CurrentTarget;
        if (target == null) return false;

        Gravity targetGravity = target.GetComponent<Gravity>();
        if (targetGravity == null || targetGravity.GravityRadius <= 0) return false;

        OrbitRevolution targetOrbit = target.GetComponent<OrbitRevolution>();
        SpaceshipFlightController flightController = spaceship.GetComponent<SpaceshipFlightController>();
        GravityAffectedMover mover = spaceship.GetComponent<GravityAffectedMover>();
        WorldPosition spaceshipWorldPosition = spaceship.GetComponent<WorldPosition>();
        if (flightController == null || mover == null || spaceshipWorldPosition == null) return false;

        context = new Context(
            spaceship,
            flightController,
            mover,
            spaceshipWorldPosition,
            target,
            targetOrbit,
            targetGravity);
        return true;
    }

    public static Double3 ResolveWorldPosition(Transform source)
    {
        if (source == null) return Double3.Zero;

        WorldPosition worldPosition = source.GetComponent<WorldPosition>();
        if (worldPosition != null)
        {
            return worldPosition.worldPosition;
        }

        if (CoreRuntimeAccess.TryGetLargeWorldCoordinator(out LargeWorldCoordinator coordinator))
        {
            return coordinator.ToWorld(source.position);
        }

        return (Double3)source.position;
    }

    public static Vector3 ResolvePlanarVelocity(Transform source)
    {
        if (source == null) return Vector3.zero;

        OrbitRevolution orbit = source.GetComponent<OrbitRevolution>();
        if (orbit != null)
        {
            Vector3 orbitalVelocity = orbit.GetCurrentOrbitalVelocity();
            orbitalVelocity.z = 0f;
            return orbitalVelocity;
        }

        Rigidbody rigidbodyComponent = source.GetComponent<Rigidbody>();
        if (rigidbodyComponent != null)
        {
            Vector3 rigidbodyVelocity = rigidbodyComponent.linearVelocity;
            rigidbodyVelocity.z = 0f;
            return rigidbodyVelocity;
        }

        return Vector3.zero;
    }

    public static Vector3 ResolvePlanarAcceleration(Transform source)
    {
        if (source == null) return Vector3.zero;

        OrbitRevolution orbit = source.GetComponent<OrbitRevolution>();
        if (orbit != null)
        {
            Vector3 orbitalAcceleration = orbit.GetCurrentOrbitalAcceleration();
            orbitalAcceleration.z = 0f;
            return orbitalAcceleration;
        }

        return Vector3.zero;
    }
}
