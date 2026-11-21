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

        private TilemapRenderer sourceRenderer;
        private bool rebuild;
        private Bounds[] decorationExclusions;

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
                uint hash = unchecked((uint)(cell.x * 73856093 ^ cell.y * 19349663));
                if (hash % (uint)Mathf.Max(1, decorationSpacing) != 0) continue;
                bool tree = (hash & 1) == 0;
                GameObject[] choices = tree ? trees : rocks;
                if (choices == null || choices.Length == 0) continue;
                if (count >= decorationLimit) break;
                GameObject prefab = choices[(int)((hash >> 4) % (uint)choices.Length)];
                if (prefab == null) continue;
                GameObject instance = Instantiate(prefab, decorations);
                // Trees sit behind walkable surfaces; small rocks are embedded in the solid tile.
                instance.transform.position = source.CellToWorld(cell)
                    + source.transform.TransformVector(new Vector3(source.layoutGrid.cellSize.x * 0.5f,
                        source.layoutGrid.cellSize.y * (tree ? 1f : 0.6f), 0));
                float size = tree ? 0.55f + (hash % 5) * 0.07f : 0.2f + (hash % 3) * 0.04f;
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
            Bounds bounds = sprite.bounds;
            // Compare XY only: sorting depth must not allow scenery to hide an entrance.
            foreach (Bounds area in decorationExclusions)
                if (bounds.min.x < area.max.x + portalDecorationPadding
                    && bounds.max.x > area.min.x - portalDecorationPadding
                    && bounds.min.y < area.max.y + portalDecorationPadding
                    && bounds.max.y > area.min.y - portalDecorationPadding) return true;
            return false;
        }
    }
}
