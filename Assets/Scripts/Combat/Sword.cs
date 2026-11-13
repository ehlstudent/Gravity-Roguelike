using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Animator))]
    public class Sword : MonoBehaviour
    {
        [Header("Input and animation")]
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private SlashAnim slashPrefab;
        [SerializeField] private Transform slashAnimSpawnPoint;
        [SerializeField, Min(0.01f)] private float attackDuration = 0.42f;
        [Tooltip("Minimum time between attack starts. An unfinished swing also blocks attacks.")]
        [SerializeField, Min(0f)] private float attackCooldown = 0.45f;

        [Header("Damage")]
        [SerializeField] private Transform hitPoint;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.45f;
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("Only colliders on these layers with a Health component on a parent can be hit.")]
        [SerializeField] private LayerMask targetLayers = 1;
        [Tooltip("Solid level geometry blocks damage. Uses the existing Ground layer.")]
        [SerializeField] private LayerMask blockingLayers = 8;

        private static readonly int AttackParameter = Animator.StringToHash("Attack");
        private static readonly int SwingUpParameter = Animator.StringToHash("SwingUp");
        private readonly HashSet<Health> hitTargets = new HashSet<Health>();
        private readonly List<Collider2D> hitColliders = new List<Collider2D>();
        private Animator animator;
        private InputAction ownedAttackAction;
        private Transform wielder;
        private SlashAnim activeSlash;
        private bool hitWindowOpen;
        private bool swingUp;
        private float attackStartedAt;
        private float nextAttackAt;

        public bool IsAttacking { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ActiveWeapon holder = GetComponentInParent<ActiveWeapon>();
            wielder = holder != null ? holder.transform.parent : transform.root;
        }

        private void OnEnable()
        {
            if (attackAction == null || attackAction.action == null)
            {
                Debug.LogWarning("Sword: assign the existing Player/Attack input action.", this);
                return;
            }

            // Own only this action's lifecycle, never enable/disable the shared player map.
            ownedAttackAction = attackAction.action.Clone();
            ownedAttackAction.performed += OnAttack;
            ownedAttackAction.Enable();
        }

        private void OnDisable()
        {
            if (ownedAttackAction != null)
            {
                ownedAttackAction.performed -= OnAttack;
                ownedAttackAction.Disable();
                ownedAttackAction.Dispose();
                ownedAttackAction = null;
            }

            CancelAttack();
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            TryAttack();
        }

        /// <summary>Starts one alternating swing if ready. Animation events gate damage.</summary>
        public bool TryAttack()
        {
            if (!isActiveAndEnabled || IsAttacking || Time.time < nextAttackAt) return false;
            if (animator == null || animator.runtimeAnimatorController == null ||
                slashPrefab == null || slashAnimSpawnPoint == null || hitPoint == null ||
                wielder == null || attackDuration <= 0f || attackCooldown < 0f ||
                hitRadius <= 0f || damage <= 0)
            {
                Debug.LogWarning("Sword: assign its Animator, slash prefab/spawn point and hit point, " +
                    "and use positive duration, damage and hit radius with a non-negative cooldown.", this);
                return false;
            }

            hitTargets.Clear();
            hitWindowOpen = false;
            IsAttacking = true;
            attackStartedAt = Time.time;
            nextAttackAt = Time.time + attackCooldown;
            swingUp = !swingUp;
            animator.speed = 1f / attackDuration; // Swing clips are one second long.
            animator.SetBool(SwingUpParameter, swingUp);
            animator.SetTrigger(AttackParameter);
            return true;
        }

        private void LateUpdate()
        {
            if (!IsAttacking) return;
            if (Time.time - attackStartedAt > attackDuration + 0.1f)
            {
                CancelAttack();
                return;
            }

            if (hitWindowOpen) DamageTargets();
        }

        // Swing clip events: they start both the correctly mirrored effect and hit window.
        public void SwingUpFlipAnim() => OpenHitWindow(true);
        public void SwingDownFlipAnim() => OpenHitWindow(false);

        private void OpenHitWindow(bool upwards)
        {
            if (!IsAttacking || hitWindowOpen) return;
            hitWindowOpen = true;
            if (activeSlash != null) Destroy(activeSlash.gameObject);
            activeSlash = Instantiate(slashPrefab, slashAnimSpawnPoint);
            activeSlash.transform.localPosition = Vector3.zero;
            activeSlash.transform.localRotation = Quaternion.identity;
            activeSlash.Play(upwards, attackDuration * 0.75f);
        }

        public void CloseHitWindow() => hitWindowOpen = false;

        public void FinishAttack()
        {
            hitWindowOpen = false;
            IsAttacking = false;
        }

        private void DamageTargets()
        {
            // The swing and holder move in animation/LateUpdate, after the physics tick.
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            Physics2D.OverlapCircle(hitPoint.position, hitRadius, filter, hitColliders);
            foreach (Collider2D candidate in hitColliders)
            {
                Health health = candidate.GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled ||
                    health.transform.IsChildOf(wielder) || hitTargets.Contains(health)) continue;

                Vector2 targetPoint = candidate.ClosestPoint(hitPoint.position);
                if (Physics2D.Linecast(slashAnimSpawnPoint.position, targetPoint, blockingLayers)) continue;
                hitTargets.Add(health);
                health.TakeDamage(damage);
            }
        }

        private void CancelAttack()
        {
            FinishAttack();
            nextAttackAt = 0f;
            hitTargets.Clear();
            if (activeSlash != null) Destroy(activeSlash.gameObject);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.ResetTrigger(AttackParameter);
                if (animator.isActiveAndEnabled) animator.Play("Idle", 0, 0f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (hitPoint == null) return;
            Gizmos.color = hitWindowOpen ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
        }
    }
}
