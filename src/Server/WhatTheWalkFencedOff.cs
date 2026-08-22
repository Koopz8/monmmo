using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One map the walk reached, and how much of it it never stood on (287).</summary>
/// <param name="MapId">The map.</param>
/// <param name="Walkable">Squares somebody could stand on.</param>
/// <param name="StoodOn">Squares the walk actually stood on.</param>
public sealed record APocket(string MapId, int Walkable, int StoodOn)
{
    /// <summary>Walkable squares on a reached map that the walk never got to.</summary>
    public int Fenced => Walkable - StoodOn;

    public override string ToString() => $"{MapId}  {Fenced} of {Walkable} fenced off";
}

/// <summary>
/// The walkable ground inside a REACHED map that the walk never stood on (287).
/// <para>
/// <b>Reaching a map is not standing on a square</b> (282), and the corollary nothing had asked
/// is that a map can be reached and still hold ground nothing can walk to. Every POKéMON CENTER
/// in this game is one: 86 walkable squares, 74 stood on, and the twelve left over are behind the
/// counter — which is where the CABLE CLUB's door is. Nineteen maps declare a warp into
/// <c>0.1</c> and nineteen more into <c>0.4</c>, every one of those squares is walkable, every
/// one is on a map the widest run reaches, and the walk stands on none of them.
/// </para>
/// <para>
/// Only reached maps are counted. A map nothing reaches is a REACH problem and 249's fault was
/// exactly to let those two share a number.
/// </para>
/// <para>
/// The walk's own squares, against the walk's own grid: measuring the pocket with the water open
/// against a walk that could not swim would report the sea as fenced-off ground.
/// </para>
/// </summary>
public static class WhatTheWalkFencedOff
{
    /// <summary>Every reached map with walkable ground the walk never stood on, biggest first.</summary>
    /// <param name="world">The world walked.</param>
    /// <param name="stood">Where the walk stood, from <c>Reach.Stood</c>.</param>
    /// <param name="reached">The maps it reached.</param>
    /// <param name="surfing">Whether the walk could cross water — the grid must be the walk's.</param>
    public static IReadOnlyList<APocket> In(
        WorldData world,
        IEnumerable<(string MapId, GridPosition Square)> stood,
        IReadOnlyCollection<string> reached,
        bool surfing)
    {
        Dictionary<string, int> standing = stood.GroupBy(s => s.MapId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Square).Distinct().Count());

        var pockets = new List<APocket>();

        foreach (MapData map in world.Maps)
        {
            if (!reached.Contains(map.Id)) continue;

            CollisionGrid grid = map.ToGrid(surfing);

            var open = 0;

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                    if (grid.IsWalkable(new GridPosition(x, y))) open++;
            }

            var pocket = new APocket(map.Id, open, standing.GetValueOrDefault(map.Id));

            if (pocket.Fenced > 0) pockets.Add(pocket);
        }

        return
        [
            .. pockets.OrderByDescending(p => p.Fenced).ThenBy(p => p.MapId, StringComparer.Ordinal),
        ];
    }
}
