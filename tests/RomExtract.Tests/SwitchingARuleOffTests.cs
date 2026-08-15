using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The four moves that switch off a rule this engine already follows.
/// <para>
/// HAZE stops the stages counting, MIST stops anybody lowering them, SAFEGUARD stops
/// anybody afflicting anything, and MIND READER and LOCK-ON stop the next move rolling
/// for accuracy. None of the four does anything to anybody. Each of them stops something
/// else from happening — which is why they were cheap: every rule they turn off was
/// already written, in one place, with one caller.
/// </para>
/// <para>
/// Their records agree as far as records can. All four have no power and all four are
/// status moves; the three that shield are aimed at their own side by the target byte,
/// which is the group whose whole effect is on whoever used them. What is modelled is how
/// long two of them hold — and MIND READER holds for no count at all, because "the next
/// one" is the only reading of it that needs no number.
/// </para>
/// <para>
/// The half of each rule that is deliberately not switched off is the interesting half.
/// A shield is against somebody else: a move that trades its own user's stat for power
/// still trades it through MIST, and REST still puts its own user to sleep through a
/// SAFEGUARD.
/// </para>
/// </summary>
public class SwitchingARuleOffTests
{
    private const byte HazeEffect = 0x19;
    private const byte MistEffect = 0x2E;
    private const byte SafeguardEffect = 0x7C;
    private const byte AimEffect = 0x5E;

    private const byte LowersTheirAttack = 0x12;
    private const byte RaisesMyAttack = 0x0A;
    private const byte PutsThemToSleep = 0x01;
    private const byte Rest = 0x25;

    private static MoveData Move(int id, byte effect, byte power = 0, byte accuracy = 100) =>
        new(id, "", effect, power, PokemonType.Normal, accuracy, 20, 0, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            Name = string.Empty,
            BaseHp = 200, BaseAttack = 60, BaseDefense = 60,
            BaseSpeed = (byte)speed, BaseSpAttack = 60, BaseSpDefense = 60,
            Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
            CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        };

        return new Battler(species, 50, Nature.Hardy, null).Knowing(moves);
    }

    // ---- haze -------------------------------------------------------------------------

    /// <summary>Everything on both sides goes back to nothing, including the user's own.</summary>
    [Fact]
    public void HazeTakesEveryStageOffBothSides()
    {
        Battler you = Make(250, Move(1, HazeEffect));
        Battler them = Make(1, Move(2, 0x00, 10));

        you.ChangeStage(Stat.Attack, +2);
        them.ChangeStage(Stat.Defense, -1);

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.StagesCleared { Side: Side.Player });
        Assert.Equal(0, you.StageOf(Stat.Attack));
        Assert.Equal(0, them.StageOf(Stat.Defense));
    }

    // ---- mist -------------------------------------------------------------------------

    /// <summary>Nothing from outside may lower a stat while it holds.</summary>
    [Fact]
    public void MistRefusesWhatTheOtherSideWouldLower()
    {
        Battler you = Make(250, Move(1, MistEffect));
        Battler them = Make(1, Move(2, LowersTheirAttack));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> up =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(up, e => e is BattleEvent.MistRose { Side: Side.Player });
        Assert.Contains(up, e => e is BattleEvent.Shielded { Side: Side.Player });
        Assert.Equal(0, you.StageOf(Stat.Attack));
    }

    /// <summary>And it does not stop this one raising its own.</summary>
    [Fact]
    public void AndMistDoesNotStopThisOneRaisingItsOwn()
    {
        Battler you = Make(250, Move(1, MistEffect), Move(2, RaisesMyAttack));
        Battler them = Make(1, Move(3, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(1, you.StageOf(Stat.Attack));
    }

    /// <summary>And it runs out.</summary>
    [Fact]
    public void AndMistRunsOut()
    {
        Battler you = Make(250, Move(1, MistEffect), Move(2, 0x00, 10));
        Battler them = Make(1, Move(3, LowersTheirAttack));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(you.IsMisted);

        // Anything but the mist itself, which would only put it back up.
        for (int turn = 0; turn < 6; turn++)
            battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.False(you.IsMisted);
    }

    // ---- safeguard --------------------------------------------------------------------

    /// <summary>Nothing from outside may afflict this side while it holds.</summary>
    [Fact]
    public void ASafeguardRefusesWhatWouldAfflictThisSide()
    {
        Battler you = Make(250, Move(1, SafeguardEffect));
        Battler them = Make(1, Move(2, PutsThemToSleep));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.Safeguarded { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.Shielded { Side: Side.Player });
        Assert.Equal(StatusCondition.None, you.Status);
    }

    /// <summary>
    /// And this one may still put itself to sleep. A move that shields a side does not
    /// shield it from itself, which is the whole difference between a rule about who is
    /// doing something and a rule about what is being done.
    /// </summary>
    [Fact]
    public void AndASafeguardDoesNotStopThisOneResting()
    {
        Battler you = Make(250, Move(1, SafeguardEffect), Move(2, Rest));
        Battler them = Make(1, Move(3, 0x00, 60));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.Sleep, you.Status);
    }

    // ---- taking aim -------------------------------------------------------------------

    /// <summary>The next move cannot miss, however unlikely it was to land.</summary>
    [Fact]
    public void AimTakenMeansTheNextOneCannotMiss()
    {
        Battler you = Make(250, Move(1, AimEffect), Move(2, 0x00, 40, accuracy: 1));
        Battler them = Make(1, Move(3, 0x00, 10));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> aimed =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(aimed, e => e is BattleEvent.TookAim { Side: Side.Player });
        Assert.True(you.HasAimed);

        List<BattleEvent> landed =
            battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.DoesNotContain(landed, e => e is BattleEvent.MoveMissed { Side: Side.Player });
        Assert.False(you.HasAimed);
    }

    /// <summary>And it is spent on one move, not held until something lands.</summary>
    [Fact]
    public void AndItIsSpentOnOneMove()
    {
        Battler you = Make(250, Move(1, AimEffect), Move(2, 0x00, 40, accuracy: 1));
        Battler them = Make(1, Move(3, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        bool missed = false;

        for (int turn = 0; turn < 12 && !missed; turn++)
        {
            missed = battle
                .ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0))
                .Any(e => e is BattleEvent.MoveMissed { Side: Side.Player });
        }

        Assert.True(missed);
    }

    // ---- and none of the four is silence any more --------------------------------------

    [Theory]
    [InlineData(HazeEffect)]
    [InlineData(MistEffect)]
    [InlineData(SafeguardEffect)]
    [InlineData(AimEffect)]
    public void AndNoneOfThemIsSilentNow(byte effect)
    {
        Assert.False(MoveEffects.IsSilent(effect));
    }

    /// <summary>And all four are aimed at their own side, which is what a shield is.</summary>
    [Theory]
    [InlineData(HazeEffect)]
    [InlineData(MistEffect)]
    [InlineData(SafeguardEffect)]
    [InlineData(AimEffect)]
    public void AndAllFourActOnTheirOwnSide(byte effect)
    {
        Assert.True(MoveEffects.Of(effect).OnUser);
    }
}
