using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Twice, and the two that cost everything.
/// <para>
/// Four groups that were one line each and had been on the open list for four
/// milestones: 0x2C lands exactly twice, 0x4D lands twice and may poison, 0x2D hurts the
/// user when it misses, and 0x07 takes the user with it.
/// </para>
/// <para>
/// Two is not a roll. 0x1D's dozen land two to five times and have their own kind for
/// it; these land twice every time, and a group of two whose members are DOUBLE KICK and
/// BONEMERANG is a group about the number two.
/// </para>
/// <para>
/// The same pass separated two kinds of silence in the report. A group this engine does
/// nothing for is not the same as a group there is nothing to do for: every move in 0x11
/// carries no accuracy and every move in 0x67 carries a priority, and both fields are
/// already read for every move on the cartridge whatever group it is in. Counting those
/// as unfinished work is a report claiming there is more left than there is.
/// </para>
/// </summary>
public class CheapGroupTests
{
    private const byte TwiceEffect = 0x2C;
    private const byte TwiceAndPoisonEffect = 0x4D;
    private const byte CrashEffect = 0x2D;
    private const byte BlowUpEffect = 0x07;

    private const int Kicker = 1;
    private const int Needle = 2;
    private const int Leaper = 3;
    private const int Bomb = 4;
    private const int Plain = 5;

    private static MoveData Move(int id, byte effect, byte power, byte accuracy = 100, byte secondary = 0) =>
        new(id, "", effect, power, PokemonType.Normal, accuracy, 20, secondary, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 200,
            BaseAttack = 60,
            BaseDefense = 60,
            BaseSpeed = (byte)speed,
            BaseSpAttack = 60,
            BaseSpDefense = 60,
            Type1 = PokemonType.Normal,
            Type2 = PokemonType.Normal,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, 50, Nature.Hardy);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Fact]
    public void TheFourGroupsAreRead()
    {
        Assert.Equal(EffectKind.Twice, MoveEffects.Of(TwiceEffect).Kind);
        Assert.Equal(EffectKind.Twice, MoveEffects.Of(TwiceAndPoisonEffect).Kind);
        Assert.Equal(EffectKind.CrashOnMiss, MoveEffects.Of(CrashEffect).Kind);
        Assert.Equal(EffectKind.UserFaints, MoveEffects.Of(BlowUpEffect).Kind);
    }

    // ---- DOUBLE KICK ----------------------------------------------------------------

    [Fact]
    public void ItLandsTwice()
    {
        Battler you = Make(250, Move(Kicker, TwiceEffect, 30));
        Battler them = Make(1, Move(Plain, 0, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(2, events.Count(e => e is BattleEvent.DamageDealt { Side: Side.Opponent }));
        Assert.Contains(events, e => e is BattleEvent.HitSeveralTimes { Times: 2 });
    }

    /// <summary>
    /// Twice every time, which is the whole difference between this group and the one
    /// that rolls. A test over several seeds is the only way to say that out loud.
    /// </summary>
    [Fact]
    public void ItIsAlwaysTwiceAndNeverThree()
    {
        var counts = new HashSet<int>();

        for (uint seed = 1; seed <= 8; seed++)
        {
            Battler you = Make(250, Move(Kicker, TwiceEffect, 5));
            Battler them = Make(1, Move(Plain, 0, 10));

            counts.Add(Turn(new Battle(you, them, seed))
                .Count(e => e is BattleEvent.DamageDealt { Side: Side.Opponent }));
        }

        Assert.Equal([2], counts);
    }

    /// <summary>The second one does not land on something already down.</summary>
    [Fact]
    public void TheSecondOneStopsIfTheFirstEndedIt()
    {
        Battler you = Make(250, Move(Kicker, TwiceEffect, 250));
        Battler them = Make(1, Move(Plain, 0, 10));

        them.TakeDamage(them.CurrentHp - 1);

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Single(events.Where(e => e is BattleEvent.DamageDealt { Side: Side.Opponent }));
        Assert.Contains(events, e => e is BattleEvent.Fainted { Side: Side.Opponent });
    }

    // ---- TWINEEDLE ------------------------------------------------------------------

    /// <summary>
    /// The only move on this cartridge that lands twice and carries a condition. Its own
    /// record says a secondary chance of twenty; every other member of both groups says
    /// nought, which is what makes the poison belong to this move rather than the group.
    /// </summary>
    [Fact]
    public void TheOneThatAlsoPoisonsLandsTwiceAndPoisons()
    {
        Battler you = Make(250, Move(Needle, TwiceAndPoisonEffect, 25, secondary: 100));
        Battler them = Make(1, Move(Plain, 0, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(2, events.Count(e => e is BattleEvent.DamageDealt { Side: Side.Opponent }));
        Assert.Contains(events, e => e is BattleEvent.StatusInflicted { Status: StatusCondition.Poison });
    }

    [Fact]
    public void TheOtherTwiceGroupPoisonsNobody()
    {
        Battler you = Make(250, Move(Kicker, TwiceEffect, 30, secondary: 100));
        Battler them = Make(1, Move(Plain, 0, 10));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.StatusInflicted);
    }

    // ---- JUMP KICK ------------------------------------------------------------------

    /// <summary>
    /// The cost of missing. How much is modelled — the share is in the game's code — but
    /// which moves pay it is read, and it is the whole of group 0x2D and nothing else.
    /// </summary>
    [Fact]
    public void AMissCostsTheOneWhoMissed()
    {
        Battler you = Make(250, Move(Leaper, CrashEffect, 70, accuracy: 1));
        Battler them = Make(1, Move(Plain, 0, 10));

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 3));

        Assert.Contains(events, e => e is BattleEvent.MoveMissed { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.Crashed { Side: Side.Player });
        Assert.True(you.CurrentHp < before, "missing cost nothing");
    }

    [Fact]
    public void LandingCostsNothing()
    {
        Battler you = Make(250, Move(Leaper, CrashEffect, 70));
        Battler them = Make(1, Move(Plain, 0, 0));

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.DoesNotContain(events, e => e is BattleEvent.Crashed);
        Assert.Equal(before, you.CurrentHp);
    }

    /// <summary>And a crash can be the end of the one who crashed.</summary>
    [Fact]
    public void ACrashCanFinishTheOneWhoMissed()
    {
        Battler you = Make(250, Move(Leaper, CrashEffect, 250, accuracy: 1));
        Battler them = Make(1, Move(Plain, 0, 10));

        you.TakeDamage(you.CurrentHp - 1);

        List<BattleEvent> events = Turn(new Battle(you, them, 3));

        Assert.Contains(events, e => e is BattleEvent.Crashed);
        Assert.Contains(events, e => e is BattleEvent.Fainted { Side: Side.Player });
    }

    // ---- EXPLOSION ------------------------------------------------------------------

    /// <summary>
    /// The one number in the group that did not have to be modelled: it is everything.
    /// </summary>
    [Fact]
    public void TheUserGoesDownWithIt()
    {
        Battler you = Make(250, Move(Bomb, BlowUpEffect, 200));
        Battler them = Make(1, Move(Plain, 0, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.BlewUp { Side: Side.Player });
        Assert.True(you.HasFainted);
        Assert.Equal(0, you.CurrentHp);
    }

    /// <summary>
    /// The target takes the hit first. Written the other way round — the user faints and
    /// then the damage is worked out — EXPLOSION is a move that does nothing at all.
    /// </summary>
    [Fact]
    public void TheTargetIsHitBeforeTheUserGoesDown()
    {
        Battler you = Make(250, Move(Bomb, BlowUpEffect, 200));
        Battler them = Make(1, Move(Plain, 0, 10));

        int before = them.CurrentHp;

        Turn(new Battle(you, them, 7));

        Assert.True(them.CurrentHp < before, "it went off and hurt nobody");
    }

    /// <summary>And it goes off even when it misses, which is what makes it a gamble.</summary>
    [Fact]
    public void ItGoesOffEvenWhenItMisses()
    {
        Battler you = Make(250, Move(Bomb, BlowUpEffect, 200, accuracy: 1));
        Battler them = Make(1, Move(Plain, 0, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 3));

        Assert.Contains(events, e => e is BattleEvent.MoveMissed);
        Assert.Contains(events, e => e is BattleEvent.BlewUp);
        Assert.True(you.HasFainted);
    }

    /// <summary>
    /// None of the four is a rider on a hit, so none may fall through to the handler
    /// that applies riders. That is where the multi-turn four landed once already, and
    /// it is how WRAP came to announce something about a stat.
    /// </summary>
    [Fact]
    public void NoneOfThemAlsoSaysSomethingAboutAStat()
    {
        foreach (byte effect in new[] { CrashEffect, BlowUpEffect })
        {
            Battler you = Make(250, Move(1, effect, 40));
            Battler them = Make(1, Move(Plain, 0, 10));

            Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.StageChanged);
        }
    }

    // ---- and the two kinds of silence ------------------------------------------------

    /// <summary>
    /// A group this engine does nothing for is not the same as a group there is nothing
    /// to do for. Both of these are already carried by fields the engine reads for every
    /// move on the cartridge, whatever group it is in.
    /// <para>
    /// They used to answer <see cref="EffectKind.None"/>, which is the same answer the
    /// engine gives for an effect nobody has written — so a count of what it understands
    /// was wrong by three groups and there was no way to tell the two apart. They answer
    /// <see cref="EffectKind.Nothing"/> now, and that difference is what makes it possible
    /// for a fight to say how much of itself went unmodelled.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData((byte)0x00)]  // POUND, SCRATCH, CUT — they hit, and that is all
    [InlineData((byte)0x11)]  // SWIFT, AERIAL ACE — accuracy of nought, which never misses
    [InlineData((byte)0x67)]  // QUICK ATTACK, EXTREMESPEED — a priority above nought
    public void SomeSilenceIsTheRecordHavingAlreadySaidIt(byte effect)
    {
        Assert.Equal(EffectKind.Nothing, MoveEffects.Of(effect).Kind);
        Assert.False(MoveEffects.IsSilent(effect));
    }

    // ---- the guard ------------------------------------------------------------------

    private const byte GuardEffect = 0x6F;   // PROTECT, DETECT

    /// <summary>
    /// PROTECT and DETECT carry a priority of three in their own records — read, not
    /// modelled, and the reason the guard is up before anything arrives. Only ENDURE,
    /// MAGIC COAT, SNATCH, FOLLOW ME and HELPING HAND carry as much or more.
    /// </summary>
    private static MoveData Guard(int id) =>
        new(id, "", GuardEffect, 0, PokemonType.Normal, 0, 10, 0, 0, Priority: 3);

    /// <summary>
    /// A guard stops the move rather than dodging it, so it is checked before accuracy —
    /// PROTECT works against something that never misses, and evasion does not.
    /// </summary>
    [Fact]
    public void AGuardStopsWhatWouldOtherwiseLand()
    {
        // Slower, and it still goes up first, because the record says so.
        Battler you = Make(1, Guard(9));
        Battler them = Make(250, Move(Plain, 0x00, 60));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.Protected { Side: Side.Player });
        Assert.Equal(you.MaxHp, you.CurrentHp);
    }

    /// <summary>And the one who put it up is announced as having done so.</summary>
    [Fact]
    public void AndSaysItPutOneUp()
    {
        Battler you = Make(250, Guard(9));
        Battler them = Make(1, Move(Plain, 0x00, 60));

        List<BattleEvent> events =
            new Battle(you, them, 7).ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.Protected);
        Assert.Contains(events, e => e is BattleEvent.Unaffected);
    }

    /// <summary>
    /// And it lasts one turn. A guard that outlived the turn it was put up in would be a
    /// move that ends fights rather than one that buys a moment.
    /// </summary>
    [Fact]
    public void AndOnlyForThatTurn()
    {
        Battler you = Make(250, Guard(9), Move(Plain, 0x00, 10));
        Battler them = Make(1, Move(Plain, 0x00, 60));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        int after = you.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.True(you.CurrentHp < after);
    }

    /// <summary>A guard is not silence — it is one of the groups this engine now does.</summary>
    [Fact]
    public void AndIsNotCountedAsSomethingSteppedOver()
    {
        Battler you = Make(250, Guard(9));
        Battler them = Make(1, Move(Plain, 0x00, 60));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Empty(battle.SteppedOver);
    }

    /// <summary>And an effect nobody has written still says so.</summary>
    [Fact]
    public void AndTheOtherKindOfSilenceSaysSo()
    {
        // METRONOME, and the hundred-odd like it.
        Assert.Equal(EffectKind.None, MoveEffects.Of(0x54).Kind);
        Assert.True(MoveEffects.IsSilent(0x54));
    }

    /// <summary>
    /// A fight keeps count of what it could not do. One entry per use rather than per
    /// move, because the question afterwards is how much of this fight went unmodelled.
    /// </summary>
    [Fact]
    public void AFightCountsWhatItSteppedOver()
    {
        Battler you = Make(250, Move(9, 0x54, 40));
        Battler them = Make(250, Move(Plain, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Single(battle.SteppedOver);
    }

    [Fact]
    public void AndCountsNothingWhenItDidTheWholeThing()
    {
        Battler you = Make(250, Move(9, 0x00, 40));
        Battler them = Make(250, Move(Plain, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Empty(battle.SteppedOver);
    }

    [Fact]
    public void AMoveWithNoAccuracyNeverMisses()
    {
        Battler you = Make(250, new MoveData(9, "", 0x11, 40, PokemonType.Normal, 0, 20, 0, 0, 0));
        Battler them = Make(1, Move(Plain, 0, 10));

        for (uint seed = 1; seed <= 6; seed++)
        {
            Battler again = Make(250, new MoveData(9, "", 0x11, 40, PokemonType.Normal, 0, 20, 0, 0, 0));
            Battler other = Make(1, Move(Plain, 0, 10));

            Assert.DoesNotContain(Turn(new Battle(again, other, seed)), e => e is BattleEvent.MoveMissed);
        }

        Assert.NotNull(you);
        Assert.NotNull(them);
    }

    [Fact]
    public void AMoveWithPriorityGoesFirstHoweverSlowItIs()
    {
        Battler you = Make(1, new MoveData(9, "", 0x67, 40, PokemonType.Normal, 100, 20, 0, 0, 1));
        Battler them = Make(250, Move(Plain, 0, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        BattleEvent first = events.First(e => e is BattleEvent.MoveUsed);

        Assert.Equal(Side.Player, ((BattleEvent.MoveUsed)first).Side);
    }
}
