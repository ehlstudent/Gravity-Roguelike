using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates the global 2D gravity in 90-degree steps (default: Q and R).
/// The player aligns itself to the gravity, and CameraFollow2D rotates along,
/// so on screen it looks as if the room itself is rotating.
/// Put exactly one of these on an empty GameObject in each scene.
/// </summary>
public class GravityRotator : MonoBehaviour
{
    public static GravityRotator Instance { get; private set; }

    // Static values can survive between Play sessions in the editor (when "Reload Domain"
    // is turned off), same as in Portal.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Rotation")]
    [Tooltip("Degrees per key press. With the camera rotating along, a positive step turns the room clockwise on screen.")]
    [SerializeField] private float stepAngle = 90f;
    [Tooltip("Minimum time between two rotations, in seconds.")]
    [SerializeField] private float cooldown = 0.25f;
    [Tooltip("If enabled, the gravity can only be rotated while the player is standing on the ground.")]
    [SerializeField] private bool onlyWhenGrounded = false;
    [SerializeField] private PlayerController2D player;   // optional, found automatically
    [Tooltip("Writes a Console message on start and on every rotation, to check that it works.")]
    [SerializeField] private bool logRotations = true;

    /// <summary>0 = normal gravity (down). Positive values rotate the gravity counterclockwise in world space.</summary>
    public float GravityAngle { get; private set; }

    /// <summary>Rotation that turns "normal" up/down into the current up/down.</summary>
    public Quaternion GravityRotation => Quaternion.Euler(0f, 0f, GravityAngle);

    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;
    private Vector2 defaultGravity;
    private float gravityStrength;
    private float cooldownTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GravityRotator: there is already one in this scene; this one is disabled.", this);
            enabled = false;
            return;
        }
        Instance = this;

        // Remember the project gravity so it can be restored when the scene ends.
        defaultGravity = Physics2D.gravity;
        gravityStrength = defaultGravity.magnitude;

        // Q: room turns counterclockwise, E: room turns clockwise. Gamepad: shoulder buttons.
        rotateLeftAction = new InputAction("RotateLeft", InputActionType.Button);
        rotateLeftAction.AddBinding("<Keyboard>/q");
        rotateLeftAction.AddBinding("<Gamepad>/leftShoulder");

        rotateRightAction = new InputAction("RotateRight", InputActionType.Button);
        rotateRightAction.AddBinding("<Keyboard>/e");
        rotateRightAction.AddBinding("<Gamepad>/rightShoulder");
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (logRotations)
            Debug.Log($"GravityRotator active (Q = left, E/R = right). Player found: {(player != null ? player.name : "none")}.", this);
    }

    private void OnEnable()
    {
        rotateLeftAction?.Enable();
        rotateRightAction?.Enable();
    }

    private void OnDisable()
    {
        rotateLeftAction?.Disable();
        rotateRightAction?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        // Physics2D.gravity is global: reset it so the next scene starts with normal gravity.
        Physics2D.gravity = defaultGravity;
        Instance = null;
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f) return;
        if (onlyWhenGrounded && player != null && !player.IsGrounded) return;

        if (rotateLeftAction.WasPressedThisFrame()) Rotate(-stepAngle);
        else if (rotateRightAction.WasPressedThisFrame()) Rotate(stepAngle);
    }

    /// <summary>Rotates the gravity by the given angle in degrees.</summary>
    public void Rotate(float degrees)
    {
        GravityAngle = Mathf.Repeat(GravityAngle + degrees, 360f);

        Vector2 down = GravityRotation * Vector2.down;
        // Remove tiny floating-point errors for the usual 90-degree steps.
        down = new Vector2(Mathf.Round(down.x * 1000f) / 1000f, Mathf.Round(down.y * 1000f) / 1000f);
        Physics2D.gravity = down * gravityStrength;

        cooldownTimer = cooldown;
        if (logRotations) Debug.Log($"Gravity rotated: angle {GravityAngle}, gravity {Physics2D.gravity}.", this);
    }
}