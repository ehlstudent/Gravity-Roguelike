using UnityEngine;

public class BatBehaviour : MonoBehaviour
{
    EnemyChase chase;

    // Caches the chase component and keeps it switched off while hanging.
    void Awake()
    {
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

    // Called by the gravity event; switches the bat from hanging to chasing.
    void Wake(Vector2Int newGravity)
    {
        Debug.Log("Bat woken by gravity event\n" + System.Environment.StackTrace);
        chase.enabled = true;
    }
}