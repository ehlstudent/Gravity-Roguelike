using System;
using System.Collections;
using GravityRoguelike.UI;
using GravityRoguelike.World;
using GravityRoguelike.World.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravityRoguelike.Run
{
    [DefaultExecutionOrder(-200)]
    public class RunFlowController : MonoBehaviour
    {
        public enum RunState { GeneratingMap, SelectingClass, SpawningPlayer, Playing }
        [SerializeField] private ProceduralGameplayDemo generator;
        [Tooltip("A fixed prefab footprint shared by all classes. Selection never changes route validation.")]
        [SerializeField] private GameObject clearancePrefab;
        [SerializeField] private CharacterSelectUI selectionUI;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private BlackHoleBackdrop backdrop;
        [Header("Demo only")]
        [SerializeField] private bool enableDemoKeys = true;
        [SerializeField] private Key reselectKey = Key.Backspace;
        [SerializeField] private Key regenerateKey = Key.R;

        public RunState CurrentState { get; private set; }
        public PlayerController2D Player { get; private set; }
        public event Action<RunState> StateChanged;
        private Transform previewAnchor;
        private bool busy;

        private void Awake()
        {
            if (generator == null || clearancePrefab == null || selectionUI == null || cameraFollow == null ||
                clearancePrefab.GetComponent<BoxCollider2D>() == null)
                throw new InvalidOperationException("RunFlowController needs generator, clearance prefab, selection UI and camera references.");
            if (FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include) != null)
                throw new InvalidOperationException("Remove scene player instances before using character selection.");
            generator.UseExternalRunFlow(clearancePrefab.GetComponent<BoxCollider2D>());
            previewAnchor = new GameObject("Start Room Camera Anchor").transform;
            previewAnchor.SetParent(transform, false);
            cameraFollow.SetTarget(null);
            if (backdrop != null) backdrop.enabled = false;
            SelectedClass.Clear();
            selectionUI.ShowLoading();
        }

        private void OnEnable() => SelectedClass.Confirmed += SpawnSelectedPlayer;
        private void OnDisable() => SelectedClass.Confirmed -= SpawnSelectedPlayer;
        private void Start() => RegenerateRun();

        public void RegenerateRun()
        {
            if (!busy) StartCoroutine(GenerateAndSelect());
        }

        private IEnumerator GenerateAndSelect()
        {
            busy = true;
            SetState(RunState.GeneratingMap);
            selectionUI.ShowLoading();
            yield return RemovePlayer();
            yield return generator.GenerateMap(); // Completion, not a guessed delay.
            busy = false;
            if (!generator.HasGeneratedMap)
            {
                selectionUI.ShowFailure("Generation could not find a valid route. Check the Console, then press R to retry.");
                yield break;
            }
            ShowSelection();
        }

        private void ShowSelection()
        {
            SelectedClass.Clear();
            previewAnchor.position = generator.StartSpawnPosition;
            cameraFollow.SetTarget(previewAnchor, true);
            // Re-enable to snap its existing room-offset calculation at the preview viewpoint.
            if (backdrop != null) { backdrop.enabled = false; backdrop.enabled = true; }
            SetState(RunState.SelectingClass);
            selectionUI.Show();
        }

        private void SpawnSelectedPlayer(ClassDefinition choice)
        {
            if (busy || CurrentState != RunState.SelectingClass) return;
            if (choice.PlayerPrefab == null || choice.PlayerPrefab.GetComponent<PlayerController2D>() == null)
            {
                Debug.LogError("The selected class needs a player prefab with PlayerController2D.", this);
                ShowSelection();
                return;
            }
            SetState(RunState.SpawningPlayer);
            GameObject instance = Instantiate(choice.PlayerPrefab, generator.StartSpawnPosition, Quaternion.identity);
            Player = instance.GetComponent<PlayerController2D>();
            generator.BeginPlay(Player);
            cameraFollow.SetTarget(Player.transform, true);
            SetState(RunState.Playing);
        }

        public void ReturnToSelection()
        {
            if (!busy && CurrentState == RunState.Playing) StartCoroutine(Reselect());
        }

        private IEnumerator Reselect()
        {
            busy = true;
            yield return RemovePlayer();
            busy = false;
            ShowSelection();
        }

        private IEnumerator RemovePlayer()
        {
            generator.SuspendPlay();
            cameraFollow.SetTarget(null);
            if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); Player = null; }
            // This demo owns the auto-created rotator. Recreate after the next spawn so its cached player is current.
            if (GravityRotator.Instance != null) Destroy(GravityRotator.Instance.gameObject);
            if (backdrop != null) backdrop.enabled = false;
            yield return null; // Finish Destroy before another player can be instantiated.
        }

        private void Update()
        {
            if (!enableDemoKeys || busy || Keyboard.current == null) return;
            if (Keyboard.current[reselectKey].wasPressedThisFrame) ReturnToSelection();
            else if (Keyboard.current[regenerateKey].wasPressedThisFrame) RegenerateRun();
        }

        private void SetState(RunState next)
        {
            CurrentState = next;
            StateChanged?.Invoke(next);
        }
    }
}
