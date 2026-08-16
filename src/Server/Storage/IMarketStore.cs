using PokeMmo.Core.Save;

namespace PokeMmo.Server.Storage;

/// <summary>
/// Where things for sale are kept.
/// <para>
/// A separate interface from <see cref="IPlayerStore"/> because it answers a different
/// question. A player store is asked about one account at a time and knows nothing about
/// any other; a market is the one place in this project that is about everybody at once.
/// </para>
/// <para>
/// Every method here takes the seller's or buyer's whole character as well as what they
/// are doing, and that is the shape the market's safety depends on rather than an
/// awkwardness to be tidied away. The reason is in the way saving works: a character's
/// creatures are rewritten wholesale from an in-memory snapshot, so any change this store
/// made to them on its own would be undone by the next save the server happened to make.
/// Listing a creature is therefore not "escrow this" — it is "write this character down
/// without it, and escrow it, and do both or neither".
/// </para>
/// </summary>
/// <summary>
/// What somebody is looking for.
/// <para>
/// Every part optional, because a market is searched by whatever the person searching
/// happens to care about — and a search type with four required fields is a search nobody
/// can start.
/// </para>
/// </summary>
public sealed record MarketSearch
{
    /// <summary>Only this species, or anything.</summary>
    public int? Species { get; init; }

    /// <summary>Nothing dearer than this.</summary>
    public int? Most { get; init; }

    /// <summary>
    /// Nothing born with less than this, added across the six.
    /// <para>
    /// A total rather than a floor per stat. Somebody shopping for a parent wants "good
    /// enough overall" far more often than they want a particular number in a particular
    /// slot, and the per-stat version can be added the day anybody asks for it.
    /// </para>
    /// </summary>
    public int? Born { get; init; }

    /// <summary>
    /// Only piles of this item.
    /// <para>
    /// Its own field rather than a second meaning for <see cref="Species"/>, even though
    /// both are an index into a table on a cartridge. They are indices into
    /// <em>different</em> tables, and the day somebody searched species 4 and was shown
    /// four hundred POKé BALLs would be the day that shortcut cost more than it saved.
    /// </para>
    /// </summary>
    public int? Item { get; init; }

    /// <summary>True when this asks for nothing in particular.</summary>
    public bool IsEverything => Species is null && Most is null && Born is null && Item is null;
}

public interface IMarketStore
{
    /// <summary>
    /// What the market keeps out of every sale, as a percentage. <b>Modelled</b>, and the
    /// only number here that is an opinion rather than a rule.
    /// <para>
    /// Five per cent. It exists because a game where money is only ever created is a game
    /// whose prices go one way, and a market is the one place in this project where every
    /// coin that changes hands is visible. Real markets take a cut for the same reason
    /// rather than out of greed.
    /// </para>
    /// <para>
    /// Taken from the seller rather than added to the buyer, so a listed price is the price
    /// — a buyer who is quoted one number and charged another is a buyer who stops
    /// trusting the board.
    /// </para>
    /// </summary>
    public const int Cut = 5;

    /// <summary>
    /// Puts one creature up at a price, in the same transaction that writes its seller
    /// down without it.
    /// <para>
    /// <paramref name="withoutIt"/> is the seller exactly as they will be once this
    /// succeeds — the creature already removed from whichever list it was in. Handing the
    /// character in rather than having this work it out is what makes the two halves one
    /// transaction: there is no moment where a creature is both escrowed and still in
    /// somebody's box, which is the moment it would be sold twice.
    /// </para>
    /// </summary>
    Task<long> ListAsync(
        long sellerId,
        SavedCharacter withoutIt,
        SavedMon offered,
        int price,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a number of one item up at a price, in the same transaction that writes its
    /// seller down without them.
    /// <para>
    /// The count on the listing row <em>is</em> the escrow, and that is the whole
    /// difference between this and the creature above. A creature is escrowed by moving
    /// its row into a list nobody owns, because a creature is a row and moving it is the
    /// only way to hold it without duplicating it. A number has no row to move: writing
    /// the seller's bag down short by five and the listing up by five is the entire act,
    /// and a second table holding those five would be a second place the number can be
    /// wrong.
    /// </para>
    /// <para>
    /// <paramref name="price"/> is for the lot rather than for each, because that is what
    /// a buyer is agreeing to and a market that quotes per-item and charges per-lot is a
    /// market that has surprised somebody.
    /// </para>
    /// </summary>
    Task<long> ListItemsAsync(
        long sellerId,
        SavedCharacter withoutThem,
        int itemId,
        int count,
        int price,
        CancellationToken cancellationToken = default);

    /// <summary>What is for sale, newest first, and never anything already sold.</summary>
    Task<IReadOnlyList<Listing>> BrowseAsync(int most = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// What is for sale that matches, cheapest first.
    /// <para>
    /// A market you can only read newest-first is unusable past a hundred listings, which
    /// is a number one afternoon of play would pass. This is the difference between a board
    /// and a market.
    /// </para>
    /// <para>
    /// Cheapest first rather than newest, because the question anybody actually has is
    /// "what is the least I can pay for one of these" and a newest-first list answers it by
    /// making them read the whole thing.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Listing>> SearchAsync(
        MarketSearch what, int most = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// This seller's own listings, sold ones included — those are the ones with money
    /// waiting on them.
    /// </summary>
    Task<IReadOnlyList<Listing>> MineAsync(long sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Buys one, in the single transaction that moves the creature and the money.
    /// <para>
    /// <paramref name="buyer"/> is the buyer as they are now — their money is read from it
    /// rather than from the row, because for somebody who is online the row is the stale
    /// copy. What is written back is that same character with the creature in their box
    /// and the price gone, which is why this cannot be split into "take the money" and
    /// "hand it over": either half alone is somebody robbed.
    /// </para>
    /// <para>
    /// Returns what was bought and what it cost, or nothing when the listing has already
    /// gone, is the buyer's own, or costs more than they have. Two people pressing buy in
    /// the same instant is the ordinary case in a market rather than the exotic one, and
    /// the loser is told rather than left holding a creature that was already sold.
    /// </para>
    /// </summary>
    /// <para>
    /// A buyer whose bag cannot take the whole lot is refused rather than given what fits.
    /// Half a lot is somebody who paid for five and got two, and the fixing of that is a
    /// conversation rather than a query.
    /// </para>
    Task<(Parcel Bought, int Price)?> BuyAsync(
        long buyerId,
        long listingId,
        SavedCharacter buyer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the money for everything of this seller's that has sold, and forgets those
    /// listings.
    /// <para>
    /// Everything at once rather than one at a time, because a seller has no reason to want
    /// only some of their money and an interface that offers the choice is an interface
    /// with a wrong answer in it.
    /// </para>
    /// <para>
    /// This is where a sold listing's price has been sitting since it sold, and why it sat
    /// there rather than being paid into the seller's row: crediting somebody who is online
    /// means their next save writes the old figure back over it, and they sold something
    /// and got nothing. Money waiting to be collected is money nothing else is touching.
    /// </para>
    /// </summary>
    Task<int> CollectAsync(
        long sellerId,
        SavedCharacter current,
        int ceiling,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes one back off the market, returning it to the seller's box or bag.
    /// <para>
    /// The box rather than the party, for the reason an egg goes to the box: a party that
    /// fills itself while somebody is elsewhere is a party that stops them catching
    /// anything.
    /// </para>
    /// <para>
    /// Items come back to the bag, and a bag with no room for them refuses the whole
    /// cancellation rather than taking what fits. The seller keeps the listing and is told
    /// to make room, which is recoverable; handing back nine of a lot of ten and dropping
    /// the tenth is not.
    /// </para>
    /// <para>
    /// Returns what came back, or nothing when the listing is not theirs, is already sold,
    /// or never existed. Nothing is thrown for any of those — they are all things a player
    /// can legitimately ask by being slightly out of date.
    /// </para>
    /// </summary>
    Task<Parcel?> CancelAsync(
        long sellerId,
        long listingId,
        SavedCharacter current,
        CancellationToken cancellationToken = default);
}
