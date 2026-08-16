using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Six groups whose whole point is that they do not happen now.
/// <para>
/// Four of them are the end-of-turn hook the berries were built on, used for the fifth,
/// sixth, seventh and eighth time. That hook has now paid for itself several times over,
/// which is the argument for building machinery when the second thing needs it rather than
/// the fifth.
/// </para>
/// </summary>
public class ThingsThatHappenLaterTests
{
    private const byte Nightmare = 0x6B;
    private const byte Yawn = 0xBB;
    private const byte Ingrain = 0xB5;
    private const byte PerishSong = 0x72;
    private const byte Swagger = 0x76;
    private const byte Flatter = 0xA6;

    private static SpeciesData Species() => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 100, BaseDefense = 200,
        BaseSpeed = 60, BaseSpAttack = 100, BaseSpDefense = 200,
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

    [Theory]
    [InlineData(Nightmare)] [InlineData(Yawn)] [InlineData(Ingrain)]
    [InlineData(PerishSong)] [InlineData(Swagger)] [InlineData(Flatter)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- sleep, made worse ---------------------------------------------------------------

    /// <summary>
    /// A nightmare is not a condition of its own — it is a thing sleep does once somebody has
    /// made it do it, which is why it needs somebody already asleep and ends when they wake.
    /// </summary>
    [Fact]
    public void ANightmareNeedsSomebodyAlreadyAsleep()
    {
        Battler you = Make(Move(Nightmare));
        Battler awake = Make(Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, awake, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Opponent });

        Assert.False(awake.InNightmare);
    }

    [Fact]
    public void AndCostsThemHealthEveryTurnUntilTheyWake()
    {
        Battler you = Make(Move(Nightmare));
        Battler them = Make(Move(0x00, 0));

        them.Status = StatusCondition.Sleep;
        them.SleepTurns = 99;

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.HurtBySleep { Side: Side.Opponent });

        int after = them.CurrentHp;

        Turn(battle);

        Assert.True(them.CurrentHp < after);

        // And it stops when the sleep does, rather than outliving it.
        them.Status = StatusCondition.None;

        int awake = them.CurrentHp;

        Turn(battle);

        Assert.Equal(awake, them.CurrentHp);
        Assert.False(them.InNightmare);
    }

    // ---- sleep, delayed -----------------------------------------------------------------

    /// <summary>
    /// The delay is the entire move. One that put somebody to sleep now would be a different
    /// and much better move.
    /// </summary>
    [Fact]
    public void DrowsinessLandsLaterRatherThanNow()
    {
        Battler you = Make(Move(Yawn));
        Battler them = Make(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.Drowsy { Side: Side.Opponent });
        Assert.Equal(StatusCondition.None, them.Status);

        Turn(battle);

        Assert.Equal(StatusCondition.Sleep, them.Status);
    }

    [Fact]
    public void AndSomebodyAlreadySufferingIsLeftAlone()
    {
        Battler you = Make(Move(Yawn));
        Battler them = Make(Move(0x00, 0));

        them.Status = StatusCondition.Burn;

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Opponent });

        Assert.Equal(0, them.DrowsyTurns);
    }

    // ---- roots ---------------------------------------------------------------------------

    /// <summary>Health every turn, and no leaving — which is the price rather than a detail.</summary>
    [Fact]
    public void TakingRootGivesHealthBackAndGivesUpLeaving()
    {
        Battler you = Make(Move(Ingrain));
        Battler them = Make(Move(0x00, 0));

        you.TakeDamage(you.MaxHp / 2);

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.TookRoot { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.Recovered { Side: Side.Player });

        Assert.True(you.CurrentHp > before);
        Assert.True(you.CannotEscape);
    }

    // ---- the count -----------------------------------------------------------------------

    /// <summary>
    /// Everybody hears it, including whoever sang it — which is what makes it a threat rather
    /// than a win.
    /// </summary>
    [Fact]
    public void EverybodyHearsItIncludingWhoeverSangIt()
    {
        Battler you = Make(Move(PerishSong));
        Battler them = Make(Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.True(you.PerishTurns > 0);
        Assert.True(them.PerishTurns > 0);
    }

    [Fact]
    public void AndBothGoDownWhenItRunsOut()
    {
        Battler you = Make(Move(PerishSong));
        Battler them = Make(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        for (int turn = 0; turn < 4 && !battle.IsOver; turn++) Turn(battle);

        Assert.True(you.HasFainted || them.HasFainted);
        Assert.True(battle.IsOver);
    }

    /// <summary>
    /// And leaving the field does not take it with you, which is the one thing on a battler
    /// that leaving does not clear — and the whole of what makes the move worth using.
    /// </summary>
    [Fact]
    public void AndLeavingTheFieldDoesNotEscapeIt()
    {
        Battler you = Make();

        you.PerishTurns = 3;
        you.IsRooted = true;

        you.ForgetWhatWasStarted();

        Assert.Equal(3, you.PerishTurns);
        Assert.False(you.IsRooted);
    }

    // ---- stronger, and unable to use it --------------------------------------------------

    /// <summary>
    /// Stronger first and then confused, because a creature that fainted to its own confusion
    /// before the stage landed would be a move that sometimes did half of itself.
    /// </summary>
    [Theory]
    [InlineData(Swagger, Stat.Attack)]
    [InlineData(Flatter, Stat.SpAttack)]
    public void EachMakesThemStrongerAndThenTooConfusedToUseIt(int effect, Stat stat)
    {
        Battler you = Make(Move((byte)effect));
        Battler them = Make(Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(2, them.StageOf(stat));
        Assert.True(them.ConfusedTurns > 0);

        Assert.Contains(events, e => e is BattleEvent.StageChanged { Side: Side.Opponent });
        Assert.Contains(events, e => e is BattleEvent.Confused { Side: Side.Opponent });
    }
}
