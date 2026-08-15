using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The sky, which is the first state in this engine that belongs to the battle rather than
/// to either side of it.
/// <para>
/// Everything else that lasts turns — a trap, a confusion, a disabled move — hangs off one
/// battler, and there was nowhere for a fact about the room to live. That is why ten
/// abilities have been sitting named and silent: six of them only ever read this, and three
/// more only ever set it.
/// </para>
/// <para>
/// What is <b>read</b>: which moves cause it. Each of the four is a group of exactly one in
/// the cartridge's own effect table, checked against the real image before the numbers were
/// written down. What is <b>modelled</b>: five turns, a sixteenth, and every multiplier.
/// </para>
/// </summary>
public class WeatherTests
{
    private static SpeciesData Species(
        PokemonType first = PokemonType.Normal, PokemonType? second = null, int ability = 0, int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 100, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = (byte)speed, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = first,
        Type2 = second ?? first,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
    };

    private static MoveData Move(int effect, PokemonType type = PokemonType.Normal, int power = 0, int accuracy = 100) =>
        new(1, string.Empty, (byte)effect, (byte)power, type, (byte)accuracy, 20, 0, 0, 0);

    /// <summary>The four, by the effect ids read off the real cartridge.</summary>
    private static MoveData Bringing(Weather weather) => weather switch
    {
        Weather.Rain => Move(Skies.RainDance, PokemonType.Water),
        Weather.Sun => Move(Skies.SunnyDay, PokemonType.Fire),
        Weather.Sandstorm => Move(Skies.Sandstorm, PokemonType.Rock),
        _ => Move(Skies.Hail, PokemonType.Ice),
    };

    private static Battle Fight(SpeciesData mine, SpeciesData theirs, params MoveData[] moves)
    {
        Battler you = new Battler(mine, 50).Knowing(moves);
        Battler them = new Battler(theirs, 50).Knowing(moves);

        return new Battle(you, them, 7);
    }

    // ---- the four moves -----------------------------------------------------------------

    [Theory]
    [InlineData(Weather.Rain)]
    [InlineData(Weather.Sun)]
    [InlineData(Weather.Sandstorm)]
    [InlineData(Weather.Hail)]
    public void EachOfTheFourMovesBringsItsOwnSky(Weather weather)
    {
        // Rock, so a sandstorm does not quietly finish somebody mid-test.
        Battle battle = Fight(
            Species(PokemonType.Rock), Species(PokemonType.Rock), Bringing(weather));

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(weather, battle.Sky);

        Assert.Contains(
            events.OfType<BattleEvent.WeatherBegan>(), e => e.Weather == weather);
    }

    /// <summary>And it runs out, saying so rather than simply stopping.</summary>
    [Fact]
    public void AndItRunsOut()
    {
        Battle battle = Fight(
            Species(PokemonType.Rock), Species(PokemonType.Rock),
            Bringing(Weather.Rain), Move(0, PokemonType.Normal, power: 1));

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Equal(Skies.Turns - 1, battle.SkyTurns);

        var ended = new List<BattleEvent.WeatherEnded>();

        for (int turn = 0; turn < Skies.Turns; turn++)
        {
            ended.AddRange(battle
                .ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(1))
                .OfType<BattleEvent.WeatherEnded>());
        }

        Assert.Equal(Weather.None, battle.Sky);
        Assert.Single(ended);
    }

    /// <summary>Asking for weather that is already here does nothing, and says so.</summary>
    [Fact]
    public void AndAskingForItTwiceDoesNothing()
    {
        Battle battle = Fight(
            Species(PokemonType.Rock), Species(PokemonType.Rock), Bringing(Weather.Rain));

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        List<BattleEvent> again = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.NotEmpty(again.OfType<BattleEvent.NothingHappened>());
    }

    // ---- what it does to damage ---------------------------------------------------------

    [Theory]
    [InlineData(Weather.Rain, PokemonType.Water, 150)]
    [InlineData(Weather.Rain, PokemonType.Fire, 50)]
    [InlineData(Weather.Sun, PokemonType.Fire, 150)]
    [InlineData(Weather.Sun, PokemonType.Water, 50)]
    [InlineData(Weather.Sandstorm, PokemonType.Water, 100)]
    [InlineData(Weather.None, PokemonType.Fire, 100)]
    public void TheSkyMakesOneTypeAndUnmakesTheOther(Weather weather, PokemonType type, int hundredths)
    {
        var attacker = new Battler(Species(), 50);
        var defender = new Battler(Species(), 50);

        MoveData move = Move(0, type, power: 80);

        int plain = DamageCalculator.Calculate(attacker, defender, move, false, 100).Damage;
        int under = DamageCalculator.Calculate(attacker, defender, move, false, 100, weather).Damage;

        Assert.Equal(plain * hundredths / 100, under);
    }

    // ---- what it takes off you ----------------------------------------------------------

    [Fact]
    public void ASandstormBitesEverybodyItDoesNotSuit()
    {
        Battle battle = Fight(
            Species(PokemonType.Normal), Species(PokemonType.Rock), Bringing(Weather.Sandstorm));

        int mine = battle.Player.CurrentHp;
        int theirs = battle.Opponent.CurrentHp;

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        // The Normal one is buffeted; the Rock one is at home in it.
        Assert.True(battle.Player.CurrentHp < mine);
        Assert.Equal(theirs, battle.Opponent.CurrentHp);

        BattleEvent.WeatherHurt hurt = Assert.Single(events.OfType<BattleEvent.WeatherHurt>());

        Assert.Equal(Side.Player, hurt.Side);
        Assert.Equal(Weather.Sandstorm, hurt.Weather);
    }

    [Theory]
    [InlineData(Weather.Sandstorm, PokemonType.Ground, false)]
    [InlineData(Weather.Sandstorm, PokemonType.Steel, false)]
    [InlineData(Weather.Sandstorm, PokemonType.Ice, true)]
    [InlineData(Weather.Hail, PokemonType.Ice, false)]
    [InlineData(Weather.Hail, PokemonType.Rock, true)]
    public void AndWhoItLeavesAlone(Weather weather, PokemonType type, bool bitten)
    {
        Battle battle = Fight(Species(type), Species(PokemonType.Rock), Bringing(weather));

        int before = battle.Player.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(bitten, battle.Player.CurrentHp < before);
    }

    /// <summary>SAND VEIL, which is at home in a sandstorm whatever it is made of.</summary>
    [Fact]
    public void SandVeilIsLeftAlone()
    {
        Battle battle = Fight(
            Species(PokemonType.Normal, ability: Abilities.SandVeil),
            Species(PokemonType.Rock),
            Bringing(Weather.Sandstorm));

        int before = battle.Player.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(before, battle.Player.CurrentHp);
    }

    /// <summary>And RAIN DISH, which is the one the weather is kind to.</summary>
    [Fact]
    public void RainDishDrinksItIn()
    {
        Battle battle = Fight(
            Species(PokemonType.Normal, ability: Abilities.RainDish),
            Species(PokemonType.Rock),
            Bringing(Weather.Rain));

        battle.Player.TakeDamage(battle.Player.MaxHp / 2);

        int hurt = battle.Player.CurrentHp;

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(battle.Player.CurrentHp > hurt);
        Assert.Single(events.OfType<BattleEvent.WeatherHealed>());
    }

    // ---- what it does to who goes first --------------------------------------------------

    [Theory]
    [InlineData(Abilities.SwiftSwim, Weather.Rain)]
    [InlineData(Abilities.Chlorophyll, Weather.Sun)]
    public void TheSpeedAbilitiesChangeWhoGoesFirst(int ability, Weather weather)
    {
        // Slower on paper, and only the sky makes up the difference.
        SpeciesData quick = Species(PokemonType.Rock, ability: ability, speed: 60);
        SpeciesData quicker = Species(PokemonType.Rock, speed: 90);

        Battle battle = Fight(quick, quicker, Bringing(weather), Move(0, PokemonType.Normal, power: 1));

        Assert.Equal(100, Abilities.Speed(ability, Weather.None));
        Assert.Equal(200, Abilities.Speed(ability, weather));

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Equal(weather, battle.Sky);

        // Under that sky the slower one is now the faster one.
        Assert.True(
            battle.Player.EffectiveStat(Stat.Speed) * Abilities.Speed(ability, battle.Overhead) / 100
            > battle.Opponent.EffectiveStat(Stat.Speed));
    }

    // ---- and the two that switch it off --------------------------------------------------

    [Theory]
    [InlineData(Abilities.CloudNine)]
    [InlineData(Abilities.AirLock)]
    public void CloudNineAndAirLockSwitchTheSkyOffForEverybody(int ability)
    {
        Battle battle = Fight(
            Species(PokemonType.Normal, ability: ability),
            Species(PokemonType.Normal),
            Bringing(Weather.Sandstorm));

        int mine = battle.Player.CurrentHp;
        int theirs = battle.Opponent.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        // The sandstorm is there, and nobody can feel it — including the far side, who has
        // no such ability of their own.
        Assert.Equal(Weather.Sandstorm, battle.Sky);
        Assert.Equal(Weather.None, battle.Overhead);

        Assert.Equal(mine, battle.Player.CurrentHp);
        Assert.Equal(theirs, battle.Opponent.CurrentHp);
    }

    /// <summary>
    /// And the countdown runs underneath it. An AIR LOCK does not make weather last longer
    /// — it makes nobody notice it, and what is left of the five turns is still there.
    /// </summary>
    [Fact]
    public void AndTheCountdownRunsUnderneathIt()
    {
        Battle battle = Fight(
            Species(PokemonType.Normal, ability: Abilities.AirLock),
            Species(PokemonType.Normal),
            Bringing(Weather.Rain),
            Move(0, PokemonType.Normal, power: 1));

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Equal(Skies.Turns - 1, battle.SkyTurns);
    }

    // ---- the one move the sky overrules ---------------------------------------------------

    [Fact]
    public void ThunderCannotMissInRainAndOftenDoesInSun()
    {
        Assert.Equal(101, Skies.Accuracy(Weather.Rain, Skies.Thunder));
        Assert.Equal(50, Skies.Accuracy(Weather.Sun, Skies.Thunder));

        // And it is a rule about that one move, not about a family.
        Assert.Null(Skies.Accuracy(Weather.Rain, 0));
        Assert.Null(Skies.Accuracy(Weather.None, Skies.Thunder));

        var attacker = new Battler(Species(), 50);
        var defender = new Battler(Species(), 50);

        MoveData thunder = Move(Skies.Thunder, PokemonType.Electric, power: 120, accuracy: 70);

        var rng = new BattleRng(3);

        // A hundred rolls in rain, and not one of them a miss.
        for (int roll = 0; roll < 100; roll++)
            Assert.True(DamageCalculator.RollAccuracy(rng, thunder, attacker, defender, Weather.Rain));
    }

    /// <summary>The four ids, which came off the real cartridge rather than out of memory.</summary>
    [Fact]
    public void TheFourEffectIdsAreWhatTheImageSays()
    {
        Assert.Equal(Weather.Sandstorm, Skies.Of(115));
        Assert.Equal(Weather.Rain, Skies.Of(136));
        Assert.Equal(Weather.Sun, Skies.Of(137));
        Assert.Equal(Weather.Hail, Skies.Of(164));

        Assert.Equal(Weather.None, Skies.Of(0));
        Assert.Equal(Weather.None, Skies.Of(Skies.Thunder));
    }
}
