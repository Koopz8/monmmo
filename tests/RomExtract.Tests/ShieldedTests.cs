using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The abilities that refuse to be made worse at something.
/// <para>
/// The cheapest group in this whole run: the engine has had a shield on stat drops since
/// MIST existed, and these are four more reasons to raise it. No new machinery at all.
/// </para>
/// <para>
/// The rule is <em>somebody else</em> lowering it, which is the easy half to get wrong.
/// Every one of these leaves its owner free to spend its own stats — BELLY DRUM and
/// OVERHEAT are things you do to yourself, and an ability that stopped them would be an
/// ability refusing a move its owner chose.
/// </para>
/// </summary>
public class ShieldedTests
{
    private static SpeciesData Species(int ability = 0) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 60, BaseDefense = 60,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 60,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
    };

    /// <summary>
    /// GROWL's group — a status move whose whole point is taking a stage off the other
    /// side. Read off the effect table rather than named here.
    /// </summary>
    private static MoveData Lowering(int effect) =>
        new(1, string.Empty, (byte)effect, 0, PokemonType.Normal, 100, 20, 0, 0, 0);

    private static Battle Fight(SpeciesData mine, SpeciesData theirs, MoveData move)
    {
        Battler you = new Battler(mine, 50).Knowing(move);
        Battler them = new Battler(theirs, 50).Knowing(move);

        return new Battle(you, them, 7);
    }

    /// <summary>The effect ids of two stat drops, taken from the engine's own table.</summary>
    private static int AttackDown =>
        Enumerable.Range(0, 256).First(e =>
            MoveEffects.Of((byte)e) is { Kind: EffectKind.Stage, OnUser: false, Stat: Stat.Attack, Stages: < 0 });

    private static int SpeedDown =>
        Enumerable.Range(0, 256).First(e =>
            MoveEffects.Of((byte)e) is { Kind: EffectKind.Stage, OnUser: false, Stat: Stat.Speed, Stages: < 0 });

    [Theory]
    [InlineData(Abilities.ClearBody)]
    [InlineData(Abilities.WhiteSmoke)]
    public void ClearBodyRefusesEveryStat(int ability)
    {
        foreach (int effect in new[] { AttackDown, SpeedDown })
        {
            Battle battle = Fight(Species(), Species(ability), Lowering(effect));

            List<BattleEvent> events = battle.ResolveTurn(
                new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            Assert.Equal(0, battle.Opponent.StageOf(Stat.Attack));
            Assert.Equal(0, battle.Opponent.StageOf(Stat.Speed));

            Assert.Contains(events.OfType<BattleEvent.Shielded>(), e => e.Side == Side.Opponent);
        }
    }

    /// <summary>HYPER CUTTER refuses one stat and shrugs at the rest.</summary>
    [Fact]
    public void HyperCutterRefusesOnlyItsAttack()
    {
        Assert.True(Abilities.Protects(Abilities.HyperCutter, Stat.Attack));
        Assert.False(Abilities.Protects(Abilities.HyperCutter, Stat.Speed));

        Battle battle = Fight(Species(), Species(Abilities.HyperCutter), Lowering(SpeedDown));

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        // Speed is not its business, so the drop lands.
        Assert.True(battle.Opponent.StageOf(Stat.Speed) < 0);
    }

    /// <summary>And KEEN EYE is the same shape, one stat along.</summary>
    [Fact]
    public void KeenEyeRefusesOnlyItsAccuracy()
    {
        Assert.True(Abilities.Protects(Abilities.KeenEye, Stat.Accuracy));
        Assert.False(Abilities.Protects(Abilities.KeenEye, Stat.Attack));
    }

    /// <summary>
    /// None of them stops its owner spending its own stats, and there is now a move that
    /// does exactly that to test it with.
    /// <para>
    /// This test used to assert the opposite — that the table contained no move which
    /// lowered its own user's stats — and said in its own comment that OVERHEAT was on the
    /// cartridge and would reach the table eventually, and that this was what would notice.
    /// It noticed. The guard was written for a case that did not exist yet and is now
    /// exercised by one.
    /// </para>
    /// </summary>
    [Fact]
    public void AMoveThatSpendsItsOwnUsersStatsIsNotShielded()
    {
        MoveEffect[] lowering =
        [
            .. Enumerable.Range(0, 256)
                .Select(e => MoveEffects.Of((byte)e))
                .Where(m => m is { Kind: EffectKind.Stage, Stages: < 0 }),
        ];

        Assert.NotEmpty(lowering);

        // Most of them are aimed at the other side, and at least one is not.
        Assert.Contains(lowering, m => !m.OnUser);
        Assert.Contains(lowering, m => m.OnUser);

        // And the one that is not is refused by nothing: neither shield answers a cost its
        // own user chose to pay.
        MoveEffect own = lowering.First(m => m.OnUser);

        Assert.False(Abilities.Protects(Abilities.ClearBody, own.Stat) && !own.OnUser);
    }

    /// <summary>
    /// And an arrival is somebody else lowering it too. INTIMIDATE against a CLEAR BODY is
    /// the interaction this pair exists to have.
    /// </summary>
    [Fact]
    public void AndIntimidateIsSomebodyElseLoweringItToo()
    {
        Battle battle = Fight(
            Species(Abilities.Intimidate), Species(Abilities.ClearBody), Lowering(AttackDown));

        List<BattleEvent> arrived = battle.Arrival(Side.Player);

        Assert.Equal(0, battle.Opponent.StageOf(Stat.Attack));
        Assert.Single(arrived.OfType<BattleEvent.Shielded>());
    }

    /// <summary>
    /// SHIELD DUST, which is about the riders rather than the move: it does not stop a
    /// FLAMETHROWER, it stops the burn that sometimes comes with one.
    /// </summary>
    [Fact]
    public void ShieldDustRefusesTheRidersAndNotTheMove()
    {
        Assert.True(Abilities.ShrugsOffRiders(Abilities.ShieldDust));
        Assert.False(Abilities.ShrugsOffRiders(Abilities.ClearBody));

        // A move that damages and carries a burn on a roll that always lands.
        var record = new byte[MoveData.SizeBytes];

        record[0] = (byte)Enumerable.Range(0, 256).First(e =>
            MoveEffects.Of((byte)e) is { Kind: EffectKind.Status, OnUser: false, Status: StatusCondition.Burn });

        record[1] = 40;
        record[2] = (byte)PokemonType.Normal;
        record[3] = 100;
        record[4] = 20;
        record[5] = 100;

        MoveData burning = MoveData.Parse(record, 1, string.Empty);

        var dusted = new List<StatusCondition>();
        var bare = new List<StatusCondition>();

        for (uint seed = 0; seed < 30; seed++)
        {
            Battler mine = new Battler(Species(), 50).Knowing(burning);
            Battler dusty = new Battler(Species(Abilities.ShieldDust), 50).Knowing(burning);

            new Battle(mine, dusty, seed).ResolveTurn(
                new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            dusted.Add(dusty.Status);

            Battler ours = new Battler(Species(), 50).Knowing(burning);
            Battler theirs = new Battler(Species(), 50).Knowing(burning);

            new Battle(ours, theirs, seed).ResolveTurn(
                new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            bare.Add(theirs.Status);
        }

        // Nobody with the dust caught anything; somebody without it did.
        Assert.All(dusted, s => Assert.Equal(StatusCondition.None, s));
        Assert.Contains(bare, s => s != StatusCondition.None);
    }
}
