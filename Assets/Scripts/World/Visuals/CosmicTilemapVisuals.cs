using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World.Visuals
{
    /// <summary>Non-colliding scenery copied from occupied cells; the source map is never edited.</summary>
    [ExecuteAlways]
    public class CosmicTilemapVisuals : MonoBehaviour
    {
        [SerializeField] private Tilemap source;
        [SerializeField] private Tilemap walls;
        [SerializeField] private Tilemap background;
        [SerializeField] private TileBase wallTile;
        [SerializeField] private TileBase backgroundTile;
        [SerializeField] private Transform decorations;
        [SerializeField] private GameObject[] trees;
        [SerializeField] private GameObject[] rocks;
        [Min(0), SerializeField] private int decorationLimit = 36;
        [Min(1), SerializeField] private int decorationSpacing = 7;
        [Tooltip("Extra world-space clearance between decoration artwork and generated portals/spawns.")]
        [Min(0), SerializeField] private float portalDecorationPadding = 0.5f;
        [Header("Seeded tile decoration")]
        [SerializeField] private int decorationSeed = 1337;
        [Range(0, 1), SerializeField] private float embeddedStoneProbability = 0.1f;
        [Range(0, 1), SerializeField] private float grassProbability = 0.7f;
        [Range(0, 1), SerializeField] private float rockFillProbability = 0.08f;
        [SerializeField] private Tilemap embeddedStones;
        [SerializeField] private Tilemap surfaceGrass;

        private TilemapRenderer sourceRenderer;
        private bool rebuild;
        private Bounds[] decorationExclusions;
        private readonly HashSet<Vector3Int> exterior = new HashSet<Vector3Int>();
        private static readonly Vector3Int[] Cardinal =
            { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        public float RockFillProbability => rockFillProbability;

        public void SetDecorationSeed(int seed)
        {
            decorationSeed = seed;
            rebuild = true;
        }

        // Independent coordinate samples keep decoration stable across rebuilds and traversal order.
        private uint Hash(Vector3Int cell, int salt)
        {
            unchecked
            {
                uint hash = (uint)(decorationSeed ^ cell.x * 73856093 ^ cell.y * 19349663 ^ salt * 83492791);
                hash = (hash ^ (hash >> 16)) * 0x7feb352du;
                hash = (hash ^ (hash >> 15)) * 0x846ca68bu;
                return hash ^ (hash >> 16);
            }
        }

        public float Sample(Vector3Int cell, int salt) => (Hash(cell, salt) & 0xffffff) / 16777216f;

        public bool IsCavity(Vector3Int cell) => source != null && source.cellBounds.Contains(cell)
            && !source.HasTile(cell) && !exterior.Contains(cell);

        public bool IsFillCell(Vector3Int cell)
        {
            if (!source.HasTile(cell)) return false;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    if (!source.HasTile(cell + new Vector3Int(x, y, 0))) return false;
            return true;
        }

        private void FindExterior()
        {
            // Visual classification only: flood empty space from outside the map; never edit solids.
            // Local neighbours alone cannot distinguish a straight exterior edge from a cave ceiling.
            exterior.Clear();
            BoundsInt area = source.cellBounds;
            var min = new Vector3Int(area.xMin - 1, area.yMin - 1, 0);
            var max = new Vector3Int(area.xMax, area.yMax, 0);
            var queue = new Queue<Vector3Int>();
            exterior.Add(min);
            queue.Enqueue(min);
            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                foreach (Vector3Int direction in Cardinal)
                {
                    Vector3Int next = cell + direction;
                    if (next.x < min.x || next.y < min.y || next.x > max.x || next.y > max.y
                        || source.HasTile(next) || !exterior.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
        }

        /// <summary>Connects the reusable visual template to the generator's existing collision map.</summary>
        public void BindTo(Tilemap tilemap)
        {
            source = tilemap;
            rebuild = true;
        }

        /// <summary>Only excludes scenery; portal and player collision geometry stays untouched.</summary>
        public void SetDecorationExclusions(params Bounds[] areas)
        {
            decorationExclusions = areas;
            rebuild = true;
        }

        private void OnEnable()
        {
            Tilemap.tilemapTileChanged += SourceChanged;
            rebuild = true;
        }

        private void OnDisable()
        {
            Tilemap.tilemapTileChanged -= SourceChanged;
            if (sourceRenderer != null) sourceRenderer.forceRenderingOff = false;
        }

        private void SourceChanged(Tilemap changed, Tilemap.SyncTile[] changes)
        {
            if (changed == source) rebuild = true;
        }

        private void LateUpdate()
        {
            // Coalesce generation's many SetTile calls into one visual rebuild.
            if (rebuild) RebuildVisuals();
        }

        [ContextMenu("Rebuild Cosmic Visuals")]
        public void RebuildVisuals()
        {
            rebuild = false;
            if (source == null || walls == null || background == null || wallTile == null
                || backgroundTile == null || source == walls || source == background) return;

            sourceRenderer = source.GetComponent<TilemapRenderer>();
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            transform.localScale = source.transform.localScale;
            walls.tileAnchor = source.tileAnchor;
            walls.orientation = source.orientation;
            walls.orientationMatrix = source.orientationMatrix;
            background.tileAnchor = source.tileAnchor;
            background.orientation = source.orientation;
            background.orientationMatrix = source.orientationMatrix;
            FindExterior();

            var positions = new List<Vector3Int>();
            var tiles = new List<TileBase>();
            var fringe = new HashSet<Vector3Int>();
            var supports = new List<Vector3Int>();
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(cell)) continue;
                positions.Add(cell);
                tiles.Add(wallTile);
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        Vector3Int adjacent = cell + new Vector3Int(x, y, 0);
                        if (!source.HasTile(adjacent)) fringe.Add(adjacent);
                    }
                if (!source.HasTile(cell + Vector3Int.up) && !source.HasTile(cell + Vector3Int.up * 2)
                    && !source.HasTile(cell + Vector3Int.up * 3)) supports.Add(cell);
            }
            walls.ClearAllTiles();
            background.ClearAllTiles();
            walls.SetTiles(positions.ToArray(), tiles.ToArray());
            // Carry visual transforms across without touching the source's collision transforms.
            foreach (Vector3Int cell in positions)
            {
                walls.SetTileFlags(cell, TileFlags.None);
                walls.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
            }
            positions.Clear();
            tiles.Clear();
            foreach (Vector3Int cell in fringe)
            {
                positions.Add(cell);
                tiles.Add(backgroundTile);
            }
            background.SetTiles(positions.ToArray(), tiles.ToArray());
            RebuildTileDecorations();
            RebuildDecorations(supports);
            if (sourceRenderer != null) sourceRenderer.forceRenderingOff = true;
        }

        private void RebuildDecorations(List<Vector3Int> supports)
        {
            if (decorations == null) return;
            for (int i = decorations.childCount - 1; i >= 0; i--)
            {
                GameObject child = decorations.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            int count = 0;
            foreach (Vector3Int cell in supports)
            {
                uint hash = Hash(cell, 71);
                if (hash % (uint)Mathf.Max(1, decorationSpacing) != 0) continue;
                bool tree = (hash & 1) == 0;
                GameObject[] choices = tree ? trees : rocks;
                if (choices == null || choices.Length == 0) continue;
                if (count >= decorationLimit) break;
                GameObject prefab = choices[(int)((hash >> 4) % (uint)choices.Length)];
                if (prefab == null) continue;
                GameObject instance = Instantiate(prefab, decorations);
                // Both large rocks and trees are translucent background scenery.
                instance.transform.position = source.CellToWorld(cell)
                    + source.transform.TransformVector(new Vector3(source.layoutGrid.cellSize.x * 0.5f,
                        source.layoutGrid.cellSize.y, 0));
                float size = tree ? 0.55f + (hash % 5) * 0.07f : 0.75f + (hash % 5) * 0.12f;
                instance.transform.localScale = new Vector3((hash & 4) == 0 ? size : -size, size, 1);
                if (CoversReservedArea(instance))
                {
                    instance.SetActive(false);
                    if (Application.isPlaying) Destroy(instance);
                    else DestroyImmediate(instance);
                    continue;
                }
                count++;
            }
        }

        private bool CoversReservedArea(GameObject decoration)
        {
            if (decorationExclusions == null) return false;
            SpriteRenderer sprite = decoration.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null) return false;
            return CoversReservedArea(sprite.bounds);
        }

        private bool CoversReservedArea(Bounds bounds)
        {
            if (decorationExclusions == null) return false;
            // Compare XY only: sorting depth must not allow scenery to hide an entrance.
            foreach (Bounds area in decorationExclusions)
                if (bounds.min.x < area.max.x + portalDecorationPadding
                    && bounds.max.x > area.min.x - portalDecorationPadding
                    && bounds.min.y < area.max.y + portalDecorationPadding
                    && bounds.max.y > area.min.y - portalDecorationPadding) return true;
            return false;
        }

        private Tilemap EnsureDecorationLayer(Tilemap layer, string name, int order)
        {
            if (layer == null)
            {
                Transform existing = transform.Find(name);
                if (existing != null) layer = existing.GetComponent<Tilemap>();
                if (layer == null)
                {
                    var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
                    child.transform.SetParent(transform, false);
                    layer = child.GetComponent<Tilemap>();
                }
            }
            var renderer = layer.GetComponent<TilemapRenderer>();
            var wallRenderer = walls.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = wallRenderer.sharedMaterial;
            renderer.sortingLayerID = wallRenderer.sortingLayerID;
            renderer.sortingOrder = wallRenderer.sortingOrder + order;
            layer.tileAnchor = walls.tileAnchor;
            layer.orientation = walls.orientation;
            layer.orientationMatrix = walls.orientationMatrix;
            layer.ClearAllTiles();
            return layer;
        }

        private void RebuildTileDecorations()
        {
            var rules = wallTile as CosmicWallRuleTile;
            if (rules == null) return;
            embeddedStones = EnsureDecorationLayer(embeddedStones, "Embedded Wall Stones", 1);
            surfaceGrass = EnsureDecorationLayer(surfaceGrass, "Surface Grass", 2);
            walls.RefreshAllTiles();
            foreach (Vector3Int cell in source.cellBounds.allPositionsWithin)
            {
                if (!source.HasTile(cell)) continue;
                Sprite sprite = walls.GetSprite(cell);
                if (IsFillCell(cell) && rules.IsFill(sprite) && rules.embeddedStoneTiles.Length > 0
                    && Sample(cell, 31) < embeddedStoneProbability)
                    embeddedStones.SetTile(cell, rules.embeddedStoneTiles[Hash(cell, 32) % (uint)rules.embeddedStoneTiles.Length]);
                Vector3Int above = cell + Vector3Int.up;
                if (!rules.IsGrassSurface(sprite) || source.HasTile(above) || rules.grassTiles.Length == 0
                    || Sample(cell, 41) >= grassProbability) continue;
                Vector3 centre = source.GetCellCenterWorld(above);
                Vector3 size = source.transform.TransformVector(source.layoutGrid.cellSize);
                if (CoversReservedArea(new Bounds(centre, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1)))) continue;
                surfaceGrass.SetTile(above, rules.grassTiles[Hash(cell, 42) % (uint)rules.grassTiles.Length]);
                surfaceGrass.SetTileFlags(above, TileFlags.None);
                surfaceGrass.SetTransformMatrix(above, Matrix4x4.Scale(new Vector3((Hash(cell, 43) & 1) == 0 ? 1 : -1, 1, 1)));
            }
        }
    }
}
