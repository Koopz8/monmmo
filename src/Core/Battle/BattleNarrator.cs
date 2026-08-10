namespace PokeMmo.Core.Battle;

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
    public static string Describe(BattleEvent battleEvent) => battleEvent switch
    {
        BattleEvent.MoveUsed e => $"{e.Attacker} used {e.Move}!",

        BattleEvent.MoveMissed e => $"{e.Attacker}'s attack missed!",

        BattleEvent.NoEffect e => $"It doesn't affect {e.Target}...",

        BattleEvent.DamageDealt e => DescribeDamage(e),

        BattleEvent.Immobilised e => e.Cause switch
        {
            StatusCondition.Sleep => $"{e.Name} is fast asleep.",
            StatusCondition.Freeze => $"{e.Name} is frozen solid!",
            _ => $"{e.Name} is paralysed! It can't move!",
        },

        BattleEvent.WokeUp e => $"{e.Name} woke up!",

        BattleEvent.StatusHurt e => e.Status == StatusCondition.Burn
            ? $"{e.Name} is hurt by its burn!"
            : $"{e.Name} is hurt by poison!",

        BattleEvent.Fainted e => $"{e.Name} fainted!",

        BattleEvent.BallThrown e => e.Caught
            ? $"Gotcha! {e.Target} was caught!"
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
    private static string DescribeDamage(BattleEvent.DamageDealt e)
    {
        var line = new System.Text.StringBuilder();

        if (e.Detail.Critical) line.Append("A critical hit! ");

        if (e.Detail.SuperEffective) line.Append("It's super effective! ");
        else if (e.Detail.NotVeryEffective) line.Append("It's not very effective... ");

        line.Append($"{e.Target} took {e.Damage} damage.");
        return line.ToString();
    }

    /// <summary>Every line for a turn, skipping events that have nothing to say.</summary>
    public static IEnumerable<string> Describe(IEnumerable<BattleEvent> events) =>
        events.Select(Describe).Where(line => line.Length > 0);
}
