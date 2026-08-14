using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Leaving, which was the one thing a player could not do.
/// <para>
/// Every one of these games lets you walk away from something wild. This one did not:
/// a player who met something they could not beat had no way out of the fight except
/// losing it. Two move groups were waiting on it — SPIDER WEB and MEAN LOOK stop you
/// leaving, WHIRLWIND and ROAR make the other one leave — and neither could mean
/// anything while leaving was not a thing.
/// </para>
/// </summary>
public class RunningAwayTests
{
    private const byte NoEscapeEffect = 0x6A;
    private const byte BlowAwayEffect = 0x1C;
    private const byte TrapEffect = 0x2A;

    private const int Sticky = 1;
    private const int Gale = 2;
    private const int Holder = 3;
    private const int Plain = 4;

    private static MoveData Move(int id, byte effect, byte power) =>
        new(id, "", effect, power, PokemonType.Normal, 100, 20, 0, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            BaseHp = 200,
            BaseAttack = 20,
            BaseDefense = 200,
            BaseSpeed = (byte)speed,
            BaseSpAttack = 20,
            BaseSpDefense = 200,
            Type1 = PokemonType.Normal,
            Type2 = PokemonType.Normal,
            GrowthRate = GrowthRate.MediumFast,
        };

        var battler = new Battler(species, 50, Nature.Hardy);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Run(Battle battle) =>
        battle.ResolveTurn(new BattleAction.RunAway(), new BattleAction.UseMove(0));

    [Fact]
    public void SomethingFastGetsAwayFromSomethingSlow()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Plain, 0, 10));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = Run(battle);

        Assert.Contains(events, e => e is BattleEvent.GotAway { Side: Side.Player });
        Assert.True(battle.Escaped);
        Assert.True(battle.IsOver);
    }

    /// <summary>Nobody won it. A fight that ends with no winner had never happened here.</summary>
    [Fact]
    public void NobodyWinsAFightSomebodyWalkedOutOf()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Plain, 0, 10));

        var battle = new Battle(you, them, 7);

        Run(battle);

        Assert.Null(battle.Winner);
        Assert.False(you.HasFainted);
        Assert.False(them.HasFainted);
    }

    /// <summary>
    /// And trying again is meant to get easier. Modelled — the numbers are in the game's
    /// code — but the shape is the part that matters: something slow and cornered gets
    /// out eventually rather than never.
    /// </summary>
    [Fact]
    public void TryingAgainEventuallyWorks()
    {
        Battler you = Make(1, Move(Plain, 0, 1));
        Battler them = Make(250, Move(Plain, 0, 1));

        var battle = new Battle(you, them, 11);

        var got = false;

        for (int tries = 0; tries < 12 && !got; tries++)
            got = Run(battle).Any(e => e is BattleEvent.GotAway);

        Assert.True(got, "nothing ever got away");
    }

    [Fact]
    public void ThereIsNoRunningFromATrainer()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Plain, 0, 10));

        var battle = new Battle(you, them, 7) { IsWild = false };

        List<BattleEvent> events = Run(battle);

        Assert.Contains(events, e => e is BattleEvent.CouldNotGetAway);
        Assert.False(battle.Escaped);
    }

    /// <summary>
    /// What WRAP was for. Trapping had no teeth at all while there was nothing to stop.
    /// </summary>
    [Fact]
    public void SomethingWrappedIsNotGoingAnywhere()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Holder, TrapEffect, 15));

        var battle = new Battle(you, them, 7);

        // Let them wrap first, which takes a turn of theirs.
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(you.TrappedTurns > 0, "nothing got wrapped");

        List<BattleEvent> events = Run(battle);

        Assert.Contains(events, e => e is BattleEvent.HeldFast { MoveId: Holder });
        Assert.False(battle.Escaped);
    }

    // ---- MEAN LOOK ------------------------------------------------------------------

    [Fact]
    public void BeingLookedAtStopsYouLeavingForGood()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Sticky, NoEscapeEffect, 0));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(you.CannotEscape);

        // And it does not wear off, which is what makes it worse than being wrapped.
        for (int turn = 0; turn < 4; turn++)
            Assert.Contains(Run(battle), e => e is BattleEvent.CouldNotGetAway);

        Assert.False(battle.Escaped);
    }

    [Fact]
    public void BeingLookedAtTwiceIsBeingLookedAtOnce()
    {
        Battler you = Make(250, Move(Plain, 0, 10));
        Battler them = Make(1, Move(Sticky, NoEscapeEffect, 0));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        List<BattleEvent> again = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(again, e => e is BattleEvent.NothingHappened);
    }

    // ---- WHIRLWIND ------------------------------------------------------------------

    [Fact]
    public void SomethingWildBlownAwayEndsTheFight()
    {
        Battler you = Make(250, Move(Gale, BlowAwayEffect, 0));
        Battler them = Make(1, Move(Plain, 0, 10));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.BlownAway { Side: Side.Opponent, MoveId: Gale });
        Assert.True(battle.IsOver);
        Assert.Null(battle.Winner);
    }

    /// <summary>
    /// And it does nothing to somebody's trainer, because the games make them switch
    /// instead and switching another party is not something this engine can do. Stated
    /// out loud rather than hidden behind a shrug.
    /// </summary>
    [Fact]
    public void BlowingAtATrainersPokemonDoesNothing()
    {
        Battler you = Make(250, Move(Gale, BlowAwayEffect, 0));
        Battler them = Make(1, Move(Plain, 0, 10));

        var battle = new Battle(you, them, 7) { IsWild = false };

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.NothingHappened);
        Assert.False(battle.IsOver);
    }

    [Fact]
    public void NothingBlowsAwaySomethingThatCannotLeave()
    {
        Battler you = Make(250, Move(Gale, BlowAwayEffect, 0));
        Battler them = Make(1, Move(Plain, 0, 10));

        them.CannotEscape = true;

        var battle = new Battle(you, them, 7);

        Assert.Contains(
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0)),
            e => e is BattleEvent.NothingHappened);

        Assert.False(battle.IsOver);
    }
}
