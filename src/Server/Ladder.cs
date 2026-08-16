using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// The ladder: what a duel was worth, and who is at the top of each band.
/// <para>
/// It sits outside <see cref="GameWorld"/> with the market, the friends list and the guilds,
/// and for the same reason all four do: every act here is a database transaction, and the
/// world's console runs inside the lock every other player is waiting on.
/// </para>
/// <para>
/// The world reports a finished duel and this writes it down. That split is what makes it
/// possible for a rating to be a transaction at all — a result recorded halfway would be two
/// players whose numbers no longer add up to the game they played.
/// </para>
/// </summary>
public sealed class Ladder(IRatingStore store)
{
    /// <summary>How many rungs a listing shows.</summary>
    private const int APageful = 20;

    /// <summary>True when this console verb belongs here rather than to the world.</summary>
    public static bool Handles(string verb) => verb is "ladder" or "rating";

    /// <summary>What the last thing that happened on the ladder came to, for the log.</summary>
    public string? Last { get; private set; }

    /// <summary>
    /// Writes down a finished duel and tells both sides what it was worth.
    /// <para>
    /// Both are told the same two numbers, because a rating is a comparison and somebody who
    /// only saw their own would have no way to tell a hard-won fight from an easy one.
    /// </para>
    /// </summary>
    public async Task<List<Outgoing>> RecordAsync(
        GameWorld world, DuelResult result, CancellationToken cancellationToken = default)
    {
        (int winner, int loser) = await store
            .RecordAsync(result.Winner, result.Loser, result.Band, cancellationToken)
            .ConfigureAwait(false);

        Last = $"{Tiers.NameOf(result.Band)}: {result.Winner} to {winner}, {result.Loser} to {loser}";

        var told = new List<Outgoing>();

        // Whoever is still connected. A rating is written whether or not either of them is
        // there to read it — the disk is what the ladder is — and this only says so out
        // loud to whoever can hear it.
        if (world.PlayingAs(result.Winner) is { } won)
            told.Add(Said(won.Id, $"{Tiers.NameOf(result.Band)}: you are on {winner}"));

        if (world.PlayingAs(result.Loser) is { } lost)
            told.Add(Said(lost.Id, $"{Tiers.NameOf(result.Band)}: you are on {loser}"));

        return told;
    }

    public async Task<List<Outgoing>> RunAsync(
        GameWorld world,
        int playerId,
        long accountId,
        ConsoleLine line,
        CancellationToken cancellationToken = default)
    {
        Last = null;

        // Whichever band was asked for, or the one this party would fight in. Defaulting to
        // their own rather than to the top, because "how am I doing" is the question
        // somebody typing this has, and the top band is where they are least likely to be.
        int band = line.Number(0)
            ?? (world.Find(playerId) is { } player ? world.BandOf(player) : 0);

        band = Math.Clamp(band, 0, Tiers.Bands - 1);

        if (line.Verb == "rating")
        {
            Rung mine = await store.StandingAsync(accountId, band, cancellationToken);

            return
            [
                Said(playerId, $"{Tiers.NameOf(band)}: {mine.Rating}, {mine.Won} won and {mine.Lost} lost"),
                Said(playerId, mine.Played == 0
                    ? "everybody starts here — a duel in this band is the first thing that moves it"
                    : "/ladder to see the band, /tier to see why you are in it"),
            ];
        }

        IReadOnlyList<Rung> top = await store.TopAsync(band, APageful, cancellationToken);

        if (top.Count == 0)
            return [Said(playerId, $"nobody has fought in {Tiers.NameOf(band)} yet")];

        var said = new List<Outgoing> { Said(playerId, $"{Tiers.NameOf(band)} — {top.Count} on the board") };

        for (int at = 0; at < top.Count; at++)
        {
            Rung rung = top[at];

            said.Add(Said(playerId, $"  {at + 1,3}. {rung.Name,-16} {rung.Rating,5}  {rung.Won}-{rung.Lost}"));
        }

        return said;
    }

    private static Outgoing Said(int playerId, string line) =>
        new(new ConsoleReply(line), OnlyTo: playerId);
}
