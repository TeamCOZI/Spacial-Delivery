using UnityEngine;

[DefaultExecutionOrder(30050)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpaceshipRendezvousController))]
[RequireComponent(typeof(OrbitRevolution))]
public sealed class SpaceshipCommittedOrbitRenderSync : MonoBehaviour
{
    private const float MinimumVectorSqrMagnitude = 0.000001f;

    private SpaceshipRendezvousController rendezvousController;
    private OrbitRevolution orbitRevolution;

    private void Awake()
    {
        CacheComponents();
    }

    private void LateUpdate()
    {
        CacheComponents();
        if (rendezvousController == null || orbitRevolution == null) return;
        if (!rendezvousController.IsOrbitCommitted) return;
        if (orbitRevolution.center == null) return;

        Vector3 centerToShip = orbitRevolution.GetInterpolatedCenterToOrbitVector();
        if (centerToShip.sqrMagnitude <= MinimumVectorSqrMagnitude) return;

        Vector3 renderPosition = orbitRevolution.GetInterpolatedLocalPosition();
        Vector3 radialDirection = centerToShip.normalized;
        Vector3 tangentialDirection = orbitRevolution.isClockwise
            ? new Vector3(radialDirection.y, -radialDirection.x, 0f)
            : new Vector3(-radialDirection.y, radialDirection.x, 0f);
        float z = Mathf.Atan2(tangentialDirection.y, tangentialDirection.x) * Mathf.Rad2Deg - 90f;

        transform.SetPositionAndRotation(renderPosition, Quaternion.Euler(0f, 0f, z));
    }

    private void CacheComponents()
    {
        if (rendezvousController == null) rendezvousController = GetComponent<SpaceshipRendezvousController>();
        if (orbitRevolution == null) orbitRevolution = GetComponent<OrbitRevolution>();
    }
}
