using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The ladder: what a duel was worth.
/// <para>
/// Elo, and picked because it is the only rating system where somebody can be told why their
/// number moved by the amount it did. Everything here is modelled — this is a rule about a
/// competition rather than about a game, and no cartridge has an opinion on it — so what
/// these tests check is that the arithmetic is the arithmetic, and that the two halves of a
/// result always add up.
/// </para>
/// <para>
/// One rating per player per band, which is the decision worth defending. A single number
/// would be the average of two abilities that never meet, and it would let a strong party
/// farm the bottom of the ladder — which it cannot, because it is not on that ladder.
/// </para>
/// </summary>
public class LadderTests
{
    private const string Town = "1.0";

    private static SavedCharacter Fresh() =>
        new(Town, 3, 4, Direction.Down, [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [1])]);

    private static async Task<long> AccountAsync(SqlitePlayerStore store, string name)
    {
        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", Fresh()));

        return made.Account.Id;
    }

    // ---- the arithmetic ---------------------------------------------------------------

    [Fact]
    public void TwoEqualPlayersAreExpectedToWinHalfEach()
    {
        Assert.Equal(0.5, Elo.Expected(1_000, 1_000), 6);

        // And the two expectations of any pair always add to one, which is the property the
        // whole system rests on and the one that makes a result zero-sum.
        Assert.Equal(1.0, Elo.Expected(1_400, 1_000) + Elo.Expected(1_000, 1_400), 6);
    }

    [Fact]
    public void TheScaleIsTheDifferenceThatMakesItTenToOne()
    {
        // Four hundred is not a tuning knob — it is the unit the whole system is measured
        // in, and this is what it means.
        Assert.Equal(10.0 / 11.0, Elo.Expected(1_000 + Elo.Scale, 1_000), 3);
    }

    [Fact]
    public void AnUpsetMovesMoreThanAnExpectedWin()
    {
        int easy = Elo.After(1_400, 1_000, won: true) - 1_400;
        int upset = Elo.After(1_000, 1_400, won: true) - 1_000;

        Assert.True(upset > easy, $"{upset} should be more than {easy}");
        Assert.True(easy > 0, "a win is never worth nothing");
        Assert.True(upset <= Elo.Swing, "and nothing is worth more than the swing");
    }

    /// <summary>
    /// A heavily favoured player who wins is owed a fraction of a point, and gets one — a
    /// system that rounded it away would be one where the top of the ladder stops moving.
    /// </summary>
    [Fact]
    public void AWinIsNeverWorthNothingEvenAgainstNobody()
    {
        Assert.True(Elo.After(3_000, 0, won: true) > 3_000);
        Assert.True(Elo.After(3_000, 0, won: false) < 3_000);
    }

    [Fact]
    public void NobodyGoesBelowNothing()
    {
        Assert.Equal(0, Elo.After(0, 3_000, won: false));

        // And it stays there however many times somebody loses, rather than going negative
        // — a negative rating says nothing a nought does not and reads as a bug to whoever
        // has one.
        int rating = 20;

        for (int lost = 0; lost < 50; lost++) rating = Elo.After(rating, 3_000, won: false);

        Assert.Equal(0, rating);
    }

    // ---- the store ---------------------------------------------------------------------

    [Fact]
    public async Task EverybodyStartsOnTheSameNumberWhetherOrNotTheyHavePlayed()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");

        Rung mine = await store.StandingAsync(mason, band: 2);

        Assert.Equal(Elo.Starting, mine.Rating);
        Assert.Equal(0, mine.Played);
        Assert.Equal("Mason", mine.Name);

        // And a band nobody has fought in is empty rather than full of everybody on nothing.
        Assert.Empty(await store.TopAsync(2));
    }

    /// <summary>
    /// One result, and the two halves of it add up: what the winner gained is what the loser
    /// lost. That is what makes a ladder a comparison rather than a scoreboard.
    /// </summary>
    [Fact]
    public async Task WhatOneGainsTheOtherLoses()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");

        (int winner, int loser) = await store.RecordAsync(mason, koop, band: 2);

        Assert.Equal(winner - Elo.Starting, Elo.Starting - loser);
        Assert.True(winner > Elo.Starting);
        Assert.True(loser < Elo.Starting);

        Rung theirs = await store.StandingAsync(koop, 2);

        Assert.Equal(loser, theirs.Rating);
        Assert.Equal(0, theirs.Won);
        Assert.Equal(1, theirs.Lost);
    }

    /// <summary>
    /// Both are worked out against the pair as it stood before either moved.
    /// <para>
    /// The reason the whole thing is one transaction. Updating the winner and then reading
    /// the loser would compute the second half against a rating that has already been paid,
    /// and the two halves would no longer add up — silently, and by a few points at a time.
    /// </para>
    /// </summary>
    [Fact]
    public async Task NeitherIsRatedAgainstTheOtherAfterItHasAlreadyMoved()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");

        // A lopsided pair, so a rating computed against a moved one is off by an amount a
        // test can see rather than by a rounding.
        await store.RecordAsync(mason, koop, band: 2);
        await store.RecordAsync(mason, koop, band: 2);
        await store.RecordAsync(mason, koop, band: 2);

        Rung strong = await store.StandingAsync(mason, 2);
        Rung weak = await store.StandingAsync(koop, 2);

        Assert.Equal(2 * Elo.Starting, strong.Rating + weak.Rating);
        Assert.Equal(3, strong.Won);
        Assert.Equal(3, weak.Lost);
    }

    /// <summary>
    /// A rating in one band says nothing about another, which is what stops a strong party
    /// farming the bottom of the ladder: it is not on that ladder.
    /// </summary>
    [Fact]
    public async Task ARatingInOneBandIsNothingInAnother()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");

        await store.RecordAsync(mason, koop, band: 4);

        Assert.True((await store.StandingAsync(mason, 4)).Rating > Elo.Starting);
        Assert.Equal(Elo.Starting, (await store.StandingAsync(mason, 0)).Rating);

        // Both of them are on the band they fought in, and neither is on any other.
        Assert.Equal(2, (await store.TopAsync(4)).Count);
        Assert.Empty(await store.TopAsync(0));

        // And a fight in a second band starts from the beginning rather than from what
        // they earned in the first. This is the half a reader would assume and the half
        // that is easiest to write wrongly: the rating a result is computed against has to
        // be looked up per band, not per account.
        (int lower, _) = await store.RecordAsync(mason, koop, band: 0);

        Assert.Equal(Elo.After(Elo.Starting, Elo.Starting, won: true), lower);
    }

    [Fact]
    public async Task TheBoardIsBestFirst()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");
        long ash = await AccountAsync(store, "Ash");

        await store.RecordAsync(mason, koop, band: 1);
        await store.RecordAsync(mason, ash, band: 1);
        await store.RecordAsync(koop, ash, band: 1);

        IReadOnlyList<Rung> top = await store.TopAsync(1);

        Assert.Equal(3, top.Count);
        Assert.Equal("Mason", top[0].Name);
        Assert.Equal("Ash", top[^1].Name);

        Assert.True(top[0].Rating >= top[1].Rating);
        Assert.True(top[1].Rating >= top[2].Rating);
    }

    [Fact]
    public async Task AndItIsAllStillThereAfterARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            long mason;
            int after;

            using (var store = new SqlitePlayerStore(path))
            {
                mason = await AccountAsync(store, "Mason");
                long koop = await AccountAsync(store, "Koop");

                (after, _) = await store.RecordAsync(mason, koop, band: 3);
            }

            using var reopened = new SqlitePlayerStore(path);

            Assert.Equal(after, (await reopened.StandingAsync(mason, 3)).Rating);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- the console -------------------------------------------------------------------

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text));

    private static async Task<(ServerPlayer Player, long AccountId)> ArriveAsync(
        SqlitePlayerStore store, GameWorld world, string name)
    {
        SavedCharacter fresh = Fresh();

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", fresh));

        (ServerPlayer player, _) = world.Join(made.Account.Id, name, fresh);

        return (player, made.Account.Id);
    }

    [Fact]
    public async Task AskingForARatingBeforeEverPlayingSaysSo()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var ladder = new Ladder(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        string reply = Said(
            await ladder.RunAsync(world, mason.Id, masonId, ConsoleLine.Of("/rating")));

        Assert.Contains($"{Elo.Starting}", reply);
        Assert.Contains("everybody starts here", reply);
    }

    [Fact]
    public async Task AnEmptyBandSaysNobodyHasFoughtInIt()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var ladder = new Ladder(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        Assert.Contains(
            "nobody has fought",
            Said(await ladder.RunAsync(world, mason.Id, masonId, ConsoleLine.Of("/ladder 4"))));
    }

    /// <summary>
    /// A finished duel is written down, and both sides are told the same two numbers.
    /// </summary>
    [Fact]
    public async Task AFinishedDuelReachesTheBoardAndBothSidesAreTold()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var ladder = new Ladder(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");
        (ServerPlayer koop, long koopId) = await ArriveAsync(store, world, "Koop");

        List<Outgoing> told = await ladder.RecordAsync(world, new DuelResult(masonId, koopId, 2));

        var to = told.Select(o => o.OnlyTo).ToList();

        Assert.Contains(mason.Id, to);
        Assert.Contains(koop.Id, to);

        IReadOnlyList<Rung> board = await store.TopAsync(2);

        Assert.Equal(2, board.Count);
        Assert.Equal("Mason", board[0].Name);

        // And a second call with the same result would pay twice, which is why the world
        // hands a result over rather than leaving one to be read.
        Assert.Contains("Seasoned", Said(told));
    }

    [Fact]
    public async Task ARatingIsWrittenEvenWhenNobodyIsThereToRead()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var ladder = new Ladder(store);

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");

        // Neither of them has joined the world, which is what a disconnect mid-duel looks
        // like from here. The disk is what the ladder is.
        Assert.Empty(await ladder.RecordAsync(world, new DuelResult(mason, koop, 1)));

        Assert.Equal(2, (await store.TopAsync(1)).Count);
    }

    /// <summary>
    /// Every verb this class claims is one it can answer, and nothing it does not claim
    /// reaches it.
    /// </summary>
    [Fact]
    public void ItClaimsExactlyTheVerbsItAnswers()
    {
        Assert.True(Ladder.Handles("ladder"));
        Assert.True(Ladder.Handles("rating"));

        Assert.False(Ladder.Handles("tier"));
        Assert.False(Ladder.Handles("guild"));
        Assert.False(Ladder.Handles("market"));
    }
}
