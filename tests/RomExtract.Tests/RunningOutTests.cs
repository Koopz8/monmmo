using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using PokeMmo.Core.World;
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

    // ---- and it outlives the fight ----------------------------------------------------

    /// <summary>
    /// What is left goes into the save and comes back out of it. Without this every
    /// battler was rebuilt full and running out meant nothing past the last turn of the
    /// battle it happened in — which is a mechanic that looks whole and is not.
    /// </summary>
    [Fact]
    public void WhatIsLeftSurvivesTheFight()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Battler battler = factory.Restore(saved)!;

        int full = battler.PpLeft(0);

        Assert.True(full > 0);

        battler.Spend(0);

        SavedMon written = BattleFactory.Save(battler);

        Assert.Equal(full - 1, written.Pp[0]);
        Assert.Equal(full - 1, factory.Restore(written)!.PpLeft(0));
    }

    /// <summary>
    /// A save written before any of this existed carries nothing, and nothing means full
    /// — which is what those creatures were.
    /// </summary>
    [Fact]
    public void AndASaveFromBeforeThisComesBackFull()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon old = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Assert.Empty(old.Pp);

        Battler battler = factory.Restore(old)!;

        Assert.Equal(battler.Moves[0].Pp, battler.PpLeft(0));
    }

    /// <summary>A counter puts every use back, and until it does nobody is well.</summary>
    [Fact]
    public void AndACounterPutsThemBack()
    {
        var factory = new BattleFactory(TestRules.All);

        SavedMon saved = new(1, 20, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]);

        Battler battler = factory.Restore(saved)!;
        battler.Spend(0);

        SavedMon spent = BattleFactory.Save(battler);

        Assert.False(factory.IsWell(spent));

        SavedMon healed = factory.Healed(spent);

        Assert.True(factory.IsWell(healed));
        Assert.Equal(battler.Moves[0].Pp, factory.Restore(healed)!.PpLeft(0));
    }

    /// <summary>
    /// And it survives the process, which is the half the database has to get right. The
    /// column was added to an existing table rather than only to a fresh one, so a save
    /// written yesterday reads back as full rather than as nought — which would be a
    /// party that could only struggle.
    /// </summary>
    [Fact]
    public async Task AndItSurvivesTheProcess()
    {
        string path = TempDatabase.Path();

        try
        {
            SavedMon worn = new(16, 3, null, 8, StatusCondition.None, Nature.Bold, [33, 45])
            {
                Pp = [4, 15],
            };

            using (var writing = new SqlitePlayerStore(path))
            {
                await writing.RegisterAsync(
                    "Mason",
                    "a-good-password",
                    new SavedCharacter("1.0", 3, 4, Direction.Down, [worn]));
            }

            using (var reading = new SqlitePlayerStore(path))
            {
                var login = Assert.IsType<AuthOutcome.Success>(
                    await reading.LoginAsync("Mason", "a-good-password"));

                Assert.Equal([4, 15], Assert.Single(login.Character.Party).Pp);
            }
        }
        finally
        {
            TempDatabase.Delete(path);
        }
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
