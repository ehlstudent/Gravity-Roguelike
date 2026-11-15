using UnityEngine;

// Everything a single hit carries. A struct, so it's copied cheaply and creates no garbage
public struct DamageInfo
{
    public int amount;              // how much health to remove
    public Vector2 sourcePosition;  // where the hit came from (used to compute knockback direction)
    public float knockbackForce;    // how hard to push the target away (0 = no knockback)
}