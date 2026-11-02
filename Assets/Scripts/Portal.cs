using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class Portal : MonoBehaviour
{
    // Remembers which spawn point the next scene should use.
    // Static, so it survives the scene change.
    public static string NextSpawnId;

    // Static values can survive between Play sessions in the editor (when "Reload Domain"
    // is turned off). Clear it every time Play starts so a stale ID can't teleport the player.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        NextSpawnId = null;
    }

    [Header("Destination")]
    [SerializeField] private string targetSceneName;   // must be added to File > Build Profiles / Build Settings
    [SerializeField] private string targetSpawnId;     // ID of a SpawnPoint in the target scene

    private bool isLoading;
    private Vector2? sameSceneArrival;

    // The demo supplies a physics-validated arrival after generating its static cave.
    public void SetSameSceneArrival(Vector2 position) => sameSceneArrival = position;

    private void Reset()
    {
        // Runs when the component is added: make the collider a trigger automatically
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Identify the player by its component instead of a tag string.
        // GetComponentInParent also works if the collider sits on a child object.
        var player = other.GetComponentInParent<PlayerController2D>();
        if (isLoading || player == null) return;

        if (sameSceneArrival.HasValue)
        {
            isLoading = true; // One-way showcase portal.
            var body = player.GetComponent<Rigidbody2D>();
            var gravity = GravityRotator.Instance;
            gravity.Rotate(-gravity.GravityAngle);
            body.rotation = 0f;
            body.position = sameSceneArrival.Value;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"Portal: scene '{targetSceneName}' is not in the Build Settings (or the name is misspelled).", this);
            return;
        }

        isLoading = true;                 // prevents loading twice if several colliders trigger at once
        NextSpawnId = targetSpawnId;
        SceneManager.LoadScene(targetSceneName);
    }
}
