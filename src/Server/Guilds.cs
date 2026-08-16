using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// Guilds, as somebody uses them.
/// <para>
/// It sits beside <see cref="Friends"/> and <see cref="Market"/> for the same reason all
/// three are outside <see cref="GameWorld"/>: every act here is a database call, and the
/// world's console runs inside the lock every other player on the server is waiting on.
/// </para>
/// <para>
/// The one thing it does that a friends list cannot is talk to people who are nowhere near
/// you. A room hears you because it is a room; a guild hears you because it is a guild, and
/// that is the whole difference between the two and the reason this exists at all now that
/// chat does.
/// </para>
/// </summary>
public sealed class Guilds(IGuildStore store)
{
    /// <summary>How many guilds a listing shows.</summary>
    private const int APageful = 20;

    /// <summary>True when this console verb belongs here rather than to the world.</summary>
    public static bool Handles(string verb) =>
        verb is "guild" or "guilds" or "invite" or "join" or "leave" or "kick" or "g";

    /// <summary>What the last thing anybody did with a guild came to, for the log.</summary>
    public string? Last { get; private set; }

    public async Task<List<Outgoing>> RunAsync(
        GameWorld world,
        int playerId,
        long accountId,
        ConsoleLine line,
        CancellationToken cancellationToken = default) => line.Verb switch
    {
        "guilds" => Show(playerId, await store.AllAsync(APageful, cancellationToken)),
        "invite" => await InviteAsync(playerId, accountId, line, cancellationToken),
        "join" => await JoinAsync(playerId, accountId, line, cancellationToken),
        "leave" => await LeaveAsync(playerId, accountId, cancellationToken),
        "kick" => await KickAsync(playerId, accountId, line, cancellationToken),
        "g" => await SayAsync(world, playerId, accountId, line, cancellationToken),
        _ => await MineAsync(world, playerId, accountId, line, cancellationToken),
    };

    /// <summary>
    /// Mine, or a new one under the name given.
    /// <para>
    /// One verb for both because they are the same question asked from either side of having
    /// a guild — and somebody typing <c>/guild</c> with a name while already in one is told
    /// so rather than quietly shown their own.
    /// </para>
    /// </summary>
    private async Task<List<Outgoing>> MineAsync(
        GameWorld world, int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        string wanted = string.Join(" ", line.Words);

        Guild? mine = await store.OfAsync(accountId, cancellationToken);

        if (wanted.Length > 0)
        {
            if (mine is { } already)
                return [Said(playerId, $"you are already in {already.Name} — /leave first")];

            if (!Guild.IsAName(wanted))
            {
                return
                [
                    Said(playerId,
                        $"a name is {Guild.ShortestName} to {Guild.LongestName} letters, digits and single spaces"),
                ];
            }

            Guild? made = await store.FoundAsync(accountId, wanted, cancellationToken);

            Last = made is null ? $"could not found {wanted}" : $"founded {wanted}";

            return [Said(playerId, made is null ? $"the name {wanted} is taken" : $"{made.Name} exists, and you lead it")];
        }

        if (mine is not { } guild)
        {
            IReadOnlyList<Guild> asked = await store.InvitationsAsync(accountId, cancellationToken);

            return asked.Count == 0
                ? [Said(playerId, "you are in no guild — /guild <name> founds one")]
                :
                [
                    Said(playerId, $"{asked.Count} guild(s) have asked you:"),
                    .. asked.Select(a => Said(playerId, $"  {a.Name} — /join {a.Name}")),
                ];
        }

        IReadOnlyList<GuildMember> members = await store.MembersAsync(guild.Id, cancellationToken);

        var said = new List<Outgoing> { Said(playerId, $"{guild.Name} — {members.Count} member(s)") };

        foreach (GuildMember member in members)
        {
            // Whether they are on is asked of the world rather than remembered, for the
            // reason a friends list asks: online is only ever true as of the instant
            // somebody asks it.
            ServerPlayer? here = world.Named(member.Name);

            said.Add(Said(
                playerId,
                $"  {member.Name,-16} {(member.IsLeader ? "leader" : "      ")} " +
                (here is null ? "away" : world.NameOfMap(here.MapId))));
        }

        return said;
    }

    private async Task<List<Outgoing>> InviteAsync(
        int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        if (line.Word(0) is not { Length: > 0 } name) return [Said(playerId, "/invite <player name>")];

        bool asked = await store.InviteAsync(accountId, name, cancellationToken);

        Last = asked ? $"asked {name}" : $"did not ask {name}";

        return
        [
            Said(playerId, asked
                ? $"{name} has been asked"
                : "no: you do not lead a guild, there is nobody by that name, or they are in one"),
        ];
    }

    private async Task<List<Outgoing>> JoinAsync(
        int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        string wanted = string.Join(" ", line.Words);

        if (wanted.Length == 0) return [Said(playerId, "/join <guild name>")];

        Guild? joined = await store.AcceptAsync(accountId, wanted, cancellationToken);

        Last = joined is null ? $"did not join {wanted}" : $"joined {joined.Name}";

        return
        [
            Said(playerId, joined is null
                ? "no: they have not asked you, or you are already in one"
                : $"you are in {joined.Name}"),
        ];
    }

    private async Task<List<Outgoing>> LeaveAsync(
        int playerId, long accountId, CancellationToken cancellationToken)
    {
        Guild? mine = await store.OfAsync(accountId, cancellationToken);

        bool left = await store.LeaveAsync(accountId, cancellationToken);

        Last = left ? $"left {mine?.Name}" : "left nothing";

        return
        [
            Said(playerId, left
                ? mine is { Members: <= 1 }
                    ? $"you have left {mine.Name}, and it is gone with you"
                    : $"you have left {mine?.Name}"
                : "you are in no guild"),
        ];
    }

    private async Task<List<Outgoing>> KickAsync(
        int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        if (line.Word(0) is not { Length: > 0 } name) return [Said(playerId, "/kick <player name>")];

        bool gone = await store.KickAsync(accountId, name, cancellationToken);

        Last = gone ? $"put {name} out" : $"did not put {name} out";

        return
        [
            Said(playerId, gone
                ? $"{name} is out"
                : "no: you do not lead a guild, or they are not in it"),
        ];
    }

    /// <summary>
    /// Says something to everybody in the guild who is online, wherever they are.
    /// <para>
    /// The one thing a guild does that nothing else here can. Room chat reaches a room and
    /// stops at the edge of a copy of it; this reaches people who are on other continents
    /// and in other instances, which is what a guild is for.
    /// </para>
    /// <para>
    /// Sent to accounts rather than to a map, so it is the only thing in this project that
    /// addresses a message by who somebody is rather than by where they are standing.
    /// </para>
    /// </summary>
    private async Task<List<Outgoing>> SayAsync(
        GameWorld world, int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        string text = string.Join(" ", line.Words).Trim();

        if (text.Length == 0) return [Said(playerId, "/g <something to say>")];

        if (await store.OfAsync(accountId, cancellationToken) is not { } guild)
            return [Said(playerId, "you are in no guild")];

        if (world.Find(playerId) is not { } speaker) return [];

        IReadOnlyList<long> everybody = await store.MemberIdsAsync(guild.Id, cancellationToken);

        Last = $"[{guild.Name}] {speaker.Name}: {text}";

        var heard = new List<Outgoing>();

        foreach (long member in everybody)
        {
            // Nobody is sent a line twice, and anybody who is not on is simply not there —
            // a guild message is not kept for somebody to read when they arrive, because
            // this project has nowhere to keep one and pretending otherwise would be worse
            // than not having it.
            if (world.PlayingAs(member) is not { } listener) continue;

            heard.Add(new Outgoing(
                new ChatSaid(speaker.Id, speaker.Name, text) { Private = true, Mine = listener.Id == playerId },
                OnlyTo: listener.Id));
        }

        return heard;
    }

    private static List<Outgoing> Show(int playerId, IReadOnlyList<Guild> guilds) =>
        guilds.Count == 0
            ? [Said(playerId, "there are no guilds — /guild <name> founds the first")]
            :
            [
                Said(playerId, $"{guilds.Count} guild(s), biggest first"),
                .. guilds.Select(g => Said(playerId, $"  {g.Name,-22} {g.Members,3} member(s)")),
            ];

    private static Outgoing Said(int playerId, string line) =>
        new(new ConsoleReply(line), OnlyTo: playerId);
}
