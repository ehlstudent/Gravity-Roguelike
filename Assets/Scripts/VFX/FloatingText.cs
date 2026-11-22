using UnityEngine;
using UnityEngine.UI;

namespace GravityRoguelike.VFX
{
    /// <summary>
    /// Short text that pops up at a world position and floats upward on screen, e.g. "+1 Max HP".
    /// Lives on the screen-space overlay, so it stays upright and readable while the camera rotates.
    /// Created by <see cref="GameVfx.ShowFloatingText"/>.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        private Text text;
        private RectTransform rect;
        private Vector3 worldPosition;
        private Color color;
        private float startTime;
        private float duration;
        private float risePixels;
        private Canvas canvas;
        private Outline outline;
        private float outlineAlpha;

        public void Init(Text label, Canvas overlay, Vector3 world, Color textColor, float lifetime, float rise)
        {
            text = label;
            rect = (RectTransform)label.transform;
            canvas = overlay;
            outline = label.GetComponent<Outline>();
            outlineAlpha = outline != null ? outline.effectColor.a : 0f;
            worldPosition = world;
            color = textColor;
            duration = Mathf.Max(0.05f, lifetime);
            risePixels = rise;
            startTime = Time.unscaledTime;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (text == null) return;
            float t = (Time.unscaledTime - startTime) / duration;
            Camera cam = Camera.main;
            if (t >= 1f || cam == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPosition);
            text.enabled = screen.z > 0f;   // behind the camera: hide

            // Ease out upward drift, in screen pixels (independent of the camera rotation).
            float ease = 1f - (1f - t) * (1f - t);
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            screen.y += risePixels * ease * scale;
            screen.z = 0f;
            rect.position = screen;

            // Quick pop, then settle.
            float pop = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f)
                      : t < 0.25f ? Mathf.Lerp(1.15f, 1f, (t - 0.12f) / 0.13f)
                      : 1f;
            rect.localScale = Vector3.one * pop;

            // Fully visible most of the time, fade at the end.
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            text.color = new Color(color.r, color.g, color.b, color.a * alpha);
            if (outline != null)
            {
                Color c = outline.effectColor;
                outline.effectColor = new Color(c.r, c.g, c.b, outlineAlpha * alpha);
            }
        }
    }
}
