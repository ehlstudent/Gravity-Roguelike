// Anything that can be hit: enemies, the player, ...
public interface IDamageable
{
    // Receives a hit. The receiver decides how to react
    void TakeDamage(DamageInfo info);
}