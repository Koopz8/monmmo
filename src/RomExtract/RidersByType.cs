using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract;

/// <summary>A type whose rider this engine has already settled, and the groups that settle it.</summary>
public sealed record TypeRider(PokemonType Type, StatusCondition Status, IReadOnlyList<byte> From);

/// <summary>A group the rule accounts for, and whether the engine currently agrees.</summary>
public sealed record RiderGroup(
    byte Effect,
    PokemonType Type,
    StatusCondition Status,
    IReadOnlyList<string> Moves,
    bool EngineAgrees);

/// <summary>
/// What a type already means, and the silent groups that follow from it.
/// <para>
/// A hundred and thirty-one effect groups do nothing in this engine, and the reason is
/// always the same: the effect byte names a group and what the group does is in the
/// cartridge's code. Writing the answers down from memory of another game is the mistake
/// this project rules out, so most of them stay silent.
/// </para>
/// <para>
/// Some do not have to. This engine already applies a status to nine effect groups, and
/// four of those groups are one type all the way through — every FIRE group it knows burns,
/// every POISON group it knows poisons. That is not memory, it is this engine's own settled
/// behaviour, and a silent damaging group that is also one type all the way through and
/// carries a secondary chance in every record is the same claim asked a second time.
/// </para>
/// <para>
/// The single-type condition is the whole guard, on both sides. A modelled group of mixed
/// types says nothing about any one of them: the group holding THUNDERBOLT also holds BODY
/// SLAM and LICK and DRAGONBREATH, and reading it as "electric means paralysis" would
/// equally read it as "normal means paralysis" and "dragon means paralysis". So it is not
/// read at all, and the cost is named rather than hidden — THUNDER carries a thirty percent
/// rider this rule declines to name, because the only evidence for it is a group that is
/// evidence for four contradictory things.
/// </para>
/// </summary>
public static class RidersByType
{
    /// <summary>
    /// The types this engine has already settled: those whose every modelled status group
    /// is that one type throughout, and where all such groups agree on the status.
    /// </summary>
    public static List<TypeRider> Settled(IEnumerable<MoveData> moves, Func<byte, MoveEffect> engine)
    {
        List<MoveData> all = [.. moves];

        var byType = new Dictionary<PokemonType, (SortedSet<byte> From, HashSet<StatusCondition> Says)>();

        foreach (IGrouping<byte, MoveData> group in all.GroupBy(m => m.Effect))
        {
            StatusCondition status = engine(group.Key).Status;

            if (status == StatusCondition.None) continue;

            List<PokemonType> types = [.. group.Select(m => m.Type).Distinct()];

            // A group of more than one type is evidence about none of them.
            if (types.Count != 1) continue;

            if (!byType.TryGetValue(types[0], out var found)) byType[types[0]] = found = ([], []);

            found.From.Add(group.Key);
            found.Says.Add(status);
        }

        return
        [
            .. byType
                .Where(t => t.Value.Says.Count == 1)
                .Select(t => new TypeRider(t.Key, t.Value.Says.Single(), [.. t.Value.From]))
                .OrderBy(t => t.Type),
        ];
    }

    /// <summary>
    /// The damaging groups the rule accounts for: one type throughout, a secondary chance
    /// in every record, and a type this engine has settled.
    /// <para>
    /// The groups that settled the type are in here too, and on purpose. Leaving them out
    /// looked tidier and was a trap: a group the rule teaches the engine becomes, on the
    /// next reading, one of the groups that settles its own type — so the four answers
    /// vanished from the report the moment they were acted on. Everything is listed, and
    /// each says whether the engine currently agrees with it. Before the arms were written
    /// four of them disagreed; after, none do, and that difference is the whole audit.
    /// </para>
    /// </summary>
    public static List<RiderGroup> Accounted(IEnumerable<MoveData> moves, Func<byte, MoveEffect> engine)
    {
        List<MoveData> all = [.. moves];

        Dictionary<PokemonType, StatusCondition> settled =
            Settled(all, engine).ToDictionary(t => t.Type, t => t.Status);

        var found = new List<RiderGroup>();

        foreach (IGrouping<byte, MoveData> group in all.GroupBy(m => m.Effect).OrderBy(g => g.Key))
        {
            List<MoveData> list = [.. group];

            // Damaging, and every record says there is something riding on it.
            if (list.Any(m => m.Power == 0 || m.SecondaryChance == 0)) continue;

            List<PokemonType> types = [.. list.Select(m => m.Type).Distinct()];

            if (types.Count != 1) continue;
            if (!settled.TryGetValue(types[0], out StatusCondition status)) continue;

            // A group this engine already has an answer for is not a group the rule
            // gets to speak about, whatever type it is. The first draft of this had no
            // such guard and its first answer was AURORA BEAM — one ICE move, one record,
            // a ten percent rider, and every mark of a group that freezes. It does not
            // freeze; this engine has had it down as an attack drop since long before,
            // for reasons of its own. That is the shape of everything this rule can get
            // wrong, and the reason the guard is a refusal rather than a preference.
            MoveEffect said = engine(group.Key);

            if (said.Kind != EffectKind.None && said.Status != status) continue;

            found.Add(new RiderGroup(
                group.Key,
                types[0],
                status,
                [.. list.Select(m => m.Name)],
                engine(group.Key).Status == status));
        }

        return found;
    }
}
