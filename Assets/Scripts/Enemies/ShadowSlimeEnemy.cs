using GravityRoguelike.Combat;
using UnityEngine;

/// <summary>Uses WalkerEnemy's movement unchanged, with slime-specific damage/death hooks.</summary>
public class ShadowSlimeEnemy : WalkerEnemy
{
    private ShadowSlimeCombat combat;

    protected override void Awake()
    {
        base.Awake();
        combat = GetComponent<ShadowSlimeCombat>();
    }

    private void Start()
    {
        if (roomGrid != null && player != null) return; // Already injected by EnemySpawner.
        RoomGrid grid = FindFirstObjectByType<RoomGrid>();
        GameObject target = GameObject.FindWithTag("Player");
        if (grid != null && target != null) Init(grid, target.transform);
        else
        {
            enabled = false;
            Debug.LogWarning("ShadowSlime needs a RoomGrid and an active Player, or EnemySpawner.Init.", this);
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        combat.Bind(player);
    }

    protected override void OnDamaged(DamageInfo info)
    {
        base.OnDamaged(info);
        combat.ReceiveHit(info);
    }

    protected override void OnDeath() => combat.BeginDeath();

    // Spikes are the only damage source. Touching the body during windup is safe.
    protected override void OnCollisionStay2D(Collision2D collision) { }
}
