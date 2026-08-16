using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The three that were left because each needs machinery of its own.
/// <para>
/// Every other family on the list was a line pointing at something the engine already had.
/// These three are not: a thing standing in front of a creature that takes hits instead of
/// it, an accumulator that spends two turns collecting and then gives back double, and a
/// move that lands on a turn other than the one it was used on.
/// </para>
/// <para>
/// The last of those is the first thing in this engine that outlives the turn that made it
/// and belongs to neither creature. It is aimed at a <em>side</em>, and what is standing on
/// that side when it arrives is not its business — which is the whole reason anybody uses it.
/// </para>
/// </summary>
public class TheLargeThreeTests
{
    private const byte Substitute = 0x4F;
    private const byte Bide = 0x1A;
    private const byte FutureSight = 0x94;

    private static SpeciesData Species(int speed = 60, int hp = 200) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = (byte)hp, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0, int id = 1) =>
        new(id, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Theory]
    [InlineData(Substitute)] [InlineData(Bide)] [InlineData(FutureSight)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- the thing standing in front -------------------------------------------------

    /// <summary>It costs real health, paid now, out of its own.</summary>
    [Fact]
    public void PuttingSomethingUpCostsHealthNow()
    {
        Battler you = Make(200, Move(Substitute));
        Battler them = Make(10, Move(0x00, 0));

        int before = you.CurrentHp;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        BattleEvent.PutSomethingUp put =
            events.OfType<BattleEvent.PutSomethingUp>().Single();

        Assert.True(you.HasStandIn);
        Assert.Equal(before - put.Cost, you.CurrentHp);
        Assert.Equal(put.Cost, you.StandInHp);
    }

    /// <summary>And what it costs is what it can then take.</summary>
    [Fact]
    public void AndWhatItCostIsWhatItCanTake()
    {
        Battler you = Make(200, Move(Substitute));
        Battler them = Make(10, Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.Equal(you.MaxHp / 4, you.StandInHp);
    }

    /// <summary>
    /// A hit lands on it and not on the creature, which is the whole of the move.
    /// </summary>
    [Fact]
    public void AHitLandsOnItAndNotOnTheCreature()
    {
        Battler you = Make(200, Move(Substitute), Move(0x00, 0, id: 2));
        Battler them = Make(10, Move(0x00, 40, id: 3));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int afterPaying = you.CurrentHp;
        int standing = you.StandInHp;

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(afterPaying, you.CurrentHp);
        Assert.True(you.StandInHp < standing);
    }

    /// <summary>
    /// And the blow that breaks it is absorbed whole — the part that was more than it had
    /// left does not spill through.
    /// <para>
    /// This is the rule that separates a stand-in from a damage reduction, and getting it
    /// wrong would not look like a bug: it would look like the move being slightly weaker
    /// than it should be, for ever.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheBlowThatBreaksItIsAbsorbedWhole()
    {
        Battler you = Make(200, Move(Substitute), Move(0x00, 0, id: 2));

        // Harmless first, then enormous — many times what the stand-in can hold. Two moves
        // rather than one because a first version gave them the big one straight away, which
        // broke the stand-in on the turn it went up and left the second turn measuring an
        // ordinary hit on an ordinary creature.
        Battler them = Make(10, Move(0x00, 0, id: 3), Move(0x00, 250, id: 4));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int afterPaying = you.CurrentHp;

        List<BattleEvent> second =
            battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(1));

        Assert.Contains(second, e => e is BattleEvent.StandInTookIt { Broke: true });

        Assert.False(you.HasStandIn);
        Assert.Equal(afterPaying, you.CurrentHp);
        Assert.False(you.HasFainted);
    }

    /// <summary>And once it is gone the next hit reaches the creature.</summary>
    [Fact]
    public void AndOnceItIsGoneTheNextHitReaches()
    {
        Battler you = Make(200, Move(Substitute), Move(0x00, 0, id: 2));
        Battler them = Make(10, Move(0x00, 0, id: 3), Move(0x00, 250, id: 4));

        var battle = new Battle(you, them, 7);

        Turn(battle);
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(1));

        int standing = you.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(1));

        Assert.True(you.CurrentHp < standing);
    }

    /// <summary>
    /// A second one is refused while the first is up. A free reset would turn a costly move
    /// into an endless one.
    /// </summary>
    [Fact]
    public void AndASecondOneIsRefusedWhileTheFirstIsUp()
    {
        Battler you = Make(200, Move(Substitute));
        Battler them = Make(10, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int standing = you.StandInHp;

        Assert.Contains(
            Turn(battle),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });

        Assert.Equal(standing, you.StandInHp);
    }

    /// <summary>
    /// And somebody who cannot pay does not put one up. Paying for it with the last of your
    /// health would be a move that finishes you.
    /// </summary>
    [Fact]
    public void AndSomebodyWhoCannotPayDoesNotPutOneUp()
    {
        Battler you = Make(200, Move(Substitute));
        Battler them = Make(10, Move(0x00, 0));

        you.TakeDamage(you.CurrentHp - 2);

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });

        Assert.False(you.HasStandIn);
        Assert.False(you.HasFainted);
    }

    /// <summary>And it does not follow anybody out of the door.</summary>
    [Fact]
    public void AndItDoesNotFollowAnybodyOut()
    {
        Battler you = Make(60, Move(Substitute));

        you.StandInHp = 40;

        you.ForgetWhatWasStarted();

        Assert.False(you.HasStandIn);
    }

    // ---- gathering -----------------------------------------------------------------------

    /// <summary>
    /// It takes two turns and gives back twice what it took, measured against what actually
    /// landed rather than against a number written here.
    /// </summary>
    [Fact]
    public void GatheringGivesBackTwiceWhatItTook()
    {
        // Faster, so the gathering starts before they swing. Slower, and the first turn's
        // hit lands on somebody who has not begun yet — which is correct behaviour and not
        // what this test is asking about.
        Battler you = Make(200, Move(Bide));
        Battler them = Make(10, Move(0x00, 40, id: 2));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.True(you.IsGathering);

        int took = you.Gathered;

        Assert.True(took > 0, "nothing landed, so there is nothing to give back");

        int before = them.CurrentHp;

        Turn(battle);

        Assert.False(you.IsGathering);

        // Everything it took over both turns, doubled.
        Assert.True(before - them.CurrentHp >= took * 2);
    }

    /// <summary>And it says both halves out loud.</summary>
    [Fact]
    public void AndItSaysBothHalvesOutLoud()
    {
        Battler you = Make(200, Move(Bide));
        Battler them = Make(10, Move(0x00, 40, id: 2));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.Gathering { Side: Side.Player });
        Assert.Contains(Turn(battle), e => e is BattleEvent.GaveItBack { Side: Side.Player });
    }

    /// <summary>
    /// And gathering through two quiet turns still hits. A move that could come to nothing
    /// after committing two turns is a move nobody would press.
    /// </summary>
    [Fact]
    public void AndGatheringThroughQuietStillHits()
    {
        Battler you = Make(200, Move(Bide));
        Battler them = Make(10, Move(0x00, 0, id: 2));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int before = them.CurrentHp;

        Turn(battle);

        Assert.True(them.CurrentHp < before);
    }

    /// <summary>And starting a second one while gathering is refused.</summary>
    [Fact]
    public void AndStartingASecondWhileGatheringIsRefused()
    {
        Battler you = Make(200, Move(Bide));
        Battler them = Make(10, Move(0x00, 0, id: 2));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Contains(
            Turn(battle),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// And what lands on a stand-in is not gathered. Energy nobody felt is not energy to
    /// give back.
    /// </summary>
    [Fact]
    public void AndWhatLandsOnAStandInIsNotGathered()
    {
        Battler you = Make(200, Move(Bide));
        Battler them = Make(10, Move(0x00, 40, id: 2));

        you.StandInHp = 500;

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Equal(0, you.Gathered);
    }

    // ---- landing later --------------------------------------------------------------------

    /// <summary>
    /// It does nothing on the turn it is used and lands two turns later, on whoever is
    /// standing there then.
    /// </summary>
    [Fact]
    public void LandingLaterDoesNothingNow()
    {
        Battler you = Make(200, Move(FutureSight, 80), Move(0x00, 0, id: 2));
        Battler them = Make(10, Move(0x00, 0, id: 3));

        var battle = new Battle(you, them, 7);

        int before = them.CurrentHp;

        Turn(battle);

        Assert.Equal(before, them.CurrentHp);
    }

    /// <summary>And it arrives, on its own turn, with a line of its own.</summary>
    [Fact]
    public void AndItArrivesLater()
    {
        Battler you = Make(200, Move(FutureSight, 80), Move(0x00, 0, id: 2));
        Battler them = Make(10, Move(0x00, 0, id: 3));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        int before = them.CurrentHp;

        List<BattleEvent> landing =
            battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Contains(landing, e => e is BattleEvent.StruckFromEarlier { Side: Side.Opponent });
        Assert.True(them.CurrentHp < before);
    }

    /// <summary>
    /// And a second one is refused while the first is still coming, so the two cannot be
    /// stacked into one enormous turn.
    /// </summary>
    [Fact]
    public void AndASecondIsRefusedWhileTheFirstIsStillComing()
    {
        Battler you = Make(200, Move(FutureSight, 80));
        Battler them = Make(10, Move(0x00, 0, id: 3));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.Contains(
            Turn(battle),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>And it goes through a stand-in like anything else.</summary>
    [Fact]
    public void AndItGoesThroughAStandInLikeAnythingElse()
    {
        Battler you = Make(200, Move(FutureSight, 80), Move(0x00, 0, id: 2));
        Battler them = Make(10, Move(0x00, 0, id: 3));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        them.StandInHp = 500;

        int before = them.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(before, them.CurrentHp);
        Assert.True(them.StandInHp < 500);
    }
}
