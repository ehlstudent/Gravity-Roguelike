using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GravityRoguelike.World
{
    /// <summary>Inspector-only configuration, separate from the runtime room grid.</summary>
    [Serializable]
    public class RoomPreviewEntry
    {
        public int Id;
        public RectInt Bounds;
        public int Seed;

        public RoomPreviewEntry(int id, RectInt bounds, int seed)
        {
            Id = id;
            Bounds = bounds;
            Seed = seed;
        }
    }

    /// <summary>Draws absolute world-grid bounds at one Unity unit per cell.</summary>
    [ExecuteAlways]
    public class WorldBoundsPreview : MonoBehaviour
    {
        [SerializeField] private RectInt worldBounds = new RectInt(0, 0, 60, 36);
        [SerializeField] private int worldSeed = 123;
        [Header("Random placement (component context menu)")]
        [SerializeField] private int requestedRoomCount = 8;
        [SerializeField] private int minRoomWidth = 8;
        [SerializeField] private int maxRoomWidth = 18;
        [SerializeField] private int minRoomHeight = 6;
        [SerializeField] private int maxRoomHeight = 14;
        [SerializeField] private int minimumGap = 2;
        [SerializeField] private int maxPlacementAttempts = 500;
        [TextArea(2, 4)]
        [SerializeField] private string lastGenerationSummary;
        [Header("Editable room layout")]
        [SerializeField] private List<RoomPreviewEntry> rooms = new List<RoomPreviewEntry>
        {
            new RoomPreviewEntry(1, new RectInt(4, 4, 16, 12), 101),
            new RoomPreviewEntry(2, new RectInt(26, 4, 28, 12), 102),
            new RoomPreviewEntry(3, new RectInt(12, 22, 36, 10), 103)
        };
        [SerializeField] private Color worldColor = new Color(0f, 0.85f, 1f, 1f);
        [SerializeField] private Color roomColor = new Color(0.3f, 1f, 0.35f, 1f);

        [TextArea(2, 8)]
        [SerializeField] private string validationSummary;

        private WorldData previewWorld;
        private WireBounds worldWire;
        private WireBounds[] roomWires = Array.Empty<WireBounds>();
        private string lastReportedErrors;
        private bool rebuildPending = true;

        [ContextMenu("Generate New Random Layout")]
        private void GenerateNewRandomLayout()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Randomize World Seed");
#endif

            worldSeed = Guid.NewGuid().GetHashCode();
            GenerateRoomLayout();
        }
        /// <summary>Explicitly replaces the editable preview layout; never called by repaint or Update.</summary>
        [ContextMenu("Generate Room Layout")]
        private void GenerateRoomLayout()
        {
            try
            {
                RoomPlacementResult result = RoomPlacementGenerator.Generate(worldBounds, worldSeed,
                    requestedRoomCount, minRoomWidth, maxRoomWidth, minRoomHeight, maxRoomHeight,
                    minimumGap, maxPlacementAttempts);
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(this, "Generate Room Layout");
#endif
                var generatedRooms = new List<RoomPreviewEntry>(result.PlacedCount);
                foreach (RoomData room in result.World.Rooms)
                    generatedRooms.Add(new RoomPreviewEntry(room.Id, room.Bounds, room.Seed));
                rooms = generatedRooms;
                lastGenerationSummary = $"Last generation: placed {result.PlacedCount}/{result.RequestedCount} rooms in {result.AttemptsUsed} attempts."
                    + (result.FinishedEarly ? $" Attempt limit reached; shortfall: {result.RequestedCount - result.PlacedCount}." : " Request completed.");
                RebuildPreview();
                rebuildPending = false;
                if (result.FinishedEarly) Debug.LogWarning(lastGenerationSummary, this);
                else Debug.Log(lastGenerationSummary, this);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                if (!Application.isPlaying && gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                UnityEditor.SceneView.RepaintAll();
#endif
            }
            catch (ArgumentException exception)
            {
                // Preserve the last layout when settings are invalid.
                Debug.LogWarning($"Room layout generation failed: {exception.Message}", this);
            }
        }

        private struct WireBounds
        {
            public Vector3 Center;
            public Vector3 Size;
            public bool Drawable;
            public bool Valid;
        }

        private void OnEnable()
        {
            rebuildPending = true;
        }

        private void OnValidate()
        {
            // OnValidate can run during loading. Defer construction/logging to Update.
            rebuildPending = true;
        }

        private void Update()
        {
            if (!rebuildPending) return;
            rebuildPending = false;
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            var errors = new StringBuilder();
            previewWorld = null;
            try
            {
                previewWorld = new WorldData(worldBounds, worldSeed);
            }
            catch (ArgumentException exception)
            {
                errors.AppendLine($"World: {exception.Message}");
            }

            worldWire = CreateWire(worldBounds, previewWorld != null);
            roomWires = new WireBounds[rooms == null ? 0 : rooms.Count];
            for (int i = 0; i < roomWires.Length; i++)
            {
                RoomPreviewEntry entry = rooms[i];
                if (entry == null)
                {
                    errors.AppendLine($"Room entry {i}: configuration is null.");
                    continue;
                }

                bool valid = false;
                try
                {
                    var room = new RoomData(entry.Id, entry.Bounds, entry.Seed);
                    if (previewWorld == null)
                    {
                        errors.AppendLine($"Room entry {i} (ID {entry.Id}): cannot validate placement until world bounds are valid.");
                    }
                    else
                    {
                        // Earlier accepted entries take precedence when rooms conflict.
                        previewWorld.AddRoom(room);
                        valid = true;
                    }
                }
                catch (ArgumentException exception)
                {
                    errors.AppendLine($"Room entry {i} (ID {entry.Id}): {exception.Message}");
                }

                roomWires[i] = CreateWire(entry.Bounds, valid);
            }

            string errorText = errors.ToString();
            validationSummary = errorText.Length == 0
                ? $"Valid preview: {previewWorld.Rooms.Count} rooms. Cyan: world; green: valid rooms; red: invalid rooms."
                : errorText;
            // Log changed errors once, never from a Scene repaint.
            if (errorText.Length > 0 && errorText != lastReportedErrors)
            {
                Debug.LogWarning($"World bounds preview '{name}':\n{errorText}", this);
            }
            lastReportedErrors = errorText;
        }

        private static WireBounds CreateWire(RectInt bounds, bool valid)
        {
            return new WireBounds
            {
                // Exclusive maxima describe rectangle edges, not cell centres.
                Center = new Vector3(bounds.x + bounds.width * 0.5f, bounds.y + bounds.height * 0.5f, 0f),
                Size = new Vector3(bounds.width, bounds.height, 0f),
                Drawable = bounds.width > 0 && bounds.height > 0,
                Valid = valid
            };
        }

        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            // Absolute grid coordinates: deliberately ignore this object's Transform.
            Gizmos.matrix = Matrix4x4.identity;
            DrawWire(worldWire, worldColor);
            for (int i = 0; i < roomWires.Length; i++)
            {
                DrawWire(roomWires[i], roomColor);
            }
            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private static void DrawWire(WireBounds wire, Color validColor)
        {
            if (!wire.Drawable) return;
            Gizmos.color = wire.Valid ? validColor : Color.red;
            Gizmos.DrawWireCube(wire.Center, wire.Size);
        }
    }
}
