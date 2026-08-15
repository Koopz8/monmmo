using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Touching somebody, and what five abilities do about it.
/// <para>
/// Bit nought of the flags byte, which has been on every move record this project has ever
/// parsed and had never once been looked at. What it means was worked out from its
/// membership rather than remembered, and the membership is unusually decisive: 111 moves
/// carry it on a real image and <b>every single one of them deals damage</b>. Not one
/// status move has it.
/// </para>
/// <para>
/// The corroboration is the two dozen that sit on the special side of this generation's
/// type-based split — FIRE PUNCH, VINE WHIP, BITE, CRUNCH, DRAGON CLAW, LEAF BLADE. Punches,
/// kicks, bites and whips with an elemental type on them, and no other reading of that byte
/// would collect exactly those. FLAMETHROWER, SURF, PSYCHIC and EARTHQUAKE — things that
/// arrive from somewhere else — do not have it.
/// </para>
/// </summary>
public class ContactTests
{
    private static SpeciesData Species(int ability = 0, PokemonType type = PokemonType.Normal) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = type, Type2 = type,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
    };

    /// <summary>A record with the flags byte set the way the cartridge sets it.</summary>
    private static MoveData Move(bool contact, int power = 40)
    {
        var record = new byte[MoveData.SizeBytes];

        record[1] = (byte)power;
        record[2] = (byte)PokemonType.Normal;
        record[3] = 100;
        record[4] = 20;
        record[8] = (byte)(contact ? 1 : 0);

        return MoveData.Parse(record, 1, string.Empty);
    }

    private static Battle Fight(SpeciesData mine, SpeciesData theirs, MoveData move)
    {
        Battler you = new Battler(mine, 50).Knowing(move);
        Battler them = new Battler(theirs, 50).Knowing(move);

        return new Battle(you, them, 7);
    }

    /// <summary>The bit is read off the byte, and off no other byte.</summary>
    [Fact]
    public void TheFlagIsReadOffTheRecord()
    {
        Assert.True(Move(contact: true).MakesContact);
        Assert.False(Move(contact: false).MakesContact);
    }

    /// <summary>
    /// ROUGH SKIN, the simplest of the five: touching it costs you, whatever you are made
    /// of and whatever it did to you.
    /// </summary>
    [Fact]
    public void TouchingRoughSkinCosts()
    {
        Battle battle = Fight(Species(), Species(Abilities.RoughSkin), Move(contact: true));

        int before = battle.Player.CurrentHp;

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        BattleEvent.Grazed grazed = Assert.Single(events.OfType<BattleEvent.Grazed>());

        Assert.Equal(Side.Player, grazed.Side);
        Assert.True(battle.Player.CurrentHp < before);
    }

    /// <summary>And reaching them from a distance costs nothing.</summary>
    [Fact]
    public void AndReachingThemFromADistanceDoesNot()
    {
        Battle battle = Fight(Species(), Species(Abilities.RoughSkin), Move(contact: false));

        Assert.Empty(
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0))
                .OfType<BattleEvent.Grazed>());
    }

    /// <summary>
    /// The three that hand a condition back. Rolled rather than certain, so this asks
    /// across many fights whether it ever happens rather than whether it happened once.
    /// </summary>
    [Theory]
    [InlineData(Abilities.Static, StatusCondition.Paralysis)]
    [InlineData(Abilities.PoisonPoint, StatusCondition.Poison)]
    [InlineData(Abilities.FlameBody, StatusCondition.Burn)]
    public void TouchingOneOfTheThreeMayCatchSomething(int ability, StatusCondition caught)
    {
        var seen = new List<StatusCondition>();

        for (int seed = 0; seed < 40; seed++)
        {
            Battler you = new Battler(Species(), 50).Knowing(Move(contact: true));
            Battler them = new Battler(Species(ability), 50).Knowing(Move(contact: true));

            var battle = new Battle(you, them, (uint)seed);

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            if (you.Status != StatusCondition.None) seen.Add(you.Status);
        }

        Assert.NotEmpty(seen);
        Assert.All(seen, s => Assert.Equal(caught, s));
    }

    /// <summary>And never from a move that does not reach them.</summary>
    [Theory]
    [InlineData(Abilities.Static)]
    [InlineData(Abilities.PoisonPoint)]
    [InlineData(Abilities.FlameBody)]
    [InlineData(Abilities.EffectSpore)]
    public void AndNeverFromOneThatDoesNot(int ability)
    {
        for (int seed = 0; seed < 40; seed++)
        {
            Battler you = new Battler(Species(), 50).Knowing(Move(contact: false));
            Battler them = new Battler(Species(ability), 50).Knowing(Move(contact: false));

            var battle = new Battle(you, them, (uint)seed);

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            Assert.Equal(StatusCondition.None, you.Status);
        }
    }

    /// <summary>EFFECT SPORE, which has three things it might do and does one of them.</summary>
    [Fact]
    public void EffectSporeHandsOverOneOfThree()
    {
        var seen = new HashSet<StatusCondition>();

        for (int seed = 0; seed < 200; seed++)
        {
            Battler you = new Battler(Species(), 50).Knowing(Move(contact: true));
            Battler them = new Battler(Species(Abilities.EffectSpore), 50).Knowing(Move(contact: true));

            var battle = new Battle(you, them, (uint)seed);

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            if (you.Status != StatusCondition.None) seen.Add(you.Status);
        }

        Assert.NotEmpty(seen);

        Assert.All(
            seen,
            s => Assert.Contains(s, new[] { StatusCondition.Poison, StatusCondition.Paralysis, StatusCondition.Sleep }));
    }

    /// <summary>
    /// And the attacker's own ability still gets its say. TryApplyStatus is the one door a
    /// condition goes through, so LIMBER refuses a STATIC without either of them knowing
    /// about the other.
    /// </summary>
    [Fact]
    public void AndTheToucherKeepsItsOwnRefusals()
    {
        for (int seed = 0; seed < 60; seed++)
        {
            Battler you = new Battler(Species(Abilities.Limber), 50).Knowing(Move(contact: true));
            Battler them = new Battler(Species(Abilities.Static), 50).Knowing(Move(contact: true));

            var battle = new Battle(you, them, (uint)seed);

            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            Assert.NotEqual(StatusCondition.Paralysis, you.Status);
        }
    }

    /// <summary>
    /// One answer per move, not one per hit. A move that lands five times is one act of
    /// touching somebody, and a five-times-more-dangerous DOUBLESLAP is not what this rule
    /// says.
    /// </summary>
    [Fact]
    public void AndItAnswersOncePerMoveRatherThanPerHit()
    {
        Battle battle = Fight(Species(), Species(Abilities.RoughSkin), Move(contact: true));

        Assert.Single(
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0))
                .OfType<BattleEvent.Grazed>()
                .Where(g => g.Side == Side.Player));
    }
}
