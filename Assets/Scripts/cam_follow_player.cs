using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;                 // drag the player here
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    [SerializeField] private float smoothTime = 0.15f;         // lower = snappier, higher = floatier

    [Header("Gravity rotation")]
    [SerializeField] private bool rotateWithGravity = true;   // keeps the player upright on screen
    [SerializeField] private float rotationSmoothTime = 0.15f; // how long the room turn takes, roughly

    private Vector3 velocity;
    private float angleVelocity;

    private void LateUpdate()
    {
        if (target == null) return;

        if (rotateWithGravity)
        {
            float targetAngle = GravityRotator.Instance != null ? GravityRotator.Instance.GravityAngle : 0f;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.z, targetAngle, ref angleVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // LateUpdate runs after the player has moved, which avoids camera jitter.
        // The offset turns with the camera, so "above the player" stays above on screen.
        Vector3 targetPosition = target.position + transform.rotation * offset;
        transform.position = targetPosition;
    }
}