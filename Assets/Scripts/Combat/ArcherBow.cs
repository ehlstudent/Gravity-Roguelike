using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    // ActiveWeapon updates mouse aim at order 100, before a queued release is fired here.
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(Animator))]
    public class ArcherBow : MonoBehaviour
    {
        [SerializeField] private ActiveWeapon weapon;
        [SerializeField] private Transform firePoint;
        [SerializeField] private ArrowProjectile arrowPrefab;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.01f)] private float shootDuration = 0.7f;
        [Tooltip("Minimum seconds between shot starts. A draw in progress cannot be interrupted.")]
        [SerializeField, Min(0f)] private float attackDelay = 0.8f;
#if UNITY_EDITOR
        [SerializeField] private bool showAimGizmos = true;
#endif
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private static readonly int ShootState = Animator.StringToHash("Shoot");
        private Animator animator;
        private InputAction shootAction;
        private Transform wielder;
        private float nextShotAt;
        private float shotStartedAt;
        private bool releaseRequested;
        private bool released;

        public bool IsShooting { get; private set; }
        public event Action<float> ShotStarted;
        public event Action ShotFinished;
        public Vector2 AimDirection => weapon.transform.TransformVector(Vector3.right).normalized;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            wielder = weapon != null ? weapon.transform.parent : transform.root;
        }

        private void OnEnable()
        {
            shootAction = new InputAction("Archer Shoot", InputActionType.Button, "<Mouse>/leftButton");
            shootAction.performed += OnShoot;
            shootAction.Enable();
        }

        private void OnDisable()
        {
            if (shootAction != null)
            {
                shootAction.performed -= OnShoot;
                shootAction.Disable();
                shootAction.Dispose();
                shootAction = null;
            }
            releaseRequested = false;
            FinishShot();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.ResetTrigger(Shoot);
                if (animator.isActiveAndEnabled) animator.Play("Idle", 0, 0f);
            }
        }

        private void OnShoot(InputAction.CallbackContext context) => TryShoot();

        private void Update()
        {
            // Holding is allowed, but both the start-based cooldown and the draw must finish.
            if (shootAction != null && shootAction.IsPressed()) TryShoot();
        }

        public bool TryShoot()
        {
            if (!isActiveAndEnabled || IsShooting || Time.time < nextShotAt) return false;
            if (viewCamera == null) viewCamera = Camera.main;
            if (weapon == null || firePoint == null || arrowPrefab == null || viewCamera == null ||
                animator == null || animator.runtimeAnimatorController == null ||
                shootDuration <= 0f || attackDelay < 0f)
            {
                Debug.LogWarning("ArcherBow: assign the aim pivot, fire point, Arrow prefab, Animator and camera; use a positive duration and non-negative delay.", this);
                return false;
            }
            IsShooting = true;
            released = false;
            releaseRequested = false;
            shotStartedAt = Time.time;
            nextShotAt = shotStartedAt + attackDelay;
            animator.speed = 1f / shootDuration; // Both Shoot clips have a one-second timeline.
            animator.SetTrigger(Shoot);
            ShotStarted?.Invoke(shootDuration);
            return true;
        }

        // The bow clip's release frame queues one arrow, after this frame's mouse aim update.
        public void ReleaseArrow()
        {
            if (IsShooting && !released) releaseRequested = true;
        }

        private void LateUpdate()
        {
            if (releaseRequested)
            {
                releaseRequested = false;
                released = true;
                if (viewCamera != null)
                {
                    var arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
                    arrow.Launch(wielder, AimDirection, viewCamera);
                }
            }
            if (IsShooting && Time.time - shotStartedAt > shootDuration + 0.1f) FinishShot();
        }

        public void FinishShot()
        {
            if (!IsShooting) return;
            IsShooting = false;
            ShotFinished?.Invoke();
        }

        public bool TryGetShootProgress(out float progress)
        {
            progress = 0f;
            if (!IsShooting || animator == null) return false;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != ShootState) return false;
            progress = Mathf.Clamp01(state.normalizedTime);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showAimGizmos || weapon == null || firePoint == null || arrowPrefab == null) return;
            Color old = Gizmos.color;
            Gizmos.color = Color.cyan;
            // Show the actual projectile direction and distance travelled during one physics step.
            Gizmos.DrawLine(transform.position, firePoint.position + (Vector3)(AimDirection * arrowPrefab.Speed * Time.fixedDeltaTime));
            Gizmos.DrawWireSphere(firePoint.position, arrowPrefab.CollisionSize.y * 0.5f);
            Gizmos.color = old;
        }
#endif
    }
}
