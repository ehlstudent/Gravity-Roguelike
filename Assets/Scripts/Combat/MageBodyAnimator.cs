using UnityEngine;

namespace GravityRoguelike.Combat
{
    /// <summary>Live body observations only. Movement never cancels or pauses the separate staff.</summary>
    [DefaultExecutionOrder(105)]
    [RequireComponent(typeof(Animator))]
    public class MageBodyAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController2D player;
        [SerializeField] private MageSpellController spells;
        [SerializeField, Min(0f)] private float risingVelocityThreshold = 0.1f;
        [SerializeField, Min(0f)] private float landCheckDistance = 0.25f;
        private Animator animator;
        private Rigidbody2D body;
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int Velocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int Rising = Animator.StringToHash("IsRising");
        private static readonly int NearGround = Animator.StringToHash("NearGround");
        private static readonly int Running = Animator.StringToHash("Running");
        private static readonly int CastingA = Animator.StringToHash("IsCastingA");
        private static readonly int CastingB = Animator.StringToHash("IsCastingB");
        private static readonly int LoopingB = Animator.StringToHash("IsLoopingB");
        private static readonly int BCastState = Animator.StringToHash("Base Layer.Spell B Body");
        private static readonly int BLoopState = Animator.StringToHash("Base Layer.Spell B Loop");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            body = player.GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            float velocity = Vector2.Dot(body.linearVelocity, player.transform.up);
            bool grounded = player.IsGrounded;
            bool rising = velocity > risingVelocityThreshold;
            animator.SetBool(Grounded, grounded);
            animator.SetFloat(Velocity, velocity);
            animator.SetBool(Rising, rising);
            animator.SetBool(NearGround, !grounded && !rising && player.HasGroundBelow(landCheckDistance));
            animator.SetBool(Running, Mathf.Abs(Vector2.Dot(body.linearVelocity, player.transform.right)) > 0.1f);
            animator.SetBool(CastingA, spells.IsCastingA);
            animator.SetBool(CastingB, spells.IsCastingB);
            animator.SetBool(LoopingB, spells.IsLoopingB);
        }

        private void LateUpdate()
        {
            // Air/landing keeps full body priority. Otherwise follow the staff's exact B phase,
            // even when returning from a jump partway through the ongoing swing.
            if (player.IsGrounded && !animator.GetCurrentAnimatorStateInfo(0).IsName("Land") &&
                spells.TryGetSpellBProgress(out bool looping, out float progress))
            {
                animator.Play(looping ? BLoopState : BCastState, 0, progress);
                animator.Update(0f);
            }
        }
    }
}
