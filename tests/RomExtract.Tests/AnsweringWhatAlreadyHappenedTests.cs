using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Six moves that are answers to what has already happened this turn.
/// <para>
/// The list this came off described them as needing to act out of turn, and that turned out
/// to be wrong in the most useful way: <b>the ordering was already there.</b> A move's
/// priority is a signed byte on its own record, read off the cartridge since moves were
/// first read, and this engine has ordered by it since long before any of these six had an
/// effect. COUNTER comes last because its record says minus five, not because anything here
/// arranges it.
/// </para>
/// <para>
/// So what was actually missing was memory: a creature that knows what has been done to it
/// since the turn began, and how. That is two fields and a line in the damage loop, and it
/// is the third time on this list that a family named for the machinery it seemed to need
/// turned out to need something much smaller.
/// </para>
/// </summary>
public class AnsweringWhatAlreadyHappenedTests
{
    private const byte Counter = 0x59;
    private const byte MirrorCoat = 0x90;
    private const byte Revenge = 0xB9;
    private const byte FakeOut = 0x9E;
    private const byte FocusPunch = 0xAA;
    private const byte VitalThrow = 0x4E;

    private static SpeciesData Species(int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    /// <summary>A move, with its priority given as the cartridge would.</summary>
    private static MoveData Move(
        byte effect, byte power = 0, PokemonType type = PokemonType.Normal,
        sbyte priority = 0, byte accuracy = 100, int id = 1) =>
        new(id, string.Empty, effect, power, type, accuracy, 20, 100, 0, priority);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(Counter)] [InlineData(MirrorCoat)] [InlineData(Revenge)]
    [InlineData(FakeOut)] [InlineData(FocusPunch)] [InlineData(VitalThrow)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- the memory itself ----------------------------------------------------------

    /// <summary>A creature knows what has been done to it this turn, and by which kind.</summary>
    [Fact]
    public void ACreatureKnowsWhatWasDoneToItThisTurn()
    {
        Battler you = Make(60, Move(0x00, 60));
        Battler them = Make(60, Move(0x00, 60, PokemonType.Fire));

        Turn(new Battle(you, them, 7));

        Assert.True(you.HurtThisTurn > 0);
        Assert.Equal(DamageCategory.Special, you.HurtThisTurnBy);

        Assert.True(them.HurtThisTurn > 0);
        Assert.Equal(DamageCategory.Physical, them.HurtThisTurnBy);
    }

    /// <summary>
    /// And it is forgotten at the top of the next turn, so an answer answers this turn's hit
    /// rather than last turn's.
    /// </summary>
    [Fact]
    public void AndForgetsItWhenTheNextTurnBegins()
    {
        Battler you = Make(60, Move(0x00, 60), Move(0x00, 0, id: 2));
        Battler them = Make(60, Move(0x00, 60), Move(0x00, 0, id: 2));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.True(you.HurtThisTurn > 0);

        // Both use the move with no power, so nothing is dealt.
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(1));

        Assert.Equal(0, you.HurtThisTurn);
        Assert.Null(you.HurtThisTurnBy);
    }

    // ---- giving it back ------------------------------------------------------------------

    /// <summary>
    /// It gives back twice what was taken — the amount, not a formula. Measured against what
    /// the hit actually did rather than against a number written here.
    /// </summary>
    [Fact]
    public void ItGivesBackTwiceWhatWasTaken()
    {
        // Minus five, which is what the record says and what makes it go last.
        Battler you = Make(200, Move(Counter, 1, priority: -5));
        Battler them = Make(10, Move(0x00, 60));

        Battler you2 = you;

        var battle = new Battle(you, them, 7);

        int before = them.CurrentHp;

        Turn(battle);

        Assert.True(you2.HurtThisTurn > 0);
        Assert.Equal(you2.HurtThisTurn * 2, before - them.CurrentHp);
    }

    /// <summary>
    /// And each one only answers its own kind. This is the whole difference between the two
    /// of them, and getting it wrong makes them the same move.
    /// </summary>
    [Theory]
    [InlineData(Counter, PokemonType.Fire, false)]
    [InlineData(Counter, PokemonType.Normal, true)]
    [InlineData(MirrorCoat, PokemonType.Fire, true)]
    [InlineData(MirrorCoat, PokemonType.Normal, false)]
    public void AndEachOnlyAnswersItsOwnKind(int effect, PokemonType coming, bool answers)
    {
        Battler you = Make(200, Move((byte)effect, 1, priority: -5));
        Battler them = Make(10, Move(0x00, 60, coming));

        int before = them.CurrentHp;

        Turn(new Battle(you, them, 7));

        Assert.Equal(answers, them.CurrentHp < before);
    }

    /// <summary>And on a quiet turn it does nothing at all.</summary>
    [Fact]
    public void AndOnAQuietTurnItDoesNothing()
    {
        Battler you = Make(200, Move(Counter, 1, priority: -5));
        Battler them = Make(10, Move(0x00, 0));

        int before = them.CurrentHp;

        Turn(new Battle(you, them, 7));

        Assert.Equal(before, them.CurrentHp);
    }

    // ---- revenge -----------------------------------------------------------------------------

    /// <summary>
    /// Twice as hard when its user has already been hit, and its ordinary power otherwise.
    /// <para>
    /// Measured on the power rather than on the damage, because damage carries an
    /// eighty-five to a hundred roll and one doubling is inside that noise at these numbers —
    /// which is a mistake this project has already made once, in the milestone before.
    /// </para>
    /// </summary>
    [Fact]
    public void RevengeIsTwiceAsHardWhenItsUserHasBeenHit()
    {
        Battler you = Make(60, Move(Revenge, 60, priority: -4));
        Battler them = Make(60, Move(0x00, 0));

        MoveData revenge = you.MoveAt(0)!;

        Assert.Null(MovePower.Of(revenge, you, them));

        you.HurtThisTurn = 20;

        Assert.Equal(120, MovePower.Of(revenge, you, them));
    }

    // ---- only on arrival -----------------------------------------------------------------------

    /// <summary>It works on the turn its user arrives, and it takes the other one's turn.</summary>
    [Fact]
    public void ArrivingWorksOnTheFirstTurn()
    {
        Battler you = Make(200, Move(FakeOut, 40, priority: 1));
        Battler them = Make(10, Move(0x00, 60));

        int before = you.CurrentHp;

        Turn(new Battle(you, them, 7));

        // It landed, and they never got their go.
        Assert.True(them.CurrentHp < them.MaxHp);
        Assert.Equal(before, you.CurrentHp);
    }

    /// <summary>
    /// And on any turn after that it does nothing — which is what makes it a move somebody
    /// leads with rather than a free turn every turn.
    /// </summary>
    [Fact]
    public void AndDoesNothingOnAnyTurnAfterThat()
    {
        Battler you = Make(200, Move(FakeOut, 40, priority: 1));
        Battler them = Make(10, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int after = them.CurrentHp;

        List<BattleEvent> second = Turn(battle);

        Assert.Equal(after, them.CurrentHp);
        Assert.Contains(second, e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>And leaving and coming back makes it the first turn again.</summary>
    [Fact]
    public void AndComingBackMakesItTheFirstTurnAgain()
    {
        Battler you = Make(200, Move(FakeOut, 40, priority: 1));

        you.TurnsOut = 6;

        you.ForgetWhatWasStarted();

        Assert.Equal(0, you.TurnsOut);
    }

    // ---- needing quiet --------------------------------------------------------------------------

    /// <summary>Hit before it lands, and it comes to nothing — with its own line, not a miss.</summary>
    [Fact]
    public void BeingHitFirstCostsTheOneThatNeedsQuiet()
    {
        Battler you = Make(10, Move(FocusPunch, 150, priority: -3));
        Battler them = Make(200, Move(0x00, 60));

        int before = them.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.LostItsNerve { Side: Side.Player });
        Assert.Equal(before, them.CurrentHp);

        // A refusal rather than a miss, which is a difference a player has to be able to see.
        Assert.DoesNotContain(events, e => e is BattleEvent.MoveMissed { Side: Side.Player });
    }

    /// <summary>And on a quiet turn it lands.</summary>
    [Fact]
    public void AndOnAQuietTurnItLands()
    {
        Battler you = Make(10, Move(FocusPunch, 150, priority: -3));
        Battler them = Make(200, Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.DoesNotContain(events, e => e is BattleEvent.LostItsNerve);
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    // ---- slow and sure -----------------------------------------------------------------------------

    /// <summary>
    /// It never misses, and its record does not say so — the accuracy field is an ordinary
    /// number, and the certainty is what the effect adds in exchange for going last.
    /// </summary>
    [Fact]
    public void TheSlowOneNeverMisses()
    {
        MoveData throwing = Move(VitalThrow, 70, priority: -1, accuracy: 1);

        // Its record is not one of the never-miss ones, so this is the effect's doing.
        Assert.False(throwing.AlwaysHits);

        Battler you = Make(60, throwing);
        Battler them = Make(60, Move(0x00, 0));

        var rng = new BattleRng(7);

        for (int roll = 0; roll < 20; roll++)
            Assert.True(DamageCalculator.RollAccuracy(rng, throwing, you, them));
    }

    /// <summary>
    /// And an ordinary move with the same hopeless accuracy does miss, which is what says the
    /// test above is measuring the effect rather than measuring nothing.
    /// </summary>
    [Fact]
    public void AndAnOrdinaryMoveWithTheSameAccuracyDoesMiss()
    {
        MoveData hopeless = Move(0x00, 70, accuracy: 1);

        Battler you = Make(60, hopeless);
        Battler them = Make(60, Move(0x00, 0));

        var rng = new BattleRng(7);

        var missed = false;

        for (int roll = 0; roll < 40 && !missed; roll++)
            missed = !DamageCalculator.RollAccuracy(rng, hopeless, you, them);

        Assert.True(missed, "a move accurate one time in a hundred never missed in forty goes");
    }

    // ---- the ordering that was already there -------------------------------------------------------

    /// <summary>
    /// The priority on the record decides the order, and it decided it before any of these
    /// six meant anything.
    /// <para>
    /// Worth its own test because it is the reason this family cost so little: what looked
    /// like six moves needing a new turn-order mechanism was six moves needing to be
    /// pointed at one the engine has had all along.
    /// </para>
    /// </summary>
    [Fact]
    public void ThePriorityOnTheRecordDecidesTheOrder()
    {
        // Slower, and going first anyway.
        Battler you = Make(10, Move(0x00, 60, priority: 1));
        Battler them = Make(200, Move(0x00, 60, id: 2));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        BattleEvent.MoveUsed first = events.OfType<BattleEvent.MoveUsed>().First();

        Assert.Equal(Side.Player, first.Side);
    }
}
