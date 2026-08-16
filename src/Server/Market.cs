using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Server.Storage;

namespace PokeMmo.Server;

/// <summary>
/// The market, as something a player can actually use.
/// <para>
/// It lives here rather than in <see cref="GameWorld"/> for one reason: every act it
/// performs is a database transaction, and the world does everything it does inside a lock
/// nothing may go to disk under. Holding that lock across a write would stop every player
/// on the server for the length of it.
/// </para>
/// <para>
/// So the shape is read, commit, apply. Read the seller or buyer as they stand, go away and
/// commit the whole change in one transaction, and come back through
/// <see cref="GameWorld.Locked"/> to bring the in-memory copy into line. The disk is the
/// side that decides; memory is the copy that catches up.
/// </para>
/// <para>
/// Nothing here holds state. Two people using the market at once are two calls into a
/// store that settles them, which is the only place that argument can be settled correctly.
/// </para>
/// </summary>
public sealed class Market(IMarketStore store, Action<long>? forget = null)
{
    /// <summary>How many listings a browse shows before it stops.</summary>
    private const int APageful = 20;

    /// <summary>True when this console verb belongs to the market rather than the world.</summary>
    public static bool Handles(string verb) =>
        verb is "market" or "sell" or "buy" or "mine" or "collect";

    /// <summary>What the last thing anybody did at the market came to, for the log.</summary>
    public string? Last { get; private set; }

    public async Task<List<Outgoing>> RunAsync(
        GameWorld world,
        int playerId,
        long accountId,
        ConsoleLine line,
        CancellationToken cancellationToken = default)
    {
        Last = null;

        return line.Verb switch
        {
            "sell" => await SellAsync(world, playerId, accountId, line, cancellationToken),
            "buy" => await BuyAsync(world, playerId, accountId, line, cancellationToken),
            "collect" => await CollectAsync(world, playerId, accountId, cancellationToken),
            "mine" => Show(playerId, await store.MineAsync(accountId, cancellationToken), mine: true),
            _ => await BoardAsync(playerId, line, cancellationToken),
        };
    }

    /// <summary>
    /// The board, or the part of it somebody asked about.
    /// <para>
    /// With no arguments this is the newest listings, which is the right answer to "what is
    /// going on". With any argument it is a search, cheapest first, which is the right
    /// answer to every other question anybody has.
    /// </para>
    /// </summary>
    private async Task<List<Outgoing>> BoardAsync(
        int playerId, ConsoleLine line, CancellationToken cancellationToken)
    {
        MarketSearch what = new()
        {
            Species = Argument(line, "species"),
            Most = Argument(line, "under"),
            Born = Argument(line, "born"),
        };

        if (what.IsEverything)
            return Show(playerId, await store.BrowseAsync(APageful, cancellationToken), mine: false);

        List<Outgoing> found = Show(
            playerId, await store.SearchAsync(what, APageful, cancellationToken), mine: false);

        return
        [
            Said(playerId, Describing(what)),
            .. found,
        ];
    }

    /// <summary>
    /// A named number out of a console line — <c>species 25</c>, <c>under 5000</c>,
    /// <c>born 150</c>.
    /// <para>
    /// Named rather than positional because a search has three optional parts and nobody
    /// can be asked to remember which of three blanks to leave empty.
    /// </para>
    /// </summary>
    private static int? Argument(ConsoleLine line, string named)
    {
        for (int at = 0; at + 1 < line.Words.Count; at++)
        {
            if (string.Equals(line.Word(at), named, StringComparison.OrdinalIgnoreCase))
                return ConsoleLine.Number(line.Word(at + 1));
        }

        return null;
    }

    /// <summary>What was asked for, said back, so a search with a typo in it is obvious.</summary>
    private static string Describing(MarketSearch what)
    {
        var parts = new List<string>();

        if (what.Species is { } species) parts.Add($"species {species}");
        if (what.Most is { } most) parts.Add($"under {most}");
        if (what.Born is { } born) parts.Add($"born {born}+ of {Genes.Best * 6}");

        return $"cheapest first, {string.Join(", ", parts)}";
    }

    private async Task<List<Outgoing>> SellAsync(
        GameWorld world, int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        if (line.Number(0) is not { } slot || line.Number(1) is not { } price)
            return [Said(playerId, "/sell <box slot> <price>")];

        if (price <= 0) return [Said(playerId, "a price is a number above nought")];

        if (world.Find(playerId) is not { } player) return [];

        if (slot < 0 || slot >= player.Box.Count)
            return [Said(playerId, $"there is nobody in box slot {slot}")];

        SavedMon offered = player.Box[slot];

        // The snapshot as it will be once this works, taken before anything is committed.
        // The in-memory copy is not touched until the disk has agreed — a creature removed
        // from a box in memory and then refused by the store is a creature nobody has.
        if (world.Snapshot(playerId) is not { } before) return [];

        SavedCharacter withoutIt = before with { Box = [.. before.Box.Where((_, at) => at != slot)] };

        // Anything the scribe is still holding for this account is dropped, because it is
        // older than what is about to be written and would land on top of it.
        forget?.Invoke(accountId);

        long listingId = await store.ListAsync(accountId, withoutIt, offered, price, cancellationToken);

        world.Locked(playerId, p =>
        {
            if (slot < p.Box.Count) p.Box.RemoveAt(slot);
        });

        forget?.Invoke(accountId);

        Last = $"listed species {offered.Species} at {price} as listing {listingId}";

        return [Said(playerId, $"listing {listingId}: species {offered.Species} at {price}")];
    }

    private async Task<List<Outgoing>> BuyAsync(
        GameWorld world, int playerId, long accountId, ConsoleLine line, CancellationToken cancellationToken)
    {
        if (line.Number(0) is not { } listingId) return [Said(playerId, "/buy <listing id>")];

        if (world.Find(playerId) is not { } player) return [];

        // Somewhere to put it, asked here because how big a box is comes off the cartridge
        // and the store has never seen one.
        if (world.BoxSize > 0 && player.Box.Count >= world.BoxSize)
            return [Said(playerId, "the box is full")];

        if (world.Snapshot(playerId) is not { } before) return [];

        forget?.Invoke(accountId);

        var bought = await store.BuyAsync(accountId, listingId, before, cancellationToken);

        if (bought is not { } deal)
        {
            Last = $"refused listing {listingId}";

            return [Said(playerId, "no: it has gone, it is yours, or you cannot afford it")];
        }

        world.Locked(playerId, p =>
        {
            p.Box.Add(deal.Bought);
            p.Money = Math.Max(0, p.Money - deal.Price);
        });

        forget?.Invoke(accountId);

        Last = $"bought listing {listingId}, species {deal.Bought.Species}, for {deal.Price}";

        return [Said(playerId, $"bought species {deal.Bought.Species} for {deal.Price} — it is in the box")];
    }

    private async Task<List<Outgoing>> CollectAsync(
        GameWorld world, int playerId, long accountId, CancellationToken cancellationToken)
    {
        if (world.Snapshot(playerId) is not { } before) return [];

        forget?.Invoke(accountId);

        int paid = await store.CollectAsync(accountId, before, GameWorld.MaxMoney, cancellationToken);

        if (paid == 0)
        {
            Last = "collected nothing";

            return [Said(playerId, "nothing has sold")];
        }

        world.Locked(playerId, p => p.Money = Math.Min(GameWorld.MaxMoney, p.Money + paid));

        forget?.Invoke(accountId);

        Last = $"collected {paid}";

        return [Said(playerId, $"collected {paid}")];
    }

    private static List<Outgoing> Show(int playerId, IReadOnlyList<Listing> listings, bool mine)
    {
        if (listings.Count == 0)
            return [Said(playerId, mine ? "you have nothing on the market" : "nothing is for sale")];

        var said = new List<Outgoing>
        {
            Said(playerId, mine ? $"{listings.Count} of yours" : $"{listings.Count} for sale"),
        };

        said.AddRange(listings.Select(l => Said(
            playerId,
            $"  {l.Id,4}  species {l.Species,3} Lv{l.Level,-3} {l.Sex,-6} " +
            $"ivs {string.Join("/", l.Ivs),-17} {l.Total,3}/{Genes.Best * 6} {l.Price,7}" +
            (l.Sold ? "   SOLD, collect it" : mine ? "" : $"   from {l.Seller}"))));

        return said;
    }

    private static Outgoing Said(int playerId, string line) =>
        new(new ConsoleReply(line), OnlyTo: playerId);
}
