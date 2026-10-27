using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;      // empty child object at the character's feet
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Feel")]
    [SerializeField] private float coyoteTime = 0.1f;     // grace period after leaving a ledge
    [SerializeField] private float jumpBufferTime = 0.1f; // jump press remembered shortly before landing

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction jumpAction;

    private float horizontalInput;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isGrounded;

    /// <summary>True while the ground check touches the ground layer.</summary>
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

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

        // "Up" and "right" follow the player's rotation, so they are relative to the gravity
        Vector2 up = transform.up;
        Vector2 right = transform.right;
        float rightSpeed = Vector2.Dot(rb.linearVelocity, right);
        float upSpeed = Vector2.Dot(rb.linearVelocity, up);

        // Jump
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.linearVelocity = right * rightSpeed + up * jumpForce;
            upSpeed = jumpForce;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // Variable jump height: releasing early cuts the jump short
        if (jumpAction.WasReleasedThisFrame() && upSpeed > 0f)
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

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}