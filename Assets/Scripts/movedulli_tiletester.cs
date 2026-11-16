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

    private Rigidbody2D rb;
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
        baseMoveSpeed = moveSpeed;
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
        rb.linearVelocity = right * (horizontalInput * moveSpeed) + up * upSpeed;
    }

    private void AlignToGravity()
    {
        float targetAngle = GravityRotator.Instance != null ? GravityRotator.Instance.GravityAngle : 0f;
        if (Mathf.Approximately(Mathf.DeltaAngle(rb.rotation, targetAngle), 0f)) return;

        // Snap instantly; the camera rotates smoothly, so it still looks like a turn.
        // Setting the rotation directly works even with "Freeze Rotation" enabled.
        rb.rotation = targetAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
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