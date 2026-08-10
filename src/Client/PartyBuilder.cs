using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract;

namespace PokeMmo.Client;

/// <summary>
/// Builds the battlers for a wild encounter.
/// <para>
/// Both sides are placeholders and deliberately obvious ones. There is no party
/// system yet, so the player gets a fixed starter; there are no learnsets extracted
/// yet, so a wild creature gets a basic attack rather than the moves it should know.
/// Pretending otherwise would hide two missing features behind something that looks
/// finished.
/// </para>
/// </summary>
public static class PartyBuilder
{
    /// <summary>Moves given to the player's starter, by name so no index is hardcoded.</summary>
    private static readonly string[] StarterMoves = ["TACKLE", "VINE WHIP", "GROWL"];

    /// <summary>What a wild creature attacks with until learnsets are read.</summary>
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

        if (data.MoveNamed(WildFallbackMove) is { } move) battler.Moves.Add(move);
        else if (data.Moves.Count > 1) battler.Moves.Add(data.Moves[1]);

        return battler;
    }
}
