using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

// this class is used by the enemy pathfinder. It translates vectors to solid/empty block logic
public class RoomGrid : MonoBehaviour
{
    public Tilemap solid;                          // all blocking tiles, ground included
    // Current gravity direction as a unit vector, read live from the GravityRotator.
    public Vector2Int gravity =>
        GravityRotator.Instance != null
            ? Vector2Int.RoundToInt(GravityRotator.Instance.GravityRotation * Vector2.down)
            : Vector2Int.down;

    static readonly Vector2Int[] Dirs =
    { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    static readonly Vector2Int[] Dirs8 =
{
    Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
};

    // Yields free neighbours in 8 directions; a diagonal is only allowed if both
    // adjacent straight cells are free, so the enemy never cuts through a wall corner.
    public IEnumerable<Vector2Int> FlyingNeighbors8(Vector2Int c)
    {
        foreach (var d in Dirs8)
        {
            if (IsSolid(c + d)) continue;

            bool diagonal = d.x != 0 && d.y != 0;
            if (diagonal && (IsSolid(c + new Vector2Int(d.x, 0)) || IsSolid(c + new Vector2Int(0, d.y))))
                continue;

            yield return c + d;
        }
    }

    public bool IsSolid(Vector2Int c) =>
        solid.HasTile(new Vector3Int(c.x, c.y, 0));

    public bool IsStandable(Vector2Int c) =>
        !IsSolid(c) && IsSolid(c + gravity);

    public Vector2Int WorldToCell(Vector2 w)
    {
        var c = solid.WorldToCell(w);
        return new Vector2Int(c.x, c.y);
    }

    public Vector2 CellToWorld(Vector2Int c) =>
        solid.GetCellCenterWorld(new Vector3Int(c.x, c.y, 0));

    public IEnumerable<Vector2Int> FlyingNeighbors(Vector2Int c)
    {
        foreach (var d in Dirs)
            if (!IsSolid(c + d)) yield return c + d;
    }

    // True if a straight line between two cells passes only through free cells,
    // checking a small margin around the line so a body with the given radius fits.
    public bool HasClearLine(Vector2Int a, Vector2Int b, float radius)
    {
        Vector2 pa = CellToWorld(a);
        Vector2 pb = CellToWorld(b);
        float step = solid.cellSize.x * 0.25f;                 // sample 4 times per cell
        int samples = Mathf.CeilToInt(Vector2.Distance(pa, pb) / step);

        for (int i = 0; i <= samples; i++)
        {
            Vector2 p = Vector2.Lerp(pa, pb, samples == 0 ? 0f : (float)i / samples);

            // Check the centre point and four points around it at body radius
            if (IsSolid(WorldToCell(p))) return false;
            if (IsSolid(WorldToCell(p + Vector2.right * radius))) return false;
            if (IsSolid(WorldToCell(p + Vector2.left * radius))) return false;
            if (IsSolid(WorldToCell(p + Vector2.up * radius))) return false;
            if (IsSolid(WorldToCell(p + Vector2.down * radius))) return false;
        }
        return true;
    }
}

