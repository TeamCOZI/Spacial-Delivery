using UnityEngine;

[DisallowMultipleComponent]
public class LauncherSpaceshipStock : MonoBehaviour
{
    [SerializeField, Min(0)] private int availableSpaceshipCount;

    public int AvailableSpaceshipCount => Mathf.Max(0, availableSpaceshipCount);

    public void AddAvailableSpaceships(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        availableSpaceshipCount = Mathf.Max(0, availableSpaceshipCount + amount);
    }

    public bool TryConsumeAvailableSpaceships(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (AvailableSpaceshipCount < amount)
        {
            return false;
        }

        availableSpaceshipCount -= amount;
        return true;
    }

    public static int GetAvailableSpaceshipCount(ArtificialSatellite ownerSatellite)
    {
        LauncherSpaceshipStock stock = ResolveForSatellite(ownerSatellite, createIfMissing: false);
        return stock != null ? stock.AvailableSpaceshipCount : 0;
    }

    public static int GetAvailableSpaceshipCount(Transform launcher)
    {
        LauncherSpaceshipStock stock = ResolveForLauncher(launcher, createIfMissing: false);
        return stock != null ? stock.AvailableSpaceshipCount : 0;
    }

    public static LauncherSpaceshipStock ResolveForLauncher(Transform launcher, bool createIfMissing)
    {
        ArtificialSatellite ownerSatellite = ResolveOwnerSatellite(launcher);
        return ResolveForSatellite(ownerSatellite, createIfMissing);
    }

    public static LauncherSpaceshipStock ResolveForSatellite(ArtificialSatellite ownerSatellite, bool createIfMissing)
    {
        if (ownerSatellite == null)
        {
            return null;
        }

        if (createIfMissing)
        {
            return ComponentUtility.GetOrAddComponent<LauncherSpaceshipStock>(ownerSatellite.gameObject);
        }

        return ownerSatellite.GetComponent<LauncherSpaceshipStock>();
    }

    private static ArtificialSatellite ResolveOwnerSatellite(Transform launcher)
    {
        if (launcher == null)
        {
            return null;
        }

        AssemblyPartFocus partFocus = launcher.GetComponent<AssemblyPartFocus>();
        if (partFocus == null)
        {
            partFocus = launcher.GetComponentInParent<AssemblyPartFocus>();
        }

        if (partFocus != null && partFocus.OwnerSatellite != null)
        {
            return partFocus.OwnerSatellite;
        }

        return launcher.GetComponentInParent<ArtificialSatellite>();
    }
}
