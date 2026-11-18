using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Visuals only: observes movement and the sword's accepted attacks.</summary>
    [DefaultExecutionOrder(105)]
    [RequireComponent(typeof(Animator))]
    public class PlayerBodyAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController2D player;
        [SerializeField] private Sword sword;
        [SerializeField] private ActiveWeapon weapon;

        [Header("Air animation")]
        [Tooltip("Velocity away from gravity above this value uses Jump Up; otherwise use Fall.")]
        [SerializeField, Min(0f)] private float risingVelocityThreshold = 0.1f;
        [Tooltip("Extra ground-check distance below the feet for anticipating Land. 0 waits for contact.")]
        [SerializeField, Min(0f)] private float landCheckDistance = 0.25f;
        private Animator animator;
        private Rigidbody2D body;
        private static readonly int Running = Animator.StringToHash("Running");
        private static readonly int Stabbing = Animator.StringToHash("Stabbing");
        private static readonly int Attack = Animator.StringToHash("Attack A");
        private static readonly int StabSpeed = Animator.StringToHash("StabSpeed");
        private static readonly int Slashing = Animator.StringToHash("Slashing");
        private static readonly int Slash = Animator.StringToHash("Slash");
        private static readonly int SlashSpeed = Animator.StringToHash("SlashSpeed");
        private static readonly int SlashState = Animator.StringToHash("Base Layer.Slash");
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int Rising = Animator.StringToHash("IsRising");
        private static readonly int NearGround = Animator.StringToHash("NearGround");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = player.GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            sword.StabStarted += BeginStab;
            sword.StabFinished += EndStab;
            sword.SlashStarted += BeginSlash;
            sword.SlashFinished += EndSlash;
        }

        private void OnDisable()
        {
            sword.StabStarted -= BeginStab;
            sword.StabFinished -= EndStab;
            sword.SlashStarted -= BeginSlash;
            sword.SlashFinished -= EndSlash;
            EndStab();
            EndSlash();
        }

        private void Update()
        {
            // Keep tracking physics during attacks. These are live observations, not saved air states.
            float verticalVelocity = Vector2.Dot(body.linearVelocity, player.transform.up);
            bool grounded = player.IsGrounded;
            bool rising = verticalVelocity > risingVelocityThreshold;
            animator.SetBool(Grounded, grounded);
            animator.SetFloat(VerticalVelocity, verticalVelocity);
            // Animator conditions cannot compare two parameters, so expose the tunable comparison.
            animator.SetBool(Rising, rising);
            animator.SetBool(NearGround, !grounded && !rising && player.HasGroundBelow(landCheckDistance));
            float sidewaysSpeed = Mathf.Abs(Vector2.Dot(body.linearVelocity, player.transform.right));
            animator.SetBool(Running, grounded && sidewaysSpeed > 0.1f);
        }

        private void LateUpdate()
        {
            // Both Animators have evaluated this frame. Sample the body at the actual
            // sword progress so either swing stays in phase, including speed changes.
            if (sword.TryGetSlashProgress(out float progress))
            {
                animator.Play(SlashState, 0, progress);
                animator.Update(0f);
            }

            // Keep movement-facing normally; only the visual turns towards an attack.
            // Never change the player's transform or the weapon's mouse aim.
            float facing = 1f;
            if (sword.IsAttacking)
            {
                Vector3 localAim = player.transform.InverseTransformVector(
                    weapon.transform.TransformVector(Vector3.right));
                if (Mathf.Abs(localAim.x) > 0.001f) facing = Mathf.Sign(localAim.x);
            }
            transform.localScale = new Vector3(facing, 1f, 1f);
        }

        private void BeginStab(float duration)
        {
            animator.SetFloat(StabSpeed, 1f / duration);
            animator.SetBool(Stabbing, true);
            animator.SetTrigger(Attack);
        }

        private void EndStab()
        {
            animator.SetBool(Stabbing, false);
            animator.ResetTrigger(Attack);
        }

        private void BeginSlash(float duration)
        {
            // Body and sword clips share the same normalized one-second timeline.
            animator.SetFloat(SlashSpeed, 1f / duration);
            animator.SetBool(Slashing, true);
            animator.SetTrigger(Slash);
        }

        private void EndSlash()
        {
            animator.SetBool(Slashing, false);
            animator.ResetTrigger(Slash);
        }
    }
}
