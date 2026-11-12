using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WalkerEnemy : MonoBehaviour
{
    // The two behaviours of the walker.
    private enum State { Patrolling, Chasing }

    [SerializeField] private RoomGrid roomGrid;
    [SerializeField] private Transform player;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float maxFallSpeed = 12f;   // cap, prevents tunnelling through the ground
    [SerializeField] private float aggroRadius = 6f;     // start chasing inside this distance
    [SerializeField] private float loseRadius = 10f;     // give up outside this (larger, so it doesn't flicker)
    [SerializeField] private float bodyRadius = 0.4f;    // used for the line-of-sight check
    [SerializeField] private float repathInterval = 0.3f;
    [SerializeField] private float giveUpCooldown = 2f;  // pause before it may notice the player again
    [SerializeField] private float jumpHeightInCells = 1.5f;   // a little more than one cell, so it clears the edge
    private bool jumpRequested;                                // set by the chase logic, used once in ComputeVelocity

    private Rigidbody2D rb;
    private State state = State.Patrolling;
    private int patrolSign = 1;                // +1 or -1: patrol direction along the ground
    private List<Vector2Int> path;             // remaining waypoints while chasing
    private float nextRepathTime;
    private float noAggroUntil;                // time until which the walker ignores the player
    [SerializeField] private float chaseSpeed = 4f;   // speed while chasing (patrol keeps using walkSpeed)
    [SerializeField] private float chaseAcceleration = 4f;   // speed gained per second (2 -> 4 takes ~0.5 s)
    private float currentSpeed;                              // the speed we are really using right now

    // Caches the Rigidbody and stops it from tumbling. Gravity comes from the Gravity Scale.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
    }

    // Each physics tick: decide how fast to walk along the ground, then build the velocity.
    private void FixedUpdate()
    {
        Vector2Int g = roomGrid.gravity;
        Vector2 gravityDir = new Vector2(g.x, g.y);
        Vector2 tangent = new Vector2(-g.y, g.x);   // along the ground, works for all four gravities
        Vector2Int cell = roomGrid.WorldToCell(rb.position);

        float tangentSpeed = state == State.Patrolling
            ? PatrolSpeed(cell, g)
            : ChaseSpeed(cell, g, tangent);

        rb.linearVelocity = ComputeVelocity(gravityDir, tangent, tangentSpeed);
    }

    // Keeps the physics-driven fall speed and adds walking speed along the ground.
    // A requested jump replaces the fall speed with an upward speed, once.
    private Vector2 ComputeVelocity(Vector2 gravityDir, Vector2 tangent, float tangentSpeed)
    {
        float fallSpeed = Vector2.Dot(rb.linearVelocity, gravityDir);

        if (jumpRequested)
        {
            fallSpeed = -JumpSpeed();   // negative along gravity = upwards
            jumpRequested = false;
        }

        fallSpeed = Mathf.Min(fallSpeed, maxFallSpeed);
        return gravityDir * fallSpeed + tangent * tangentSpeed;
    }

    // Patrol logic: switch to chasing if the player is close, otherwise walk and turn at edges.
    private float PatrolSpeed(Vector2Int cell, Vector2Int g)
    {
        if (CanNoticePlayer(cell))
        {
            state = State.Chasing;
            currentSpeed = walkSpeed;   // new: start the ramp-up from the patrol speed

            state = State.Chasing;
            path = null;
            nextRepathTime = 0f;     // plan immediately
            return 0f;
        }

        if (!roomGrid.IsStandable(cell)) return 0f;   // in the air: just fall

        TurnIfAtEdge(cell, g);
        return patrolSign * walkSpeed;
    }

    // Reverses direction at walls and ledges, once the walker reached the middle of its cell.
    private void TurnIfAtEdge(Vector2Int cell, Vector2Int g)
    {
        Vector2Int tangent = new Vector2Int(-g.y, g.x);
        Vector2Int next = cell + tangent * patrolSign;

        Vector2 offset = rb.position - roomGrid.CellToWorld(cell);
        float progress = Vector2.Dot(offset, new Vector2(tangent.x, tangent.y) * patrolSign);

        if (progress >= 0f && !roomGrid.IsStandable(next))
            patrolSign = -patrolSign;
    }

    // True if the player is close, visible, and the walker is not in its "gave up" pause.
    private bool CanNoticePlayer(Vector2Int cell)
    {
        if (Time.time < noAggroUntil) return false;

        float dist = Vector2.Distance(rb.position, player.position);
        if (dist > aggroRadius) return false;

        Vector2Int playerCell = roomGrid.WorldToCell(player.position);
        return roomGrid.HasClearLine(cell, playerCell, bodyRadius);
    }

    // Chase logic: ramp up speed, re-plan while on the ground, jump when the path says so,
    // and steer towards the next waypoint.
    private float ChaseSpeed(Vector2Int cell, Vector2Int g, Vector2 tangent)
    {
        if (Vector2.Distance(rb.position, player.position) > loseRadius)
        {
            StopChasing("player out of range", false);
            return 0f;
        }

        // Ramp-up: accelerate from patrol speed to full chase speed.
        currentSpeed = Mathf.MoveTowards(currentSpeed, chaseSpeed, chaseAcceleration * Time.fixedDeltaTime);

        bool grounded = roomGrid.IsStandable(cell);

        // Re-plan only while standing (or when there is no path yet). Re-planning in mid-jump
        // would pull the walker back towards the cell below it.
        if (Time.time >= nextRepathTime && (grounded || path == null))
        {
            Repath(cell);
            nextRepathTime = Time.time + repathInterval;

            if (path == null)
            {
                StopChasing("no path to player", true);
                return 0f;
            }
        }

        if (path == null) return 0f;

        // Drop waypoints we already reached.
        while (path.Count > 0 && path[0] == cell) path.RemoveAt(0);
        if (path.Count == 0) return 0f;

        // Jump: only from a standing position, otherwise it would fire again while rising.
        float verticalSpeed = Vector2.Dot(rb.linearVelocity, new Vector2(g.x, g.y));
        bool resting = Mathf.Abs(verticalSpeed) < 0.5f;
        if (grounded && resting && IsJumpStep(cell, path[0], g)) jumpRequested = true;

        Vector2 aim = roomGrid.CellToWorld(path[0]);

        // Stepping sideways off a ledge: aim a bit past the cell centre so the body clears the edge.
        if (!roomGrid.IsStandable(path[0]) && path[0] != cell + roomGrid.gravity)
        {
            float side = Mathf.Sign(Vector2.Dot(aim - rb.position, tangent));
            aim += tangent * side * 0.3f;
        }

        float offset = Vector2.Dot(aim - rb.position, tangent);
        if (Mathf.Abs(offset) < 0.05f) return 0f;

        // Proportional speed, capped at the current (ramping) speed.
        return Mathf.Clamp(offset / Time.fixedDeltaTime, -currentSpeed, currentSpeed);
    }

    // Plans a walking path to the ground cell below the player (null if there is none).
    private void Repath(Vector2Int cell)
    {
        Vector2Int playerCell = roomGrid.WorldToCell(player.position);
        if (!roomGrid.TryFindGround(playerCell, out Vector2Int goal))
        {
            path = null;
            return;
        }
        path = Pathfinder.FindPath(cell, goal, roomGrid.WalkingNeighbors);
    }

    // Returns to patrolling and ignores the player for a short while, so it doesn't flicker.
    private void StopChasing()
    {
        state = State.Patrolling;
        path = null;
        noAggroUntil = Time.time + giveUpCooldown;
    }
    // Keeps loseRadius larger than aggroRadius, whatever is typed in the Inspector.
    private void OnValidate()
    {
        loseRadius = Mathf.Max(loseRadius, aggroRadius + 1f);
    }

    // Shows both ranges in the Scene view when the walker is selected.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRadius);
    }

    // Returns to patrolling. The cooldown is only used when there is no path, so that
    // going out of range doesn't block the walker from noticing the player again.
    private void StopChasing(string reason, bool useCooldown)
    {
        Debug.Log($"{name} stopped chasing: {reason}");
        state = State.Patrolling;
        path = null;
        if (useCooldown) noAggroUntil = Time.time + giveUpCooldown;
    }

    // Launch speed needed to reach the wanted jump height: v = sqrt(2 * a * h).
    private float JumpSpeed()
    {
        float cellSize = roomGrid.solid.cellSize.y;
        float accel = Physics2D.gravity.magnitude * rb.gravityScale;   // same gravity the player feels
        return Mathf.Sqrt(2f * accel * jumpHeightInCells * cellSize);
    }

    // True if 'target' is the cell diagonally up and over from 'cell' (one step up, one to the side).
    private bool IsJumpStep(Vector2Int cell, Vector2Int target, Vector2Int g)
    {
        Vector2Int d = target - cell;
        Vector2Int tangent = new Vector2Int(-g.y, g.x);
        return d == tangent - g || d == -tangent - g;
    }
}