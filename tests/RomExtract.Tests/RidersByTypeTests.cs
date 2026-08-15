using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a type already means, and what follows from it.
/// <para>
/// A hundred and thirty-one effect groups do nothing in this engine because the effect
/// byte names a group and what the group does is in the cartridge's code. Most of them
/// have to stay that way. Some do not: this engine already burns for one FIRE group and
/// poisons for three POISON ones, and every one of those groups is that type the whole way
/// through. A silent damaging group that is also one type throughout, and carries a
/// secondary chance in every record, is the same claim asked a second time.
/// </para>
/// <para>
/// The tests that matter here are the refusals. A mixed-type precedent is evidence about
/// nothing, and a group this engine already has an answer for is not the rule's to speak
/// about — which is what stops it from freezing AURORA BEAM.
/// </para>
/// </summary>
public class RidersByTypeTests
{
    private static MoveData Move(
        string name, byte effect, PokemonType type, byte power = 60, byte secondary = 10) =>
        new(1, name, effect, power, type, 100, 20, secondary, 0, 0);

    /// <summary>An engine that knows the given effects and nothing else.</summary>
    private static Func<byte, MoveEffect> Knows(params (byte Effect, StatusCondition Status)[] known) =>
        effect => known.FirstOrDefault(k => k.Effect == effect) is { Status: not StatusCondition.None } found
            ? new MoveEffect(EffectKind.Status, OnUser: false, Status: found.Status)
            : new MoveEffect(EffectKind.None, OnUser: false);

    [Fact]
    public void ATypeWhoseEveryModelledGroupIsThatTypeIsSettled()
    {
        List<TypeRider> settled = RidersByType.Settled(
            [Move("EMBER", 0x04, PokemonType.Fire), Move("FLAMETHROWER", 0x04, PokemonType.Fire)],
            Knows((0x04, StatusCondition.Burn)));

        TypeRider only = Assert.Single(settled);

        Assert.Equal(PokemonType.Fire, only.Type);
        Assert.Equal(StatusCondition.Burn, only.Status);
    }

    /// <summary>
    /// And a mixed-type group settles nothing. This is the whole guard: the group holding
    /// THUNDERBOLT also holds BODY SLAM, LICK and DRAGONBREATH, and reading it as
    /// "electric means paralysis" reads it equally as "normal means paralysis".
    /// </summary>
    [Fact]
    public void AMixedTypeGroupSettlesNothing()
    {
        List<TypeRider> settled = RidersByType.Settled(
            [
                Move("THUNDERBOLT", 0x06, PokemonType.Electric),
                Move("BODY SLAM", 0x06, PokemonType.Normal),
                Move("LICK", 0x06, PokemonType.Ghost),
            ],
            Knows((0x06, StatusCondition.Paralysis)));

        Assert.Empty(settled);
    }

    /// <summary>And two single-type groups that disagree settle nothing either.</summary>
    [Fact]
    public void TwoGroupsOfOneTypeThatDisagreeSettleNothing()
    {
        List<TypeRider> settled = RidersByType.Settled(
            [Move("ONE", 0x04, PokemonType.Fire), Move("TWO", 0x05, PokemonType.Fire)],
            Knows((0x04, StatusCondition.Burn), (0x05, StatusCondition.Freeze)));

        Assert.Empty(settled);
    }

    [Fact]
    public void ASilentGroupOfASettledTypeIsAccountedFor()
    {
        List<RiderGroup> found = RidersByType.Accounted(
            [
                Move("EMBER", 0x04, PokemonType.Fire),
                Move("SACRED FIRE", 0x7D, PokemonType.Fire, secondary: 50),
            ],
            Knows((0x04, StatusCondition.Burn)));

        RiderGroup rider = Assert.Single(found, g => g.Effect == 0x7D);

        Assert.Equal(StatusCondition.Burn, rider.Status);
        Assert.False(rider.EngineAgrees);
    }

    /// <summary>
    /// And once the engine has been taught it, the same reading says so. The report is
    /// meant to be run before and after and to say which it is looking at.
    /// </summary>
    [Fact]
    public void OnceTaughtTheSameGroupReadsAsAgreed()
    {
        List<RiderGroup> found = RidersByType.Accounted(
            [
                Move("EMBER", 0x04, PokemonType.Fire),
                Move("SACRED FIRE", 0x7D, PokemonType.Fire, secondary: 50),
            ],
            Knows((0x04, StatusCondition.Burn), (0x7D, StatusCondition.Burn)));

        Assert.All(found, g => Assert.True(g.EngineAgrees));
        Assert.Contains(found, g => g.Effect == 0x7D);
    }

    /// <summary>
    /// The refusal that matters. A group this engine already has an answer for is not the
    /// rule's to speak about — AURORA BEAM is one ICE move with a ten percent rider and
    /// every outward mark of a group that freezes, and this engine has had it down as an
    /// attack drop all along. The first draft of the rule froze it.
    /// </summary>
    [Fact]
    public void AGroupTheEngineAlreadyAnswersIsLeftAlone()
    {
        Func<byte, MoveEffect> engine = effect => effect switch
        {
            0x05 => new MoveEffect(EffectKind.Status, OnUser: false, Status: StatusCondition.Freeze),
            0x44 => new MoveEffect(EffectKind.Stage, OnUser: false, Stat: Stat.Attack, Stages: -1),
            _ => new MoveEffect(EffectKind.None, OnUser: false),
        };

        List<RiderGroup> found = RidersByType.Accounted(
            [Move("ICE BEAM", 0x05, PokemonType.Ice), Move("AURORA BEAM", 0x44, PokemonType.Ice)],
            engine);

        Assert.DoesNotContain(0x44, found.Select(g => (int)g.Effect));
    }

    /// <summary>
    /// A status move is not a rider. A group with no power is the move itself, and this
    /// rule is only about what rides along with a hit.
    /// </summary>
    [Fact]
    public void AGroupWithNoPowerIsNotARider()
    {
        List<RiderGroup> found = RidersByType.Accounted(
            [
                Move("EMBER", 0x04, PokemonType.Fire),
                Move("WILL-O-WISP", 0xDA, PokemonType.Fire, power: 0, secondary: 100),
            ],
            Knows((0x04, StatusCondition.Burn)));

        Assert.DoesNotContain(0xDA, found.Select(g => (int)g.Effect));
    }

    /// <summary>And neither is a group whose records claim nothing rides on them.</summary>
    [Fact]
    public void AGroupWithNoSecondaryChanceIsNotARider()
    {
        List<RiderGroup> found = RidersByType.Accounted(
            [
                Move("EMBER", 0x04, PokemonType.Fire),
                Move("QUIET", 0xDA, PokemonType.Fire, secondary: 0),
            ],
            Knows((0x04, StatusCondition.Burn)));

        Assert.DoesNotContain(0xDA, found.Select(g => (int)g.Effect));
    }

    /// <summary>
    /// And the four arms this rule produced against the real cartridge, asked of the real
    /// table this engine ships with rather than of a cartridge — no test here has ever
    /// needed one and none is going to start.
    /// </summary>
    [Theory]
    [InlineData(0x7D, StatusCondition.Burn)]
    [InlineData(0xC8, StatusCondition.Burn)]
    [InlineData(0xCA, StatusCondition.Poison)]
    [InlineData(0xD1, StatusCondition.Poison)]
    public void TheFourGroupsItSettledAreInTheEngine(byte effect, StatusCondition status)
    {
        MoveEffect said = MoveEffects.Of(effect);

        Assert.Equal(EffectKind.Status, said.Kind);
        Assert.Equal(status, said.Status);
        Assert.False(said.OnUser);
    }

    /// <summary>
    /// The shape of the cartridge's answer, without the cartridge: two FIRE groups where
    /// one is already known, two POISON groups likewise, a mixed-type group that settles
    /// nothing, and a group the engine already answers. Four in, two out.
    /// </summary>
    [Fact]
    public void TheWholeRuleOnAWorldShapedLikeThisOne()
    {
        List<RiderGroup> found = RidersByType.Accounted(
            [
                Move("EMBER", 0x04, PokemonType.Fire),
                Move("SACRED FIRE", 0x7D, PokemonType.Fire, secondary: 50),
                Move("BLAZE KICK", 0xC8, PokemonType.Fire),
                Move("SLUDGE", 0x02, PokemonType.Poison, secondary: 30),
                Move("POISON FANG", 0xCA, PokemonType.Poison, secondary: 30),
                Move("THUNDERBOLT", 0x06, PokemonType.Electric),
                Move("BODY SLAM", 0x06, PokemonType.Normal, secondary: 30),
                Move("THUNDER", 0x98, PokemonType.Electric, power: 120, secondary: 30),
            ],
            Knows((0x04, StatusCondition.Burn), (0x02, StatusCondition.Poison),
                (0x06, StatusCondition.Paralysis)));

        // The two that settled their types are listed as well, agreeing; the four the
        // rule reaches are the ones that do not.
        Assert.Equal([0x02, 0x04, 0x7D, 0xC8, 0xCA], found.Select(g => g.Effect));
        Assert.Equal([0x7D, 0xC8, 0xCA], found.Where(g => !g.EngineAgrees).Select(g => g.Effect));

        // And THUNDER is not among them at all, which is the price of the mixed-type guard.
        Assert.DoesNotContain(0x98, found.Select(g => (int)g.Effect));
    }
}

/// <summary>
/// The four groups in a fight, which is where a mapping is either true or decorative.
/// <para>
/// The arms are one line each and reuse machinery that has worked since the first status
/// move, so the risk is not that burning is broken — it is that the chance is taken from
/// the wrong place. SACRED FIRE's record says fifty and FLAME WHEEL's says ten, and
/// nothing in this engine has to know which is which.
/// </para>
/// </summary>
public class RidersInAFightTests
{
    private static MoveData Fire(string name, byte effect, byte secondary) =>
        new(1, name, effect, 60, PokemonType.Fire, 100, 20, secondary, 0, 0);

    private static Battler With(MoveData move) =>
        new Battler(TestMons.Species("ONE", PokemonType.Normal, speed: 200), level: 50, nickname: "ONE")
            .Knowing(move);

    private static Battler Target() =>
        new Battler(TestMons.Species("TWO", PokemonType.Normal, speed: 5), level: 50, nickname: "TWO");

    /// <summary>A rider that always rolls lands, which is the mapping doing its work.</summary>
    [Fact]
    public void SacredFireBurns()
    {
        // A hundred rather than fifty, because a test that turns on a die is a test that
        // fails one morning for nothing. What is being checked is the mapping, not the rng.
        var battle = new Battle(With(Fire("SACRED FIRE", 0x7D, 100)), Target(), seed: 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.Burn, battle.Opponent.Status);
    }

    /// <summary>And a record that claims nothing rides on it burns nobody.</summary>
    [Fact]
    public void ARecordClaimingNothingBurnsNobody()
    {
        var battle = new Battle(With(Fire("QUIET", 0x7D, 0)), Target(), seed: 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.None, battle.Opponent.Status);
    }

    /// <summary>And the poison pair poisons rather than burning.</summary>
    [Fact]
    public void PoisonFangPoisons()
    {
        MoveData fang = new(1, "POISON FANG", 0xCA, 50, PokemonType.Poison, 100, 20, 100, 0, 0);

        var battle = new Battle(With(fang), Target(), seed: 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(StatusCondition.Poison, battle.Opponent.Status);
    }
}
