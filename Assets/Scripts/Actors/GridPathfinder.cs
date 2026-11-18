using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Standard four-way A*: https://www.redblobgames.com/pathfinding/a-star/introduction.html
public sealed class GridPathfinder
{
    private static readonly Vector3Int[] Neighbours =
    {
        Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
    };

    private readonly Tilemap floor;
    private readonly List<RaycastHit2D> hits = new();
    private readonly ContactFilter2D filter = new() { useTriggers = false };

    public GridPathfinder(Tilemap floor)
    {
        this.floor = floor;
    }

    public bool CanTravel(Vector2 from, Vector2 to, Vector2 bodySize)
    {
        if (floor == null) return false;
        Vector2 delta = to - from;
        // Sample floor as well as walls so shortcuts cannot cross gaps in the station.
        float cellSize = Mathf.Min(floor.layoutGrid.cellSize.x * floor.transform.lossyScale.x,
            floor.layoutGrid.cellSize.y * floor.transform.lossyScale.y);
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / (cellSize * 0.25f)));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, (float)i / steps);
            if (!floor.HasTile(floor.WorldToCell(point))) return false;
        }

        Physics2D.BoxCast(from, bodySize + Vector2.one * 0.04f, 0f, delta.normalized,
            filter, hits, delta.magnitude);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D obstacle = hit.collider;
            if (obstacle.gameObject.layer == floor.gameObject.layer) continue;
            // Actors and movable items remain physics interactions, not permanent walls.
            if (obstacle.attachedRigidbody == null ||
                obstacle.attachedRigidbody.bodyType == RigidbodyType2D.Static) return false;
        }
        return true;
    }

    public bool FindPath(Vector2 from, Vector2 to, Vector2 bodySize, List<Vector2> path)
    {
        path.Clear();
        if (floor == null || !floor.HasTile(floor.WorldToCell(from)) ||
            !floor.HasTile(floor.WorldToCell(to))) return false;
        if (!TryNearestCell(from, bodySize, true, out Vector3Int start) ||
            !TryNearestCell(to, bodySize, false, out Vector3Int goal)) return false;

        var frontier = new List<Vector3Int> { start };
        var costs = new Dictionary<Vector3Int, int> { [start] = 0 };
        var previous = new Dictionary<Vector3Int, Vector3Int>();
        var walkable = new Dictionary<Vector3Int, bool>();

        while (frontier.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < frontier.Count; i++)
            {
                if (costs[frontier[i]] + Distance(frontier[i], goal) <
                    costs[frontier[best]] + Distance(frontier[best], goal)) best = i;
            }
            Vector3Int current = frontier[best];
            frontier.RemoveAt(best);
            if (current == goal)
            {
                path.Add(floor.GetCellCenterWorld(current));
                while (previous.TryGetValue(current, out Vector3Int parent))
                {
                    current = parent;
                    path.Add(floor.GetCellCenterWorld(current));
                }
                path.Reverse();
                return true;
            }

            foreach (Vector3Int direction in Neighbours)
            {
                Vector3Int next = current + direction;
                int cost = costs[current] + 1;
                if (costs.TryGetValue(next, out int oldCost) && cost >= oldCost) continue;
                if (!walkable.TryGetValue(next, out bool canStand))
                {
                    Vector2 point = floor.GetCellCenterWorld(next);
                    canStand = floor.HasTile(next) && CanTravel(point, point, bodySize);
                    walkable[next] = canStand;
                }
                if (!canStand || !CanTravel(floor.GetCellCenterWorld(current),
                    floor.GetCellCenterWorld(next), bodySize)) continue;

                costs[next] = cost;
                previous[next] = current;
                if (!frontier.Contains(next)) frontier.Add(next);
            }
        }
        return false;
    }

    private bool TryNearestCell(Vector2 position, Vector2 size, bool requireConnection,
        out Vector3Int nearest)
    {
        Vector3Int origin = floor.WorldToCell(position);
        nearest = origin;
        float best = float.PositiveInfinity;
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            Vector3Int cell = origin + new Vector3Int(x, y, 0);
            Vector2 centre = floor.GetCellCenterWorld(cell);
            float distance = (centre - position).sqrMagnitude;
            if (distance >= best || !floor.HasTile(cell) || !CanTravel(centre, centre, size)) continue;
            if (requireConnection && !CanTravel(position, centre, size)) continue;
            nearest = cell;
            best = distance;
        }
        return !float.IsPositiveInfinity(best);
    }

    private static int Distance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
