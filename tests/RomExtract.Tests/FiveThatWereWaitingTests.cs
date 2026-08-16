using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Five abilities that were waiting for something, and got it from somewhere else.
/// <para>
/// Three wanted a creature whose type or ability a fight can change. Two wanted somewhere to
/// happen at the end of a turn. Neither of those was built for an ability: the first arrived
/// with the moves that move a type and an ability, and the second arrived with the berries.
/// All five are old hooks with a new caller.
/// </para>
/// </summary>
public class FiveThatWereWaitingTests
{
    private static SpeciesData Species(
        int ability = 0, int speed = 60,
        PokemonType first = PokemonType.Normal, PokemonType second = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = first, Type2 = second,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability, Ability2 = (byte)ability,
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
    [InlineData(Abilities.Trace)]
    [InlineData(Abilities.ColorChange)]
    [InlineData(Abilities.Forecast)]
    [InlineData(Abilities.ShedSkin)]
    [InlineData(Abilities.SpeedBoost)]
    public void NoneOfThemIsSilent(int ability) => Assert.True(Abilities.DoesSomething(ability));

    // ---- taking the ability opposite --------------------------------------------------

    [Fact]
    public void ItTakesTheAbilityOpposite()
    {
        Battler you = Make(Species(Abilities.Trace), Move(0x00, 0));
        Battler them = Make(Species(Abilities.Guts), Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.Arrival(Side.Player);

        Assert.Equal(Abilities.Guts, you.Ability);
        Assert.Contains(events, e => e is BattleEvent.AbilityMoved { Side: Side.Player });

        // And theirs is untouched — it is a copy rather than a theft.
        Assert.Equal(Abilities.Guts, them.Ability);
    }

    /// <summary>And it takes nothing from somebody with nothing.</summary>
    [Fact]
    public void AndTakesNothingFromSomebodyWithNothing()
    {
        Battler you = Make(Species(Abilities.Trace), Move(0x00, 0));
        Battler them = Make(Species(), Move(0x00, 0));

        new Battle(you, them, 7).Arrival(Side.Player);

        Assert.Equal(Abilities.Trace, you.Ability);
    }

    /// <summary>
    /// And two of them do not copy each other. Without that rule they would be two creatures
    /// each holding a copy of the other's copy, which is not an ability anybody has.
    /// </summary>
    [Fact]
    public void AndTwoOfThemDoNotCopyEachOther()
    {
        Battler you = Make(Species(Abilities.Trace), Move(0x00, 0));
        Battler them = Make(Species(Abilities.Trace), Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        battle.Arrival(Side.Player);
        battle.Arrival(Side.Opponent);

        Assert.Equal(Abilities.Trace, you.Ability);
        Assert.Equal(Abilities.Trace, them.Ability);
    }

    // ---- becoming what hit it -----------------------------------------------------------

    /// <summary>
    /// It becomes the type of whatever just hit it — the only ability in the game whose owner
    /// is a different creature after every exchange.
    /// </summary>
    [Fact]
    public void ItBecomesTheTypeOfWhateverHitIt()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 40, PokemonType.Fire));
        Battler them = Make(Species(Abilities.ColorChange), Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(PokemonType.Fire, them.Type1);
        Assert.Equal(PokemonType.Fire, them.Type2);

        Assert.Contains(events, e => e is BattleEvent.ChangedType { Side: Side.Opponent });
    }

    /// <summary>And being hit by what it already is changes nothing and says nothing.</summary>
    [Fact]
    public void AndBeingHitByWhatItAlreadyIsChangesNothing()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 40, PokemonType.Fire));

        Battler them = Make(
            Species(Abilities.ColorChange, first: PokemonType.Fire, second: PokemonType.Fire),
            Move(0x00, 0));

        Assert.DoesNotContain(Turn(new Battle(you, them, 7)), e => e is BattleEvent.ChangedType);
    }

    /// <summary>
    /// And what a stand-in absorbed does not change it, because the hit never reached the
    /// creature.
    /// </summary>
    [Fact]
    public void AndWhatAStandInAbsorbedDoesNotChangeIt()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 40, PokemonType.Fire));
        Battler them = Make(Species(Abilities.ColorChange), Move(0x00, 0));

        them.StandInHp = 500;

        Turn(new Battle(you, them, 7));

        Assert.Null(them.BorrowedType);
    }

    // ---- following the sky ------------------------------------------------------------------

    /// <summary>
    /// Its type is whatever the sky is, settled the moment the weather changes.
    /// </summary>
    [Theory]
    [InlineData(Weather.Rain, PokemonType.Water)]
    [InlineData(Weather.Sun, PokemonType.Fire)]
    [InlineData(Weather.Sandstorm, PokemonType.Rock)]
    [InlineData(Weather.Hail, PokemonType.Ice)]
    public void ItsTypeFollowsTheSky(Weather sky, PokemonType becomes)
    {
        Battler you = Make(Species(speed: 200), Move(Bringing(sky)));
        Battler them = Make(Species(Abilities.Forecast), Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.Equal(becomes, them.Type1);
    }

    /// <summary>
    /// And walking in under a sky settles it too. Arriving is not a change of weather, and an
    /// ability that only listened for changes would be wrong for the whole of the first turn.
    /// </summary>
    [Fact]
    public void AndWalkingInUnderASkySettlesItToo()
    {
        Battler you = Make(Species(speed: 200), Move(Bringing(Weather.Rain)));
        Battler them = Make(Species(), Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Battler arriving = Make(Species(Abilities.Forecast), Move(0x00, 0));

        // The same battle, with somebody new on that side — which is what an arrival is.
        Battler swapped = arriving;

        var second = new Battle(you, swapped, 7);

        second.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(PokemonType.Water, swapped.Type1);
    }

    /// <summary>
    /// And it goes back to what it was born as when the sky clears — by forgetting the
    /// borrowed type rather than by writing its birth type in, which look the same and are
    /// not.
    /// </summary>
    [Fact]
    public void AndItGoesBackWhenTheSkyClears()
    {
        Battler you = Make(Species(speed: 200), Move(Bringing(Weather.Rain)));
        Battler them = Make(Species(Abilities.Forecast, first: PokemonType.Psychic, second: PokemonType.Psychic), Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Equal(PokemonType.Water, them.Type1);

        // Long enough for the sky to run out.
        for (int turn = 0; turn < 12 && battle.Sky != Weather.None; turn++)
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(Weather.None, battle.Sky);
        Assert.Null(them.BorrowedType);
        Assert.Equal(PokemonType.Psychic, them.Type1);
    }

    // ---- shedding, and getting faster ------------------------------------------------------------

    /// <summary>
    /// It sheds whatever ails it, eventually — a chance a turn rather than a certainty, so
    /// this asks over enough turns that never shedding would be a rule rather than luck.
    /// </summary>
    [Fact]
    public void ItShedsWhateverAilsIt()
    {
        var shed = 0;

        for (uint seed = 1; seed < 30; seed++)
        {
            Battler you = Make(Species(speed: 200), Move(0x00, 0));
            Battler them = Make(Species(Abilities.ShedSkin), Move(0x00, 0));

            them.Status = StatusCondition.Poison;

            var battle = new Battle(you, them, seed);

            for (int turn = 0; turn < 8 && them.Status != StatusCondition.None; turn++) Turn(battle);

            if (them.Status == StatusCondition.None) shed++;
        }

        Assert.True(shed > 0, "it never shed anything in thirty fights of eight turns");
    }

    /// <summary>And somebody without it stays poisoned, which is what makes that a claim.</summary>
    [Fact]
    public void AndSomebodyWithoutItStaysAiling()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 0));
        Battler them = Make(Species(), Move(0x00, 0));

        them.Status = StatusCondition.Poison;

        var battle = new Battle(you, them, 7);

        for (int turn = 0; turn < 8; turn++) Turn(battle);

        Assert.Equal(StatusCondition.Poison, them.Status);
    }

    /// <summary>It gets faster at the end of every turn, for as long as it is standing there.</summary>
    [Fact]
    public void ItGetsFasterEveryTurn()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 0));
        Battler them = Make(Species(Abilities.SpeedBoost), Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Equal(1, them.StageOf(Stat.Speed));

        Turn(battle);

        Assert.Equal(2, them.StageOf(Stat.Speed));
    }

    /// <summary>And somebody without it does not.</summary>
    [Fact]
    public void AndSomebodyWithoutItDoesNot()
    {
        Battler you = Make(Species(speed: 200), Move(0x00, 0));
        Battler them = Make(Species(), Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.Equal(0, them.StageOf(Stat.Speed));
    }

    /// <summary>The move that brings a given sky, taken from the effect table rather than named.</summary>
    private static byte Bringing(Weather sky)
    {
        for (int effect = 0; effect <= byte.MaxValue; effect++)
        {
            if (Skies.Of(effect) == sky) return (byte)effect;
        }

        throw new InvalidOperationException($"nothing on this cartridge brings {sky}");
    }
}
