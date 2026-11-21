using UnityEngine;
using UnityEngine.InputSystem;
using GravityRoguelike.Combat;   // for DamageInfo

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float knockbackLockDuration = 0.2f;   // seconds the input is ignored
    private float knockbackLockedUntil;                            // Time.time until which input is ignored


    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;      // empty child object at the character's feet
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.1f;     // grace period after leaving a ledge
    [SerializeField] private float jumpBufferTime = 0.1f; // jump press remembered shortly before landing

    [Header("Wall Handling")]
    [Tooltip("Distance in front of the player in which a wall stops sideways movement (avoids pressing into walls).")]
    [SerializeField] private float wallCheckDistance = 0.05f;
    [Tooltip("Rounds the corners of the BoxCollider2D so it does not snag on the seams between wall tiles.")]
    [SerializeField] private float colliderEdgeRadius = 0.05f;
    [Tooltip("Pushes the player out of walls after a gravity rotation (the collider is not exactly square).")]
    [SerializeField] private bool resolveOverlapAfterRotation = true;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
    private readonly Collider2D[] overlapHits = new Collider2D[8];
    private ContactFilter2D wallFilter;
    private Health health;
    private InputAction moveAction;
    private InputAction jumpAction;

    private float horizontalInput;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isGrounded;

    private float baseMoveSpeed;

    /// <summary>True while the ground check touches the ground layer.</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>Visual landing anticipation; reuses the feet, ground mask and gravity direction.</summary>
    public bool HasGroundBelow(float distance)
    {
        if (groundCheck == null || distance <= 0f) return false;
        Vector2 up = transform.up;
        int count = Physics2D.CircleCast(groundCheck.position, groundCheckRadius, -up,
            wallFilter, castHits, distance);
        for (int i = 0; i < count; i++)
            if (castHits[i].collider != bodyCollider && Vector2.Dot(castHits[i].normal, up) > 0.5f)
                return true;
        return false;
    }

    public float MoveSpeed => moveSpeed;
    public float BaseMoveSpeed => baseMoveSpeed;

    /// <summary>Item bonus (Schnelle Stiefel).</summary>
    public void SetMoveSpeed(float speed) => moveSpeed = Mathf.Max(0f, speed);

    /// <summary>Removes item bonuses, e.g. for a new run.</summary>
    public void ResetMoveSpeed()
    {
        if (baseMoveSpeed > 0f) moveSpeed = baseMoveSpeed; // 0 means Awake has not run yet.
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        baseMoveSpeed = moveSpeed;

        // Fast falls (gravity can point anywhere) must not tunnel into walls,
        // and interpolation removes the visible jitter along walls.
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Rounded corners slide over the seams between tiles instead of catching on them.
        // The box is shrunk by the radius so the outer size stays the same.
        if (bodyCollider is BoxCollider2D box && colliderEdgeRadius > 0f && box.edgeRadius <= 0f)
        {
            box.size -= Vector2.one * (2f * colliderEdgeRadius);
            box.edgeRadius = colliderEdgeRadius;
        }

        // Walls = the same layers as the ground; ignore triggers (items, portals, ...).
        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(groundLayer);
        health = GetComponent<Health>();
        health.Damaged += OnHit;

        // Move: A/D, arrow keys, gamepad left stick
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");
        moveAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftArrow")
            .With("Positive", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick/x");

        // Jump: Space, gamepad south button (A / Cross)
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    private void Update()
    {
        // Stand "upright" relative to the current gravity (see GravityRotator)
        AlignToGravity();

        // Read input every frame
        horizontalInput = moveAction.ReadValue<float>();

        if (jumpAction.WasPressedThisFrame())
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        // Ground check
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - Time.deltaTime;
        bool knockbackLocked = Time.time < knockbackLockedUntil;

        // "Up" and "right" follow the player's rotation, so they are relative to the gravity
        Vector2 up = transform.up;
        Vector2 right = transform.right;
        float rightSpeed = Vector2.Dot(rb.linearVelocity, right);
        float upSpeed = Vector2.Dot(rb.linearVelocity, up);

        // Jump
        if (!knockbackLocked && jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.linearVelocity = right * rightSpeed + up * jumpForce;
            upSpeed = jumpForce;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Variable jump height: releasing early cuts the jump short
        if (!knockbackLocked && jumpAction.WasReleasedThisFrame() && upSpeed > 0f)
            rb.linearVelocity = right * rightSpeed + up * (upSpeed * 0.5f);

        // Flip sprite to face movement direction
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalInput);
            transform.localScale = scale;
        }
    }

    private void FixedUpdate()
    {
        AlignToGravity();
        if (Time.time < knockbackLockedUntil) return;

        // Apply sideways movement in the physics step, keeping the speed along gravity
        Vector2 up = transform.up;
        Vector2 right = transform.right;
        float upSpeed = Vector2.Dot(rb.linearVelocity, up);
        float sideSpeed = horizontalInput * moveSpeed;

        // Don't keep pushing into a wall: that makes the player jitter, stick to the
        // wall while falling, or get caught on tile edges.
        if (Mathf.Abs(sideSpeed) > 0.01f && IsWallAhead(right * Mathf.Sign(sideSpeed)))
            sideSpeed = 0f;

        rb.linearVelocity = right * sideSpeed + up * upSpeed;
    }

    /// <summary>True if a solid collider is directly in front of the player in the given direction.</summary>
    private bool IsWallAhead(Vector2 direction)
    {
        if (bodyCollider == null) return false;
        int count = bodyCollider.Cast(direction, wallFilter, castHits, wallCheckDistance);
        for (int i = 0; i < count; i++)
        {
            // Only count surfaces that actually face us (walls), not floor/ceiling we graze.
            if (Vector2.Dot(castHits[i].normal, -direction) > 0.7f) return true;
        }
        return false;
    }

    private void AlignToGravity()
    {
        float targetAngle = GravityRotator.Instance != null ? GravityRotator.Instance.GravityAngle : 0f;
        if (Mathf.Approximately(Mathf.DeltaAngle(rb.rotation, targetAngle), 0f)) return;

        // Snap instantly; the camera rotates smoothly, so it still looks like a turn.
        // Setting the rotation directly works even with "Freeze Rotation" enabled.
        rb.rotation = targetAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

        if (resolveOverlapAfterRotation) ResolveWallOverlap();
    }

    /// <summary>
    /// After the snap rotation the collider can stick a little into a wall (it is not square),
    /// which makes the physics shove the player out sideways. Push it out cleanly instead.
    /// </summary>
    private void ResolveWallOverlap()
    {
        if (bodyCollider == null) return;
        Physics2D.SyncTransforms();   // the collider must use the new rotation for the queries below

        Vector2 totalPush = Vector2.zero;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            int count = bodyCollider.Overlap(wallFilter, overlapHits);
            bool moved = false;
            for (int i = 0; i < count; i++)
            {
                ColliderDistance2D distance = bodyCollider.Distance(overlapHits[i]);
                if (!distance.isValid || !distance.isOverlapped) continue;
                // Moving our closest point (A) onto the wall's point (B) separates the two.
                Vector2 push = distance.pointB - distance.pointA;
                rb.position += push;
                totalPush += push;
                Physics2D.SyncTransforms();
                moved = true;
            }
            if (!moved) break;
        }

        // Remove only the velocity that points back into the wall we were pushed out of.
        if (totalPush.sqrMagnitude > 0.0001f)
        {
            Vector2 outward = totalPush.normalized;
            float into = Vector2.Dot(rb.linearVelocity, outward);
            if (into < 0f) rb.linearVelocity -= outward * into;
        }
    }

    // Unsubscribes so the event doesn't point at a destroyed object.
    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnHit;
    }

    // Pushes away from the hit source and locks movement input briefly.
    private void OnHit(DamageInfo info)
    {
        if (info.knockbackForce <= 0f) return;

        Vector2 direction = ((Vector2)transform.position - info.sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;   // cancel current motion so the push is consistent
        rb.AddForce(direction * info.knockbackForce, ForceMode2D.Impulse);
        knockbackLockedUntil = Time.time + knockbackLockDuration;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
