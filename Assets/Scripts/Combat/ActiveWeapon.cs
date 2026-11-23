using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Combat
{
    // Aim after CameraFollow2D has moved and rotated the camera in LateUpdate.
    [DefaultExecutionOrder(100)]
    public class ActiveWeapon : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [Tooltip("Second sword: aims at the opposite side of the player, mirroring the first sword.")]
        [SerializeField] private bool mirrorAim;

        private void LateUpdate()
        {
            if (Mouse.current == null) return;
            if (aimCamera == null) aimCamera = Camera.main;
            if (aimCamera == null) return;

            Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane weaponPlane = new Plane(Vector3.forward, transform.position);
            if (weaponPlane.Raycast(ray, out float distance))
                AimAt(ray.GetPoint(distance));
        }

        /// <summary>Aim the pivot in the XY plane, independently of the animated sword.</summary>
        public void AimAt(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.000001f) return;
            // World-space reflection through the pivot; the parent conversion below keeps it
            // correct when gravity rotates the player.
            if (mirrorAim) direction = -direction;

            Transform parent = transform.parent;
            Vector3 localDirection = parent != null
                ? parent.InverseTransformVector(direction) : direction;
            float handedness = parent != null
                ? Mathf.Sign(Vector3.Cross(parent.TransformVector(Vector3.right),
                    parent.TransformVector(Vector3.up)).z) : 1f;

            // PlayerController2D flips its entire X scale. Counter the reflection on
            // this pivot's Y axis so aim, swing direction and effects stay consistent.
            transform.localScale = new Vector3(1f, handedness, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg);
        }
    }
}