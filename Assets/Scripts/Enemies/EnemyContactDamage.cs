using GravityRoguelike.Combat;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class EnemyContactDamage : MonoBehaviour
{
    private float nextHit;

    private void OnCollisionEnter2D(Collision2D contact) => HitPlayer(contact);
    private void OnCollisionStay2D(Collision2D contact) => HitPlayer(contact);

    private void HitPlayer(Collision2D contact)
    {
        if (Time.time < nextHit) return;
        var player = contact.gameObject.GetComponentInParent<PlayerController2D>();
        if (player != null && player.TryGetComponent<Health>(out var health) && health.TakeDamage(1))
            nextHit = Time.time + 1f;
    }
}
