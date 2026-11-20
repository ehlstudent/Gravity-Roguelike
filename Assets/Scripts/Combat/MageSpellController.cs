using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    /// <summary>Spell requests and timing; the staff Animator owns its raise/hold/retract states.</summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public class MageSpellController : MonoBehaviour
    {
        [SerializeField] private PlayerController2D player;
        [SerializeField] private Transform firePoint;
        [SerializeField] private ArrowProjectile spellAOrb;
        [SerializeField] private MageSpellArea spellBArea;
        [SerializeField] private Camera aimCamera;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.35f;
        [Tooltip("Seconds after the last A shot before retracting. Use at least the fire interval for uninterrupted held fire.")]
        [SerializeField, Min(0f)] private float keepRaisedWindow = 0.6f;
        [Tooltip("Repeated right clicks within this window continue B's loop without restarting its windup. Holding also continues it.")]
        [SerializeField, Min(0f)] private float spellBRepeatWindow = 0.45f;
#if UNITY_EDITOR
        [SerializeField] private bool showSpellGizmos = true;
#endif
        private static readonly int CastingA = Animator.StringToHash("IsCastingA");
        private static readonly int HoldingB = Animator.StringToHash("IsHoldingB");
        private static readonly int CastA = Animator.StringToHash("CastA");
        private static readonly int CastB = Animator.StringToHash("CastB");
        private static readonly int Grounded = Animator.StringToHash("IsGrounded");
        private static readonly int CastInAir = Animator.StringToHash("HasCastThisAirtime");
        private Animator staff;
        private InputAction left;
        private InputAction right;
        private float nextAShotAt;
        private float lastAShotAt = float.NegativeInfinity;
        private bool pendingFirstA;
        private bool releaseA;
        private bool beganBThisFrame;
        private float repeatBUntil = float.NegativeInfinity;
        private bool hasCastThisAirtime;

        public bool IsCastingA => staff != null && staff.GetBool(CastingA);
        public bool IsCastingB => beganBThisFrame || TryGetSpellBProgress(out _, out _);
        public bool IsLoopingB => staff != null && staff.GetCurrentAnimatorStateInfo(0).IsName("B Hold");

        public bool TryGetSpellBProgress(out bool looping, out float progress)
        {
            looping = false;
            progress = 0f;
            if (staff == null) return false;
            var state = staff.GetCurrentAnimatorStateInfo(0);
            looping = state.IsName("B Hold");
            if (!looping && !state.IsName("B Cast")) return false;
            progress = looping ? Mathf.Repeat(state.normalizedTime, 1f) : Mathf.Clamp01(state.normalizedTime);
            return true;
        }

        private void Awake() => staff = GetComponent<Animator>();

        private void OnEnable()
        {
            left = new InputAction("Mage Spell A", InputActionType.Button, "<Mouse>/leftButton");
            right = new InputAction("Mage Spell B", InputActionType.Button, "<Mouse>/rightButton");
            left.Enable(); right.Enable();
        }

        private void OnDisable()
        {
            left?.Disable(); left?.Dispose(); left = null;
            right?.Disable(); right?.Dispose(); right = null;
            pendingFirstA = releaseA = beganBThisFrame = false;
            repeatBUntil = float.NegativeInfinity;
            if (spellBArea != null) spellBArea.EndSweep();
            hasCastThisAirtime = false;
            if (staff == null || staff.runtimeAnimatorController == null) return;
            staff.SetBool(CastingA, false); staff.SetBool(HoldingB, false);
            staff.SetBool(CastInAir, false);
            staff.ResetTrigger(CastA); staff.ResetTrigger(CastB);
            if (staff.isActiveAndEnabled) staff.Play("Idle", 0, 0f);
        }

        private void Update()
        {
            if (aimCamera == null) aimCamera = Camera.main;
            beganBThisFrame = false;
            bool grounded = player.IsGrounded;
            var state = staff.GetCurrentAnimatorStateInfo(0);
            // A cast carried into a jump counts too, including a retract in progress.
            if (grounded) hasCastThisAirtime = false;
            else if (!state.IsName("Idle") && !state.IsName("OnBack")) hasCastThisAirtime = true;
            staff.SetBool(Grounded, grounded);
            staff.SetBool(CastInAir, hasCastThisAirtime);
            if (right.WasPressedThisFrame() && spellBArea != null)
            {
                repeatBUntil = Time.time + spellBRepeatWindow;
                pendingFirstA = releaseA = false;
                staff.ResetTrigger(CastA);
                // Further clicks sustain the loop instead of resetting hit events/windup.
                if (!state.IsName("B Cast") && !state.IsName("B Hold"))
                {
                    beganBThisFrame = true;
                    staff.SetTrigger(CastB);
                }
                MarkAirCast();
            }
            bool holdingB = right.IsPressed() || Time.time < repeatBUntil;
            staff.SetBool(HoldingB, holdingB);
            if (holdingB || beganBThisFrame || state.IsName("B Cast") || state.IsName("B Hold") || state.IsName("B Retract"))
            {
                staff.SetBool(CastingA, false);
                pendingFirstA = releaseA = false;
                return;
            }

            if ((left.IsPressed() || left.WasPressedThisFrame()) && Time.time >= nextAShotAt && ReferencesReady())
            {
                if (state.IsName("A Hold")) releaseA = true;
                else if (state.IsName("Idle") || state.IsName("OnBack") || state.IsName("A Retract"))
                {
                    pendingFirstA = true;
                    staff.SetBool(CastingA, true);
                    staff.SetTrigger(CastA);
                    MarkAirCast();
                }
            }
            // Clicks inside the interval neither queue extra shots nor extend this window.
            if (!pendingFirstA && Time.time - lastAShotAt >= keepRaisedWindow)
                staff.SetBool(CastingA, false);
        }

        private bool ReferencesReady()
        {
            bool ready = aimCamera != null && firePoint != null && spellAOrb != null;
            if (!ready && (left.WasPressedThisFrame() || right.WasPressedThisFrame()))
                Debug.LogWarning("Mage: assign the Spell A prefab, staff tip and gameplay camera.", this);
            return ready;
        }

        // Event at the raised pose; the first A shot waits for this frame, even for a tap.
        public void ReleaseSpellA()
        {
            if (pendingFirstA && !right.IsPressed() && IsCastingA) releaseA = true;
        }

        // These events are on B's visible left/right sweep frames, including each loop.
        public void BeginSpellBSweep(int side) => spellBArea.BeginSweep(side);
        public void EndSpellBSweep() => spellBArea.EndSweep();

        private void LateUpdate()
        {
            // Animation has positioned the tip and the camera has followed the player.
            if (!TryGetSpellBProgress(out _, out _) && spellBArea != null) spellBArea.EndSweep();
            if (releaseA)
            {
                releaseA = false;
                if (right.IsPressed() || Time.time < nextAShotAt) return;
                Fire(spellAOrb, MouseWorldPoint());
                pendingFirstA = false;
                lastAShotAt = Time.time;
                nextAShotAt = Time.time + fireInterval;
                staff.SetBool(CastingA, true);
            }
        }

        private void Fire(ArrowProjectile prefab, Vector3 target)
        {
            if (prefab == null || aimCamera == null) return;
            var projectile = Instantiate(prefab, firePoint.position, Quaternion.identity);
            projectile.Launch(player.transform, AimDirection(target), aimCamera);
            MarkAirCast();
        }

        private void MarkAirCast()
        {
            if (!player.IsGrounded) hasCastThisAirtime = true;
            staff.SetBool(CastInAir, hasCastThisAirtime);
        }

        private Vector3 MouseWorldPoint()
        {
            Camera camera = aimCamera != null ? aimCamera : Camera.main;
            if (camera != null && Mouse.current != null)
            {
                var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                var plane = new Plane(Vector3.forward, firePoint.position);
                if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance);
            }
            return firePoint.position + player.transform.TransformVector(Vector3.right);
        }

        private Vector2 AimDirection(Vector3 target)
        {
            Vector2 direction = target - firePoint.position;
            return direction.sqrMagnitude > 0.000001f ? direction.normalized :
                (Vector2)player.transform.TransformVector(Vector3.right).normalized;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showSpellGizmos || firePoint == null || player == null || spellAOrb == null) return;
            Color old = Gizmos.color;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePoint.position, spellAOrb.CollisionSize.y * 0.5f);
            Gizmos.DrawLine(firePoint.position, firePoint.position +
                (Vector3)(AimDirection(MouseWorldPoint()) * spellAOrb.Speed * Time.fixedDeltaTime));
            Gizmos.color = old;
        }
#endif
    }
}
