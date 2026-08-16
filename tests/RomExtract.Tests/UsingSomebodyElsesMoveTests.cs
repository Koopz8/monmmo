using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Seven groups that do nothing themselves.
/// <para>
/// Five use a move that is not the one chosen; two move an ability from one creature to
/// another. They are together because they needed the same two things, and both were things
/// this engine did not have: a creature that remembers <em>which move</em> it last used
/// rather than which slot, and an ability that a fight can change.
/// </para>
/// <para>
/// The slot was not enough, and that is worth saying plainly. A slot indexes one creature's
/// own four; the thing MIRROR MOVE wants is the move the <em>other</em> one used, which is an
/// index into a list this side has no business reading and which stops meaning anything the
/// moment somebody switches out. So the move travels rather than the number.
/// </para>
/// </summary>
public class UsingSomebodyElsesMoveTests
{
    private const byte MirrorMove = 0x09;
    private const byte Metronome = 0x53;
    private const byte SleepTalk = 0x61;
    private const byte Mimic = 0x52;
    private const byte Sketch = 0x5F;
    private const byte RolePlay = 0xB2;
    private const byte SkillSwap = 0xBF;

    private static SpeciesData Species(int speed = 60, int ability = 0) => new()
    {
        Index = 1,
        Name = string.Empty,
        BaseHp = 250, BaseAttack = 90, BaseDefense = 90,
        BaseSpeed = (byte)speed, BaseSpAttack = 90, BaseSpDefense = 90,
        Type1 = PokemonType.Normal, Type2 = PokemonType.Normal,
        CatchRate = 255, ExpYield = 64, GrowthRate = GrowthRate.MediumFast,
        Ability1 = (byte)ability, Ability2 = (byte)ability,
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
    [InlineData(MirrorMove)] [InlineData(Metronome)] [InlineData(SleepTalk)]
    [InlineData(Mimic)] [InlineData(Sketch)] [InlineData(RolePlay)] [InlineData(SkillSwap)]
    public void NoneOfThemIsSilent(int effect) =>
        Assert.NotEqual(EffectKind.None, MoveEffects.Of((byte)effect).Kind);

    // ---- remembering the move rather than the slot ------------------------------------

    /// <summary>
    /// A creature remembers the move it used, not only where it kept it. Everything else in
    /// this file depends on it.
    /// </summary>
    [Fact]
    public void ACreatureRemembersTheMoveItUsedAndNotOnlyTheSlot()
    {
        Battler you = Make(60, Move(0x00, 40, id: 33));
        Battler them = Make(60, Move(0x00, 40, id: 44));

        Turn(new Battle(you, them, 7));

        Assert.Equal(33, you.LastMove?.Id);
        Assert.Equal(44, them.LastMove?.Id);
    }

    /// <summary>And forgets it on the way out, like everything else a fight starts.</summary>
    [Fact]
    public void AndForgetsItOnTheWayOut()
    {
        Battler you = Make(60, Move(0x00, 40, id: 33));

        you.LastMove = Move(0x00, 40, id: 33);
        you.BorrowedAbility = 5;

        you.ForgetWhatWasStarted();

        Assert.Null(you.LastMove);
        Assert.Null(you.BorrowedAbility);
    }

    // ---- mirroring ---------------------------------------------------------------------

    /// <summary>
    /// It uses what the other one just used, and says which move that turned out to be.
    /// <para>
    /// The mirroring side is made slower on purpose: there is nothing to mirror until the
    /// other one has moved, so a coin flip deciding the order would make this test pass most
    /// of the time, which is worse than failing.
    /// </para>
    /// </summary>
    [Fact]
    public void MirroringUsesWhatTheOtherOneJustUsed()
    {
        Battler you = Make(10, Move(MirrorMove));
        Battler them = Make(200, Move(0x00, 60, id: 77));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.UsedInstead { Side: Side.Player, MoveId: 77 });

        // And it actually happened rather than being announced — the mirrored move does
        // damage, and the only thing on this field that does damage is theirs.
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    /// <summary>And when there is nothing to mirror it says so rather than doing something.</summary>
    [Fact]
    public void AndWhenThereIsNothingToMirrorItSaysSo()
    {
        Battler you = Make(200, Move(MirrorMove));
        Battler them = Make(10, Move(0x00, 60, id: 77));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// Mirroring something that itself borrows comes to nothing, and comes to nothing
    /// <em>once</em>.
    /// <para>
    /// This is the whole of the once-only rule, and it is the only place it can be seen. It
    /// used to be guarded twice — once here and once by a filter refusing to mirror a
    /// borrower at all — and breaking either changed nothing, because each was covered by
    /// the other. The filter is gone; this is the rule that remains, and it is the one that
    /// holds no matter what a future source of borrowed moves does.
    /// </para>
    /// </summary>
    [Fact]
    public void MirroringSomethingThatItselfBorrowsComesToNothingOnce()
    {
        Battler you = Make(10, Move(MirrorMove));
        Battler them = Make(200, Move(Metronome));

        var battle = new Battle(you, them, 7)
        {
            EveryMove = [Move(0x00, 60, id: 500)],
        };

        List<BattleEvent> events = Turn(battle);

        List<BattleEvent.UsedInstead> mine =
            [.. events.OfType<BattleEvent.UsedInstead>().Where(e => e.Side == Side.Player)];

        // Exactly one. The mirror reached for what they did, which was a borrower, and the
        // borrower did not go round again.
        Assert.Single(mine);

        Assert.False(battle.IsOver);
    }

    /// <summary>
    /// And two of them do not reflect each other for ever, which is the same rule seen from
    /// the side where getting it wrong is a stack that never comes back.
    /// </summary>
    [Fact]
    public void AndTwoOfThemDoNotReflectEachOtherForEver()
    {
        Battler you = Make(60, Move(MirrorMove));
        Battler them = Make(20, Move(MirrorMove));

        var battle = new Battle(you, them, 7);

        for (int turn = 0; turn < 3; turn++) Turn(battle);

        Assert.False(battle.IsOver);
    }

    // ---- at random -----------------------------------------------------------------------

    /// <summary>It picks something out of the whole table and uses it.</summary>
    [Fact]
    public void AtRandomPicksSomethingOutOfTheWholeTable()
    {
        Battler you = Make(200, Move(Metronome));
        Battler them = Make(10, Move(0x00, 0, id: 5));

        var battle = new Battle(you, them, 7)
        {
            EveryMove = [Move(0x00, 60, id: 200), Move(0x00, 60, id: 201)],
        };

        List<BattleEvent> events = Turn(battle);

        BattleEvent.UsedInstead used =
            events.OfType<BattleEvent.UsedInstead>().Single(e => e.Side == Side.Player);

        Assert.Contains(used.MoveId, (int[])[200, 201]);
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    /// <summary>
    /// A battle given no table finds nothing to pick and says so. The engine has never held
    /// the move table and this move is not the reason to start.
    /// </summary>
    [Fact]
    public void AndABattleWithNoTableFindsNothing()
    {
        Battler you = Make(200, Move(Metronome));
        Battler them = Make(10, Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// And it never picks another borrower, which is what stops it choosing itself and
    /// going round again.
    /// </summary>
    [Fact]
    public void AndItNeverPicksAnotherBorrower()
    {
        var picked = new List<int>();

        for (uint seed = 1; seed < 40; seed++)
        {
            Battler you = Make(200, Move(Metronome));
            Battler them = Make(10, Move(0x00, 0));

            var battle = new Battle(you, them, seed)
            {
                EveryMove = [Move(Metronome, 0, id: 300), Move(0x00, 10, id: 301)],
            };

            picked.AddRange(
                Turn(battle).OfType<BattleEvent.UsedInstead>().Select(e => e.MoveId));
        }

        Assert.NotEmpty(picked);
        Assert.DoesNotContain(300, picked);
    }

    // ---- talking in its sleep ---------------------------------------------------------------

    /// <summary>Only while asleep — which is the whole of the move rather than a condition on it.</summary>
    [Fact]
    public void TalkingInItsSleepNeedsToBeAsleep()
    {
        Battler awake = Make(200, Move(SleepTalk), Move(0x00, 60, id: 88));
        Battler them = Make(10, Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(awake, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>And asleep, it uses one of its own.</summary>
    [Fact]
    public void AndAsleepItUsesOneOfItsOwn()
    {
        Battler you = Make(200, Move(SleepTalk), Move(0x00, 60, id: 88));
        Battler them = Make(10, Move(0x00, 0));

        you.Status = StatusCondition.Sleep;
        you.SleepTurns = 5;

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.UsedInstead { Side: Side.Player, MoveId: 88 });

        // Asleep and still hitting, which is the only reason anybody carries it.
        Assert.True(them.CurrentHp < them.MaxHp);
    }

    // ---- taking a move ----------------------------------------------------------------------

    /// <summary>
    /// Taking a move puts it in the slot the taking came from. Something has to be given up,
    /// and the thing given up is the move that took it.
    /// </summary>
    [Theory]
    [InlineData(Mimic, false)]
    [InlineData(Sketch, true)]
    public void TakingAMovePutsItInTheSlotThatTookIt(int effect, bool forGood)
    {
        Battler you = Make(10, Move((byte)effect));
        Battler them = Make(200, Move(0x00, 40, id: 99));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(
            events,
            e => e is BattleEvent.LearnedMove { Side: Side.Player, MoveId: 99 } learned
                && learned.ForGood == forGood);

        Assert.Equal(99, you.MoveAt(0)?.Id);
    }

    /// <summary>And with nothing to take it says so.</summary>
    [Fact]
    public void AndWithNothingToTakeItSaysSo()
    {
        Battler you = Make(200, Move(Mimic));
        Battler them = Make(10, Move(0x00, 40, id: 99));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });

        Assert.Equal(Mimic, you.MoveAt(0)?.Effect);
    }

    // ---- moving an ability ------------------------------------------------------------------

    /// <summary>
    /// Taking an ability leaves the other one with theirs. The first thing in this engine
    /// that changes an ability at all.
    /// </summary>
    [Fact]
    public void TakingAnAbilityLeavesTheirs()
    {
        Battler you = new Battler(Species(200, ability: 0), 50);
        Battler them = new Battler(Species(10, ability: Abilities.Guts), 50);

        you.Moves.Add(Move(RolePlay));
        them.Moves.Add(Move(0x00, 0));

        List<BattleEvent> events = Turn(new Battle(you, them, 7));

        Assert.Contains(events, e => e is BattleEvent.AbilityMoved { Side: Side.Player });

        Assert.Equal(Abilities.Guts, you.Ability);
        Assert.Equal(Abilities.Guts, them.Ability);
    }

    /// <summary>And trading gives each of them the other's.</summary>
    [Fact]
    public void AndTradingGivesEachOfThemTheOthers()
    {
        Battler you = new Battler(Species(200, ability: 0), 50);
        Battler them = new Battler(Species(10, ability: Abilities.Guts), 50);

        you.Moves.Add(Move(SkillSwap));
        them.Moves.Add(Move(0x00, 0));

        Turn(new Battle(you, them, 7));

        Assert.Equal(Abilities.Guts, you.Ability);
        Assert.Equal(0, them.Ability);
    }

    /// <summary>
    /// And moving an ability onto somebody who already has it does nothing, rather than
    /// announcing a change that did not happen.
    /// </summary>
    [Fact]
    public void AndMovingAnAbilityNobodyGainsDoesNothing()
    {
        Battler you = new Battler(Species(200, ability: Abilities.Guts), 50);
        Battler them = new Battler(Species(10, ability: Abilities.Guts), 50);

        you.Moves.Add(Move(RolePlay));
        them.Moves.Add(Move(0x00, 0));

        Assert.Contains(
            Turn(new Battle(you, them, 7)),
            e => e is BattleEvent.NothingHappened { Side: Side.Player });
    }

    /// <summary>
    /// A borrowed ability is a fact about the fight, not about the creature — so what it was
    /// born with is untouched underneath it.
    /// </summary>
    [Fact]
    public void AndABorrowedAbilityDoesNotChangeWhatItWasBornWith()
    {
        Battler you = new Battler(Species(200, ability: 0), 50);

        Assert.Equal(0, you.AbilitySlot);

        you.BorrowedAbility = Abilities.Guts;

        Assert.Equal(Abilities.Guts, you.Ability);
        Assert.Equal(0, you.AbilitySlot);

        you.ForgetWhatWasStarted();

        Assert.Equal(0, you.Ability);
    }
}
