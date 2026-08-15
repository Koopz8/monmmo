using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A list of people, and where they are right now.
/// <para>
/// Two halves that live in different places on purpose. Who is on the list is a fact about
/// an account and belongs on the disk; whether they are online is a fact about this second
/// and belongs to the world. A list that stored "online" would be a list that is wrong the
/// moment anybody closes the game.
/// </para>
/// <para>
/// One-directional, which is the decision worth arguing about. A mutual list needs a
/// request, an acceptance, a refusal and somewhere for a pending one to sit, and buys
/// nothing the thing is for — and a list that worked both ways the instant one person added
/// the other would tell strangers when you are online because they typed your name.
/// </para>
/// </summary>
public class FriendTests
{
    private const string Town = "1.0";

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static async Task<long> RegisterAsync(SqlitePlayerStore store, string name)
    {
        var made = Assert.IsType<AuthOutcome.Success>(await store.RegisterAsync(
            name, "a-good-password", new SavedCharacter(Town, 3, 4, Direction.Down, [])));

        return made.Account.Id;
    }

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text));

    // ---- the list itself ----------------------------------------------------------------

    [Fact]
    public async Task SomebodyAddedStaysOnTheList()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");

            Assert.True(await store.BefriendAsync(mason, "Koop"));

            Assert.Equal("Koop", Assert.Single(await store.FriendsAsync(mason)).Name);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And it survives a restart, which is the only thing that makes it a list rather than
    /// a note.
    /// </summary>
    [Fact]
    public async Task AndSurvivesARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            long mason;

            using (var store = new SqlitePlayerStore(path))
            {
                mason = await RegisterAsync(store, "Mason");
                await RegisterAsync(store, "Koop");

                await store.BefriendAsync(mason, "Koop");
            }

            using var reopened = new SqlitePlayerStore(path);

            Assert.Equal("Koop", Assert.Single(await reopened.FriendsAsync(mason)).Name);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// Whatever capitals were read off the top of a head. The same folding logging in uses,
    /// because it is the same question — which account plays under this name.
    /// </summary>
    [Fact]
    public async Task AndTheCapitalsDoNotMatter()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");

            Assert.True(await store.BefriendAsync(mason, "kOOp"));
            Assert.Equal("Koop", Assert.Single(await store.FriendsAsync(mason)).Name);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task NobodyIsAddedTwiceAndNobodyIsTheirOwnFriend()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");

            Assert.True(await store.BefriendAsync(mason, "Koop"));
            Assert.False(await store.BefriendAsync(mason, "Koop"));

            Assert.False(await store.BefriendAsync(mason, "Mason"));
            Assert.False(await store.BefriendAsync(mason, "Nobody"));

            Assert.Single(await store.FriendsAsync(mason));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>
    /// And it is one-directional: adding somebody tells them nothing and gives them
    /// nothing. That is the point rather than an omission.
    /// </summary>
    [Fact]
    public async Task AndItGoesOneWay()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            long mason = await RegisterAsync(store, "Mason");
            long koop = await RegisterAsync(store, "Koop");

            await store.BefriendAsync(mason, "Koop");

            Assert.Single(await store.FriendsAsync(mason));
            Assert.Empty(await store.FriendsAsync(koop));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task SomebodyRemovedIsOffIt()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");

            await store.BefriendAsync(mason, "Koop");

            Assert.True(await store.ForgetAsync(mason, "Koop"));
            Assert.Empty(await store.FriendsAsync(mason));

            // And removing them again says so rather than pretending.
            Assert.False(await store.ForgetAsync(mason, "Koop"));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- and where they are -------------------------------------------------------------

    /// <summary>
    /// The half the disk cannot answer. Somebody on the list who is online is reported with
    /// where they are, and somebody who is not is away — asked of the world at the instant
    /// the question is put, because that is the only instant the answer is true for.
    /// </summary>
    [Fact]
    public async Task TheListSaysWhoIsOnAndWhere()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var friends = new Friends(store);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");
            await RegisterAsync(store, "Ash");

            await store.BefriendAsync(mason, "Koop");
            await store.BefriendAsync(mason, "Ash");

            (ServerPlayer player, _) = world.Join(mason, "Mason", SavedCharacter.Fresh(Town, 3, 4));

            // Only one of them has actually logged in.
            world.Join(2, "Koop", SavedCharacter.Fresh(Town, 4, 4));

            string listed = Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/friends")));

            Assert.Contains("Koop", listed);
            Assert.Contains("PALLET TOWN", listed);
            Assert.Contains("/with Koop", listed);

            // And the one who is not on says so rather than being left off.
            Assert.Contains("Ash", listed);
            Assert.Contains("away", listed);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    [Fact]
    public async Task AnEmptyListSaysHowToStartOne()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var friends = new Friends(store);

            long mason = await RegisterAsync(store, "Mason");

            (ServerPlayer player, _) = world.Join(mason, "Mason", SavedCharacter.Fresh(Town, 3, 4));

            Assert.Contains(
                "/friend",
                Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/friends"))));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>And the console says what happened rather than only doing it.</summary>
    [Fact]
    public async Task TheConsoleSaysWhatHappened()
    {
        string path = TempDatabase.Path();

        try
        {
            using var store = new SqlitePlayerStore(path);

            GameWorld world = World();
            var friends = new Friends(store);

            long mason = await RegisterAsync(store, "Mason");
            await RegisterAsync(store, "Koop");

            (ServerPlayer player, _) = world.Join(mason, "Mason", SavedCharacter.Fresh(Town, 3, 4));

            Assert.Contains(
                "on your list",
                Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/friend Koop"))));

            Assert.Contains(
                "nobody called",
                Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/friend Nobody"))));

            Assert.Contains(
                "off your list",
                Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/unfriend Koop"))));

            Assert.Contains(
                "/friend <player name>",
                Said(await friends.RunAsync(world, player.Id, mason, ConsoleLine.Of("/friend"))));
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    /// <summary>The verbs this takes, and the ones it leaves alone.</summary>
    [Fact]
    public void ItTakesItsOwnVerbs()
    {
        Assert.True(Friends.Handles("friend"));
        Assert.True(Friends.Handles("unfriend"));
        Assert.True(Friends.Handles("friends"));

        Assert.False(Friends.Handles("with"));
        Assert.False(Friends.Handles("market"));
        Assert.False(Friends.Handles(""));
    }
}
