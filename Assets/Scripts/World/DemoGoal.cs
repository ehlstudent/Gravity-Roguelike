using UnityEngine;

namespace GravityRoguelike.World
{
    public class DemoGoal : MonoBehaviour
    {
        public bool Complete { get; private set; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Complete || other.GetComponentInParent<PlayerController2D>() == null) return;
            Complete = true;
            GetComponent<Collider2D>().enabled = false;
        }
    }
}
