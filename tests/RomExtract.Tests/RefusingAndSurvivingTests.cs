using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Five groups about what somebody may do, and what happens when they cannot survive it.
/// <para>
/// Two take choices away, two are about the moment a hit would finish somebody, and one puts
/// both sides on the same number. All five needed small state and no new machinery, which is
/// becoming the ordinary case rather than the lucky one.
/// </para>
/// </summary>
public class RefusingAndSurvivingTests
{
    private const byte Taunt = 0xAF;
    private const byte Torment = 0xA5;
    private const byte Endure = 0x74;
    private const byte DestinyBond = 0x62;
    private const byte PainSplit = 0x5B;

    private static SpeciesData Species(int speed = 60, int hp = 200, int defence = 200) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = (byte)hp, BaseAttack = 100, BaseDefense = (byte)defence,
        BaseSpeed = (byte)speed, BaseSpAttack = 100, BaseSpDefense = (byte)defence,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    /// <summary>
    /// A status move has no power, which is how this generation tells the two apart — the
    /// category is not a field on a record, it comes off the type and the power.
    /// </summary>
    private static MoveData Move(byte effect, byte power = 0) =>
        new(1, string.Empty, effect, power, PokemonType.Normal, 100, 20, 100, 0, 0);

    /// <summary>Something a big hit actually finishes, for the two tests that need one.</summary>
    private static Battler Fragile(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed, hp: 1, defence: 1), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static Battler Make(params MoveData[] moves) => Make(60, moves);

    private static List<BattleEvent> Turn(Battle battle, int mine = 0, int theirs = 0) =>
        battle.ResolveTurn(new BattleAction.UseMove(mine), new BattleAction.UseMove(theirs));

    [Theory]
    [InlineData(Taunt)] [InlineData(Torment)] [InlineData(Endure)]
    [InlineData(DestinyBond)] [InlineData(PainSplit)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- taking choices away --------------------------------------------------------------

    /// <summary>
    /// A taunted creature has nothing to do but attack. Refused where every other refusal
    /// lives rather than where a move is chosen — a client chooses and a server decides.
    /// </summary>
    [Fact]
    public void ATauntedCreatureMayOnlyAttack()
    {
        Battler you = Make(200, Move(Taunt));
        Battler them = Make(60, Move(0x00, 0), Move(0x00, 40));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.Taunted { Side: Side.Opponent });

        // Their status move is refused and their attack is not.
        Assert.Contains(Turn(battle, 0, 0), e => e is BattleEvent.CannotUse { Side: Side.Opponent });

        int before = you.CurrentHp;

        Turn(battle, 0, 1);

        Assert.True(you.CurrentHp < before, "an attack should still have landed");
    }

    /// <summary>And it runs out, rather than lasting the fight.</summary>
    [Fact]
    public void AndItRunsOut()
    {
        Battler you = Make(200, Move(Taunt));
        Battler them = Make(60, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.True(them.TauntTurns > 0);

        // Bounded rather than counted exactly, because how many turns it lasts is a modelled
        // number and this test is about it ending rather than about which number it is.
        for (int turn = 0; turn < 8 && them.TauntTurns > 0; turn++) Turn(battle);

        Assert.Equal(0, them.TauntTurns);
    }

    /// <summary>
    /// A tormented creature may not do the same thing twice running — which is a different
    /// refusal from a blocked slot, because what is refused changes every turn.
    /// </summary>
    [Fact]
    public void ATormentedCreatureMayNotRepeatItself()
    {
        Battler you = Make(200, Move(Torment));
        Battler them = Make(60, Move(0x00, 40), Move(0x00, 40));

        var battle = new Battle(you, them, 7);

        Assert.Contains(Turn(battle), e => e is BattleEvent.Tormented { Side: Side.Opponent });

        Turn(battle, 0, 0);

        // The same slot again is refused; the other one is not.
        Assert.Contains(Turn(battle, 0, 0), e => e is BattleEvent.CannotUse { Side: Side.Opponent });
        Assert.DoesNotContain(Turn(battle, 0, 1), e => e is BattleEvent.CannotUse { Side: Side.Opponent });
    }

    // ---- surviving --------------------------------------------------------------------

    /// <summary>
    /// Bracing is certain rather than a chance, which is the whole difference between a move
    /// somebody chose and an item they happened to be carrying.
    /// </summary>
    [Fact]
    public void BracingSurvivesWhateverLandsOnASinglePoint()
    {
        Battler you = Fragile(200, Move(Endure));
        Battler them = Make(60, Move(0x00, 250));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.BracedItself { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.Endured { Side: Side.Player });

        Assert.False(you.HasFainted);
        Assert.Equal(1, you.CurrentHp);
    }

    /// <summary>And it lasts one turn, like a guard.</summary>
    [Fact]
    public void AndItLastsOneTurn()
    {
        Battler you = Fragile(200, Move(Endure), Move(0x00, 0));
        Battler them = Make(60, Move(0x00, 250));

        var battle = new Battle(you, them, 7);

        Turn(battle);

        Assert.False(you.IsEnduring);

        Turn(battle, 1, 0);

        Assert.True(you.HasFainted);
    }

    /// <summary>
    /// A promise kept: whoever finishes it goes down as well, which is the only thing in this
    /// engine that can end a fight with nobody standing.
    /// </summary>
    [Fact]
    public void ThePromiseTakesWhoeverKeepsIt()
    {
        Battler you = Fragile(200, Move(DestinyBond));
        Battler them = Make(60, Move(0x00, 250));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = Turn(battle);

        Assert.Contains(events, e => e is BattleEvent.Bonded { Side: Side.Player });
        Assert.Contains(events, e => e is BattleEvent.TookThemWith { Side: Side.Opponent });

        Assert.True(you.HasFainted);
        Assert.True(them.HasFainted);
    }

    [Fact]
    public void AndNothingHappensWhenNobodyIsFinished()
    {
        Battler you = Make(200, Move(DestinyBond));
        Battler them = Make(60, Move(0x00, 1));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.DoesNotContain(events, e => e is BattleEvent.TookThemWith);
        Assert.False(them.HasFainted);
    }

    // ---- sharing --------------------------------------------------------------------------

    /// <summary>
    /// The only move in this game that can put health back on somebody by hurting them, and
    /// hurt somebody by healing them.
    /// </summary>
    [Fact]
    public void SharingPutsBothOnTheSameNumber()
    {
        Battler you = Make(200, Move(PainSplit));
        Battler them = Make(60, Move(0x00, 0));

        you.TakeDamage(you.MaxHp - 10);

        int between = (you.CurrentHp + them.CurrentHp) / 2;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.HealthShared);

        Assert.Equal(between, you.CurrentHp);
        Assert.Equal(between, them.CurrentHp);

        // One went up and the other went down, which is what makes it a sharing.
        Assert.True(you.CurrentHp > 10);
        Assert.True(them.CurrentHp < them.MaxHp);
    }
}
