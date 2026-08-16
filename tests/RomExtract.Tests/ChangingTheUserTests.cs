using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The three effect groups whose subject is the creature using the move.
/// <para>
/// Every stat change this engine had ever applied came from the other side. That is why both
/// shields — MIST and CLEAR BODY's family — ask "was this somebody else", and why there was a
/// test asserting no such move existed: until these three groups reached the table, none did.
/// </para>
/// <para>
/// What is read: which moves are in each group, and the chance on each record. What is
/// modelled: which stat, and by how much. The stat is in the game's code and no amount of
/// dumping crosses that, so it is marked rather than pretended about.
/// </para>
/// </summary>
public class ChangingTheUserTests
{
    private const byte Rising = 0x8B;
    private const byte Everything = 0x8C;
    private const byte Costing = 0xCC;

    private static SpeciesData Species(int ability = 0) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 200, BaseAttack = 60, BaseDefense = 200,
        BaseSpeed = 60, BaseSpAttack = 60, BaseSpDefense = 200,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability,
        Ability2 = (byte)ability,
    };

    /// <summary>A move of that group, certain to do its other thing.</summary>
    private static MoveData Move(byte effect, byte power = 40) =>
        new(1, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    private static Battler Make(int ability = 0, params MoveData[] moves)
    {
        var battler = new Battler(Species(ability), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static List<BattleEvent> Turn(Battle battle) =>
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

    [Fact]
    public void AllThreeGroupsAreAboutTheUser()
    {
        Assert.True(MoveEffects.Of(Rising).OnUser);
        Assert.True(MoveEffects.Of(Everything).OnUser);
        Assert.True(MoveEffects.Of(Costing).OnUser);
    }

    [Fact]
    public void OneOfThemRaisesTheUsersAttack()
    {
        Battler you = Make(0, Move(Rising));
        Battler them = Make(0, Move(0x00, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.StageChanged { Side: Side.Player, Stat: Stat.Attack });
        Assert.Equal(1, you.StageOf(Stat.Attack));

        // And it did damage as well, which is the half a group of damaging moves must not
        // lose when it grows a second part.
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    /// <summary>
    /// All five on one roll, said one line per stat.
    /// <para>
    /// One roll rather than five, because ANCIENTPOWER either raises all of them or raises
    /// none — five separate chances would be a move that usually raises two.
    /// </para>
    /// </summary>
    [Fact]
    public void AnotherRaisesAllFiveAtOnce()
    {
        Battler you = Make(0, Move(Everything));
        Battler them = Make(0, Move(0x00, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        foreach (Stat stat in MoveEffects.Five)
        {
            Assert.Equal(1, you.StageOf(stat));

            Assert.Contains(
                events,
                e => e is BattleEvent.StageChanged { Side: Side.Player } changed && changed.Stat == stat);
        }

        // Five stats, one line each. A single "everything went up" would be a line that is
        // sometimes a lie, because a stage already at its ceiling does not move — and a
        // stat said twice would be a stat raised twice on one roll.
        var said = events
            .OfType<BattleEvent.StageChanged>()
            .Where(e => e.Side == Side.Player)
            .GroupBy(e => e.Stat)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(MoveEffects.Five, stat => Assert.Equal(1, said.GetValueOrDefault(stat)));

        // And nothing outside the five, which is what says this group is about the five
        // rather than about every stage a battler has.
        Assert.All(said.Keys, stat => Assert.Contains(stat, MoveEffects.Five));
    }

    /// <summary>
    /// And the third costs its user something, which is the first move in this table that
    /// ever has.
    /// </summary>
    [Fact]
    public void AndTheThirdCostsItsUserTwoStages()
    {
        Battler you = Make(0, Move(Costing, 140));
        Battler them = Make(0, Move(0x00, 10));

        Turn(new Battle(you, them, 7));

        Assert.Equal(-2, you.StageOf(Stat.SpAttack));
    }

    /// <summary>
    /// Neither shield answers it, because neither is about a cost its own user chose.
    /// <para>
    /// The guard those shields carry was written for a case the table did not contain. This
    /// is that case, and it is what the guard was for.
    /// </para>
    /// </summary>
    [Fact]
    public void NeitherShieldRefusesACostItsOwnUserChose()
    {
        Battler you = Make(Abilities.ClearBody, Move(Costing, 140));
        Battler them = Make(0, Move(0x00, 10));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Equal(-2, you.StageOf(Stat.SpAttack));
        Assert.DoesNotContain(events, e => e is BattleEvent.Shielded { Side: Side.Player });
    }

    /// <summary>
    /// And a stat that cannot go further is said not to have moved, rather than said to
    /// have.
    /// </summary>
    [Fact]
    public void AStatAtItsCeilingIsSaidNotToHaveMoved()
    {
        Battler you = Make(0, Move(Everything));
        Battler them = Make(0, Move(0x00, 10));

        for (int at = 0; at < Stats.MaxStage; at++) you.ChangeStage(Stat.Attack, 1);

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        BattleEvent.StageChanged attack = events
            .OfType<BattleEvent.StageChanged>()
            .First(e => e.Side == Side.Player && e.Stat == Stat.Attack);

        Assert.False(attack.Moved);

        // The other four still went up, which is what says the five are one roll and not
        // one decision.
        Assert.Equal(1, you.StageOf(Stat.Defense));
    }
}
