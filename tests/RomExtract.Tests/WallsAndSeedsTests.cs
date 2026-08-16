using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Four more groups: two walls, a drain, and a way out.
/// <para>
/// Each needed something the engine already had. The walls are MIST's shape with a different
/// answer; the drain is the end-of-turn hook the berries were built on; and TELEPORT is
/// running away reached by a move instead of by a choice — the same field, the same code.
/// </para>
/// </summary>
public class WallsAndSeedsTests
{
    private const byte Reflect = 0x41;
    private const byte LightScreen = 0x23;
    private const byte LeechSeed = 0x54;
    private const byte Teleport = 0x99;

    private static SpeciesData Species(PokemonType type = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 100, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 100, BaseSpDefense = 60,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0, PokemonType type = PokemonType.Normal) =>
        new(1, string.Empty, effect, power, type, 100, 20, 100, 0, 0);

    private static Battler Make(PokemonType type = PokemonType.Normal, params MoveData[] moves)
    {
        var battler = new Battler(Species(type), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    // ---- the walls ---------------------------------------------------------------------

    /// <summary>
    /// A wall halves what it is for and leaves the other kind alone, which is the whole of
    /// what makes them two moves rather than one.
    /// </summary>
    [Theory]
    [InlineData(Reflect, PokemonType.Normal, true)]
    [InlineData(Reflect, PokemonType.Fire, false)]
    [InlineData(LightScreen, PokemonType.Fire, true)]
    [InlineData(LightScreen, PokemonType.Normal, false)]
    public void EachWallHalvesOnlyWhatItIsFor(int wall, PokemonType coming, bool halved)
    {
        Battler attacker = Make();
        Battler defender = Make();

        MoveData hit = Move(0x00, 80, coming);

        int bare = DamageCalculator.Calculate(attacker, defender, hit, false, 100).Damage;

        if (wall == Reflect) defender.ReflectTurns = 5;
        else defender.ScreenTurns = 5;

        int behind = DamageCalculator.Calculate(attacker, defender, hit, false, 100).Damage;

        Assert.Equal(halved ? bare / 2 : bare, behind);
    }

    /// <summary>
    /// A critical hit goes through it, which is the games' rule and also the only reading
    /// that makes a screen a wall rather than a stat.
    /// </summary>
    [Fact]
    public void ACriticalHitGoesThroughAWall()
    {
        Battler attacker = Make();
        Battler defender = Make();

        defender.ReflectTurns = 5;

        MoveData hit = Move(0x00, 80);

        int through = DamageCalculator.Calculate(attacker, defender, hit, critical: true, 100).Damage;
        int bare = DamageCalculator.Calculate(attacker, Make(), hit, critical: true, 100).Damage;

        Assert.Equal(bare, through);
    }

    [Fact]
    public void PuttingOneUpSaysSoAndOnlyOnce()
    {
        Battler you = Make(PokemonType.Normal, Move(Reflect));
        Battler them = Make(PokemonType.Normal, Move(0x00, 10));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.ScreenRose { Side: Side.Player, Physical: true });

        // Five turns, one of which has now gone.
        Assert.Equal(4, you.ReflectTurns);

        // And a second go while it is up finds it up.
        Assert.Contains(Turn(battle), e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// And it runs out, counted down with everything else that lasts turns.
    /// <para>
    /// Put up by hand rather than by using the move every turn, because a creature that uses
    /// it every turn renews it the moment it lapses — which is correct, and which is what the
    /// first version of this test measured instead.
    /// </para>
    /// </summary>
    [Fact]
    public void AndItRunsOut()
    {
        Battler you = Make(PokemonType.Normal, Move(0x00, 0));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        you.ReflectTurns = Skies.Turns;

        var battle = new Battle(you, them, 7);

        for (int turn = 0; turn < Skies.Turns - 1; turn++) Turn(battle);

        Assert.Equal(1, you.ReflectTurns);

        Turn(battle);

        Assert.Equal(0, you.ReflectTurns);
    }

    /// <summary>And leaving the field takes it with you.</summary>
    [Fact]
    public void AndSteppingOutTakesItWithYou()
    {
        Battler you = Make();

        you.ReflectTurns = 5;
        you.ScreenTurns = 5;

        you.ForgetWhatWasStarted();

        Assert.Equal(0, you.ReflectTurns);
        Assert.Equal(0, you.ScreenTurns);
    }

    // ---- the drain ---------------------------------------------------------------------

    /// <summary>
    /// What one side loses at the end of a turn, the other gains — which is what makes it a
    /// drain rather than a poison.
    /// </summary>
    [Fact]
    public void WhatIsSappedFromOneSideGoesToTheOther()
    {
        Battler you = Make(PokemonType.Normal, Move(LeechSeed));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        you.TakeDamage(you.MaxHp / 2);

        int mine = you.CurrentHp;
        int theirs = them.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.Seeded { Side: Side.Opponent });
        Assert.Contains(events, e => e is BattleEvent.Sapped { Side: Side.Opponent });

        int lost = theirs - them.CurrentHp;

        Assert.True(lost > 0);
        Assert.Equal(mine + lost, you.CurrentHp);
    }

    [Fact]
    public void NothingIsSeededTwice()
    {
        Battler you = Make(PokemonType.Normal, Move(LeechSeed));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Contains(Turn(battle), e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
    }

    /// <summary>
    /// And it lasts as long as its target is standing there rather than for a count, which
    /// is what makes leaving the field the only answer to it.
    /// </summary>
    [Fact]
    public void ItLastsUntilItsTargetLeaves()
    {
        Battler them = Make();

        them.IsSeeded = true;

        them.ForgetWhatWasStarted();

        Assert.False(them.IsSeeded);
    }

    // ---- the way out -------------------------------------------------------------------

    /// <summary>
    /// TELEPORT is running away by another name, and reaches the same field rather than a
    /// second one that would have to be kept in step with it.
    /// </summary>
    [Fact]
    public void TeleportingEndsTheFightTheWayRunningDoes()
    {
        Battler you = Make(PokemonType.Normal, Move(Teleport));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.GotAway { Side: Side.Player });
        Assert.True(battle.Escaped);
        Assert.True(battle.IsOver);
    }
}
