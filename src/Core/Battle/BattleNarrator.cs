using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// The words for a battle, supplied by whoever has the cartridge.
/// <para>
/// Events carry a <see cref="Side"/> and a move index and nothing else, because the
/// server that produces them has no names to give. This is where names enter, on the
/// machine that has an image to read them from.
/// </para>
/// </summary>
public sealed class BattleNames(
    string player,
    string opponent,
    Func<int, string> moveNamed,
    Func<int, string>? speciesNamed = null,
    Func<int, string>? itemNamed = null)
{
    /// <summary>Names for a battle nobody can put words to, so narration still works.</summary>
    public static readonly BattleNames Unknown = new("Your side", "The opponent", id => $"move {id}");

    public string Of(Side side) => side == Side.Player ? player : opponent;

    public string MoveNamed(int moveId) => moveNamed(moveId);

    /// <summary>
    /// What to call a species, for the one sentence that is about a species rather than
    /// about whoever is standing there. Falls back to the number, which is what a
    /// narrator with no cartridge has.
    /// </summary>
    public string SpeciesNamed(int species) => speciesNamed?.Invoke(species) ?? $"species {species}";

    /// <summary>What to call an item, for the one sentence that is about a thing.</summary>
    public string ItemNamed(int itemId) => itemNamed?.Invoke(itemId) ?? $"item {itemId}";
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

        // Named for the miss rather than for the damage, because the damage is the
        // consequence and the miss is the reason.
        BattleEvent.Crashed e => $"{names.Of(e.Side)} kept going and crashed!",

        BattleEvent.BlewUp e => $"{names.Of(e.Side)} blew up!",

        BattleEvent.Flinched e => $"{names.Of(e.Side)} flinched and couldn't move!",

        BattleEvent.Recovered e => $"{names.Of(e.Side)} regained {e.Amount} health.",

        BattleEvent.Confused e => $"{names.Of(e.Side)} became confused!",

        BattleEvent.SnappedOut e => $"{names.Of(e.Side)} snapped out of confusion!",

        // Said without naming the move, because there is no move: a confused creature
        // hits itself, and printing one would be printing something that did not happen.
        BattleEvent.HurtItself e => $"{names.Of(e.Side)} hurt itself in its confusion!",

        BattleEvent.Fainted e => $"{names.Of(e.Side)} fainted!",

        // The amount, not the item's number: a Potion used on somebody two health short
        // restores two, and saying twenty would be a lie the player can see.
        BattleEvent.HealthRestored e => e.Amount > 0
            ? $"{names.Of(e.Side)} recovered {e.Amount} HP!"
            : $"It would have no effect on {names.Of(e.Side)}.",

        // Named one at a time rather than as a list, because an item that puts several
        // things right on a creature that had one of them should say the one it fixed.
        BattleEvent.PutRight e => WhatWasPutRight(names.Of(e.Side), e.Cleared),

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

        // Two sentences' worth in one line, because the pause between them in the games
        // is a whole animation this project does not have. What matters is that both
        // names are said: "evolved!" on its own leaves the player looking at a list.
        BattleEvent.Evolved e => $"{names.SpeciesNamed(e.From)} evolved into {names.SpeciesNamed(e.Into)}!",

        // The four that take more than one turn. Each says what is owed rather than what
        // it looks like: the games have a different picture for FLY and DIG and this
        // project has no animation for either, so the honest line is the one about the
        // turn.
        BattleEvent.WentAway e =>
            $"{names.Of(e.Side)} vanished with {names.MoveNamed(e.MoveId)} — it lands next turn!",

        BattleEvent.Recharging e => $"{names.Of(e.Side)} must recharge after {names.MoveNamed(e.MoveId)}!",

        BattleEvent.Trapped e => $"{names.Of(e.Side)} was caught in {names.MoveNamed(e.MoveId)}!",

        BattleEvent.TrapHurt e => $"{names.Of(e.Side)} is hurt by {names.MoveNamed(e.MoveId)}!",

        // The sky, said the way the games say it — what it is doing rather than what it is
        // called. "It started to rain!" is a thing that happened; "Weather: Rain" is a
        // field on a struct.
        BattleEvent.WeatherBegan e => e.Weather switch
        {
            Weather.Rain => "It started to rain!",
            Weather.Sun => "The sunlight got bright!",
            Weather.Sandstorm => "A sandstorm kicked up!",
            _ => "It started to hail!",
        },

        BattleEvent.WeatherEnded e => e.Weather switch
        {
            Weather.Rain => "The rain stopped.",
            Weather.Sun => "The sunlight faded.",
            Weather.Sandstorm => "The sandstorm subsided.",
            _ => "The hail stopped.",
        },

        BattleEvent.WeatherHurt e =>
            $"{names.Of(e.Side)} is buffeted by the {(e.Weather == Weather.Hail ? "hail" : "sandstorm")}!",

        BattleEvent.WeatherHealed e => $"{names.Of(e.Side)} drinks it in!",

        BattleEvent.ItemHealed e =>
            $"{names.Of(e.Side)} restored a little health with its {names.ItemNamed(e.ItemId)}!",

        BattleEvent.HeldOn e => $"{names.Of(e.Side)} hung on using its {names.ItemNamed(e.ItemId)}!",

        BattleEvent.WentFirst e => $"{names.Of(e.Side)}'s {names.ItemNamed(e.ItemId)} let it move first!",

        BattleEvent.AteIt e => $"{names.Of(e.Side)} used up its {names.ItemNamed(e.ItemId)}.",

        BattleEvent.ScreenRose e =>
            $"{names.Of(e.Side)} is shielded against {(e.Physical ? "physical" : "special")} moves!",

        BattleEvent.Seeded e => $"{names.Of(e.Side)} was seeded!",

        BattleEvent.Sapped e => $"{names.Of(e.Side)}'s health was sapped!",

        BattleEvent.WallsBroke e => $"{names.Of(e.Side)}'s shield was shattered!",

        BattleEvent.KnockedOff e => $"{names.Of(e.Side)}'s {names.ItemNamed(e.ItemId)} was knocked off!",

        BattleEvent.ShookFree e => $"{names.Of(e.Side)} shook itself free!",

        BattleEvent.Identified e => $"{names.Of(e.Side)} was identified!",

        BattleEvent.HurtBySleep e => $"{names.Of(e.Side)} is locked in a nightmare!",

        BattleEvent.Drowsy e => $"{names.Of(e.Side)} grew drowsy!",

        BattleEvent.TookRoot e => $"{names.Of(e.Side)} planted its roots!",

        BattleEvent.PerishCount e => $"{names.Of(e.Side)}'s count fell to {e.Turns}!",

        BattleEvent.Taunted e => $"{names.Of(e.Side)} fell for the taunt!",

        BattleEvent.Tormented e => $"{names.Of(e.Side)} was subjected to torment!",

        BattleEvent.BracedItself e => $"{names.Of(e.Side)} braced itself!",

        BattleEvent.Endured e => $"{names.Of(e.Side)} endured the hit!",

        BattleEvent.Bonded e => $"{names.Of(e.Side)} is trying to take its foe with it!",

        BattleEvent.TookThemWith e => $"{names.Of(e.Side)} was taken down with it!",

        BattleEvent.HealthShared e => $"The battlers shared their pain, at {e.Each} each.",

        BattleEvent.CopiedStages e => $"{names.Of(e.Side)} copied its foe's changes!",

        BattleEvent.UsedInstead e => $"{names.Of(e.Side)} used {names.MoveNamed(e.MoveId)} instead!",

        BattleEvent.LearnedMove e => e.ForGood
            ? $"{names.Of(e.Side)} learned {names.MoveNamed(e.MoveId)}!"
            : $"{names.Of(e.Side)} copied {names.MoveNamed(e.MoveId)}!",

        BattleEvent.AbilityMoved e => $"{names.Of(e.Side)} took on a new ability!",

        BattleEvent.LostItsNerve e => $"{names.Of(e.Side)} lost its focus and could not move!",

        BattleEvent.Damped e => $"{names.Of(e.Side)} damped one kind of move down!",

        BattleEvent.Grazed e => $"{names.Of(e.Side)} is hurt reaching them!",

        BattleEvent.BrokeFree e => $"{names.Of(e.Side)} got free of {names.MoveNamed(e.MoveId)}!",

        BattleEvent.OneHitKnockout e => $"It was a one-hit knockout on {names.Of(e.Side)}!",

        BattleEvent.Unaffected e => $"It had no effect on {names.Of(e.Side)}.",
        BattleEvent.Protected e => $"{names.Of(e.Side)} protected itself!",
        BattleEvent.StagesCleared e => $"{names.Of(e.Side)} eliminated all stat changes!",
        BattleEvent.MistRose e => $"{names.Of(e.Side)} became shrouded in MIST!",
        BattleEvent.Safeguarded e => $"{names.Of(e.Side)} is covered by a veil!",
        BattleEvent.TookAim e => $"{names.Of(e.Side)} took aim!",
        BattleEvent.Shielded e => $"{names.Of(e.Side)} is protected!",
        BattleEvent.CannotUse e => $"{names.Of(e.Side)}'s {names.MoveNamed(e.MoveId)} was disabled!",
        BattleEvent.CanUseAgain e => $"{names.Of(e.Side)} is no longer disabled!",
        BattleEvent.MustRepeat e => $"{names.Of(e.Side)} must do {names.MoveNamed(e.MoveId)} again!",

        BattleEvent.GotAway => "Got away safely!",

        BattleEvent.CouldNotGetAway => "Couldn't get away!",

        BattleEvent.HeldFast e => $"{names.MoveNamed(e.MoveId)} is holding on — there is no getting away!",

        BattleEvent.BlownAway e => $"{names.Of(e.Side)} was blown away by {names.MoveNamed(e.MoveId)}!",

        BattleEvent.Stole e => $"{names.Of(e.Side)} took the {names.ItemNamed(e.ItemId)}!",

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

    /// <summary>
    /// What an item put right, named one at a time. An item that clears six things used
    /// on a creature that had one of them should say the one it fixed.
    /// </summary>
    private static string WhatWasPutRight(string who, Ailments cleared) => cleared switch
    {
        Ailments.Poison => $"{who} was cured of its poisoning!",
        Ailments.Burn => $"{who}'s burn was healed!",
        Ailments.Paralysis => $"{who} was cured of paralysis!",
        Ailments.Sleep => $"{who} woke up!",
        Ailments.Freeze => $"{who} was defrosted!",
        Ailments.Confusion => $"{who} snapped out of its confusion!",
        _ => $"{who} is feeling better!",
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
