using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Eighteen groups that used to do nothing, and what each of them needed.
/// <para>
/// The interesting thing about this batch is how little of it was new machinery. Four of the
/// groups wind up for a turn, which this engine has done since FLY and nobody had pointed
/// them at. Two inflict conditions it already inflicts. Four move two of the user's stats,
/// which is the all-five path from the milestone before with a list instead of everything.
/// </para>
/// <para>
/// Which stat, and which condition, is in the game's code and is modelled. That a group is a
/// stat group at all — and which moves are in it — is read.
/// </para>
/// </summary>
public class SilentNoLongerTests
{
    private static SpeciesData Species() => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 60, BaseDefense = 200,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 200,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0) =>
        new(1, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(params MoveData[] moves)
    {
        var battler = new Battler(Species(), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    /// <summary>
    /// Every group in this batch says something. The blunt instrument, and the one that
    /// notices if a line is deleted.
    /// </summary>
    [Theory]
    [InlineData(0xCE)] [InlineData(0xD0)] [InlineData(0xD3)] [InlineData(0xD4)]
    [InlineData(0xCD)] [InlineData(0xB6)] [InlineData(0x6C)] [InlineData(0x9C)]
    [InlineData(0x8A)] [InlineData(0x27)] [InlineData(0x4B)] [InlineData(0x91)]
    [InlineData(0x97)] [InlineData(0x55)] [InlineData(0xA7)] [InlineData(0xC7)]
    [InlineData(0xC1)] [InlineData(0x2F)] [InlineData(0x65)]
    public void NoneOfTheseGroupsIsSilentAnyMore(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    /// <summary>
    /// The four that raise two of the user's own stats do exactly two, and it is one act
    /// rather than two — which is what the list on the effect is for.
    /// </summary>
    [Theory]
    [InlineData(0xCE, Stat.Defense, Stat.SpDefense)]
    [InlineData(0xD0, Stat.Attack, Stat.Defense)]
    [InlineData(0xD3, Stat.SpAttack, Stat.SpDefense)]
    [InlineData(0xD4, Stat.Attack, Stat.Speed)]
    public void EachOfTheFourMovesExactlyTwoOfItsOwn(int effect, Stat first, Stat second)
    {
        Battler you = Make(Move((byte)effect));
        Battler them = Make(Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.Equal(1, you.StageOf(first));
        Assert.Equal(1, you.StageOf(second));

        // And nothing else moved, which is what says the list is the list.
        foreach (Stat other in MoveEffects.Five)
        {
            if (other == first || other == second) Assert.Equal(1, you.StageOf(other));
            else Assert.Equal(0, you.StageOf(other));
        }
    }

    /// <summary>And one takes two off somebody else, which is the same shape aimed outwards.</summary>
    [Fact]
    public void AndOneTakesTwoOffSomebodyElse()
    {
        Battler you = Make(Move(0xCD));
        Battler them = Make(Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.Equal(-1, them.StageOf(Stat.Attack));
        Assert.Equal(-1, them.StageOf(Stat.Defense));
        Assert.Equal(0, you.StageOf(Stat.Attack));
    }

    /// <summary>
    /// SUPERPOWER lands and then costs its user the two stats it just used, which is the
    /// second group in this table to charge its own user anything.
    /// </summary>
    [Fact]
    public void SuperpowerCostsItsUserWhatItJustUsed()
    {
        Battler you = Make(Move(0xB6, 120));
        Battler them = Make(Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.Equal(-1, you.StageOf(Stat.Attack));
        Assert.Equal(-1, you.StageOf(Stat.Defense));
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    /// <summary>
    /// The four that wind up needed no new machinery at all — this engine has taken a turn
    /// to charge since FLY, and these were never pointed at it.
    /// </summary>
    [Theory]
    [InlineData(0x27)] [InlineData(0x4B)] [InlineData(0x91)] [InlineData(0x97)]
    public void TheFourThatWindUpTakeATurnToDoIt(int effect)
    {
        Assert.Equal(EffectKind.TwoTurn, MoveEffects.Of((byte)effect).Kind);

        Battler you = Make(Move((byte)effect, 120));
        Battler them = Make(Move(0x00, 10));

        var battle = new Battle(you, them, 7);

        int before = them.CurrentHp;

        Turn(battle);

        // The first turn is the winding, so nothing has landed and the user has no choice
        // about the second.
        Assert.Equal(before, them.CurrentHp);
        Assert.NotNull(you.ForcedSlot);

        Turn(battle);

        Assert.True(them.CurrentHp < before);
    }

    /// <summary>
    /// SPLASH is not silent. It is finished, which is a different answer and the whole
    /// reason this engine tells the two apart.
    /// </summary>
    [Fact]
    public void SplashIsFinishedRatherThanSilent()
    {
        Assert.Equal(EffectKind.Nothing, MoveEffects.Of(0x55).Kind);
        Assert.NotEqual(EffectKind.None, MoveEffects.Of(0x55).Kind);
    }

    [Fact]
    public void TwoMoreWaysToInflictWhatWasAlreadyInflicted()
    {
        Battler you = Make(Move(0xA7));
        Battler them = Make(Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.Equal(StatusCondition.Burn, them.Status);

        Battler dancer = Make(Move(0xC7));
        Battler danced = Make(Move(0x00, 10));

        Turn(new Battle(dancer, danced, 7));

        Assert.True(danced.ConfusedTurns > 0);
    }

    [Fact]
    public void RefreshClearsItsOwnUsersCondition()
    {
        Battler you = Make(Move(0xC1));
        Battler them = Make(Move(0x00, 10));

        you.Status = StatusCondition.Poison;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(StatusCondition.None, you.Status);
        Assert.Contains(events, e => e is BattleEvent.PutRight { Side: Side.Player });
    }

    [Fact]
    public void AndSaysSoWhenThereIsNothingToClear()
    {
        Battler you = Make(Move(0xC1));
        Battler them = Make(Move(0x00, 10));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// Being focused is a flag rather than a stage, and HAZE is what proves it: a stage
    /// would be cleared and this is not.
    /// </summary>
    [Fact]
    public void FocusingIsAFlagRatherThanAStage()
    {
        Battler you = Make(Move(0x2F));
        Battler them = Make(Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.True(you.IsFocused);

        // Only once — a second go finds it already true and says so rather than pretending.
        Battler again = Make(Move(0x2F));
        again.IsFocused = true;

        Assert.Contains(
            Turn(new Battle(again, Make(Move(0x00, 10)), 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });

        // And it goes when its owner does, which is the whole of what "until it leaves"
        // means.
        you.ForgetWhatWasStarted();

        Assert.False(you.IsFocused);
    }

    /// <summary>
    /// FALSE SWIPE leaves one point, and leaves it every time rather than most of the time.
    /// </summary>
    [Fact]
    public void FalseSwipeNeverTakesTheLastPoint()
    {
        Battler you = new Battler(Species(), 100);
        you.Moves.Add(Move(0x65, 250));

        Battler them = new Battler(Species(), 2);
        them.Moves.Add(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        for (int turn = 0; turn < 5; turn++) Turn(battle);

        Assert.False(them.HasFainted);
        Assert.Equal(1, them.CurrentHp);
    }

    /// <summary>And an ordinary move in its place finishes the same creature.</summary>
    [Fact]
    public void AndAnOrdinaryMoveInItsPlaceDoesNot()
    {
        Battler you = new Battler(Species(), 100);
        you.Moves.Add(Move(0x00, 250));

        Battler them = new Battler(Species(), 2);
        them.Moves.Add(Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.True(them.HasFainted);
    }
}
