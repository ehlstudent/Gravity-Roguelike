using UnityEngine;

public class EnemyChaseSimple : MonoBehaviour
{
    public float speed = 2.5f;
    Transform player;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void FixedUpdate()
    {
        if (player == null) return;

        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }
}