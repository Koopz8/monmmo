namespace PokeMmo.Core.World;

/// <summary>One door, and what the door it names says about it.</summary>
/// <param name="MapId">The map this door is on.</param>
/// <param name="Index">Its place in that map's warp list, which is the number other doors name.</param>
/// <param name="Square">Where it is.</param>
/// <param name="TargetMapId">The map it names.</param>
/// <param name="TargetIndex">The warp it names on that map, after the sentinel is resolved.</param>
public sealed record ADoorAndItsOther(
    string MapId, int Index, GridPosition Square, string TargetMapId, int TargetIndex)
{
    /// <summary>True when the map it names is in the world file at all.</summary>
    public bool TheMapIsThere { get; init; }

    /// <summary>True when that map has a warp at the index this one names.</summary>
    public bool TheDoorIsThere { get; init; }

    /// <summary>True when the door it names names this map back.</summary>
    public bool NamesTheMapBack { get; init; }

    /// <summary>True when the door it names names THIS door back — index and all.</summary>
    /// <remarks>
    /// The tight half of the pair. Most maps' doors all lead to the same place, so "names the map
    /// back" is nearly free and says very little; naming the exact warp back is a claim that can
    /// fail, and the mis-indexed control below is what shows the difference is real.
    /// </remarks>
    public bool NamesThisDoorBack { get; init; }

    /// <summary>True when the target is the runtime sentinel — where you came from.</summary>
    public bool DecidedAtRuntime { get; init; }

    public override string ToString() =>
        $"{MapId} warp {Index} {Square} -> {TargetMapId} warp {TargetIndex}"
        + (DecidedAtRuntime ? "  (decided at runtime)"
            : !TheMapIsThere ? "  (no such map here)"
            : !TheDoorIsThere ? "  (no such warp there)"
            : NamesThisDoorBack ? "  (mirrored)"
            : NamesTheMapBack ? "  (comes back to this map, by another door)"
            : "  ONE WAY");
}

/// <summary>
/// Whether the door on the other side of a door leads back through it.
/// <para>
/// <b>The static half of <see cref="TheWayBack"/>, and it does not know the walk exists.</b> The
/// walk finds places it got into and could not get out of; this asks the map data on its own
/// whether a door has a partner, which is a fact about the cartridge and not about any route
/// through it. Two readings that cannot have been tuned to agree is what this project spends its
/// milestones looking for — 261's 751 and 263's 605 are the shape.
/// </para>
/// <para>
/// A warp record names a MAP and an INDEX into that map's own warp list. Nothing in the format
/// makes that a pair: the far warp is free to name a third map, or the same map by a different
/// door, or a sentinel meaning "wherever you came from". Whether it names this one back is
/// therefore a measurement.
/// </para>
/// </summary>
public static class TheDoorOnTheOtherSide
{
    /// <summary>Every door in the world with the door it names looked up.</summary>
    /// <param name="shift">
    /// Added to the index each door names before looking it up, which is the negative control.
    /// <para>
    /// Nought is the reading. One is the same question asked of the wrong door on the right map,
    /// and it is the only floor that matters here: if a map's doors all lead home anyway, "names
    /// the map back" scores high whatever index is used, and the difference between the two runs
    /// is the whole of the evidence.
    /// </para>
    /// </param>
    public static IReadOnlyList<ADoorAndItsOther> In(WorldData world, int shift = 0)
    {
        Dictionary<string, MapData> maps = world.Maps.ToDictionary(m => m.Id);
        var doors = new List<ADoorAndItsOther>();

        foreach (MapData map in world.Maps)
        {
            for (var i = 0; i < map.Warps.Count; i++)
            {
                Warp warp = map.Warps[i];

                if (warp.IsDynamic)
                {
                    doors.Add(new ADoorAndItsOther(
                        map.Id, i, warp.Square, warp.TargetMapId, warp.TargetWarpId)
                    {
                        DecidedAtRuntime = true,
                    });

                    continue;
                }

                bool there = maps.TryGetValue(warp.TargetMapId, out MapData? target);

                // The sentinel index resolves the way the walk resolves it — the target warp's
                // own square — so the two halves are asking about the same door. Stating that
                // rule twice is how the mirror would come to disagree with the walk about which
                // door it even means.
                int at = target is null ? warp.TargetWarpId
                    : warp.TargetWarpId == Warp.Unspecified
                        || warp.TargetWarpId < 0
                        || warp.TargetWarpId >= target.Warps.Count
                        ? 0
                        : warp.TargetWarpId;

                if (target is not null && target.Warps.Count > 0)
                    at = ((at + shift) % target.Warps.Count + target.Warps.Count) % target.Warps.Count;

                bool has = target is not null && at >= 0 && at < target.Warps.Count;
                Warp? other = has ? target!.Warps[at] : null;

                doors.Add(new ADoorAndItsOther(map.Id, i, warp.Square, warp.TargetMapId, at)
                {
                    TheMapIsThere = there,
                    TheDoorIsThere = has,
                    NamesTheMapBack = other is not null && other.TargetMapId == map.Id,
                    NamesThisDoorBack = other is not null
                        && other.TargetMapId == map.Id
                        && Points(other, map.Warps.Count, i),
                });
            }
        }

        return doors;
    }

    /// <summary>Whether a door points at warp <paramref name="index"/> of the map it names.</summary>
    /// <remarks>
    /// The sentinel counts as naming warp nought, which is what the games do and what
    /// <c>WorldWalker.Arrival</c> already does. A door that says "put them at the first warp" and
    /// a door that says "put them at warp 0" are one door said two ways.
    /// </remarks>
    /// <summary>
    /// The same mirror asked of the THIRD kind of edge: the joins along a map's own borders.
    /// </summary>
    /// <remarks>
    /// A world's edges are steps, borders and doors. Steps are symmetric by construction except
    /// where a ledge says otherwise; the doors are above. Nobody had asked the borders, and a
    /// border declared on one map and not the other is a one-way join with no ledge to explain
    /// it — the kind of asymmetry a walk would report as reach and never as a trap.
    /// <para>
    /// Returns every join and whether the map on the far side declares one back on the opposite
    /// side. The opposite of a side is the only thing modelled here, and it is arithmetic.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string MapId, ConnectionSide Side, string Other, bool Back)>
        Borders(WorldData world)
    {
        Dictionary<string, MapData> maps = world.Maps.ToDictionary(m => m.Id);

        return
        [
            .. from map in world.Maps
               from side in map.Connections
               select (map.Id, side.Side, side.MapId,
                   maps.TryGetValue(side.MapId, out MapData? other)
                   && other.Connections.Any(
                       b => b.Side == Opposite(side.Side) && b.MapId == map.Id)),
        ];
    }

    /// <summary>The side a walker crossing this one arrives on.</summary>
    public static ConnectionSide Opposite(ConnectionSide side) => side switch
    {
        ConnectionSide.Up => ConnectionSide.Down,
        ConnectionSide.Down => ConnectionSide.Up,
        ConnectionSide.Left => ConnectionSide.Right,
        _ => ConnectionSide.Left,
    };

    private static bool Points(Warp door, int warpsBackHere, int index) =>
        door.TargetWarpId == index
        || (index == 0 && (door.TargetWarpId == Warp.Unspecified
            || door.TargetWarpId < 0
            || door.TargetWarpId >= warpsBackHere));
}
