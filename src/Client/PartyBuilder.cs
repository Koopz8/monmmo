using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;

namespace PokeMmo.Client;

/// <summary>
/// Builds the battlers for a wild encounter.
/// <para>
/// Wild creatures now use the moves their species actually learns by that level. The
/// player's starter is still fixed, because there is no party system to draw from yet
/// — left obvious rather than disguised.
/// </para>
/// </summary>
public static class PartyBuilder
{
    /// <summary>Moves given to the player's starter, by name so no index is hardcoded.</summary>
    private static readonly string[] StarterMoves = ["TACKLE", "VINE WHIP", "GROWL"];

    /// <summary>Used only when a species' learnset could not be read.</summary>
    private const string WildFallbackMove = "TACKLE";

    public static Battler? BuildStarter(GameData data, int species, int level)
    {
        if (data.SpeciesAt(species) is not { } record) return null;

        var battler = new Battler(record, level);

        foreach (string name in StarterMoves)
            if (data.MoveNamed(name) is { } move) battler.Moves.Add(move);

        if (battler.Moves.Count == 0 && data.Moves.Count > 1) battler.Moves.Add(data.Moves[1]);

        return battler;
    }

    /// <summary>
    /// Turns a saved party member back into a battler.
    /// <para>
    /// This is the join the split exists for: the server kept numbers, and the
    /// cartridge on this machine supplies the name, the stats and the sprite. Nothing
    /// meaningful comes out of a save without the player's own image beside it.
    /// </para>
    /// </summary>
    public static Battler? Restore(GameData data, SavedMon saved)
    {
        if (data.SpeciesAt(saved.Species) is not { } record) return null;

        var battler = new Battler(record, saved.Level, saved.Nature, saved.Nickname);

        foreach (int moveId in saved.Moves)
            if (data.MoveAt(moveId) is { } move) battler.Moves.Add(move);

        if (battler.Moves.Count == 0) battler.Moves.AddRange(data.MovesKnownAt(saved.Species, saved.Level));

        // Damage is applied rather than set, because health is computed from base
        // stats the server never saw — a save holding "12 HP" against a maximum this
        // client works out differently would be a slow drift.
        int missing = Math.Clamp(battler.MaxHp - saved.CurrentHp, 0, battler.MaxHp);
        if (missing > 0) battler.TakeDamage(missing);

        battler.Status = saved.Status;

        return battler;
    }

    /// <summary>What to send the server for a battler, as numbers only.</summary>
    public static SavedMon ToSaved(Battler battler) =>
        new(
            Species: battler.Species.Index,
            Level: battler.Level,
            Nickname: battler.Name == battler.Species.Name ? null : battler.Name,
            CurrentHp: battler.CurrentHp,
            Status: battler.Status,
            Nature: battler.Nature,
            Moves: battler.Moves.Select(m => m.Id).ToList());

    public static Battler? BuildWild(GameData data, int species, int level)
    {
        if (data.SpeciesAt(species) is not { } record) return null;

        var battler = new Battler(record, level);
        battler.Moves.AddRange(data.MovesKnownAt(species, level));

        if (battler.Moves.Count == 0)
        {
            if (data.MoveNamed(WildFallbackMove) is { } move) battler.Moves.Add(move);
            else if (data.Moves.Count > 1) battler.Moves.Add(data.Moves[1]);
        }

        return battler;
    }
}
