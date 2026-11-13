using UnityEngine;

namespace GravityRoguelike.Combat
{
    [RequireComponent(typeof(Animator))]
    public class SlashAnim : MonoBehaviour
    {
        public void Play(bool swingUp, float duration)
        {
            // Mirror the whole effect, including the cropped frames' position curves.
            transform.localScale = new Vector3(1f, swingUp ? -1f : 1f, 1f);
            GetComponent<Animator>().speed = 1f / duration;
            // Safety net if the animation is interrupted or its final event is missed.
            Destroy(gameObject, duration + 0.1f);
        }

        // Animation event on the last frame of the Slash clip.
        public void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}
