using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class CaptureRangeHandler : MonoBehaviour
{
    private Satellite parentSatellite;
    private SphereCollider sphereCollider;

    void Awake()
    {
        parentSatellite = GetComponentInParent<Satellite>();
        if (parentSatellite == null)
        {
            Debug.LogError("Parent satellite not found.", this);
            gameObject.SetActive(false);
            return;
        }

        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    // void Start()
    // {
    //     UpdateRadius();
    // }

    // public void UpdateRadius()
    // {
    //     if (parentSatellite != null && sphereCollider != null)
    //     {
    //         sphereCollider.radius = parentSatellite.captureRange;
    //     }
    // }

    // private void OnTriggerEnter(Collider other)
    // {
    //     if (parentSatellite == null) return;

    //     if (other.CompareTag(parentSatellite.playerSpaceshipTag))
    //     {
    //         parentSatellite.CapturePlayerSpaceship(other.gameObject);
    //     }
    //     if (other.CompareTag(parentSatellite.packageTag))
    //     {
    //         parentSatellite.CapturePackage(other.gameObject);
    //     }
    // }
}