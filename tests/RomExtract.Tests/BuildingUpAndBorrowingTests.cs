using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Six groups about doing the same thing again, taking somebody else's work, and turning one
/// kind of move down for everybody.
/// <para>
/// The building pair are the first moves here whose power depends on what happened last turn,
/// which means the count has to be kept by the battle: only the battle can see whether the
/// same move was used again, and "the same move" is the whole rule.
/// </para>
/// </summary>
public class BuildingUpAndBorrowingTests
{
    private const byte FuryCutter = 0x77;
    private const byte Rollout = 0x75;
    private const byte TripleKick = 0x68;
    private const byte PsychUp = 0x8F;
    private const byte MudSport = 0xC9;
    private const byte WaterSport = 0xD2;

    private static SpeciesData Species(int speed = 60) => new()
    {
        Index = 1,
        Name = string.Empty,
        // Papery on purpose. A tough defender turns every difference this file is about into
        // the same rounded-down number, which is how the first version of it managed to
        // measure seven against seven three times running.
        BaseHp = 250, BaseAttack = 150, BaseDefense = 20,
        BaseSpeed = (byte)speed, BaseSpAttack = 150, BaseSpDefense = 20,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
    };

    private static MoveData Move(byte effect, byte power = 0, PokemonType type = PokemonType.Normal) =>
        new(1, string.Empty, effect, power, type, 100, 20, 100, 0, 0);

    private static Battler Make(int speed, params MoveData[] moves)
    {
        var battler = new Battler(Species(speed), 50);

        battler.Moves.AddRange(moves);

        return battler;
    }

    private static Battler Make(params MoveData[] moves) => Make(60, moves);

    /// <summary>
    /// A defender built to last, for the tests that count turns rather than damage.
    /// <para>
    /// The papery one above is right for measuring a difference and wrong for counting: a
    /// move that doubles finishes it inside three goes, and a finished fight does not take
    /// turns. An earlier version of the counting test below was quietly measuring that
    /// instead of the count, and got the same answer twice for a reason that had nothing to
    /// do with what it was asking.
    /// </para>
    /// </summary>
    private static Battler Wall(params MoveData[] moves)
    {
        var battler = new Battler(
            new SpeciesData
            {
                Index = 1,
                Name = string.Empty,
                BaseHp = 255, BaseAttack = 5, BaseDefense = 255,
                BaseSpeed = 5, BaseSpAttack = 5, BaseSpDefense = 255,
                Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
                CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
            },
            100);

        battler.Moves.AddRange(moves);

        return battler;
    }

    /// <summary>The same battler, with its count set — for reading a power at a given step.</summary>
    private static Battler Rolling(Battler battler, int count)
    {
        battler.RunningCount = count;

        return battler;
    }

    [Theory]
    [InlineData(FuryCutter)] [InlineData(Rollout)] [InlineData(TripleKick)]
    [InlineData(PsychUp)] [InlineData(MudSport)] [InlineData(WaterSport)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- building up ---------------------------------------------------------------------

    /// <summary>
    /// Harder every time it is used running, and it stops the moment it is not — which is why
    /// the count lives beside the slot it was counted for.
    /// </summary>
    [Fact]
    public void ItGetsHarderEveryTimeItIsUsedRunning()
    {
        Battler you = Make(200, Move(FuryCutter, 20), Move(0x00, 10));
        Battler them = Wall(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        // The count is watched rather than the damage. Damage carries an eighty-five to a
        // hundred roll, which is larger than one doubling at these numbers — the first
        // version of this compared four rolled numbers and called the noise a result.
        var counted = new List<int>();

        for (int turn = 0; turn < 4; turn++)
        {
            battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

            counted.Add(you.RunningCount);
        }

        // Said out loud, because every one of these counts is a count of turns that happened
        // and a fight that ended would give the same rising list right up to where it stopped.
        Assert.False(battle.IsOver);
        Assert.Equal([0, 1, 2, 3], counted);

        MoveData cutter = you.MoveAt(0)!;

        Assert.True(
            MovePower.Of(cutter, Rolling(you, 3), them) > MovePower.Of(cutter, Rolling(you, 0), them));
    }

    /// <summary>And using something else stops it, rather than pausing it.</summary>
    [Fact]
    public void AndUsingSomethingElseStopsIt()
    {
        Battler you = Make(200, Move(FuryCutter, 20), Move(0x00, 10));
        Battler them = Wall(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));
        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.True(you.RunningCount > 0);

        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(0, you.RunningCount);
        Assert.Null(you.RunningSlot);
    }

    /// <summary>
    /// It stops doubling somewhere. Nothing that doubles forever belongs in a game, and where
    /// the cap goes is on no record — so it is modelled and it is tested as existing rather
    /// than as being any particular number.
    /// </summary>
    [Fact]
    public void AndItStopsDoublingSomewhere()
    {
        Battler you = Make(200, Move(FuryCutter, 20));
        Battler them = Make(60, Move(0x00, 0));

        MoveData cutter = you.MoveAt(0)!;

        var seen = new List<int>();

        for (int running = 0; running < 10; running++)
        {
            you.RunningCount = running;

            seen.Add(MovePower.Of(cutter, you, them)!.Value);
        }

        Assert.Equal(seen.Max(), seen[^1]);
        Assert.True(seen.Distinct().Count() < seen.Count, "it never stopped doubling");
    }

    /// <summary>The rolling one takes its user's choice away while it builds.</summary>
    [Fact]
    public void TheRollingOneTakesTheChoiceAway()
    {
        Battler you = Make(200, Move(Rollout, 20), Move(0x00, 10));
        Battler them = Wall(Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        battle.ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(0, you.ForcedSlot);

        // Asking for the other one gets the rolling one anyway.
        battle.ResolveTurn(new BattleAction.UseMove(1), new BattleAction.UseMove(0));

        Assert.Equal(0, you.LastSlot);
    }

    // ---- three goes ------------------------------------------------------------------------

    /// <summary>
    /// Three hits, each harder than the last — the only place in this engine where which hit
    /// of a move it is changes what the hit is worth.
    /// </summary>
    [Fact]
    public void ThreeGoesEachHarderThanTheLast()
    {
        Battler you = Make(200, Move(TripleKick, 20));
        Battler them = Make(60, Move(0x00, 0));

        // Three of them, which is what the battle decides.
        List<BattleEvent> events = new Battle(you, them, 7)
            .ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Equal(3, events.OfType<BattleEvent.DamageDealt>().Count());

        // And each harder than the last, measured with the roll pinned — the roll is worth
        // more than one step at any sane power.
        MoveData kick = you.MoveAt(0)!;

        int[] climbing =
        [
            .. Enumerable.Range(0, 3).Select(hit =>
                DamageCalculator.Calculate(you, them, kick, false, 100, Weather.None, 100, hit).Damage),
        ];

        Assert.True(climbing[1] > climbing[0], $"{climbing[1]} should beat {climbing[0]}");
        Assert.True(climbing[2] > climbing[1], $"{climbing[2]} should beat {climbing[1]}");
    }

    // ---- borrowing ---------------------------------------------------------------------------

    /// <summary>
    /// Every stage, not the good ones. A move that copied only what helped would be a move
    /// nobody could play around.
    /// </summary>
    [Fact]
    public void CopyingTakesEveryStageAndNotOnlyTheGoodOnes()
    {
        Battler you = Make(200, Move(PsychUp));
        Battler them = Make(60, Move(0x00, 0));

        them.ChangeStage(Stat.Attack, 3);
        them.ChangeStage(Stat.Defense, -2);

        you.ChangeStage(Stat.Speed, 2);

        List<BattleEvent> events = new Battle(you, them, 7)
            .ResolveTurn(new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.CopiedStages { Side: Side.Player });

        Assert.Equal(3, you.StageOf(Stat.Attack));
        Assert.Equal(-2, you.StageOf(Stat.Defense));

        // And what it had of its own is gone, because it took theirs rather than adding to
        // its own.
        Assert.Equal(0, you.StageOf(Stat.Speed));
    }

    // ---- damping ------------------------------------------------------------------------------

    /// <summary>
    /// One type turned down for everybody, which is a fact about the room rather than about
    /// either creature — so it is the last word on the damage.
    /// </summary>
    [Theory]
    [InlineData(MudSport, PokemonType.Electric, PokemonType.Fire)]
    [InlineData(WaterSport, PokemonType.Fire, PokemonType.Electric)]
    public void EachDampsOneTypeAndLeavesTheOther(int effect, PokemonType damped, PokemonType other)
    {
        // The move is used, and then what the field is damping is asked of the battle rather
        // than inferred from a rolled number.
        Battler you = Make(200, Move((byte)effect));
        Battler them = Make(60, Move(0x00, 0));

        var battle = new Battle(you, them, 7);

        List<BattleEvent> events = battle.ResolveTurn(
            new BattleAction.UseMove(0), new BattleAction.UseMove(0));

        Assert.Contains(events, e => e is BattleEvent.Damped { Side: Side.Player });

        Assert.Equal(damped, battle.Damped);
        Assert.True(battle.DampedTurns > 0);
        Assert.NotEqual(other, battle.Damped);

        // And a damped type is worth half, measured with the roll pinned.
        int Dealt(PokemonType coming, int damping) =>
            DamageCalculator.Calculate(
                you, them, Move(0x00, 60, coming), false, 100, Weather.None, damping).Damage;

        Assert.Equal(Dealt(damped, 100) / 2, Dealt(damped, 50));
        Assert.Equal(Dealt(other, 100), Dealt(other, 100));
    }
}
