using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a creature is carrying, in a fight.
/// <para>
/// A better position than abilities were in, and the tests are shaped around the
/// difference. An ability's magnitude had to be invented; a held item's is on its own
/// record — QUICK CLAW carries twenty and goes first one time in five, SHELL BELL carries
/// eight and heals an eighth, BRIGHTPOWDER carries ten and LAX INCENSE carries five. So most
/// of these check that the number used is the number on the record rather than a number
/// written in the source, which is a different and better claim.
/// </para>
/// <para>
/// The two places that could not be read are the type each booster is about and the species
/// each locked item is for, both of which come from the item's own <em>name</em> and the
/// server has never seen a name. Those are modelled in the strongest sense and are tested as
/// what this project says rather than as what the cartridge says.
/// </para>
/// </summary>
public class WhatItIsCarryingTests
{
    private static SpeciesData Species(int index = 1, PokemonType type = PokemonType.Normal) => new()
    {
        Index = index,
        Name = string.Empty,
        BaseHp = 60, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    /// <summary>An item record with nothing on it but the two bytes this file is about.</summary>
    private static ItemData Carrying(int effect, int param = 0, int id = 200) =>
        new(id, 100, Pocket.Items, effect, param, 0, 0, 0);

    private static MoveData Move(PokemonType type, int power = 60, int accuracy = 100) =>
        new(1, string.Empty, 0, (byte)power, type, (byte)accuracy, 20, 0, 0, 0);

    private static Battler Make(int species = 1, PokemonType type = PokemonType.Normal, int level = 50)
    {
        var battler = new Battler(Species(species, type), level);

        battler.Moves.Add(Move(type));

        return battler;
    }

    // ---- the boosters, whose magnitude is read ---------------------------------------

    /// <summary>
    /// The percentage comes off the record, and the two items sharing one effect number are
    /// the proof.
    /// <para>
    /// MYSTIC WATER and SEA INCENSE are both effect fifty-two and carry ten and five. A rule
    /// that hardcoded ten would be right for one of them and wrong for the other, and right
    /// for sixteen of the seventeen boosters overall — which is the worst kind of nearly and
    /// the reason this is the first test in the file.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoItemsUnderOneEffectAreWorthWhatTheirOwnRecordsSay()
    {
        ItemData water = Carrying(52, 10);
        ItemData incense = Carrying(52, 5);

        Assert.Equal(110, HeldItems.Boosting(water, PokemonType.Water));
        Assert.Equal(105, HeldItems.Boosting(incense, PokemonType.Water));
    }

    [Fact]
    public void AndABoosterIsWorthNothingOnAnyOtherType()
    {
        ItemData charcoal = Carrying(58, 10);

        Assert.Equal(110, HeldItems.Boosting(charcoal, PokemonType.Fire));
        Assert.Equal(100, HeldItems.Boosting(charcoal, PokemonType.Water));
        Assert.Equal(100, HeldItems.Boosting(charcoal, PokemonType.Normal));
    }

    /// <summary>
    /// Every one of the seventeen is about exactly one type, and no two are about the same
    /// one.
    /// <para>
    /// This is the guardrail for the table that could not be read. An error in it would be a
    /// silent ten per cent on the wrong type — nothing throws, nothing looks wrong, and the
    /// only symptom is a fight going slightly differently than it should. A duplicate is the
    /// likeliest form that error would take when a line is copied and half-edited.
    /// </para>
    /// </summary>
    [Fact]
    public void EachBoosterIsAboutOneTypeAndNoTwoShareOne()
    {
        var found = new Dictionary<PokemonType, int>();

        foreach (int effect in HeldItems.Modelled)
        {
            ItemData item = Carrying(effect, 10);

            foreach (PokemonType type in Enum.GetValues<PokemonType>())
            {
                if (HeldItems.Boosting(item, type) == 100) continue;

                Assert.False(
                    found.TryGetValue(type, out int already),
                    $"effects {already} and {effect} both boost {type}");

                found[type] = effect;
            }
        }

        Assert.Equal(17, found.Count);
    }

    /// <summary>And it reaches the damage, which is the only thing any of it is for.</summary>
    [Fact]
    public void ABoosterShowsUpInTheDamage()
    {
        Battler attacker = Make(type: PokemonType.Water);
        Battler defender = Make(2);

        MoveData surf = Move(PokemonType.Water);

        int bare = DamageCalculator.Calculate(attacker, defender, surf, false, 100).Damage;

        attacker.Carried = Carrying(52, 10);
        attacker.Holding = 209;

        int boosted = DamageCalculator.Calculate(attacker, defender, surf, false, 100).Damage;

        Assert.True(boosted > bare, $"{boosted} should be more than {bare}");
        Assert.Equal(bare * 110 / 100, boosted);
    }

    // ---- the ones that are about who is carrying them ---------------------------------

    /// <summary>
    /// A THICK CLUB on anything but a CUBONE or a MAROWAK is a stone.
    /// <para>
    /// The species check is what makes six of these seven worth having at all. Without it
    /// this would be an item that doubles everybody's attack, which is not a rule anybody
    /// would recognise.
    /// </para>
    /// </summary>
    [Fact]
    public void AClubIsOnlyWorthAnythingToTheOneItIsFor()
    {
        ItemData club = Carrying(HeldItems.Club);

        Assert.Equal(200, HeldItems.Multiplies(club, 104, Stat.Attack));
        Assert.Equal(200, HeldItems.Multiplies(club, 105, Stat.Attack));
        Assert.Equal(100, HeldItems.Multiplies(club, 1, Stat.Attack));

        // And only that stat, on the right creature.
        Assert.Equal(100, HeldItems.Multiplies(club, 104, Stat.SpAttack));
        Assert.Equal(100, HeldItems.Multiplies(club, 104, Stat.Defense));
    }

    [Fact]
    public void AndTheOtherFiveAreEachAboutOneStat()
    {
        Assert.Equal(200, HeldItems.Multiplies(Carrying(HeldItems.Ball), 25, Stat.SpAttack));
        Assert.Equal(200, HeldItems.Multiplies(Carrying(HeldItems.Powder), 132, Stat.Defense));
        Assert.Equal(200, HeldItems.Multiplies(Carrying(HeldItems.SeaTooth), 366, Stat.SpAttack));
        Assert.Equal(200, HeldItems.Multiplies(Carrying(HeldItems.SeaScale), 366, Stat.SpDefense));

        Assert.Equal(150, HeldItems.Multiplies(Carrying(HeldItems.Dew), 380, Stat.SpAttack));
        Assert.Equal(150, HeldItems.Multiplies(Carrying(HeldItems.Dew), 381, Stat.SpDefense));

        // Nobody else gets any of it.
        Assert.Equal(100, HeldItems.Multiplies(Carrying(HeldItems.Ball), 26, Stat.SpAttack));
        Assert.Equal(100, HeldItems.Multiplies(Carrying(HeldItems.Powder), 133, Stat.Defense));
    }

    [Fact]
    public void ABraceCostsSpeedAndNothingElse()
    {
        ItemData brace = Carrying(HeldItems.Heavy);

        Assert.Equal(50, HeldItems.Multiplies(brace, 1, Stat.Speed));
        Assert.Equal(100, HeldItems.Multiplies(brace, 1, Stat.Attack));
    }

    // ---- the chances, all of which are read ------------------------------------------

    /// <summary>
    /// Four chances, four numbers, and every one of them off the record rather than out of
    /// this file.
    /// </summary>
    [Fact]
    public void EveryChanceIsTheNumberOnItsOwnRecord()
    {
        Assert.Equal(20, HeldItems.Hurries(Carrying(HeldItems.Quick, 20)));
        Assert.Equal(10, HeldItems.Startles(Carrying(HeldItems.Startling, 10)));
        Assert.Equal(10, HeldItems.Endures(Carrying(HeldItems.Enduring, 10)));
        Assert.Equal(10, HeldItems.Slipperiness(Carrying(HeldItems.Slippery, 10)));
        Assert.Equal(5, HeldItems.Slipperiness(Carrying(HeldItems.Slippery, 5)));
        Assert.Equal(8, HeldItems.Drains(Carrying(HeldItems.Bell, 8)));

        // And nothing carries any of them by accident.
        Assert.Equal(0, HeldItems.Hurries(Carrying(HeldItems.Startling, 10)));
        Assert.Equal(0, HeldItems.Startles(Carrying(HeldItems.Quick, 20)));
        Assert.Null(HeldItems.Drains(Carrying(HeldItems.Scraps, 10)));
        Assert.Equal(0, HeldItems.Slipperiness(null));
    }

    /// <summary>
    /// The one number here that contradicts its own record, said out loud so it cannot
    /// quietly become the parameter one day.
    /// </summary>
    [Fact]
    public void ScrapsAreASixteenthAndNotTheTenTheirRecordCarries()
    {
        ItemData leftovers = Carrying(HeldItems.Scraps, 10);

        Assert.True(HeldItems.Feeds(leftovers));
        Assert.Equal(16, HeldItems.ScrapsFraction);
        Assert.NotEqual(leftovers.HoldEffectParam, HeldItems.ScrapsFraction);
    }

    // ---- the sharp ones ---------------------------------------------------------------

    [Fact]
    public void ALensIsWorthOneStageAndTheTwoSpeciesOnesAreWorthTwo()
    {
        Assert.Equal(1, HeldItems.Sharpens(Carrying(HeldItems.Lens), 1));
        Assert.Equal(2, HeldItems.Sharpens(Carrying(HeldItems.Punch), 113));
        Assert.Equal(2, HeldItems.Sharpens(Carrying(HeldItems.Stick), 83));

        Assert.Equal(0, HeldItems.Sharpens(Carrying(HeldItems.Punch), 1));
        Assert.Equal(0, HeldItems.Sharpens(Carrying(HeldItems.Stick), 1));
        Assert.Equal(0, HeldItems.Sharpens(null, 113));
    }

    // ---- in a fight -------------------------------------------------------------------

    /// <summary>
    /// A band commits the carrier to the move it first made, and keeps committing.
    /// </summary>
    [Fact]
    public void ABandLetsYouChooseOnceAndThenChoosesForYou()
    {
        Battler you = Make();
        you.Moves.Add(Move(PokemonType.Normal, 20));

        you.Carried = Carrying(HeldItems.Choice);
        you.Holding = 186;

        Battler them = Make(2);

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(1, you.ChoiceSlot);

        int before = them.CurrentHp;

        // Asking for the other one, and getting the one already chosen.
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(1, you.LastSlot);
        Assert.Equal(1, you.ChoiceSlot);
        Assert.True(before - them.CurrentHp > 0);
    }

    /// <summary>And leaving the field lets go of it, which is the only thing that does.</summary>
    [Fact]
    public void AndSteppingOutForgetsIt()
    {
        Battler you = Make();
        you.ChoiceSlot = 1;

        you.ForgetWhatWasStarted();

        Assert.Null(you.ChoiceSlot);
    }

    /// <summary>
    /// A band is half again on Attack, and it reaches the damage rather than only the table.
    /// </summary>
    [Fact]
    public void ABandIsHalfAgainOnTheDamageItself()
    {
        Battler attacker = Make();
        Battler defender = Make(2);

        MoveData hit = Move(PokemonType.Fighting);

        int bare = DamageCalculator.Calculate(attacker, defender, hit, false, 100).Damage;

        attacker.Carried = Carrying(HeldItems.Choice);

        int banded = DamageCalculator.Calculate(attacker, defender, hit, false, 100).Damage;

        Assert.True(banded > bare, $"{banded} should be more than {bare}");
    }

    /// <summary>
    /// Something that makes its carrier harder to find takes a percentage off accuracy, and
    /// it is a percentage rather than a stage — so a HAZE would not clear it and CLEAR BODY
    /// has nothing to say about it.
    /// </summary>
    [Fact]
    public void SlipperinessComesOffTheAccuracyAndNotOffAStage()
    {
        Battler attacker = Make();
        Battler defender = Make(2);

        defender.Carried = Carrying(HeldItems.Slippery, 10);

        MoveData certain = Move(PokemonType.Normal, accuracy: 100);

        // Counted over two hundred seeds rather than pinned to one, because this generator
        // takes a seed rather than a list of rolls. A move that never missed missing about a
        // tenth of the time is the claim, and it is the same two hundred seeds either way.
        int missed = 0;

        for (uint seed = 1; seed <= 200; seed++)
        {
            if (!DamageCalculator.RollAccuracy(new BattleRng(seed), certain, attacker, defender)) missed++;
        }

        Assert.InRange(missed, 1, 40);

        // And with nothing carried it never misses at all, which is what says the difference
        // came from the item rather than from the dice.
        defender.Carried = null;

        for (uint seed = 1; seed <= 200; seed++)
        {
            Assert.True(DamageCalculator.RollAccuracy(new BattleRng(seed), certain, attacker, defender));
        }
    }

    /// <summary>
    /// A band that holds on turns a knockout into one point, and only then.
    /// </summary>
    [Fact]
    public void SomethingThatHoldsOnLeavesOnePointRatherThanNone()
    {
        Battler you = Make(level: 100);
        Battler them = Make(2, level: 5);

        them.Carried = Carrying(HeldItems.Enduring, 100);
        them.Holding = 196;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.HeldOn { Side: Side.Opponent });
        Assert.False(them.HasFainted);
        Assert.Equal(1, them.CurrentHp);
    }

    /// <summary>
    /// Scraps put a sixteenth back at the end of a turn, and only when there is room for it.
    /// </summary>
    [Fact]
    public void ScrapsFeedTheCarrierAtTheEndOfATurn()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        you.Carried = Carrying(HeldItems.Scraps, 10);
        you.Holding = 200;

        // Nothing coming the other way, so the only number that moves is the one this test
        // is about.
        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.TakeDamage(you.MaxHp / 2);

        int before = you.CurrentHp;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.ItemHealed { Side: Side.Player });
        Assert.Equal(before + you.MaxHp / HeldItems.ScrapsFraction, you.CurrentHp);
    }

    /// <summary>And a full one is fed nothing, because there is nowhere to put it.</summary>
    [Fact]
    public void AndAFullOneIsFedNothing()
    {
        Battler you = Make(level: 100);
        Battler them = Make(2, level: 5);

        you.Carried = Carrying(HeldItems.Scraps, 10);
        you.Holding = 200;

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(you.MaxHp, you.CurrentHp);
        Assert.DoesNotContain(events, e => e is BattleEvent.ItemHealed { Side: Side.Player });
    }

    /// <summary>
    /// Every effect this project says it has modelled changes something.
    /// <para>
    /// The same guardrail abilities keep, and it is the one that stops the published number
    /// from drifting away from the truth. An effect listed as modelled that every hook
    /// answers "no" to is an effect nobody has written a rule for, listed as though somebody
    /// had.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryModelledHeldItemActuallyDoesSomething()
    {
        var silent = new List<int>();

        foreach (int effect in HeldItems.Modelled)
        {
            // A parameter of one, so anything whose answer is its parameter answers
            // something rather than nought — and anything that ignores its parameter is
            // unaffected by the choice.
            ItemData item = Carrying(effect, 1);

            bool does =
                Enum.GetValues<PokemonType>().Any(t => HeldItems.Boosting(item, t) != 100)
                || AnyStat(item)
                || HeldItems.Slipperiness(item) > 0
                || AnySpeciesSharpens(item)
                || HeldItems.Hurries(item) > 0
                || HeldItems.Startles(item) > 0
                || HeldItems.Endures(item) > 0
                || HeldItems.Drains(item) is not null
                || HeldItems.Feeds(item)
                || HeldItems.Locks(item)

                // And the half that is used up. Asked the same way and in the same list,
                // because "modelled" has to mean one thing.
                || HeldItems.Restoring(item) is not null
                || HeldItems.Feeding(item) is not null
                || HeldItems.Clearing(item) != Ailments.None
                || HeldItems.Refilling(item) is not null
                || HeldItems.Restoring(item, stages: true)
                || HeldItems.PinchedAt(item) is not null;

            if (!does) silent.Add(effect);
        }

        Assert.Empty(silent);
    }

    /// <summary>Whether this changes any stat for any of the species it might be about.</summary>
    private static bool AnyStat(ItemData item) =>
        new[] { 1, 25, 83, 104, 105, 113, 132, 366, 380, 381 }.Any(species =>
            Enum.GetValues<Stat>().Any(stat => HeldItems.Multiplies(item, species, stat) != 100));

    private static bool AnySpeciesSharpens(ItemData item) =>
        new[] { 1, 83, 113 }.Any(species => HeldItems.Sharpens(item, species) > 0);

    /// <summary>And an effect nobody has written a rule for says so rather than pretending.</summary>
    [Fact]
    public void AnythingNotModelledIsCarriedAndDoesNothing()
    {
        // Twenty-eight is MENTAL HERB, which cures an attraction this engine has no way to
        // inflict, and forty-four is DRAGON SCALE, which looks exactly like a type booster
        // and does nothing in a fight this generation. Both are carried, both are counted,
        // and neither pretends.
        Assert.False(HeldItems.DoesSomething(28));
        Assert.False(HeldItems.DoesSomething(44));

        Assert.True(HeldItems.DoesSomething(HeldItems.Scraps));
        Assert.True(HeldItems.DoesSomething(HeldItems.Herb));
    }

    // ---- the half that is used up ------------------------------------------------------

    /// <summary>
    /// Everything that is eaten is listed as eaten, and nothing that is not is.
    /// <para>
    /// The line this pair of milestones drew, asserted rather than described. A LEFTOVERS
    /// that turned up in the eaten list would be a LEFTOVERS that vanished after one turn.
    /// </para>
    /// </summary>
    [Fact]
    public void TheLineBetweenWhatIsUsedUpAndWhatIsNot()
    {
        Assert.Equal(22, HeldItems.Eaten.Count);

        foreach (int effect in HeldItems.Eaten) Assert.True(HeldItems.DoesSomething(effect));

        Assert.True(HeldItems.IsEaten(Carrying(2)));
        Assert.True(HeldItems.IsEaten(Carrying(HeldItems.Herb)));

        Assert.False(HeldItems.IsEaten(Carrying(HeldItems.Scraps)));
        Assert.False(HeldItems.IsEaten(Carrying(HeldItems.Choice)));
        Assert.False(HeldItems.IsEaten(null));
    }

    /// <summary>
    /// The three that restore a flat amount carry three different amounts under one effect
    /// number, exactly like the two incenses.
    /// </summary>
    [Fact]
    public void TheThreeThatRestoreCarryTheirOwnAmounts()
    {
        Assert.Equal(10, HeldItems.Restoring(Carrying(HeldItems.Restores, 10)));
        Assert.Equal(20, HeldItems.Restoring(Carrying(HeldItems.Restores, 20)));
        Assert.Equal(30, HeldItems.Restoring(Carrying(HeldItems.Restores, 30)));

        Assert.Null(HeldItems.Restoring(Carrying(HeldItems.Scraps, 10)));
    }

    /// <summary>
    /// The quarter the seven go off at is on their own records, and the half the others go
    /// off at is not. Said out loud because it is the one threshold that did not have to be
    /// invented.
    /// </summary>
    [Fact]
    public void OneThresholdIsReadAndTheOtherIsNot()
    {
        Assert.Equal(4, HeldItems.PinchedAt(Carrying(15, 4)));
        Assert.Equal(4, HeldItems.PinchedAt(Carrying(21, 4)));
        Assert.Null(HeldItems.PinchedAt(Carrying(HeldItems.Restores, 10)));

        Assert.Equal(2, HeldItems.HurtShare);
    }

    [Fact]
    public void EachCuringBerryClearsOneThingAndOneClearsThemAll()
    {
        Assert.Equal(Ailments.Paralysis, HeldItems.Clearing(Carrying(2)));
        Assert.Equal(Ailments.Sleep, HeldItems.Clearing(Carrying(3)));
        Assert.Equal(Ailments.Poison, HeldItems.Clearing(Carrying(4)));
        Assert.Equal(Ailments.Burn, HeldItems.Clearing(Carrying(5)));
        Assert.Equal(Ailments.Freeze, HeldItems.Clearing(Carrying(6)));
        Assert.Equal(Ailments.Confusion, HeldItems.Clearing(Carrying(8)));
        Assert.Equal(Ailments.Everything, HeldItems.Clearing(Carrying(9)));

        Assert.Equal(Ailments.None, HeldItems.Clearing(Carrying(HeldItems.Scraps)));
    }

    /// <summary>
    /// Something hurt eats what it is carrying, and afterwards is not carrying it.
    /// <para>
    /// The second half is what the whole milestone is for. An item that healed and stayed
    /// would be an item that healed every turn forever.
    /// </para>
    /// </summary>
    [Fact]
    public void SomethingHurtEatsWhatItIsCarryingAndThenHasNone()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Carried = Carrying(HeldItems.Restores, 30);
        you.Holding = 139;

        you.TakeDamage(you.MaxHp * 3 / 4);

        int before = you.CurrentHp;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.HealthRestored { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.AteIt { Side: Side.Player, ItemId: 139 });

        Assert.Equal(before + 30, you.CurrentHp);
        Assert.Equal(0, you.Holding);
        Assert.Null(you.Carried);
    }

    /// <summary>And something that is not hurt keeps it.</summary>
    [Fact]
    public void AndSomethingUnhurtKeepsIt()
    {
        Battler you = Make(level: 100);
        Battler them = Make(2, level: 5);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Carried = Carrying(HeldItems.Restores, 30);
        you.Holding = 139;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.DoesNotContain(events, e => e is BattleEvent.AteIt);
        Assert.Equal(139, you.Holding);
    }

    /// <summary>
    /// And something only lightly hurt keeps it, which is the threshold rather than the
    /// absence of one.
    /// <para>
    /// Its own test because the full-health one cannot see it: a creature at full health is
    /// refused by "is any of it missing" before the half is ever asked about, so removing
    /// the half entirely leaves that test passing.
    /// </para>
    /// </summary>
    [Fact]
    public void AndSomethingOnlyLightlyHurtKeepsIt()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Carried = Carrying(HeldItems.Restores, 30);
        you.Holding = 139;

        // A quarter off, which is hurt and is not nearly finished.
        you.TakeDamage(you.MaxHp / 4);

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.DoesNotContain(events, e => e is BattleEvent.AteIt);
        Assert.Equal(139, you.Holding);
    }

    /// <summary>
    /// A berry that clears a condition clears the one it is for and nothing else.
    /// </summary>
    [Fact]
    public void ABerryClearsTheOneConditionItIsFor()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Status = StatusCondition.Paralysis;
        you.Carried = Carrying(2);
        you.Holding = 133;

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.PutRight { Side: Side.Player });
        Assert.Equal(StatusCondition.None, you.Status);
        Assert.Equal(0, you.Holding);
    }

    /// <summary>And leaves alone a condition it is not for.</summary>
    [Fact]
    public void AndLeavesAloneOneItIsNotFor()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Status = StatusCondition.Burn;
        you.Carried = Carrying(2);
        you.Holding = 133;

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.Burn, you.Status);
        Assert.Equal(133, you.Holding);
    }

    /// <summary>
    /// Something nearly finished eats what raises a stat, at the quarter its own record
    /// names.
    /// </summary>
    [Fact]
    public void SomethingNearlyFinishedRaisesAStat()
    {
        Battler you = Make(level: 50);
        Battler them = Make(2, level: 50);

        them.Moves.Clear();
        them.Moves.Add(Move(PokemonType.Normal, 0));

        you.Carried = Carrying(17, 4);
        you.Holding = 170;

        you.TakeDamage(you.MaxHp * 4 / 5);

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.StageChanged { Side: Side.Player, Stat: Stat.Speed });
        Assert.Equal(1, you.StageOf(Stat.Speed));
        Assert.Equal(0, you.Holding);
    }

    /// <summary>
    /// The herb that puts back what was lowered puts back only what was lowered.
    /// </summary>
    [Fact]
    public void AHerbPutsBackOnlyWhatWasLowered()
    {
        Battler you = Make();

        you.ChangeStage(Stat.Attack, -2);
        you.ChangeStage(Stat.Speed, 1);

        Assert.Equal(1, you.RaiseWhatWasLowered());

        Assert.Equal(0, you.StageOf(Stat.Attack));
        Assert.Equal(1, you.StageOf(Stat.Speed));

        // And a second go finds nothing left to put back, which is what stops it being
        // eaten for nothing.
        Assert.Equal(0, you.RaiseWhatWasLowered());
    }

    /// <summary>
    /// Uses go back into the first move that has run out, and only as many as were missing.
    /// </summary>
    [Fact]
    public void UsesGoBackIntoTheFirstMoveThatRanOut()
    {
        Battler you = Make();
        you.Moves.Add(Move(PokemonType.Normal, 20));

        Assert.Null(you.FirstSpentSlot());

        while (you.Spend(1))
        {
        }

        Assert.Equal(1, you.FirstSpentSlot());

        // Ten back into a move missing all twenty is ten, and the answer says so.
        Assert.Equal(10, you.Refill(1, 10));
        Assert.Equal(10, you.PpLeft(1));
    }
}
