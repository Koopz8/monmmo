using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Moves run out.
/// <para>
/// Every move record on this cartridge carries how many times it can be used, and that
/// field has travelled in the rules file since there was a rules file with nothing ever
/// spending it. A fight in which nothing runs out is a fight where the strongest move is
/// the only move, and the whole shape of a long battle — save the good one, wear them
/// down with the cheap one — was simply absent.
/// </para>
/// <para>
/// And what a creature is left with when everything is spent is not invented either.
/// STRUGGLE is a move in the cartridge's own table, with its own power, type and recoil,
/// found at export off the name exactly as SURF is. A rules file whose cartridge has no
/// move by that name has nought there, and a spent creature simply does nothing — which
/// is worse than struggling and better than making a move up.
/// </para>
/// </summary>
public class RunningOutTests
{
    private static MoveData Move(int id, byte power, byte pp) =>
        new(id, "", 0x00, power, PokemonType.Normal, 100, pp, 0, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            Name = string.Empty,
            BaseHp = 200, BaseAttack = 60, BaseDefense = 60,
            BaseSpeed = (byte)speed, BaseSpAttack = 60, BaseSpDefense = 60,
            Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
            CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        };

        return new Battler(species, 50, Nature.Hardy, null).Knowing(moves);
    }

    [Fact]
    public void AMoveStartsWithWhatItsRecordSays()
    {
        Battler one = Make(50, Move(1, 40, 7));

        Assert.Equal(7, one.PpLeft(0));
    }

    [Fact]
    public void AndSpendingOneTakesOneOff()
    {
        Battler one = Make(50, Move(1, 40, 7));

        Assert.True(one.Spend(0));
        Assert.Equal(6, one.PpLeft(0));
    }

    [Fact]
    public void AndYouCannotSpendWhatIsNotThere()
    {
        Battler one = Make(50, Move(1, 40, 1));

        Assert.True(one.Spend(0));
        Assert.False(one.Spend(0));
        Assert.Equal(0, one.PpLeft(0));
    }

    /// <summary>Every use, gone, and nothing left to swing with.</summary>
    [Fact]
    public void ACreatureWithNothingLeftIsSpent()
    {
        Battler one = Make(50, Move(1, 40, 2), Move(2, 40, 1));

        Assert.False(one.IsSpent);

        one.Spend(0);
        one.Spend(0);
        one.Spend(1);

        Assert.True(one.IsSpent);
    }

    /// <summary>Using one in a fight is what spends it, and a miss costs the same.</summary>
    [Fact]
    public void AFightSpendsThem()
    {
        Battler you = Make(250, Move(1, 40, 5));
        Battler them = Make(1, Move(2, 0, 20));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(4, you.PpLeft(0));
    }

    /// <summary>
    /// And when there is nothing left, the move the cartridge keeps for that comes out.
    /// Announced as itself: the events name the move that was used, and it is not the one
    /// the player pressed.
    /// </summary>
    [Fact]
    public void AndWhenThereIsNothingLeftItStruggles()
    {
        MoveData struggle = new(165, "", 0x30, 50, PokemonType.Normal, 100, 1, 0, 0, 0);

        Battler you = Make(250, Move(1, 40, 1));
        Battler them = Make(1, Move(2, 0, 20));

        var battle = new Battle(you, them, 7) { Struggle = struggle };

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        List<BattleEvent> spent =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(spent, e => e is BattleEvent.MoveUsed { MoveId: 165 });
        Assert.DoesNotContain(spent, e => e is BattleEvent.MoveUsed { MoveId: 1 });
    }

    /// <summary>
    /// And it costs what its own record says it costs, which for STRUGGLE is recoil —
    /// effect 0x30, the group TAKE DOWN and DOUBLE-EDGE are in. Nothing about that is
    /// modelled here: it is the move's own effect byte doing what that byte already does.
    /// </summary>
    [Fact]
    public void AndStrugglingCostsWhatItsRecordSaysItCosts()
    {
        MoveData struggle = new(165, "", 0x30, 50, PokemonType.Normal, 100, 1, 0, 0, 0);

        Battler you = Make(250, Move(1, 40, 1));
        Battler them = Make(1, Move(2, 0, 20));

        var battle = new Battle(you, them, 7) { Struggle = struggle };

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        int before = you.CurrentHp;

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(you.CurrentHp < before);
    }

    /// <summary>
    /// A battle with no struggle in it lets a spent creature do nothing, which is the
    /// honest answer for a cartridge with no such move rather than an invented one.
    /// </summary>
    [Fact]
    public void AndWithoutOneASpentCreatureSimplyDoesNothing()
    {
        Battler you = Make(250, Move(1, 40, 1));
        Battler them = Make(1, Move(2, 0, 20));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        List<BattleEvent> spent =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(spent, e => e is BattleEvent.NothingHappened { Side: Side.Player });
        Assert.DoesNotContain(spent, e => e is BattleEvent.MoveUsed { Side: Side.Player });
    }

    /// <summary>Resting anywhere puts every use back.</summary>
    [Fact]
    public void RestingPutsThemBack()
    {
        Battler one = Make(50, Move(1, 40, 3));

        one.Spend(0);
        one.Spend(0);
        one.RefillPp();

        Assert.Equal(3, one.PpLeft(0));
    }
}
