using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
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
