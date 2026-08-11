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
    public int Add(int itemId, int count = 1)
    {
        if (itemId <= 0 || count <= 0) return 0;

        int held = _counts.GetValueOrDefault(itemId);

        if (held == 0 && _order.Count >= PocketCapacity) return 0;

        int taken = Math.Min(count, MaxStack - held);
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
