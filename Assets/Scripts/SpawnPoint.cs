using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "default";

    private void Start()
    {
        // Only react if a portal asked for exactly this spawn point
        if (string.IsNullOrEmpty(Portal.NextSpawnId) || Portal.NextSpawnId != spawnId) return;

        Portal.NextSpawnId = null;        // consumed, so starting this scene directly later won't teleport the player

        PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
        if (player == null)
        {
            Debug.LogWarning("SpawnPoint: no PlayerController2D found in this scene.", this);
            return;
        }

        player.transform.position = transform.position;

        if (player.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = transform.position;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
