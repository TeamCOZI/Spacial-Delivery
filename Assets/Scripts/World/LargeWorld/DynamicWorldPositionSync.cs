using UnityEngine;

[DefaultExecutionOrder(29900)]
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldPosition))]
public class DynamicWorldPositionSync : MonoBehaviour
{
    private WorldPosition worldPositionComponent;

    private void Awake()
    {
        worldPositionComponent = GetComponent<WorldPosition>();
    }

    private void LateUpdate()
    {
        if (worldPositionComponent == null) return;
        LargeWorldCoordinator coordinator = LargeWorldCoordinator.Instance;

        if (coordinator != null)
        {
            worldPositionComponent.SetWorldPosition(coordinator.ToWorld(transform.position));
        }
        else
        {
            worldPositionComponent.SetWorldPosition((Double3)transform.position);
        }
    }
}
