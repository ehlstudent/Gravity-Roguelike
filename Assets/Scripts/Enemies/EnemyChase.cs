using System.Collections.Generic;
using UnityEngine;

public class EnemyChase : MonoBehaviour
{
    public RoomGrid grid;
    public Transform player;
    public float speed = 2.5f;
    private float repathInterval = 0.4f;
    public float bodyRadius = 0.3f;   // should roughly match half the collider size

    Rigidbody2D rb;
    List<Vector2Int> path;
    int waypointIndex;
    float repathTimer;

    // Runs once on creation; caches the Rigidbody2D so we don't look it up every tick.
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // random repathTimer so a swarm of bats doesn't cause CPU spikes   
        repathTimer = Random.Range(repathInterval - 0.1f, repathInterval + 0.1f);
    }

    // Runs every physics tick; re-plans on a timer and moves along the current path.
    void FixedUpdate()
    {
        // TODO 2: count the timer down, re-plan when it reaches zero
        repathTimer -= Time.fixedDeltaTime;
        if (repathTimer <= 0f)
        {
            Repath();
            repathTimer = repathInterval;
        }

        // TODO 3: follow the path
        if (path == null || waypointIndex >= path.Count) return;

        Vector2 target = grid.CellToWorld(path[waypointIndex]);
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (Vector2.Distance(next, target) < 0.05f)
            waypointIndex++;
    }


    // Asks the pathfinder for a route, then smooths it into fewer, straighter segments.
    void Repath()
    {
        Vector2Int start = grid.WorldToCell(transform.position);
        path = Pathfinder.FindPath(start, grid.WorldToCell(player.position), grid.FlyingNeighbors8);

        if (path != null)
            path = Pathfinder.Smooth(path, start, (a, b) => grid.HasClearLine(a, b, bodyRadius));

        waypointIndex = 0;
    }

    // Draws the current path and the waypoint we're heading for, visible in the Scene view.
    void OnDrawGizmos()
    {
        if (path == null) return;
        for (int i = 0; i < path.Count; i++)
        {
            Gizmos.color = i == waypointIndex ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(grid.CellToWorld(path[i]), Vector3.one * 0.3f);
        }
    }
}