using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using PokeMmo.Server.Storage;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Guilds: a named group, and the only thing in this project that talks to people by who
/// they are rather than by where they are standing.
/// <para>
/// The rule everything below turns on is that somebody is in at most one guild, and it is
/// the membership table's own key rather than a check anywhere. A check is something two
/// acceptances in the same instant can both pass, and two at once is exactly what happens
/// when somebody who has been asked by two guilds says yes twice.
/// </para>
/// <para>
/// The other half is the chat. A room hears you because it is a room and stops at the edge
/// of a copy of it; a guild hears you because it is a guild, and its members may be on
/// different continents in different instances. That is the whole reason a guild is worth
/// having on top of a friends list.
/// </para>
/// </summary>
public class GuildTests
{
    private const string Town = "1.0";

    private static SavedCharacter Fresh() =>
        new(Town, 3, 4, Direction.Down, [new SavedMon(1, 5, null, 19, StatusCondition.None, Nature.Hardy, [1])]);

    private static GameWorld World() =>
        new(new WorldData([new MapData(Town, "PALLET TOWN", 8, 8, new byte[64])]), Town, TestRules.All);

    private static async Task<long> AccountAsync(SqlitePlayerStore store, string name)
    {
        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", Fresh()));

        return made.Account.Id;
    }

    private static async Task<(ServerPlayer Player, long AccountId)> ArriveAsync(
        SqlitePlayerStore store, GameWorld world, string name)
    {
        SavedCharacter fresh = Fresh();

        var made = Assert.IsType<AuthOutcome.Success>(
            await store.RegisterAsync(name, "a-good-password", fresh));

        (ServerPlayer player, _) = world.Join(made.Account.Id, name, fresh);

        return (player, made.Account.Id);
    }

    private static string Said(IEnumerable<Outgoing> from) =>
        string.Join("\n", from.Select(o => o.Message).OfType<ConsoleReply>().Select(r => r.Text));

    private static Task<List<Outgoing>> RunAsync(
        Guilds guilds, GameWorld world, ServerPlayer player, long accountId, string text) =>
        guilds.RunAsync(world, player.Id, accountId, ConsoleLine.Of(text));

    // ---- what a name is ----------------------------------------------------------------

    /// <summary>
    /// A guild name is narrower than a player's own, and deliberately.
    /// <para>
    /// It is shown beside other people's names in a chat line, so a name made of spaces or
    /// punctuation is a name that can be made to look like somebody else's.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Team Rocket", true)]
    [InlineData("R42", true)]
    [InlineData("ab", false)]
    [InlineData("a name that is very much too long", false)]
    [InlineData(" leading", false)]
    [InlineData("trailing ", false)]
    [InlineData("two  spaces", false)]
    [InlineData("punctuation!", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void WhatCountsAsAGuildName(string? name, bool allowed) =>
        Assert.Equal(allowed, Guild.IsAName(name));

    // ---- the store ---------------------------------------------------------------------

    [Fact]
    public async Task FoundingOneMakesTheFounderItsLeader()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");

        Guild made = Assert.IsType<Guild>(await store.FoundAsync(mason, "Team Rocket"));

        Assert.Equal("Team Rocket", made.Name);
        Assert.Equal(1, made.Members);

        GuildMember only = Assert.Single(await store.MembersAsync(made.Id));

        Assert.Equal("Mason", only.Name);
        Assert.True(only.IsLeader);

        Assert.Equal(made.Id, (await store.OfAsync(mason))?.Id);
    }

    [Fact]
    public async Task ANameIsTakenOnlyOnceAndCapitalsDoNotMakeANewOne()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));

        // Different capitals, same name in a chat line, so the same name here.
        Assert.Null(await store.FoundAsync(koop, "TEAM ROCKET"));

        Assert.Single(await store.AllAsync());
    }

    /// <summary>
    /// Nobody is in two, and it is the table that says so rather than a check.
    /// </summary>
    [Fact]
    public async Task NobodyIsInTwoGuilds()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
        Assert.Null(await store.FoundAsync(mason, "Team Aqua"));

        // And the second name was not quietly used up on the way to being refused.
        Assert.Equal("Team Rocket", Assert.Single(await store.AllAsync()).Name);
    }

    /// <summary>
    /// Two acceptances at once, and exactly one of them counts.
    /// <para>
    /// The race the membership key exists for. Not a probabilistic test of the outcome —
    /// whichever wins is fine — but of the one thing that must be true afterwards: one
    /// person, one guild.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TwoInvitationsAcceptedAtOnceMeanOneGuild()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");
        long asked = await AccountAsync(store, "Ash");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
        Assert.NotNull(await store.FoundAsync(koop, "Team Aqua"));

        Assert.True(await store.InviteAsync(mason, "Ash"));
        Assert.True(await store.InviteAsync(koop, "Ash"));

        Task<Guild?> first = store.AcceptAsync(asked, "Team Rocket");
        Task<Guild?> second = store.AcceptAsync(asked, "Team Aqua");

        Guild?[] both = await Task.WhenAll(first, second);

        Assert.Single(both.Where(g => g is not null));

        Assert.NotNull(await store.OfAsync(asked));

        // And the guild they are in has two people in it, not one and a half.
        Assert.Equal(2, (await store.OfAsync(asked))!.Members);
    }

    [Fact]
    public async Task AnInvitationIsGoneOnceItHasBeenTakenUp()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long koop = await AccountAsync(store, "Koop");
        long ash = await AccountAsync(store, "Ash");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
        Assert.NotNull(await store.FoundAsync(koop, "Team Aqua"));

        Assert.True(await store.InviteAsync(mason, "Ash"));
        Assert.True(await store.InviteAsync(koop, "Ash"));

        Assert.Equal(2, (await store.InvitationsAsync(ash)).Count);

        Assert.NotNull(await store.AcceptAsync(ash, "Team Rocket"));

        // Every one of them, not only the one taken up — somebody who has joined is not
        // still being asked.
        Assert.Empty(await store.InvitationsAsync(ash));
    }

    [Fact]
    public async Task OnlyALeaderMayAskAnybody()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long ash = await AccountAsync(store, "Ash");
        await AccountAsync(store, "Misty");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
        Assert.True(await store.InviteAsync(mason, "Ash"));
        Assert.NotNull(await store.AcceptAsync(ash, "Team Rocket"));

        // In it, and not leading it.
        Assert.False(await store.InviteAsync(ash, "Misty"));

        // And nobody who is already in one can be asked into another.
        Assert.False(await store.InviteAsync(mason, "Ash"));
    }

    /// <summary>
    /// A leader who leaves hands over to whoever has been in it longest, rather than being
    /// refused — a guild whose leader stopped playing would otherwise be a guild nobody can
    /// ever invite anybody to.
    /// </summary>
    [Fact]
    public async Task ALeaderWhoLeavesHandsOverToTheLongestServing()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long ash = await AccountAsync(store, "Ash");
        long misty = await AccountAsync(store, "Misty");

        Guild made = Assert.IsType<Guild>(await store.FoundAsync(mason, "Team Rocket"));

        Assert.True(await store.InviteAsync(mason, "Ash"));
        Assert.NotNull(await store.AcceptAsync(ash, "Team Rocket"));

        Assert.True(await store.InviteAsync(mason, "Misty"));
        Assert.NotNull(await store.AcceptAsync(misty, "Team Rocket"));

        Assert.True(await store.LeaveAsync(mason));

        IReadOnlyList<GuildMember> left = await store.MembersAsync(made.Id);

        Assert.Equal(2, left.Count);

        GuildMember leader = Assert.Single(left.Where(m => m.IsLeader));

        Assert.Equal("Ash", leader.Name);
    }

    [Fact]
    public async Task TheLastOneOutTakesTheGuildWithThem()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");

        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
        Assert.True(await store.LeaveAsync(mason));

        Assert.Empty(await store.AllAsync());
        Assert.Null(await store.OfAsync(mason));

        // And the name is free again, which is the point of not leaving an empty one behind.
        Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
    }

    [Fact]
    public async Task OnlyALeaderPutsSomebodyOutAndNeverThemselves()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        long mason = await AccountAsync(store, "Mason");
        long ash = await AccountAsync(store, "Ash");

        Guild made = Assert.IsType<Guild>(await store.FoundAsync(mason, "Team Rocket"));

        Assert.True(await store.InviteAsync(mason, "Ash"));
        Assert.NotNull(await store.AcceptAsync(ash, "Team Rocket"));

        Assert.False(await store.KickAsync(ash, "Mason"));
        Assert.False(await store.KickAsync(mason, "Mason"));

        Assert.True(await store.KickAsync(mason, "Ash"));

        Assert.Equal("Mason", Assert.Single(await store.MembersAsync(made.Id)).Name);
        Assert.Null(await store.OfAsync(ash));
    }

    [Fact]
    public async Task AndItIsAllStillThereAfterARestart()
    {
        string path = TempDatabase.Path();

        try
        {
            long mason;

            using (var store = new SqlitePlayerStore(path))
            {
                mason = await AccountAsync(store, "Mason");

                Assert.NotNull(await store.FoundAsync(mason, "Team Rocket"));
            }

            using var reopened = new SqlitePlayerStore(path);

            Assert.Equal("Team Rocket", (await reopened.OfAsync(mason))?.Name);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }

    // ---- the console -------------------------------------------------------------------

    [Fact]
    public async Task FoundingAndShowingThroughTheConsole()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        Assert.Contains("no guild", Said(await RunAsync(guilds, world, mason, masonId, "/guild")));

        Assert.Contains(
            "Team Rocket exists",
            Said(await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket")));

        string shown = Said(await RunAsync(guilds, world, mason, masonId, "/guild"));

        Assert.Contains("Team Rocket", shown);
        Assert.Contains("leader", shown);

        // Where they are, because a guild list that could not tell you that would be a
        // friends list with extra steps.
        Assert.Contains("PALLET TOWN", shown);
    }

    [Fact]
    public async Task ASecondGuildIsRefusedWithAReason()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");

        Assert.Contains(
            "already in Team Rocket",
            Said(await RunAsync(guilds, world, mason, masonId, "/guild Team Aqua")));
    }

    [Fact]
    public async Task AnInvitationIsOfferedAndTakenUpThroughTheConsole()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");
        (ServerPlayer ash, long ashId) = await ArriveAsync(store, world, "Ash");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");

        Assert.Contains("Ash has been asked", Said(await RunAsync(guilds, world, mason, masonId, "/invite Ash")));

        // Somebody with an invitation and no guild is shown what is on offer rather than
        // being told they have nothing.
        Assert.Contains("/join Team Rocket", Said(await RunAsync(guilds, world, ash, ashId, "/guild")));

        Assert.Contains("you are in Team Rocket", Said(await RunAsync(guilds, world, ash, ashId, "/join Team Rocket")));

        Assert.Equal(2, (await store.MembersAsync((await store.OfAsync(ashId))!.Id)).Count);
    }

    /// <summary>
    /// The one thing a guild does that nothing else here can: reach somebody who is nowhere
    /// near you.
    /// </summary>
    [Fact]
    public async Task AGuildLineReachesEverybodyInItAndNobodyElse()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");
        (ServerPlayer ash, long ashId) = await ArriveAsync(store, world, "Ash");
        (ServerPlayer misty, _) = await ArriveAsync(store, world, "Misty");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");
        await RunAsync(guilds, world, mason, masonId, "/invite Ash");
        await RunAsync(guilds, world, ash, ashId, "/join Team Rocket");

        List<Outgoing> heard = await RunAsync(guilds, world, mason, masonId, "/g prepare for trouble");

        var to = heard.Select(o => o.OnlyTo).ToList();

        Assert.Contains(mason.Id, to);
        Assert.Contains(ash.Id, to);
        Assert.DoesNotContain(misty.Id, to);

        ChatSaid said = heard.Select(o => o.Message).OfType<ChatSaid>().First();

        Assert.Equal("prepare for trouble", said.Text);
        Assert.Equal("Mason", said.Name);
    }

    [Fact]
    public async Task AndSomebodyInNoGuildIsToldRatherThanIgnored()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        Assert.Contains("no guild", Said(await RunAsync(guilds, world, mason, masonId, "/g hello")));
    }

    /// <summary>
    /// Every verb this class claims is one it can actually answer.
    /// <para>
    /// The guardrail the market's two front ends needed, in the smaller form that suits one:
    /// a verb listed in <c>Handles</c> and missing from the switch would fall through to
    /// "show me my guild", which is a command that silently does something else.
    /// </para>
    /// </summary>
    [Fact]
    public async Task EveryVerbItClaimsDoesSomethingOfItsOwn()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        string[] verbs = ["guilds", "invite", "join", "leave", "kick", "g"];

        foreach (string verb in verbs)
        {
            Assert.True(Guilds.Handles(verb), $"{verb} is not claimed");

            string reply = Said(await RunAsync(guilds, world, mason, masonId, $"/{verb}"));

            Assert.False(
                reply.Contains("founds one", StringComparison.Ordinal),
                $"/{verb} fell through to showing a guild");
        }

        Assert.True(Guilds.Handles("guild"));
        Assert.False(Guilds.Handles("market"));
    }

    // ---- the screen --------------------------------------------------------------------

    private static async Task<GuildOpened> AskAsync(
        Guilds guilds, GameWorld world, ServerPlayer player, long accountId, GuildRequest asking)
    {
        List<Outgoing> sent = await guilds.ScreenAsync(world, player.Id, accountId, asking);

        return Assert.Single(sent.Select(o => o.Message).OfType<GuildOpened>());
    }

    /// <summary>
    /// Every kind of ask a screen can make is a console line the guild answers to.
    /// <para>
    /// The guardrail the market's two front ends needed and this one needs for the same
    /// reason. A kind added to the enum with no arm in the translation falls through to
    /// nothing, which is the value <em>Look</em> has — so the button would do nothing, say
    /// nothing, and report success.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryKindOfAskIsALineTheGuildAnswersTo()
    {
        var stranded = new List<string>();

        foreach (GuildAsk asking in Enum.GetValues<GuildAsk>())
        {
            ConsoleLine? line = Guilds.LineFor(new GuildRequest(asking, "Team Rocket"));

            if (asking == GuildAsk.Look)
            {
                if (line is not null) stranded.Add($"{asking} should ask for nothing");
                continue;
            }

            if (line is null) stranded.Add($"{asking} makes no line at all");
            else if (!Guilds.Handles(line.Verb)) stranded.Add($"{asking} makes /{line.Verb}, not ours");
        }

        Assert.Empty(stranded);
    }

    [Fact]
    public async Task LookingWithNoGuildGivesTheOffersInstead()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");
        (ServerPlayer ash, long ashId) = await ArriveAsync(store, world, "Ash");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");
        await RunAsync(guilds, world, mason, masonId, "/invite Ash");

        GuildOpened seen = await AskAsync(guilds, world, ash, ashId, new GuildRequest(GuildAsk.Look));

        Assert.False(seen.Exists);
        Assert.Empty(seen.Members);
        Assert.Equal("Team Rocket", Assert.Single(seen.Invitations));
    }

    [Fact]
    public async Task AndWithOneGivesTheRosterAndWhoIsWhere()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        // Registered but never joined the world, which is what "away" looks like from here.
        long ash = await AccountAsync(store, "Ash");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");
        await RunAsync(guilds, world, mason, masonId, "/invite Ash");
        Assert.NotNull(await store.AcceptAsync(ash, "Team Rocket"));

        GuildOpened seen = await AskAsync(guilds, world, mason, masonId, new GuildRequest(GuildAsk.Look));

        Assert.True(seen.Exists);
        Assert.True(seen.IsLeader);
        Assert.Equal("Team Rocket", seen.Name);
        Assert.Empty(seen.Invitations);

        Assert.Equal(2, seen.Members.Count);
        Assert.Equal("PALLET TOWN", seen.Members[0].Where);
        Assert.Equal("", seen.Members[1].Where);
    }

    /// <summary>
    /// A screen act is the same act as typing it, and the refusal comes back on the picture
    /// rather than as a line of its own.
    /// </summary>
    [Fact]
    public async Task FoundingThroughTheScreenIsTheSameActAndSaysSoOnThePicture()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");

        List<Outgoing> sent = await guilds.ScreenAsync(
            world, mason.Id, masonId, new GuildRequest(GuildAsk.Found, "Team Rocket"));

        Assert.Empty(sent.Select(o => o.Message).OfType<ConsoleReply>());

        GuildOpened seen = Assert.Single(sent.Select(o => o.Message).OfType<GuildOpened>());

        Assert.Equal("Team Rocket", seen.Name);
        Assert.Contains("Team Rocket exists", seen.Message);

        // And a second one is refused, on the picture, without losing the first.
        GuildOpened again = await AskAsync(
            guilds, world, mason, masonId, new GuildRequest(GuildAsk.Found, "Team Aqua"));

        Assert.Equal("Team Rocket", again.Name);
        Assert.Contains("already in", again.Message);
    }

    /// <summary>
    /// Somebody who is in a guild and does not lead it is told so, so a screen can hide the
    /// two things they may not do rather than offering a refusal.
    /// </summary>
    [Fact]
    public async Task AMemberIsNotToldTheyLeadIt()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        GameWorld world = World();
        var guilds = new Guilds(store);

        (ServerPlayer mason, long masonId) = await ArriveAsync(store, world, "Mason");
        (ServerPlayer ash, long ashId) = await ArriveAsync(store, world, "Ash");

        await RunAsync(guilds, world, mason, masonId, "/guild Team Rocket");
        await RunAsync(guilds, world, mason, masonId, "/invite Ash");
        await RunAsync(guilds, world, ash, ashId, "/join Team Rocket");

        Assert.True((await AskAsync(guilds, world, mason, masonId, new GuildRequest(GuildAsk.Look))).IsLeader);
        Assert.False((await AskAsync(guilds, world, ash, ashId, new GuildRequest(GuildAsk.Look))).IsLeader);
    }
}
