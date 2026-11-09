using System;
using System.Collections.Generic;
using UnityEngine;

// implements A* for enemy pathfinding
public static class Pathfinder
{
    static int Heuristic(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);   // Manhattan distance

    public static List<Vector2Int> FindPath(
        Vector2Int start, Vector2Int goal,
        Func<Vector2Int, IEnumerable<Vector2Int>> neighbors)
    {
        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, int> { [start] = 0 };
        var f = new Dictionary<Vector2Int, int> { [start] = Heuristic(start, goal) };
        var closed = new HashSet<Vector2Int>();

        while (open.Count > 0)
        {
            // Pick the open cell with the lowest f (a linear scan is fine for room-sized grids;
            // swap in a heap if profiling ever shows this is slow)
            int best = 0;
            for (int i = 1; i < open.Count; i++)
                if (f[open[i]] < f[open[best]]) best = i;

            Vector2Int current = open[best];
            if (current == goal) return Reconstruct(cameFrom, start, goal);

            open.RemoveAt(best);
            closed.Add(current);

            foreach (Vector2Int next in neighbors(current))
            {
                if (closed.Contains(next)) continue;

                int tentative = g[current] + 1;               // each step costs 1
                if (g.TryGetValue(next, out int old) && tentative >= old) continue;

                cameFrom[next] = current;
                g[next] = tentative;
                f[next] = tentative + Heuristic(next, goal);
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return null;   // no path
    }

    static List<Vector2Int> Reconstruct(
        Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        for (var c = goal; c != start; c = cameFrom[c]) path.Add(c);
        path.Reverse();
        return path;   // excludes the start cell, includes the goal
    }

    // Removes unnecessary waypoints: from each anchor, jumps to the farthest cell
    // that has a clear straight line to it, according to the supplied check.
    public static List<Vector2Int> Smooth(
        List<Vector2Int> path, Vector2Int start,
        Func<Vector2Int, Vector2Int, bool> hasClearLine)
    {
        var result = new List<Vector2Int>();
        Vector2Int anchor = start;
        int idx = 0;

        while (idx < path.Count)
        {
            int best = idx;   // the next cell is always reachable (it was a valid step)
            for (int j = path.Count - 1; j > idx; j--)
            {
                if (hasClearLine(anchor, path[j])) { best = j; break; }
            }
            result.Add(path[best]);
            anchor = path[best];
            idx = best + 1;
        }
        return result;
    }
}