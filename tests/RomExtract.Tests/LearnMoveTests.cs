using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The question a full move list asks, and what a client is allowed to answer.
/// <para>
/// A level-up that produced a fifth move used to end in a shrug: the move was
/// announced and dropped, because nothing could ask which of the four to lose. The
/// asking lives on the client and the deciding lives here, which makes the interesting
/// claim not "the menu works" but "the menu cannot be used to teach anything". A client
/// can only close a question this side has already opened.
/// </para>
/// </summary>
public class LearnMoveTests
{
    private const string Route = "3.19";

    /// <summary>
    /// A map that is nothing but grass, with something small in it.
    /// <para>
    /// Small on purpose. The creature doing the levelling is one level short of the
    /// level TestRules teaches at, and a fight it loses teaches nothing at all.
    /// </para>
    /// </summary>
    private static GameWorld GrassyWorld(uint seed = 11)
    {
        var behaviours = new byte[16];
        Array.Fill(behaviours, MetatileBehaviour.TallGrass);

        MapData map = new(Route, "ROUTE 1", 4, 4, new byte[16])
        {
            Behaviours = behaviours,
            Encounters = new MapEncounters(Route, Land: new EncounterTable(
                EncounterKind.Land,
                100,
                Enumerable.Range(0, 12).Select(_ => new WildSlot(16, 1, 1)).ToList())),
        };

        return new GameWorld(new WorldData([map]), Route, TestRules.All, seed);
    }

    /// <summary>
    /// Somebody with no room left, one win short of the level that teaches.
    /// <para>
    /// The experience is set just under the threshold rather than left at zero so that
    /// a single small win crosses it. Four distinct move ids, all of them ones this
    /// rules file knows about: an id nothing recognises is dropped on the way into a
    /// battle, and the party comes back rebuilt from the learnset.
    /// </para>
    /// </summary>
    private static SavedMon Crowded() =>
        new(1, 2, null, 14, StatusCondition.None, Nature.Hardy, [1, 3, 4, 5],
            Experience.TotalForLevel(GrowthRate.MediumFast, 3) - 1);

    /// <summary>Wins a fight with somebody who has no room for what winning teaches.</summary>
    private static (GameWorld World, ServerPlayer Player) AfterALevelUp()
    {
        GameWorld world = GrassyWorld();

        (ServerPlayer player, _) = world.Join(1, "Mason", TestRules.Equipped(world));

        player.Party[0] = Crowded();

        double now = 0;

        for (int step = 0; step < 200 && player.Battle is null; step++)
        {
            player.Square = new GridPosition(step % 4, 1);
            player.LastStepAt = double.NegativeInfinity;
            now += 1;

            world.Move(player.Id, Direction.Down, now);
        }

        Assert.NotNull(player.Battle);

        for (int turn = 0; turn < 40 && player.Battle is not null; turn++)
            world.TakeBattleTurn(player.Id, new BattleAction.UseMove(0));

        Assert.Null(player.Battle);
        Assert.True(player.Party[0].Level >= 3, "the fight was lost, so nothing was learned");

        return (world, player);
    }

    [Fact]
    public void AMoveThatCannotFitIsRememberedAsAQuestion()
    {
        // The point of the list. Without it the offer is gone by the time the player
        // has finished reading the fight, and an answer arriving after the battle has
        // closed would have nothing left to be an answer to.
        (_, ServerPlayer player) = AfterALevelUp();

        (int slot, int moveId, int fromItem) = Assert.Single(player.MovesOffered);

        Assert.Equal(0, slot);
        Assert.Equal(TestRules.TaughtMove, moveId);

        // Nought, because nothing was used to produce it: this offer came off a level.
        Assert.Equal(0, fromItem);

        // And nothing has happened to the four yet.
        Assert.Equal(new[] { 1, 3, 4, 5 }, player.Party[0].Moves);
    }

    [Fact]
    public void NobodyIsTaughtAMoveNobodyOfferedThem()
    {
        // The whole reason this is a list rather than a move id. A client sending any
        // move it likes is asking to be taught something, and the answer to that is no:
        // the only thing it can do is close a question already open.
        (GameWorld world, ServerPlayer player) = AfterALevelUp();

        Assert.Empty(world.LearnMove(player.Id, moveId: 999, forget: 0));

        Assert.Equal("refused: nobody was offered move 999", world.LastLearned);
        Assert.Equal(new[] { 1, 3, 4, 5 }, player.Party[0].Moves);

        // And the real question is still open, so refusing a forgery costs nothing.
        Assert.Single(player.MovesOffered);
    }

    [Fact]
    public void KeepingWhatYouHaveIsAnAnswer()
    {
        // The games let you walk away from the question, and walking away has to close
        // it — otherwise the same offer is still standing at the next level, and the
        // move you already declined is offered again on somebody else's win.
        (GameWorld world, ServerPlayer player) = AfterALevelUp();

        Assert.Empty(world.LearnMove(player.Id, TestRules.TaughtMove, forget: -1));

        Assert.Equal($"move {TestRules.TaughtMove} was not learned", world.LastLearned);
        Assert.Equal(new[] { 1, 3, 4, 5 }, player.Party[0].Moves);
        Assert.Empty(player.MovesOffered);
    }

    [Fact]
    public void ChoosingOneToDropReplacesThatOneAndNoOther()
    {
        (GameWorld world, ServerPlayer player) = AfterALevelUp();

        List<Outgoing> send = world.LearnMove(player.Id, TestRules.TaughtMove, forget: 1);

        Assert.Equal($"forgot move 3 and learned {TestRules.TaughtMove}", world.LastLearned);
        Assert.Equal(new[] { 1, TestRules.TaughtMove, 4, 5 }, player.Party[0].Moves);
        Assert.Empty(player.MovesOffered);

        // The party the client is holding is now wrong, so it is sent the new one — and
        // only to them, because nobody else's screen changed.
        Outgoing only = Assert.Single(send);

        Assert.Equal(player.Id, only.OnlyTo);

        var updated = Assert.IsType<BagUpdated>(only.Message);

        Assert.Equal(new[] { 1, TestRules.TaughtMove, 4, 5 }, Assert.Single(updated.Party).Moves);
    }

    [Fact]
    public void AQuestionCanOnlyBeAnsweredOnce()
    {
        // Answering spends the offer. A client sending the same answer twice would
        // otherwise forget a second move to make room for one already known.
        (GameWorld world, ServerPlayer player) = AfterALevelUp();

        world.LearnMove(player.Id, TestRules.TaughtMove, forget: 0);

        Assert.Equal(new[] { TestRules.TaughtMove, 3, 4, 5 }, player.Party[0].Moves);

        Assert.Empty(world.LearnMove(player.Id, TestRules.TaughtMove, forget: 1));

        Assert.Equal($"refused: nobody was offered move {TestRules.TaughtMove}", world.LastLearned);
        Assert.Equal(new[] { TestRules.TaughtMove, 3, 4, 5 }, player.Party[0].Moves);
    }

    [Fact]
    public void AnAnswerFromSomebodyWhoIsNotHereIsIgnored()
    {
        // A check that cannot fail is not a check: this is why LearnMove starts by
        // looking the player up rather than trusting the id it was handed.
        GameWorld world = GrassyWorld();

        Assert.Empty(world.LearnMove(playerId: 77, TestRules.TaughtMove, forget: 0));
    }
}

/// <summary>
/// What a counter is allowed to take off a party member.
/// <para>
/// Found by playing rather than by reading: three fights in a row, healing between
/// each, and the creature was still one win from the same level it had been one win
/// from at the start. Nothing failed. The payout was announced every time, the number
/// went up every time, and every heal quietly put it back at the bottom of the level
/// it was already on.
/// </para>
/// <para>
/// The cause is a rebuild. Health is computed from base stats, so healing goes through
/// a battler and comes back as a fresh record — and a battler has no experience to
/// bring back with it.
/// </para>
/// </summary>
public class RestingTests
{
    private static SavedMon HalfWay() =>
        new(1, 5, null, 3, StatusCondition.Poison, Nature.Hardy, [1], 200);

    [Fact]
    public void ACounterGivesBackHealthAndNothingElse()
    {
        SavedMon healed = new BattleFactory(TestRules.All).Healed(HalfWay());

        Assert.True(healed.CurrentHp > 3, "no health went back on");
        Assert.Equal(StatusCondition.None, healed.Status);

        Assert.Equal(200, healed.Experience);
        Assert.Equal(5, healed.Level);
        Assert.Equal(new[] { 1 }, healed.Moves);
    }

    [Fact]
    public void APotionCostsNoProgressEither()
    {
        // The same rebuild, reached by a different door. Fixing one and not the other
        // would leave the bug in the half of the game that is used more often.
        ItemData potion = Assert.IsType<ItemData>(TestRules.All.ItemAt(TestRules.PotionItem));

        (SavedMon mon, int restored) = new BattleFactory(TestRules.All).Restored(HalfWay(), potion);

        Assert.True(restored > 0, "no health went back on");
        Assert.Equal(200, mon.Experience);
    }

    [Fact]
    public void TwoWinsAreWorthMoreThanOneEvenWithACounterInBetween()
    {
        // The claim as a player would put it. A test against Healed alone would have
        // passed the day the bug was written, because the bug was not in the healing —
        // it was in what the next payout thought it was adding to.
        var progression = new Progression(TestRules.All);
        var factory = new BattleFactory(TestRules.All);

        // Far enough up the curve that neither win crosses a level. A test where the
        // level rises passes with the bug still in it: the reset lands on the bottom of
        // the new level, which is higher than the bottom of the old one, and the number
        // goes up for the wrong reason.
        SavedMon member = new(1, 30, null, 1, StatusCondition.None, Nature.Hardy, [1],
            Experience.TotalForLevel(GrowthRate.MediumFast, 30));

        (SavedMon once, _) = progression.Award(member, 16, 3);

        SavedMon rested = factory.Healed(once);

        (SavedMon twice, _) = progression.Award(rested, 16, 3);

        Assert.Equal(30, twice.Level);

        Assert.True(twice.Experience > once.Experience,
            $"the second win left it on {twice.Experience}, the first on {once.Experience}");
    }
}
