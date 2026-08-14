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
    /// <summary>
    /// One event as a sentence.
    /// <para>
    /// Capitalised at the end rather than in each line, because half of these start with
    /// a name and the name a wild creature goes by is "the wild PIDGEY" — which put a
    /// lower-case "the" at the start of "the wild PIDGEY was poisoned!" and of every
    /// other line it led.
    /// </para>
    /// </summary>
    public static string Describe(BattleEvent battleEvent, BattleNames names)
    {
        string line = Line(battleEvent, names);

        return line.Length > 0 && char.IsLower(line[0]) ? char.ToUpperInvariant(line[0]) + line[1..] : line;
    }

    private static string Line(BattleEvent battleEvent, BattleNames names) => battleEvent switch
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

        BattleEvent.StatusInflicted e => e.Status switch
        {
            StatusCondition.Sleep => $"{names.Of(e.Side)} fell asleep!",
            StatusCondition.Poison => $"{names.Of(e.Side)} was poisoned!",
            StatusCondition.Paralysis => $"{names.Of(e.Side)} is paralysed! It may be unable to move!",
            StatusCondition.Burn => $"{names.Of(e.Side)} was burned!",
            _ => $"{names.Of(e.Side)} was frozen solid!",
        },

        // Named by how far it moved rather than by which move did it, because the same
        // move can be at its limit on the second use and the games say so: "won't go any
        // lower" is the line that stops a player pressing SCREECH six times.
        BattleEvent.StageChanged e => !e.Moved
            ? $"{names.Of(e.Side)}'s {NameOf(e.Stat)} won't go any {(e.Stages > 0 ? "higher" : "lower")}!"
            : $"{names.Of(e.Side)}'s {NameOf(e.Stat)} {(e.Stages > 0 ? "rose" : "fell")}" +
              $"{(Math.Abs(e.Stages) > 1 ? " sharply" : "")}!",

        BattleEvent.NothingHappened e => $"It had no effect on {names.Of(e.Side)}.",

        // Said once, after the hits, rather than counted out one line at a time. Five
        // "took 4 damage" in a row is the same information read five times.
        BattleEvent.HitSeveralTimes e => $"Hit {e.Times} time{(e.Times == 1 ? "" : "s")}!",

        BattleEvent.Drained e => $"{names.Of(e.Side)} had its energy drained back.",

        BattleEvent.Recoiled e => $"{names.Of(e.Side)} was hurt by the recoil!",

        BattleEvent.Flinched e => $"{names.Of(e.Side)} flinched and couldn't move!",

        BattleEvent.Recovered e => $"{names.Of(e.Side)} regained {e.Amount} health.",

        BattleEvent.Fainted e => $"{names.Of(e.Side)} fainted!",

        // The amount, not the item's number: a Potion used on somebody two health short
        // restores two, and saying twenty would be a lie the player can see.
        BattleEvent.HealthRestored e => e.Amount > 0
            ? $"{names.Of(e.Side)} recovered {e.Amount} HP!"
            : $"It would have no effect on {names.Of(e.Side)}.",

        BattleEvent.BallThrown e => e.Caught
            ? $"Gotcha! {names.Of(e.Target)} was caught!"
            : e.Shakes switch
            {
                0 => "Oh no! It broke free!",
                1 => "Aww! It appeared to be caught!",
                2 => "Aargh! Almost had it!",
                _ => "Gah! It was so close, too!",
            },

        BattleEvent.ExperienceGained e => $"{names.Of(e.Side)} gained {e.Amount} EXP!",

        BattleEvent.LevelledUp e => $"{names.Of(e.Side)} grew to level {e.Level}!",

        BattleEvent.MoveLearned e => $"{names.Of(e.Side)} learned {names.MoveNamed(e.MoveId)}!",

        BattleEvent.MoveNotLearned e =>
            $"{names.Of(e.Side)} wants to learn {names.MoveNamed(e.MoveId)}, but already knows four moves.",

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
    /// <summary>
    /// What the games call each stat in a battle line. Spelled out rather than taken from
    /// the enum, which says "SpAttack" and "Defense".
    /// </summary>
    private static string NameOf(Stat stat) => stat switch
    {
        Stat.Attack => "ATTACK",
        Stat.Defense => "DEFENSE",
        Stat.Speed => "SPEED",
        Stat.SpAttack => "SPECIAL ATTACK",
        Stat.SpDefense => "SPECIAL DEFENSE",
        Stat.Accuracy => "accuracy",
        Stat.Evasion => "evasiveness",
        _ => "HP",
    };

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
