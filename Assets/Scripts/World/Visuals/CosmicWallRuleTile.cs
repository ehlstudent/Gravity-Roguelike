using UnityEngine;
using UnityEngine.Tilemaps;

namespace GravityRoguelike.World.Visuals
{
    /// <summary>Tilemap Extras rules, with an additional empty-cavity neighbour for inside-facing art.</summary>
    [CreateAssetMenu(menuName = "2D/Tiles/Cosmic Wall Rule Tile")]
    public class CosmicWallRuleTile : RuleTile<CosmicWallRuleTile.Neighbor>
    {
        public class Neighbor : TilingRuleOutput.Neighbor
        {
            public const int Cavity = 3;
        }

        public Sprite fillSprite;
        public Sprite rockFillSprite;
        public Sprite[] grassSurfaceSprites;
        public TileBase[] embeddedStoneTiles;
        public TileBase[] grassTiles;

        public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap,
            ref Matrix4x4 transform)
        {
            // These directional sprites use fixed (unrotated) rules.
            for (int i = 0; i < rule.m_Neighbors.Count; i++)
            {
                if (rule.m_Neighbors[i] != Neighbor.Cavity) continue;
                var visuals = tilemap.GetComponent<CosmicTilemapVisuals>();
                if (visuals == null || !visuals.IsCavity(position + rule.m_NeighborPositions[i])) return false;
            }
            return base.RuleMatches(rule, position, tilemap, ref transform);
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            var visuals = tilemap.GetComponent<CosmicTilemapVisuals>();
            if (tileData.sprite == fillSprite && visuals != null && visuals.IsFillCell(position)
                && visuals.Sample(position, 17) < visuals.RockFillProbability)
                tileData.sprite = rockFillSprite;
        }

        public bool IsFill(Sprite sprite) => sprite == fillSprite || sprite == rockFillSprite;

        public bool IsGrassSurface(Sprite sprite)
        {
            if (grassSurfaceSprites == null) return false;
            foreach (Sprite candidate in grassSurfaceSprites)
                if (sprite == candidate) return true;
            return false;
        }
    }
}
