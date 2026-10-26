using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;                 // drag the player here
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    [SerializeField] private float smoothTime = 0.15f;         // lower = snappier, higher = floatier

    private Vector3 velocity;

    private void LateUpdate()
    {
        Debug.Log($"target: {target.position} | camera: {transform.position}");
        if (target == null) return;

        // LateUpdate runs after the player has moved, which avoids camera jitter
        Vector3 targetPosition = target.position + offset;
        transform.position = targetPosition;
    }
}