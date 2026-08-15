using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Not being allowed to leave.
/// <para>
/// The only rule in this project where a creature's ability decides something about
/// somebody <em>else's</em> options rather than about what happens to it. Everything else
/// an ability does is a fact about its owner; these three are a fact about the person
/// standing opposite.
/// </para>
/// <para>
/// They turned up a gap rather than needing one filled. This engine has blocked running
/// away since WRAP existed and has never once blocked switching — and a rule that stops you
/// fleeing but not swapping is a rule about half of leaving.
/// </para>
/// </summary>
public class TrappedTests
{
    /// <summary>
    /// Speed matters here and is worth setting on purpose. Getting away is a roll against
    /// how much faster you are, so a fixture where both sides are equally quick makes
    /// "did they escape" a coin flip — which the first draft of these tests did, and which
    /// proves nothing either way.
    /// </summary>
    private static SpeciesData Species(
        int ability = 0, PokemonType first = PokemonType.Normal, PokemonType? second = null, int speed = 200) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = (byte)speed, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = first, Type2 = second ?? first,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
    };

    private static MoveData Nothing() =>
        new(1, string.Empty, 0, 40, PokemonType.Normal, 100, 20, 0, 0, 0);

    private static Battle Wild(SpeciesData mine, SpeciesData theirs)
    {
        Battler you = new Battler(mine, 50).Knowing(Nothing());
        Battler them = new Battler(theirs, 50).Knowing(Nothing());

        return new Battle(you, them, 7) { IsWild = true };
    }

    // ---- who holds whom -------------------------------------------------------------------

    /// <summary>SHADOW TAG holds anybody at all.</summary>
    [Fact]
    public void ShadowTagHoldsAnybody()
    {
        foreach (PokemonType type in Enum.GetValues<PokemonType>())
            Assert.True(Abilities.Traps(Abilities.ShadowTag, type, type, 0));
    }

    /// <summary>
    /// ARENA TRAP holds whoever is standing on the ground, which is why it asks about
    /// Flying and about LEVITATE — the two ways of not being on it.
    /// </summary>
    [Fact]
    public void ArenaTrapHoldsWhoeverIsOnTheGround()
    {
        Assert.True(Abilities.Traps(Abilities.ArenaTrap, PokemonType.Normal, PokemonType.Normal, 0));

        Assert.False(Abilities.Traps(Abilities.ArenaTrap, PokemonType.Flying, PokemonType.Normal, 0));
        Assert.False(Abilities.Traps(Abilities.ArenaTrap, PokemonType.Normal, PokemonType.Flying, 0));

        Assert.False(
            Abilities.Traps(Abilities.ArenaTrap, PokemonType.Normal, PokemonType.Normal, Abilities.Levitate));
    }

    /// <summary>And MAGNET PULL holds only what it can stick to.</summary>
    [Fact]
    public void MagnetPullHoldsOnlySteel()
    {
        Assert.True(Abilities.Traps(Abilities.MagnetPull, PokemonType.Steel, PokemonType.Rock, 0));
        Assert.True(Abilities.Traps(Abilities.MagnetPull, PokemonType.Rock, PokemonType.Steel, 0));

        Assert.False(Abilities.Traps(Abilities.MagnetPull, PokemonType.Rock, PokemonType.Rock, 0));
    }

    // ---- and what that means -------------------------------------------------------------

    [Fact]
    public void SomebodyHeldCannotRunAway()
    {
        Battle battle = Wild(Species(), Species(Abilities.ShadowTag, speed: 5));

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.RunAway(), new BattleAction.UseMove(0));

        Assert.False(battle.Escaped);
        Assert.Single(events.OfType<BattleEvent.CouldNotGetAway>());
    }

    /// <summary>And somebody nobody is holding gets away, or the test above proves nothing.</summary>
    [Fact]
    public void AndSomebodyFreeGetsAway()
    {
        // Far faster than what it is running from, so the roll cannot be the reason.
        Battle battle = Wild(Species(), Species(speed: 5));

        battle.ResolveTurn(new BattleAction.RunAway(), new BattleAction.UseMove(0));

        Assert.True(battle.Escaped);
    }

    /// <summary>
    /// The gap this work turned up. Leaving happens two ways and the engine only ever knew
    /// about one of them, so the rule is askable from outside — the server does the
    /// switching, by building a new battle around somebody else.
    /// </summary>
    [Fact]
    public void AndCannotSwitchEither()
    {
        Battle held = Wild(Species(), Species(Abilities.ShadowTag));

        Assert.True(held.MayNotLeave(Side.Player));

        Battle free = Wild(Species(), Species());

        Assert.False(free.MayNotLeave(Side.Player));
    }

    /// <summary>And a hold from a move counts as much as a hold from an ability.</summary>
    [Fact]
    public void AndAHoldFromAMoveCountsToo()
    {
        Battle battle = Wild(Species(), Species());

        Assert.False(battle.MayNotLeave(Side.Player));

        battle.Player.CannotEscape = true;

        Assert.True(battle.MayNotLeave(Side.Player));
    }

    /// <summary>
    /// SUCTION CUPS, which is the same idea from the other end: being blown off the field is
    /// a way of being made to leave, and this is the ability that refuses to be.
    /// </summary>
    [Fact]
    public void SuctionCupsWillNotBeBlownAway()
    {
        int blowAway = Enumerable.Range(0, 256)
            .First(e => MoveEffects.Of((byte)e).Kind == EffectKind.BlowAway);

        MoveData roar = new(1, string.Empty, (byte)blowAway, 0, PokemonType.Normal, 100, 20, 0, 0, 0);

        // Only one side roars. Escaped is a fact about the battle rather than about a
        // side, so a fixture where both of them roar cannot tell whose blowing away set
        // it — which the first draft did, and it read as SUCTION CUPS failing.
        Battler you = new Battler(Species(), 50).Knowing(roar, Nothing());
        Battler them = new Battler(Species(Abilities.SuctionCups), 50).Knowing(roar, Nothing());

        var battle = new Battle(you, them, 7) { IsWild = true };

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.False(battle.Escaped);

        // And somebody without it does go.
        Battler mine = new Battler(Species(), 50).Knowing(roar, Nothing());
        Battler theirs = new Battler(Species(), 50).Knowing(roar, Nothing());

        var other = new Battle(mine, theirs, 7) { IsWild = true };

        other.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.True(other.Escaped);
    }

    /// <summary>
    /// And an ability holds the other side rather than its own. The easy way to write this
    /// wrong is to ask the leaver's ability instead of the one opposite.
    /// </summary>
    [Fact]
    public void AndItIsTheOtherSideThatHolds()
    {
        Battle mine = Wild(Species(Abilities.ShadowTag), Species());

        // Mine has the ability, so mine is not the one being held.
        Assert.False(mine.MayNotLeave(Side.Player));
        Assert.True(mine.MayNotLeave(Side.Opponent));
    }
}
