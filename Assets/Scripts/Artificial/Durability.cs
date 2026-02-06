using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Durability : MonoBehaviour
{
    [Header("Durability Settings")]
    public float durability = 100f;

    private void Update()
    {
        if (durability <= 0f) Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Prefab")) durability = 0f;
    }
}