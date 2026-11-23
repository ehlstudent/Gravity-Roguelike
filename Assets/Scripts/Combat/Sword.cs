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
        [SerializeField] private InputActionReference stabAction;
        [Tooltip("Origin for checking whether level geometry blocks an attack.")]
        [SerializeField] private Transform slashAnimSpawnPoint;
        [SerializeField, Min(0.01f)] private float attackDuration = 0.42f;
        [Tooltip("Minimum time between attack starts. An unfinished swing also blocks attacks.")]
        [SerializeField, Min(0f)] private float attackCooldown = 0.45f;

        [Header("Void Knight stab")]
        [SerializeField, Min(0.01f)] private float stabDuration = 0.5f;
        [SerializeField] private SpriteRenderer swingVisual;
        [SerializeField] private SpriteRenderer stabVisual;
        [Tooltip("Animated position and XY scale describe the blade's damage box in world units.")]
        [SerializeField] private Transform stabHitPoint;
        [Tooltip("Scales the animated stab box. (1, 1) preserves the existing hit area.")]
        [SerializeField] private Vector2 stabHitBoxScale = Vector2.one;
        [Tooltip("Local offset from the animated stab hit point, shared by damage and gizmos.")]
        [SerializeField] private Vector2 stabHitBoxOffset = Vector2.zero;
        [Tooltip("Extra angle in degrees relative to the animated stab hit point.")]
        [SerializeField] private float stabHitBoxAngle = 0f;

        [Header("Damage")]
        [SerializeField] private Transform hitPoint;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.45f;
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("Only colliders on these layers with a Health component on a parent can be hit.")]
        [SerializeField] private LayerMask targetLayers = 1;
        [Tooltip("Solid level geometry blocks damage. Uses the existing Ground layer.")]
        [SerializeField] private LayerMask blockingLayers = 8;
        [SerializeField, Min(0f)] private float knockback = 6f;

#if UNITY_EDITOR
        [Header("Scene view")]
        [Tooltip("When selected, draw the actual slash circle (red) and stab box (yellow).")]
        [SerializeField] private bool showAttackGizmos = true;
#endif

        private static readonly int AttackParameter = Animator.StringToHash("Attack");
        private static readonly int SwingUpParameter = Animator.StringToHash("SwingUp");
        private static readonly int StabParameter = Animator.StringToHash("Attack A-stab");
        private static readonly int SwingUpState = Animator.StringToHash("SwingUp");
        private static readonly int SwingDownState = Animator.StringToHash("SwingDown");
        private readonly HashSet<Health> hitTargets = new HashSet<Health>();
        private readonly List<Collider2D> hitColliders = new List<Collider2D>();
        private Animator animator;
        private InputAction ownedAttackAction;
        private InputAction ownedStabAction;
        private Transform wielder;
        private bool hitWindowOpen;
        private bool swingUp;
        private float attackStartedAt;
        private float nextAttackAt;
        private float currentAttackDuration;

        public bool IsAttacking { get; private set; }
        public bool IsStabbing { get; private set; }
        public event System.Action<float> StabStarted;
        public event System.Action StabFinished;
        public event System.Action<float> SlashStarted;
        public event System.Action SlashFinished;
        public int Damage => damage;

        /// <summary>Upgrade: scales the swing's hit radius and slash visual (1 = base size).</summary>
        public float WidthMultiplier { get; private set; } = 1f;

        /// <summary>Upgrade: swing speed (2 = swing takes half as long). The stab ignores it.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>Sets the width multiplier for the swing (hit radius and visual).</summary>
        public void SetWidthMultiplier(float multiplier)
        {
            WidthMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>Sets the swing speed multiplier; only the swing duration changes, not the cooldown.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            SpeedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        /// <summary>Back to base width and speed, e.g. for a new run.</summary>
        public void ResetSwingUpgrades()
        {
            WidthMultiplier = 1f;
            SpeedMultiplier = 1f;
        }

        /// <summary>The current sword swing is the clock for the body's slash animation.</summary>
        public bool TryGetSlashProgress(out float progress)
        {
            progress = 0f;
            if (!IsAttacking || IsStabbing || animator == null) return false;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != SwingUpState && state.shortNameHash != SwingDownState)
                return false;
            progress = Mathf.Clamp01(state.normalizedTime);
            return true;
        }

        private int baseDamage;

        /// <summary>Item bonus (Schadens-Splitter).</summary>
        public void AddDamage(int amount)
        {
            if (amount <= 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            damage += amount;
        }

        /// <summary>Removes item bonuses, e.g. for a new run.</summary>
        public void ResetDamage()
        {
            if (baseDamage > 0) damage = baseDamage; // 0 means Awake has not run yet.
        }

        private void Awake()
        {
            baseDamage = damage;
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
            if (stabAction != null && stabAction.action != null)
            {
                ownedStabAction = stabAction.action.Clone();
                ownedStabAction.performed += OnStab;
                ownedStabAction.Enable();
            }
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
            if (ownedStabAction != null)
            {
                ownedStabAction.performed -= OnStab;
                ownedStabAction.Disable();
                ownedStabAction.Dispose();
                ownedStabAction = null;
            }
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            TryAttack();
        }

        /// <summary>Starts one alternating swing if ready. Animation events gate damage.</summary>
        public bool TryAttack()
        {
            if (!TryBeginAttack(false)) return false;
            swingUp = !swingUp;
            animator.SetBool(SwingUpParameter, swingUp);
            animator.SetTrigger(AttackParameter);
            SlashStarted?.Invoke(currentAttackDuration);
            return true;
        }

        private void OnStab(InputAction.CallbackContext context) => TryStab();

        /// <summary>Uses the same cooldown, damage and item bonuses as swings.</summary>
        public bool TryStab()
        {
            if (!TryBeginAttack(true)) return false;
            animator.SetTrigger(StabParameter);
            StabStarted?.Invoke(currentAttackDuration);
            return true;
        }

        private bool TryBeginAttack(bool stab)
        {
            if (!isActiveAndEnabled || IsAttacking || Time.time < nextAttackAt) return false;
            if (animator == null || animator.runtimeAnimatorController == null ||
                slashAnimSpawnPoint == null || hitPoint == null ||
                wielder == null || attackDuration <= 0f || attackCooldown < 0f ||
                hitRadius <= 0f || damage <= 0)
            {
                Debug.LogWarning("Sword: assign its Animator, attack origin and hit point, " +
                    "and use positive duration, damage and hit radius with a non-negative cooldown.", this);
                return false;
            }

            if (stab && (stabHitPoint == null || swingVisual == null || stabVisual == null ||
                stabDuration <= 0f || float.IsNaN(stabDuration) || float.IsInfinity(stabDuration)))
            {
                Debug.LogWarning("Sword: assign both visuals and the stab hit point, with a positive finite stab duration.", this);
                return false;
            }

            hitTargets.Clear();
            hitWindowOpen = false;
            IsAttacking = true;
            IsStabbing = stab;
            // Faster swing = shorter duration. The animator speed below follows automatically.
            currentAttackDuration = stab ? stabDuration : attackDuration / SpeedMultiplier;
            attackStartedAt = Time.time;
            nextAttackAt = Time.time + attackCooldown;
            animator.speed = 1f / currentAttackDuration; // Attack clips use a one-second timeline.
            if (swingVisual != null) swingVisual.enabled = !stab;
            if (stabVisual != null) stabVisual.enabled = stab;
            return true;
        }

        private void LateUpdate()
        {
            if (!IsAttacking) return;
            if (Time.time - attackStartedAt > currentAttackDuration + 0.1f)
            {
                CancelAttack();
                return;
            }

            if (hitWindowOpen) DamageTargets();
        }

        // Keep the existing clip event names and hit timing, without spawning a VFX.
        public void SwingUpFlipAnim() => OpenSlashHitWindow();
        public void SwingDownFlipAnim() => OpenSlashHitWindow();

        public void CloseHitWindow() => hitWindowOpen = false;

        public void OpenSlashHitWindow()
        {
            if (IsAttacking && !IsStabbing) hitWindowOpen = true;
        }

        // Stab clip event: damage only during extension, with no swing slash effect.
        public void OpenStabHitWindow()
        {
            if (IsAttacking && IsStabbing) hitWindowOpen = true;
        }

        public void FinishAttack()
        {
            bool wasSlashing = IsAttacking && !IsStabbing;
            hitWindowOpen = false;
            IsAttacking = false;
            bool wasStabbing = IsStabbing;
            IsStabbing = false;
            if (swingVisual != null) swingVisual.enabled = true;
            if (stabVisual != null) stabVisual.enabled = false;
            if (wasStabbing) StabFinished?.Invoke();
            if (wasSlashing) SlashFinished?.Invoke();
        }

        private void DamageTargets()
        {
            // The swing and holder move in animation/LateUpdate, after the physics tick.
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            Transform point = IsStabbing ? stabHitPoint : hitPoint;
            Vector2 hitOrigin = point.position;
            if (IsStabbing)
            {
                GetStabHitBox(out hitOrigin, out Vector2 size, out float angle);
                Physics2D.OverlapBox(hitOrigin, size, angle, filter, hitColliders);
            }
            else Physics2D.OverlapCircle(point.position, hitRadius * WidthMultiplier, filter, hitColliders);
            foreach (Collider2D candidate in hitColliders)
            {
                Health health = candidate.GetComponentInParent<Health>();
                if (health == null || !health.isActiveAndEnabled ||
                    health.transform.IsChildOf(wielder) || hitTargets.Contains(health)) continue;

                Vector2 targetPoint = candidate.ClosestPoint(hitOrigin);
                if (Physics2D.Linecast(slashAnimSpawnPoint.position, targetPoint, blockingLayers)) continue;
                hitTargets.Add(health);
                health.TakeDamage(new DamageInfo
                {
                    amount = damage,
                    sourcePosition = wielder.position,
                    knockbackForce = knockback
                });
            }
        }

        private void CancelAttack()
        {
            FinishAttack();
            nextAttackAt = 0f;
            hitTargets.Clear();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.ResetTrigger(AttackParameter);
                // Original swing-only controllers remain usable.
                if (stabAction != null) animator.ResetTrigger(StabParameter);
                if (animator.isActiveAndEnabled) animator.Play("Idle", 0, 0f);
            }
        }

        // Use the animated, serialized StabHitPoint transform for both query and drawing.
        private void GetStabHitBox(out Vector2 center, out Vector2 size, out float angle)
        {
            Vector3 right = stabHitPoint.TransformVector(
                Quaternion.Euler(0f, 0f, stabHitBoxAngle) * Vector3.right);
            Vector3 scale = stabHitPoint.lossyScale;
            center = stabHitPoint.TransformPoint(stabHitBoxOffset);
            size = new Vector2(Mathf.Abs(scale.x * stabHitBoxScale.x),
                Mathf.Abs(scale.y * stabHitBoxScale.y));
            angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showAttackGizmos) return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.identity;
            if (hitPoint != null)
            {
                // The hit point follows the actual alternating swing and the holder's aim.
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(hitPoint.position, hitRadius * WidthMultiplier);
            }
            if (stabHitPoint != null)
            {
                GetStabHitBox(out Vector2 center, out Vector2 size, out float angle);
                Gizmos.color = Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(new Vector3(center.x, center.y, stabHitPoint.position.z),
                    Quaternion.Euler(0f, 0f, angle), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}