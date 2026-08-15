using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// A player's own list of people, and where they are right now.
/// <para>
/// Two halves that live in different places on purpose. Who is on the list is a fact about
/// an account and belongs on the disk; whether they are online, and which copy of which
/// town they are standing in, is a fact about this second and belongs to the world. This
/// class is the only thing that asks both.
/// </para>
/// <para>
/// It sits beside <see cref="Market"/> and for the same reason: reading the list is a
/// database call, and the world's console runs inside the lock every other player is
/// waiting on.
/// </para>
/// </summary>
public sealed class Friends(IFriendStore store)
{
    /// <summary>True when this console verb belongs here rather than to the world.</summary>
    public static bool Handles(string verb) => verb is "friend" or "unfriend" or "friends";

    /// <summary>What the last thing anybody did with a list came to, for the log.</summary>
    public string? Last { get; private set; }

    public async Task<List<Outgoing>> RunAsync(
        GameWorld world,
        int playerId,
        long accountId,
        ConsoleLine line,
        CancellationToken cancellationToken = default)
    {
        Last = null;

        switch (line.Verb)
        {
            case "friend":
            {
                if (line.Word(0) is not { Length: > 0 } name)
                    return [Said(playerId, "/friend <player name>")];

                bool added = await store.BefriendAsync(accountId, name, cancellationToken);

                Last = added ? $"added {name}" : $"did not add {name}";

                return
                [
                    Said(playerId, added
                        ? $"{name} is on your list"
                        : $"no: there is nobody called {name}, or they are already on it"),
                ];
            }

            case "unfriend":
            {
                if (line.Word(0) is not { Length: > 0 } name)
                    return [Said(playerId, "/unfriend <player name>")];

                bool gone = await store.ForgetAsync(accountId, name, cancellationToken);

                Last = gone ? $"removed {name}" : $"did not remove {name}";

                return [Said(playerId, gone ? $"{name} is off your list" : $"{name} was not on it")];
            }
        }

        IReadOnlyList<Friend> friends = await store.FriendsAsync(accountId, cancellationToken);

        if (friends.Count == 0) return [Said(playerId, "your list is empty — /friend <name> adds somebody")];

        var said = new List<Outgoing>
        {
            Said(playerId, $"{friends.Count} on your list"),
        };

        foreach (Friend friend in friends)
        {
            // Asked of the world rather than remembered, because "online" is not a thing a
            // list can hold: it is only ever true as of the instant somebody asks.
            ServerPlayer? here = world.Named(friend.Name);

            said.Add(Said(playerId, here is null
                ? $"  {friend.Name,-16} away"
                : $"  {friend.Name,-16} {world.NameOfMap(here.MapId)} — /with {friend.Name}"));
        }

        return said;
    }

    private static Outgoing Said(int playerId, string line) =>
        new(new ConsoleReply(line), OnlyTo: playerId);
}
