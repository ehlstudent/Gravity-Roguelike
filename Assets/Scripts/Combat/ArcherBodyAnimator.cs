using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Archer visuals: the same live physics observations as the Warrior, with Shoot priority.</summary>
    [DefaultExecutionOrder(105)]
    [RequireComponent(typeof(Animator))]
    public class ArcherBodyAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController2D player;
        [SerializeField] private ArcherBow bow;
        [SerializeField] private ActiveWeapon weapon;
        [SerializeField, Min(0f)] private float risingVelocityThreshold = 0.1f;
        [Tooltip("Extra distance below the existing feet check. 0 waits for contact.")]
        [SerializeField, Min(0f)] private float landCheckDistance = 0.25f;
        private Animator animator;
        private Rigidbody2D body;
        private Vector3 visualScale;
        private static readonly int Running = Animator.StringToHash("Running");
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int Rising = Animator.StringToHash("IsRising");
        private static readonly int NearGround = Animator.StringToHash("NearGround");
        private static readonly int Shooting = Animator.StringToHash("Shooting");
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private static readonly int ShootSpeed = Animator.StringToHash("ShootSpeed");
        private static readonly int ShootState = Animator.StringToHash("Base Layer.Shoot");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = player.GetComponent<Rigidbody2D>();
            visualScale = transform.localScale;
        }

        private void OnEnable()
        {
            bow.ShotStarted += BeginShoot;
            bow.ShotFinished += EndShoot;
        }

        private void OnDisable()
        {
            bow.ShotStarted -= BeginShoot;
            bow.ShotFinished -= EndShoot;
            EndShoot();
        }

        private void Update()
        {
            float velocity = Vector2.Dot(body.linearVelocity, player.transform.up);
            bool grounded = player.IsGrounded;
            bool rising = velocity > risingVelocityThreshold;
            animator.SetBool(Grounded, grounded);
            animator.SetFloat(VerticalVelocity, velocity);
            animator.SetBool(Rising, rising);
            animator.SetBool(NearGround, !grounded && !rising && player.HasGroundBelow(landCheckDistance));
            animator.SetBool(Running, grounded && Mathf.Abs(Vector2.Dot(body.linearVelocity, player.transform.right)) > 0.1f);
        }

        private void LateUpdate()
        {
            if (bow.TryGetShootProgress(out float progress))
            {
                animator.Play(ShootState, 0, progress);
                animator.Update(0f);
            }
            float facing = 1f;
            if (bow.IsShooting)
            {
                Vector3 localAim = player.transform.InverseTransformVector(weapon.transform.TransformVector(Vector3.right));
                if (Mathf.Abs(localAim.x) > 0.001f) facing = Mathf.Sign(localAim.x);
            }
            transform.localScale = new Vector3(visualScale.x * facing, visualScale.y, visualScale.z);
        }

        private void BeginShoot(float duration)
        {
            animator.SetFloat(ShootSpeed, 1f / duration);
            animator.SetBool(Shooting, true);
            animator.SetTrigger(Shoot);
        }

        private void EndShoot()
        {
            animator.SetBool(Shooting, false);
            animator.ResetTrigger(Shoot);
        }
    }
}
