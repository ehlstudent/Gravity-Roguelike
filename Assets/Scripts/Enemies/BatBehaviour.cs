using UnityEngine;
using GravityRoguelike.Combat;   // for DamageInfo

public class BatBehaviour : Enemy
{
    EnemyChase chase;

    // Extends the Enemy setup: base sets health and rigidbody, then we cache the chase component
    protected override void Awake()
    {
        base.Awake();   // must come first: runs the Enemy setup (health, rb)
        chase = GetComponent<EnemyChase>();
        chase.enabled = false;
    }

    // Starts listening for gravity changes when the bat becomes active.
    void OnEnable()
    {
        GravityRotator.GravityChanged += Wake;
    }

    // Stops listening when the bat is disabled or destroyed.
    void OnDisable()
    {
        GravityRotator.GravityChanged -= Wake;
    }

    // Gravity event handler: just forwards to WakeUp (the parameter is required by the event signature)
    void Wake(Vector2Int newGravity)
    {
        WakeUp();
    }

    // Switches the bat from hanging to chasing. Safe to call more than once
    void WakeUp()
    {
        // In BatBehaviour.WakeUp(), first line:
        Debug.Log($"[4] WakeUp, chase already enabled: {chase.enabled}", this);
        if (chase.enabled) return;   // already awake, nothing to do
        chase.enabled = true;
    }

    // Called by Enemy.TakeDamage: a hit wakes the sleeping bat
    protected override void OnDamaged(DamageInfo info)
    {
        // In BatBehaviour.OnDamaged(DamageInfo info), first line:
        Debug.Log("[3] Bat.OnDamaged", this);
        WakeUp();
    }
}