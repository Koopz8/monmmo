using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Six groups whose answer depends on something other than the move.
/// <para>
/// Three heal by however much the sky allows. Three hit twice as hard against somebody who is
/// not standing where they were, or who cannot move properly. One takes uses off whatever the
/// other side last did.
/// </para>
/// <para>
/// Every one of them reads a state this engine already keeps — the weather, the away flag, a
/// condition, a slot's remaining uses — and turns it into a number. What the number is comes
/// from the game's code and is modelled; what it is read off is not.
/// </para>
/// </summary>
public class WhatTheSkyAndTheStateDecideTests
{
    private const byte MorningSun = 0x84;
    private const byte Synthesis = 0x85;
    private const byte Moonlight = 0x86;
    private const byte Gust = 0x95;
    private const byte Twister = 0x92;
    private const byte Earthquake = 0x93;
    private const byte SmellingSalt = 0xAB;
    private const byte Spite = 0x64;

    private static SpeciesData Species(int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 100, BaseDefense = 100,
        BaseSpeed = (byte)speed, BaseSpAttack = 100, BaseSpDefense = 100,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 40) =>
        new(1, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(params MoveData[] moves) => Make(60, moves);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(MorningSun)] [InlineData(Synthesis)] [InlineData(Moonlight)]
    [InlineData(Gust)] [InlineData(Twister)] [InlineData(Earthquake)]
    [InlineData(SmellingSalt)] [InlineData(Spite)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    /// <summary>
    /// All three heal, and all three are one group by behaviour and three by the record —
    /// which is why they are written as three rather than folded into one.
    /// </summary>
    [Theory]
    [InlineData(MorningSun)] [InlineData(Synthesis)] [InlineData(Moonlight)]
    public void EachOfTheThreePutsHealthBack(int effect)
    {
        Battler you = Make(Move((byte)effect, 0));
        Battler them = Make(Move(0x00, 0));

        you.TakeDamage(you.MaxHp * 3 / 4);

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.Recovered { Side: Side.Player });
        Assert.True(you.CurrentHp > before);
    }

    /// <summary>
    /// And the sky decides how much: more in sun than in nothing, and less in anything else
    /// that is happening.
    /// </summary>
    [Fact]
    public void TheSkyDecidesHowMuch()
    {
        // The sky is changed by using a move that changes it, rather than by setting a
        // field — there is no way to set one and there should not be, because a battle whose
        // weather could be assigned is a battle a client could assign it in.
        int InWeather(int bringing)
        {
            Battler you = Make(Move((byte)bringing, 0), Move(MorningSun, 0));
            Battler them = Make(Move(0x00, 0));

            var battle = new Battle(you, them, 7);

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            you.TakeDamage(you.CurrentHp - 1);

            List<BattleEvent> healing = battle.ResolveTurn(
                new BattleAction.UseMove(1), new BattleAction.UseMove(0));

            // What was put back, rather than what is left. A sandstorm takes a share at the
            // end of every turn, so the health afterwards is the heal minus the weather —
            // and the first version of this measured the weather and called it the heal.
            return healing.OfType<BattleEvent.Recovered>().Sum(e => e.Amount);
        }

        // Sun against a sandstorm, both reached the same way — one weather move, then the
        // heal. Comparing either against a clear sky would be comparing two fights of
        // different lengths, which is a difference this test is not about.
        int sunny = InWeather(Skies.SunnyDay);
        int sandy = InWeather(Skies.Sandstorm);

        Assert.True(sunny > sandy, $"{sunny} should beat {sandy}");
    }

    /// <summary>
    /// Three hit twice as hard against somebody who is not standing where they were.
    /// <para>
    /// This engine has one "away" state rather than one per move — a creature halfway through
    /// FLY and one halfway through DIG are both simply not there — so all three read the same
    /// flag. Worth naming rather than hiding: the cartridge distinguishes them and this does
    /// not.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Gust)] [InlineData(Twister)] [InlineData(Earthquake)]
    public void EachOfTheThreeDoublesAgainstSomebodyWhoIsNotThere(int effect)
    {
        MoveData move = Move((byte)effect, 40);

        Battler attacker = Make();

        Battler here = Make();
        Battler away = Make();

        away.IsAway = true;

        Assert.Null(MovePower.Of(move, attacker, here));
        Assert.Equal(80, MovePower.Of(move, attacker, away));
    }

    /// <summary>
    /// SMELLINGSALT hits harder for the same kind of reason and then undoes what it hit them
    /// for, which makes it the one move in this game worth less the second time.
    /// </summary>
    [Fact]
    public void RousingSomebodyHitsHarderAndThenUndoesTheReason()
    {
        MoveData salt = Move(SmellingSalt, 60);

        Battler attacker = Make();
        Battler stiff = Make();

        stiff.Status = StatusCondition.Paralysis;

        Assert.Equal(120, MovePower.Of(salt, attacker, stiff));

        Battler you = Make(salt);
        Battler them = Make(Move(0x00, 0));

        them.Status = StatusCondition.Paralysis;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.PutRight { Side: Side.Opponent });
        Assert.Equal(StatusCondition.None, them.Status);

        // And now it is an ordinary sixty, because the reason it was worth more is gone.
        Assert.Null(MovePower.Of(salt, attacker, them));
    }

    /// <summary>
    /// SPITE takes uses off whatever the other side last did — and needed nothing except PP,
    /// which this engine has spent since moves could run out.
    /// </summary>
    [Fact]
    public void SpiteTakesUsesOffWhateverTheyLastDid()
    {
        // The opponent is faster, so they have used something by the time this lands. A
        // coin flip for the order would make this a test that passes most of the time.
        Battler you = Make(Move(Spite, 0));
        Battler them = Make(200, Move(0x00, 10));

        var battle = new Battle(you, them, 7);

        int full = them.PpLeft(0);

        Turn(battle);

        // One for the move they used this turn, and four for the spite.
        Assert.Equal(full - 5, them.PpLeft(0));
    }

    [Fact]
    public void AndSaysNothingWhenTheyHaveNotDoneAnything()
    {
        Battler you = Make(200, Move(Spite, 0));
        Battler them = Make(Move(0x00, 10));

        // The user is faster this time, so nothing has been used by the time it lands and
        // there is no slot to take anything from.
        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
    }
}
