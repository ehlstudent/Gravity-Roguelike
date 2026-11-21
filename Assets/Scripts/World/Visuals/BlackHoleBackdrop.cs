using UnityEngine;

namespace GravityRoguelike.World.Visuals
{
    /// <summary>Camera-relative scenery only; never moves the camera or gameplay objects.</summary>
    [ExecuteAlways, DefaultExecutionOrder(1000)]
    public class BlackHoleBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Optional read-only room anchor. Without one, the camera's global position supplies the viewing angle.")]
        [SerializeField] private Transform roomAnchor;
        [SerializeField] private Vector2 worldReferencePoint;
        [Min(0), SerializeField] private float roomPositionToOffsetMultiplier = 0.02f;
        [Tooltip("Additional, very small motion within a room. Never moves the camera.")]
        [Min(0), SerializeField] private float parallaxFactor = 0.002f;
        [Tooltip("Maximum viewing-angle offset in camera-plane world units. Also limited to the visible view.")]
        [SerializeField] private Vector2 maximumOffset = new Vector2(3f, 1.5f);
        [Min(0), SerializeField] private float smoothingTime = 1.5f;
        [Tooltip("Height of the full black-hole artwork relative to the camera's visible height.")]
        [Min(0.1f), SerializeField] private float blackHoleViewHeight = 1.35f;
        [Min(0.01f), SerializeField] private float cameraDistance = 20f;
        [SerializeField] private SpriteRenderer sky;
        [SerializeField] private Transform blackHole;
        [SerializeField] private Transform farClouds;

        private Vector2 offset;
        private Vector2 offsetVelocity;
        private bool positioned;

        private void OnEnable() => positioned = false;

        private void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null || sky == null || blackHole == null) return;

            Transform cameraTransform = viewCamera.transform;
            float distance = Mathf.Clamp(cameraDistance, viewCamera.nearClipPlane + 0.1f,
                Mathf.Max(viewCamera.nearClipPlane + 0.1f, viewCamera.farClipPlane - 0.1f));
            float halfHeight = viewCamera.orthographic ? viewCamera.orthographicSize
                : Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float halfWidth = halfHeight * viewCamera.aspect;
            Vector2 cameraPosition = cameraTransform.position;
            Vector2 roomPosition = roomAnchor != null ? (Vector2)roomAnchor.position : cameraPosition;
            Vector2 desired = -(roomPosition - worldReferencePoint) * roomPositionToOffsetMultiplier
                - (cameraPosition - (roomAnchor != null ? roomPosition : worldReferencePoint)) * parallaxFactor;
            Vector3 worldOffset = desired;
            desired = new Vector2(Vector3.Dot(worldOffset, cameraTransform.right),
                Vector3.Dot(worldOffset, cameraTransform.up));
            float limitX = Mathf.Min(Mathf.Abs(maximumOffset.x), halfWidth * 0.3f);
            float limitY = Mathf.Min(Mathf.Abs(maximumOffset.y), halfHeight * 0.22f);
            desired.x = Mathf.Clamp(desired.x, -limitX, limitX);
            desired.y = Mathf.Clamp(desired.y, -limitY, limitY);

            offset = positioned && Application.isPlaying && smoothingTime > 0
                ? Vector2.SmoothDamp(offset, desired, ref offsetVelocity, smoothingTime,
                    Mathf.Infinity, Time.unscaledDeltaTime)
                : desired;
            // Clamp after smoothing too, including when the viewport or camera size changes.
            offset.x = Mathf.Clamp(offset.x, -limitX, limitX);
            offset.y = Mathf.Clamp(offset.y, -limitY, limitY);
            positioned = true;
            transform.SetPositionAndRotation(cameraTransform.position + cameraTransform.forward * distance,
                cameraTransform.rotation);
            transform.localScale = Vector3.one;
            // Tiled sky is deliberately oversized; camera travel cannot uncover its edges.
            sky.size = new Vector2(halfWidth * 2.2f, halfHeight * 2.2f);
            blackHole.localPosition = new Vector3(offset.x, offset.y + halfHeight * 0.08f, 0);
            // Supplied artwork is 96 px at 16 PPU, i.e. six units high.
            blackHole.localScale = Vector3.one * (halfHeight * 2f * blackHoleViewHeight / 6f);
            if (farClouds != null)
            {
                farClouds.localPosition = new Vector3(offset.x * 1.3f, offset.y * 1.3f, 0);
                farClouds.localScale = new Vector3(halfWidth, halfHeight, 1);
            }
        }
    }
}
