using UnityEngine;

public static class SpaceshipFocusUtility
{
    public static bool TryResolveSpaceship(Transform target, out Spaceship spaceship)
    {
        spaceship = null;
        if (target == null) return false;

        spaceship = target.GetComponentInParent<Spaceship>();
        if (spaceship != null) return true;

        if (TryResolveFocusProxy(target, out SpaceshipFocusProxy proxy))
        {
            spaceship = proxy.OwnerSpaceship;
        }

        return spaceship != null;
    }

    public static bool TryResolveFocusProxy(Transform target, out SpaceshipFocusProxy proxy)
    {
        proxy = null;
        if (target == null) return false;

        proxy = target.GetComponentInParent<SpaceshipFocusProxy>();
        return proxy != null && proxy.OwnerSpaceship != null;
    }

    public static Transform ResolveFocusTarget(Spaceship spaceship)
    {
        if (spaceship == null) return null;
        return spaceship.FocusTarget != null ? spaceship.FocusTarget : spaceship.transform;
    }
}
