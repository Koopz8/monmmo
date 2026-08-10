namespace PokeMmo.Core.Battle;

/// <summary>
/// The words for a battle, supplied by whoever has the cartridge.
/// <para>
/// Events carry a <see cref="Side"/> and a move index and nothing else, because the
/// server that produces them has no names to give. This is where names enter, on the
/// machine that has an image to read them from.
/// </para>
/// </summary>
public sealed class BattleNames(string player, string opponent, Func<int, string> moveNamed)
{
    /// <summary>Names for a battle nobody can put words to, so narration still works.</summary>
    public static readonly BattleNames Unknown = new("Your side", "The opponent", id => $"move {id}");

    public string Of(Side side) => side == Side.Player ? player : opponent;

    public string MoveNamed(int moveId) => moveNamed(moveId);
}

/// <summary>
/// Turns battle events into the lines a player reads.
/// <para>
/// Kept out of the renderer so the wording is testable and so a future client — or a
/// server-side log — phrases a battle identically. Drawing code should decide where
/// text goes, not what it says.
/// </para>
/// </summary>
public static class BattleNarrator
{
    public static string Describe(BattleEvent battleEvent, BattleNames names) => battleEvent switch
    {
        BattleEvent.MoveUsed e => $"{names.Of(e.Side)} used {names.MoveNamed(e.MoveId)}!",

        BattleEvent.MoveMissed e => $"{names.Of(e.Side)}'s attack missed!",

        BattleEvent.NoEffect e => $"It doesn't affect {names.Of(e.Side)}...",

        BattleEvent.DamageDealt e => DescribeDamage(e, names),

        BattleEvent.Immobilised e => e.Cause switch
        {
            StatusCondition.Sleep => $"{names.Of(e.Side)} is fast asleep.",
            StatusCondition.Freeze => $"{names.Of(e.Side)} is frozen solid!",
            _ => $"{names.Of(e.Side)} is paralysed! It can't move!",
        },

        BattleEvent.WokeUp e => $"{names.Of(e.Side)} woke up!",

        BattleEvent.StatusHurt e => e.Status == StatusCondition.Burn
            ? $"{names.Of(e.Side)} is hurt by its burn!"
            : $"{names.Of(e.Side)} is hurt by poison!",

        BattleEvent.Fainted e => $"{names.Of(e.Side)} fainted!",

        BattleEvent.BallThrown e => e.Caught
            ? $"Gotcha! {names.Of(e.Target)} was caught!"
            : e.Shakes switch
            {
                0 => "Oh no! It broke free!",
                1 => "Aww! It appeared to be caught!",
                2 => "Aargh! Almost had it!",
                _ => "Gah! It was so close, too!",
            },

        BattleEvent.Ended e => e.Winner switch
        {
            Side.Player => "You won the battle!",
            Side.Opponent => "You have no more usable Pokémon!",
            _ => "The battle ended in a draw.",
        },

        _ => string.Empty,
    };

    /// <summary>
    /// Effectiveness is announced only when it is not neutral, which is how the games
    /// do it — saying "it's normally effective" every turn would be noise.
    /// </summary>
    private static string DescribeDamage(BattleEvent.DamageDealt e, BattleNames names)
    {
        var line = new System.Text.StringBuilder();

        if (e.Detail.Critical) line.Append("A critical hit! ");

        if (e.Detail.SuperEffective) line.Append("It's super effective! ");
        else if (e.Detail.NotVeryEffective) line.Append("It's not very effective... ");

        line.Append($"{names.Of(e.Side)} took {e.Damage} damage.");
        return line.ToString();
    }

    /// <summary>Every line for a turn, skipping events that have nothing to say.</summary>
    public static IEnumerable<string> Describe(IEnumerable<BattleEvent> events, BattleNames names) =>
        events.Select(e => Describe(e, names)).Where(line => line.Length > 0);
}
