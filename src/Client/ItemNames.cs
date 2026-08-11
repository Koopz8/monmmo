using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Items;

namespace PokeMmo.Client;

/// <summary>
/// Turns an item id into a name, from this machine's own cartridge.
/// <para>
/// The same job <see cref="TrainerNames"/> does, for the same reason: the server sends
/// a number because a number is all it has. "POKé BALL" exists on exactly one machine
/// in this arrangement, and it is this one.
/// </para>
/// <para>
/// Located once at startup rather than on first use. Locating walks the whole image,
/// and the moment a player opens their bag is not the moment to do that.
/// </para>
/// </summary>
public sealed class ItemNames
{
    private readonly Rom _rom;
    private readonly int? _table;
    private readonly Dictionary<int, string> _cache = [];

    public ItemNames(Rom rom)
    {
        _rom = rom;
        _table = ItemTable.Locate(rom);
    }

    public bool IsAvailable => _table is not null;

    public string Of(int itemId)
    {
        if (_cache.TryGetValue(itemId, out string? known)) return known;

        string name = Read(itemId);

        _cache[itemId] = name;
        return name;
    }

    private string Read(int itemId)
    {
        if (_table is not { } table || itemId <= 0) return $"item {itemId}";

        int at = table + itemId * ItemRecord.RecordSizeBytes;

        if (ItemRecord.TryParse(_rom, at, itemId) is not { } record) return $"item {itemId}";

        return string.IsNullOrWhiteSpace(record.Name) ? $"item {itemId}" : GameText.ToAscii(record.Name);
    }
}
