using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The two moves that take a choice away.
/// <para>
/// DISABLE removes one option; ENCORE removes all the others. They are the same idea
/// twice, and they need the same one thing — what the other side just did — which is why
/// they are written together and tested together.
/// </para>
/// <para>
/// Almost nothing about either is modelled. Their records aim them at somebody else, say
/// what they cost and say they do no damage. The one number that is not in any record is
/// how long each holds, and that is chosen here, in the open, in the one place a reader
/// would look for it.
/// </para>
/// <para>
/// DISABLE is the second-heaviest silent group this engine had: 26 trainer parties carry
/// it and 22 species learn it by level 40.
/// </para>
/// </summary>
public class TakingTheChoiceAwayTests
{
    private const byte DisableEffect = 0x56;
    private const byte EncoreEffect = 0x5A;

    private static MoveData Move(int id, byte effect, byte power, byte pp = 20) =>
        new(id, "", effect, power, PokemonType.Normal, 100, pp, 0, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var species = new SpeciesData
        {
            Index = 1,
            Name = string.Empty,
            BaseHp = 200, BaseAttack = 40, BaseDefense = 60,
            BaseSpeed = (byte)speed, BaseSpAttack = 40, BaseSpDefense = 60,
            Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
            CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        };

        return new Battler(species, 50, Nature.Hardy, null).Knowing(moves);
    }

    /// <summary>Nothing to block before they have done anything.</summary>
    [Fact]
    public void ThereIsNothingToBlockOnTheFirstTurn()
    {
        Battler you = Make(250, Move(9, DisableEffect, 0));
        Battler them = Make(1, Move(2, 0x00, 10));

        List<BattleEvent> events =
            new Battle(you, them, 7).ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
        Assert.Null(them.DisabledSlot);
    }

    /// <summary>And what is blocked is what they just did.</summary>
    [Fact]
    public void ItBlocksWhatTheyJustDid()
    {
        // Slower, so they move first and there is something to block.
        Battler you = Make(1, Move(9, DisableEffect, 0));
        Battler them = Make(250, Move(2, 0x00, 10), Move(3, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Equal(1, them.DisabledSlot);
        Assert.True(them.IsDisabled(1));
        Assert.False(them.IsDisabled(0));
    }

    /// <summary>And a blocked slot cannot be swung.</summary>
    [Fact]
    public void AndABlockedSlotCannotBeSwung()
    {
        Battler you = Make(1, Move(9, DisableEffect, 0));
        Battler them = Make(250, Move(2, 0x00, 60));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        int after = you.CurrentHp;

        List<BattleEvent> blocked =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(blocked, e => e is BattleEvent.CannotUse { Side: Side.Opponent });
        Assert.Equal(after, you.CurrentHp);
    }

    /// <summary>One at a time — a second one finds there is nothing to do.</summary>
    [Fact]
    public void AndOnlyOneAtATime()
    {
        Battler you = Make(1, Move(9, DisableEffect, 0));
        Battler them = Make(250, Move(2, 0x00, 10), Move(3, 0x00, 10));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        List<BattleEvent> again =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Contains(again, e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
        Assert.Equal(0, them.DisabledSlot);
    }

    /// <summary>And it runs out, and says so.</summary>
    [Fact]
    public void AndItRunsOut()
    {
        Battler you = Make(1, Move(9, DisableEffect, 0));
        Battler them = Make(250, Move(2, 0x00, 0));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(them.DisabledTurns > 0);

        bool freed = false;

        for (int turn = 0; turn < 10 && !freed; turn++)
        {
            freed = battle
                .ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0))
                .Any(e => e is BattleEvent.CanUseAgain { Side: Side.Opponent });
        }

        Assert.True(freed);
        Assert.Null(them.DisabledSlot);
    }

    // ---- and the other way round ------------------------------------------------------

    /// <summary>ENCORE makes them do it again, using the holding THRASH already had.</summary>
    [Fact]
    public void AnEncoreMakesThemRepeatIt()
    {
        Battler you = Make(1, Move(9, EncoreEffect, 0));
        Battler them = Make(250, Move(2, 0x00, 10), Move(3, 0x00, 60));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.MustRepeat { Side: Side.Opponent });
        Assert.Equal(0, them.ForcedSlot);

        // And the harder move they choose next turn is not the one that lands.
        List<BattleEvent> next =
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(1));

        Assert.Contains(next, e => e is BattleEvent.MoveUsed { Side: Side.Opponent, MoveId: 2 });
        Assert.DoesNotContain(next, e => e is BattleEvent.MoveUsed { Side: Side.Opponent, MoveId: 3 });
    }

    [Fact]
    public void AndThereIsNothingToRepeatOnTheFirstTurn()
    {
        Battler you = Make(250, Move(9, EncoreEffect, 0));
        Battler them = Make(1, Move(2, 0x00, 10));

        List<BattleEvent> events =
            new Battle(you, them, 7).ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.NothingHappened { Side: Side.Opponent });
        Assert.Null(them.ForcedSlot);
    }

    /// <summary>Neither is silence any more.</summary>
    [Theory]
    [InlineData(DisableEffect)]
    [InlineData(EncoreEffect)]
    public void AndNeitherIsSilentNow(byte effect)
    {
        Assert.False(MoveEffects.IsSilent(effect));
    }
}
