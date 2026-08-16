using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Four groups that answer other moves, and could not have been written before those existed.
/// <para>
/// BRICK BREAK takes down the walls that went up two milestones ago. KNOCK OFF takes away the
/// held item that became worth carrying three milestones ago. RAPID SPIN shakes off the drain
/// written last milestone and the wrap that has been here for years. FORESIGHT undoes evasion
/// and a type chart immunity.
/// </para>
/// <para>
/// Which is the point worth recording: three of these four were silent because the thing they
/// answer did not exist, not because anybody had failed to write them. A silent list is not a
/// list of unwritten code — it is partly a list of things waiting for their subject.
/// </para>
/// </summary>
public class AnsweringOtherMovesTests
{
    private const byte BrickBreak = 0xBA;
    private const byte KnockOff = 0xBC;
    private const byte RapidSpin = 0x81;
    private const byte Foresight = 0x71;

    private static SpeciesData Species(PokemonType type = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 100, BaseDefense = 100,
        BaseSpeed = 60, BaseSpAttack = 100, BaseSpDefense = 100,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 40, PokemonType type = PokemonType.Normal) =>
        new(1, string.Empty, effect, power, type, 100, 20, 100, 0, 0);

    private static Battler Make(PokemonType type = PokemonType.Normal, params MoveData[] moves)
    {
        var battler = new Battler(Species(type), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(BrickBreak)] [InlineData(KnockOff)] [InlineData(RapidSpin)] [InlineData(Foresight)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- the walls ---------------------------------------------------------------------

    [Fact]
    public void BreakingTheWallsTakesBothDown()
    {
        Battler you = Make(PokemonType.Normal, Move(BrickBreak));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        them.ReflectTurns = 5;
        them.ScreenTurns = 5;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.WallsBroke { Side: Side.Opponent });
        Assert.Equal(0, them.ReflectTurns);
        Assert.Equal(0, them.ScreenTurns);
    }

    /// <summary>
    /// And it went through the wall on the way, which is what says the order is right: a wall
    /// the move passed is a wall that was still up when it did.
    /// </summary>
    [Fact]
    public void AndItIsHalvedOnTheWayThroughTheOneItBreaks()
    {
        Battler you = Make(PokemonType.Normal, Move(BrickBreak, 80));

        Battler walled = Make(PokemonType.Normal, Move(0x00, 0));
        walled.ReflectTurns = 5;

        Battler bare = Make(PokemonType.Normal, Move(0x00, 0));

        Turn(new Battle(you, walled, 7));
        Turn(new Battle(Make(PokemonType.Normal, Move(BrickBreak, 80)), bare, 7));

        Assert.True(
            walled.MaxHp - walled.CurrentHp < bare.MaxHp - bare.CurrentHp,
            "the wall should have halved the hit that broke it");
    }

    [Fact]
    public void AndSaysNothingWhenThereIsNoWall()
    {
        Battler you = Make(PokemonType.Normal, Move(BrickBreak));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.WallsBroke);
    }

    // ---- what somebody is carrying ------------------------------------------------------

    /// <summary>
    /// KNOCK OFF destroys rather than takes. THIEF is the move that takes one, and an item
    /// that ended up in this user's hands would be THIEF by another name.
    /// </summary>
    [Fact]
    public void KnockingSomethingOffDestroysItRatherThanTakingIt()
    {
        Battler you = Make(PokemonType.Normal, Move(KnockOff));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        them.Holding = 200;
        them.Carried = new ItemData(200, 100, Pocket.Items, HeldItems.Scraps, 10, 0, 0, 0);

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.KnockedOff { Side: Side.Opponent, ItemId: 200 });

        Assert.Equal(0, them.Holding);
        Assert.Null(them.Carried);
        Assert.Equal(0, you.Holding);
    }

    [Fact]
    public void AndEmptyHandsAreLeftAlone()
    {
        Battler you = Make(PokemonType.Normal, Move(KnockOff));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.KnockedOff);
    }

    // ---- shaking free -------------------------------------------------------------------

    /// <summary>
    /// One act, not two. A move that shook off a wrap and left a seed would be two moves
    /// sharing a name.
    /// </summary>
    [Fact]
    public void SpinningShakesOffEverythingAtOnce()
    {
        Battler you = Make(PokemonType.Normal, Move(RapidSpin));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        you.IsSeeded = true;
        you.TrappedTurns = 3;
        you.TrappedBy = 35;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.ShookFree { Side: Side.Player });

        Assert.False(you.IsSeeded);
        Assert.Equal(0, you.TrappedTurns);
    }

    [Fact]
    public void AndSaysNothingWhenThereWasNothingHoldingIt()
    {
        Battler you = Make(PokemonType.Normal, Move(RapidSpin));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.ShookFree);
    }

    // ---- being found --------------------------------------------------------------------

    /// <summary>
    /// Evasion stops counting, and accuracy stages do not — this is about being found, not
    /// about being worse at things.
    /// </summary>
    [Fact]
    public void BeingFoundStopsEvasionCounting()
    {
        Battler attacker = Make();
        Battler defender = Make();

        defender.ChangeStage(Stat.Evasion, 6);

        MoveData certain = Move(0x00, 40);

        int missedWhileHiding = 0;
        int missedOnceFound = 0;

        for (uint seed = 1; seed <= 200; seed++)
        {
            if (!DamageCalculator.RollAccuracy(new BattleRng(seed), certain, attacker, defender))
                missedWhileHiding++;
        }

        defender.IsIdentified = true;

        for (uint seed = 1; seed <= 200; seed++)
        {
            if (!DamageCalculator.RollAccuracy(new BattleRng(seed), certain, attacker, defender))
                missedOnceFound++;
        }

        Assert.True(missedWhileHiding > 0, "six stages of evasion should have caused misses");
        Assert.Equal(0, missedOnceFound);
    }

    /// <summary>
    /// And an immunity it was relying on stops applying — but only an immunity. Being
    /// resistant is not hiding, and a move that turned resistance into neutral would be a
    /// different move.
    /// </summary>
    [Fact]
    public void AndOnlyAnImmunityStopsApplying()
    {
        Battler attacker = Make();

        var ghost = new Battler(Species(PokemonType.Ghost), 50);

        MoveData ordinary = Move(0x00, 60);

        Assert.Equal(0, DamageCalculator.Calculate(attacker, ghost, ordinary, false, 100).Damage);

        ghost.IsIdentified = true;

        // A real hit rather than the damage floor. The first version of this asserted only
        // "more than nothing", and more than nothing was exactly one point: the chart still
        // returned zero and the floor made it one. One point looks like a hit until somebody
        // counts, which is what this line now does.
        var plain = new Battler(Species(), 50);

        int neutral = DamageCalculator.Calculate(attacker, plain, ordinary, false, 100).Damage;

        Assert.Equal(neutral, DamageCalculator.Calculate(attacker, ghost, ordinary, false, 100).Damage);

        // A rock resists nothing here and is not immune to anything Normal, so being found
        // changes nothing about it — which is what says only the immunity moved.
        var rock = new Battler(Species(PokemonType.Rock), 50);

        int before = DamageCalculator.Calculate(attacker, rock, ordinary, false, 100).Damage;

        rock.IsIdentified = true;

        Assert.Equal(before, DamageCalculator.Calculate(attacker, rock, ordinary, false, 100).Damage);
    }

    [Fact]
    public void BeingFoundTwiceIsNotFoundAgain()
    {
        Battler you = Make(PokemonType.Normal, Move(Foresight, 0));
        Battler them = Make(PokemonType.Normal, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.Identified { Side: Side.Opponent });
        Assert.Contains(Turn(battle), e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
    }

    /// <summary>And leaving the field is what undoes it.</summary>
    [Fact]
    public void AndLeavingTheFieldUndoesIt()
    {
        Battler them = Make();

        them.IsIdentified = true;

        them.ForgetWhatWasStarted();

        Assert.False(them.IsIdentified);
    }
}
