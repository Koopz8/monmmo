using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Four moves about a type that is not the one a creature was born with.
/// <para>
/// Three change their user; the fourth changes its own, which is a different thing wearing
/// the same word. The three needed something this engine has never had — a creature whose
/// type a fight can change. Until now <c>Type1</c> and <c>Type2</c> read straight off the
/// species record, with nowhere for an answer of its own to live, which is exactly the shape
/// the ability had before the milestone before this one.
/// </para>
/// <para>
/// One field, not two, and that is the load-bearing decision: all three of these moves make
/// their user <em>one</em> type and nothing else. A creature that kept half of what it was
/// would be a rule none of them describe.
/// </para>
/// </summary>
public class BecomingSomethingElseTests
{
    private const byte Conversion = 0x1E;
    private const byte ConversionTwo = 0x5D;
    private const byte Camouflage = 0xD5;
    private const byte WeatherBall = 0xCB;

    private static SpeciesData Species(
        PokemonType first = PokemonType.Normal, PokemonType second = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = 60, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = first, Type2 = second,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0, PokemonType type = PokemonType.Normal, int id = 1) =>
        new(id, string.Empty, effect, power, type, 100, 20, 100, 0, 0);

    private static Battler Make(SpeciesData species, params MoveData[] moves)
    {
        var battler = new Battler(species, 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(Conversion)] [InlineData(ConversionTwo)]
    [InlineData(Camouflage)] [InlineData(WeatherBall)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- a type a fight can change --------------------------------------------------

    /// <summary>
    /// A borrowed type replaces both, so the creature is that one type and nothing else.
    /// </summary>
    [Fact]
    public void ABorrowedTypeReplacesBothOfThem()
    {
        Battler you = Make(Species(PokemonType.Water, PokemonType.Flying));

        Assert.True(you.Is(PokemonType.Water));
        Assert.True(you.Is(PokemonType.Flying));

        you.BorrowedType = PokemonType.Fire;

        Assert.Equal(PokemonType.Fire, you.Type1);
        Assert.Equal(PokemonType.Fire, you.Type2);

        Assert.False(you.Is(PokemonType.Water));
        Assert.False(you.Is(PokemonType.Flying));
    }

    /// <summary>
    /// And it changes what the type chart says about them, which is the only reason any of
    /// these moves is worth using.
    /// </summary>
    [Fact]
    public void AndItChangesWhatTheChartSaysAboutThem()
    {
        Battler you = Make(Species(PokemonType.Normal, PokemonType.Normal));

        int before = TypeChart.Effectiveness(PokemonType.Fighting, you.Type1, you.Type2);

        you.BorrowedType = PokemonType.Ghost;

        int after = TypeChart.Effectiveness(PokemonType.Fighting, you.Type1, you.Type2);

        Assert.NotEqual(before, after);
        Assert.Equal(0, after);
    }

    /// <summary>And it goes when its owner does, like everything else a fight starts.</summary>
    [Fact]
    public void AndItGoesWhenItsOwnerDoes()
    {
        Battler you = Make(Species(PokemonType.Water));

        you.BorrowedType = PokemonType.Fire;

        you.ForgetWhatWasStarted();

        Assert.Null(you.BorrowedType);
        Assert.Equal(PokemonType.Water, you.Type1);
    }

    // ---- becoming one of its own moves -------------------------------------------------

    [Fact]
    public void BecomingOneOfItsOwnMovesTakesATypeItCarries()
    {
        Battler you = Make(
            Species(PokemonType.Normal),
            Move(Conversion),
            Move(0x00, 40, PokemonType.Electric, id: 2));

        Battler them = Make(Species(), Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.ChangedType { Side: Side.Player });

        // Electric is the only type it carries that it is not already, so there is no
        // guessing about which answer is right.
        Assert.Equal(PokemonType.Electric, you.Type1);
    }

    /// <summary>
    /// And a creature whose every move is already its own type has nothing to become, and
    /// says so rather than announcing a change to what it already was.
    /// </summary>
    [Fact]
    public void AndOneWithNothingToBecomeSaysSo()
    {
        Battler you = Make(
            Species(PokemonType.Normal), Move(Conversion), Move(0x00, 40, PokemonType.Normal, id: 2));

        Battler them = Make(Species(), Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });

        Assert.Null(you.BorrowedType);
    }

    // ---- becoming an answer ---------------------------------------------------------------

    /// <summary>
    /// It becomes something that stands up to what they last threw — which is the whole
    /// claim, and it is checked against the chart rather than against a list written here.
    /// </summary>
    [Fact]
    public void BecomingAnAnswerTakesSomethingThatResistsWhatTheyThrew()
    {
        // Slower, so they have moved by the time it is asked. Their move has no power on
        // purpose: what matters is that they used something Fire, and a first version of
        // this gave them a real one, which finished the slow creature before it ever acted.
        var slow = new Battler(Species(PokemonType.Normal, PokemonType.Normal), 5);
        slow.Moves.Add(Move(ConversionTwo));

        var fast = new Battler(Species(), 60);
        fast.Moves.Add(Move(0x00, 0, PokemonType.Fire, id: 2));

        Turn(new Battle(slow, fast, 7));

        Assert.NotNull(slow.BorrowedType);

        Assert.True(
            TypeChart.Against(PokemonType.Fire, slow.BorrowedType!.Value) < TypeChart.Neutral,
            $"it became {slow.BorrowedType}, which does not resist Fire");
    }

    /// <summary>And with nothing thrown yet there is nothing to answer.</summary>
    [Fact]
    public void AndWithNothingThrownYetThereIsNothingToAnswer()
    {
        var fast = new Battler(Species(PokemonType.Normal), 60);
        fast.Moves.Add(Move(ConversionTwo));

        var slow = new Battler(Species(), 5);
        slow.Moves.Add(Move(0x00, 40, PokemonType.Fire, id: 2));

        Assert.Contains(
            Turn(new Battle(fast, slow, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    // ---- becoming the ground -----------------------------------------------------------------

    /// <summary>
    /// It becomes whatever this place is made of, and what this place is made of is supplied
    /// from outside — a battle does not know where it is.
    /// </summary>
    [Fact]
    public void BecomingTheGroundTakesWhatThePlaceIsMadeOf()
    {
        Battler you = Make(Species(PokemonType.Normal), Move(Camouflage));
        Battler them = Make(Species(), Move(0x00, 0));

        Turn(new Battle(you, them, 7) { Ground = PokemonType.Rock });

        Assert.Equal(PokemonType.Rock, you.Type1);
    }

    /// <summary>
    /// And a battle told nothing about where it is says Normal, which is both the commonest
    /// answer on this cartridge and the one that makes the move do the least. A default that
    /// flattered the move would be a default nobody noticed was wrong.
    /// </summary>
    [Fact]
    public void AndABattleToldNothingSaysNormal()
    {
        // Water on both halves, because a creature whose second type is already Normal has
        // nothing to become and the move correctly does nothing — which is a right answer to
        // a question this test was not asking, and is how the first version of it failed.
        Battler you = Make(Species(PokemonType.Water, PokemonType.Water), Move(Camouflage));
        Battler them = Make(Species(), Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.Equal(PokemonType.Normal, you.Type1);
    }

    /// <summary>And somebody already made of it has nothing to become.</summary>
    [Fact]
    public void AndSomebodyAlreadyMadeOfItHasNothingToBecome()
    {
        Battler you = Make(Species(PokemonType.Rock, PokemonType.Rock), Move(Camouflage));
        Battler them = Make(Species(), Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, them, 7) { Ground = PokemonType.Rock }),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    // ---- the move whose own type moves ----------------------------------------------------------

    /// <summary>
    /// The sky decides what it is. Each of the four weathers lends the type that causes it in
    /// this game, which is a reading of the game's own arrangement rather than an opinion
    /// about weather.
    /// </summary>
    [Theory]
    [InlineData(Weather.None, PokemonType.Normal)]
    [InlineData(Weather.Rain, PokemonType.Water)]
    [InlineData(Weather.Sun, PokemonType.Fire)]
    [InlineData(Weather.Sandstorm, PokemonType.Rock)]
    [InlineData(Weather.Hail, PokemonType.Ice)]
    public void TheSkyDecidesWhatItIs(Weather sky, PokemonType becomes)
    {
        Battler you = Make(Species(), Move(WeatherBall, 50));

        Assert.Equal(becomes, MovePower.TypeOf(you.MoveAt(0)!, you, sky));
    }

    /// <summary>
    /// And it answers even under a clear sky rather than falling through, because "the sky
    /// decided Normal" and "nobody asked" are different claims and only one is true.
    /// </summary>
    [Fact]
    public void AndItAnswersEvenUnderAClearSky()
    {
        Battler you = Make(Species(), Move(WeatherBall, 50));

        Assert.NotNull(MovePower.TypeOf(you.MoveAt(0)!, you, Weather.None));

        // While an ordinary move still answers nothing at all, which is what makes the line
        // above a claim rather than a tautology.
        Assert.Null(MovePower.TypeOf(Move(0x00, 50), you, Weather.Rain));
    }

    /// <summary>And it is twice as hard under any sky, and its own power under none.</summary>
    [Fact]
    public void AndItIsTwiceAsHardUnderAnySky()
    {
        Battler you = Make(Species(), Move(WeatherBall, 50));
        Battler them = Make(Species(), Move(0x00, 0));

        MoveData ball = you.MoveAt(0)!;

        Assert.Null(MovePower.Of(ball, you, them, Weather.None));
        Assert.Equal(100, MovePower.Of(ball, you, them, Weather.Rain));
        Assert.Equal(100, MovePower.Of(ball, you, them, Weather.Hail));
    }

    /// <summary>
    /// And the sky reaches the damage sum, which is the part that could be right in
    /// isolation and wrong in the engine.
    /// </summary>
    [Fact]
    public void AndAllOfThatReachesTheDamage()
    {
        Battler you = Make(Species(), Move(WeatherBall, 50));

        // A Water defender, so a Water ball is resisted and a Normal one is not. The type
        // changing is therefore visible in the number rather than only in a property.
        Battler them = Make(Species(PokemonType.Water, PokemonType.Water), Move(0x00, 0));

        MoveData ball = you.MoveAt(0)!;

        DamageResult clear = DamageCalculator.Calculate(you, them, ball, false, 100);
        DamageResult rain = DamageCalculator.Calculate(you, them, ball, false, 100, Weather.Rain);

        // The effectiveness rather than the damage, and that is the point of this test
        // rather than a detail of it. Comparing the two damage numbers passes whether or not
        // the type reaches the sum, because the power doubles under rain either way — which
        // is exactly how the first version of this agreed with the code being broken.
        // Through the result's own words rather than against TypeChart.Neutral, which is on
        // a different scale — the chart counts in tenths and a result counts in hundredths.
        // Comparing the two directly is a mistake that reads perfectly and fails with
        // "expected 10, actual 100", and it is one this file made on the way here.
        Assert.False(clear.NotVeryEffective);
        Assert.False(clear.SuperEffective);

        Assert.True(rain.NotVeryEffective, "the ball did not come out as Water against a Water defender");

        Assert.True(clear.Damage > 0 && rain.Damage > 0);
    }
}
