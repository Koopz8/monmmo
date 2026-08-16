using PokeMmo.Core.Data;

namespace PokeMmo.Core.Save;

/// <summary>Some number of one item.</summary>
public sealed record BagEntry(int ItemId, int Count);

/// <summary>
/// What a player is carrying.
/// <para>
/// Item ids and counts, which is all the server can hold — the id becomes "ULTRA BALL"
/// on the machine with a cartridge, exactly as a species index becomes a name.
/// </para>
/// <para>
/// The pocket an item lives in is a property of the item and is not stored here. Two
/// places recording which pocket something belongs in is two places that can disagree,
/// and the rules file already knows.
/// </para>
/// </summary>
public sealed class Bag
{
    /// <summary>The most of one item a single stack holds, as the games have it.</summary>
    public const int MaxStack = 99;

    /// <summary>
    /// Distinct items allowed in one pocket.
    /// <para>
    /// A cap has to exist or a player who walks over every item in the world ends up
    /// with a save nobody can load. It is deliberately generous — running out is a
    /// gameplay problem for later, and losing a save is not.
    /// </para>
    /// </summary>
    public const int PocketCapacity = 60;

    private readonly Dictionary<int, int> _counts = [];
    private readonly List<int> _order = [];

    public Bag()
    {
    }

    public Bag(IEnumerable<BagEntry> entries)
    {
        foreach (BagEntry entry in entries)
        {
            if (entry.ItemId <= 0 || entry.Count <= 0) continue;

            if (!_counts.ContainsKey(entry.ItemId)) _order.Add(entry.ItemId);

            _counts[entry.ItemId] = Math.Min(MaxStack, _counts.GetValueOrDefault(entry.ItemId) + entry.Count);
        }
    }

    public int DistinctItems => _order.Count;

    public int CountOf(int itemId) => _counts.GetValueOrDefault(itemId);

    public bool Has(int itemId, int count = 1) => CountOf(itemId) >= count;

    /// <summary>
    /// Everything carried, in the order it was first picked up.
    /// <para>
    /// Insertion order rather than id order, because that is the order it will be shown
    /// in and a bag that reshuffles itself when you pick something up is a bag whose
    /// third slot is never the same thing twice.
    /// </para>
    /// </summary>
    public List<BagEntry> Entries => _order.Select(id => new BagEntry(id, _counts[id])).ToList();

    /// <summary>Everything carried that belongs in one pocket, as the rules define it.</summary>
    public List<BagEntry> InPocket(GameRules rules, Pocket pocket) =>
        Entries.Where(e => rules.ItemAt(e.ItemId)?.Pocket == pocket).ToList();

    /// <summary>
    /// Adds items, and says how many actually went in.
    /// <para>
    /// Returning the number taken rather than a yes or no is what lets a shop charge for
    /// the right amount. A bag with room for two more of something has to be able to
    /// sell you two, not refuse five and not silently swallow three.
    /// </para>
    /// </summary>
    /// <param name="most">
    /// The most of this one the bag may hold. One for a key item, which is how these
    /// games have it — you cannot carry two S.S. TICKETs, and a script that hands one
    /// over twice should leave you with one. The bag does not know which items those
    /// are; whoever has the rules does.
    /// </param>
    /// <param name="sharesAPocketWith">
    /// Which of the things already carried live in the same pocket as this one, so that
    /// <see cref="PocketCapacity"/> can mean what it is called.
    /// <para>
    /// It did not. The cap is named for a pocket and was counted across the whole bag, and
    /// the two are only the same thing until somebody fills it — at which point a playthrough
    /// carrying exactly sixty different things stopped being able to pick anything up, went
    /// on walking, and reported a shop it was standing in front of as one it bought nothing
    /// from. A limit described as one thing and applied as another, failing silently.
    /// </para>
    /// <para>
    /// Supplied rather than worked out here for the reason the pocket is not stored here:
    /// which pocket an item lives in is on the cartridge, and this class has never seen one.
    /// Nothing supplied keeps the old whole-bag counting, which is what the callers that have
    /// no rules to hand need.
    /// </para>
    /// </param>
    public int Add(
        int itemId, int count = 1, int most = MaxStack, Func<int, bool>? sharesAPocketWith = null)
    {
        if (itemId <= 0 || count <= 0) return 0;

        int held = _counts.GetValueOrDefault(itemId);

        int alongside = sharesAPocketWith is null ? _order.Count : _order.Count(sharesAPocketWith);

        if (held == 0 && alongside >= PocketCapacity) return 0;

        int taken = Math.Min(count, Math.Min(most, MaxStack) - held);
        if (taken <= 0) return 0;

        if (held == 0) _order.Add(itemId);
        _counts[itemId] = held + taken;

        return taken;
    }

    /// <summary>Removes items, and says how many were actually there to remove.</summary>
    public int Remove(int itemId, int count = 1)
    {
        if (count <= 0) return 0;

        int held = _counts.GetValueOrDefault(itemId);
        int taken = Math.Min(count, held);

        if (taken <= 0) return 0;

        if (held == taken)
        {
            _counts.Remove(itemId);
            _order.Remove(itemId);
        }
        else
        {
            _counts[itemId] = held - taken;
        }

        return taken;
    }
}
