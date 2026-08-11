using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Trainers;

namespace PokeMmo.Client;

/// <summary>
/// Turns a trainer id into a name, from this machine's own cartridge.
/// <para>
/// The whole reason the server sends an id. It has no cartridge and so has no names of
/// any kind — not for species, not for moves, and not for the person who just
/// challenged you. Every one of those becomes a word here or nowhere.
/// </para>
/// <para>
/// The table is located once at startup. Locating means walking the whole image, which
/// is fine while a window is opening and is not fine in the moment somebody steps into
/// a trainer's line of sight.
/// </para>
/// </summary>
public sealed class TrainerNames
{
    private readonly Rom _rom;
    private readonly int _speciesCount;
    private readonly int? _table;
    private readonly Dictionary<int, string> _cache = [];

    public TrainerNames(Rom rom, int speciesCount)
    {
        _rom = rom;
        _speciesCount = speciesCount > 0 ? speciesCount : 512;
        _table = TrainerTable.Locate(rom, _speciesCount);
    }

    public bool IsAvailable => _table is not null;

    /// <summary>
    /// This trainer's name, or a plain word when the cartridge will not give one.
    /// <para>
    /// A missing name is not worth failing over. Plenty of entries in a real table have
    /// none at all, and "TRAINER wants to fight!" reads perfectly well.
    /// </para>
    /// </summary>
    public string Of(int id)
    {
        if (_cache.TryGetValue(id, out string? known)) return known;

        string name = Read(id);

        _cache[id] = name;
        return name;
    }

    private string Read(int id)
    {
        if (_table is not { } table || id < 0) return "TRAINER";

        int at = table + id * TrainerRecord.RecordSizeBytes;

        if (TrainerRecord.TryParse(_rom, at, id, _speciesCount) is not { } record) return "TRAINER";

        return string.IsNullOrWhiteSpace(record.Name) ? "TRAINER" : GameText.ToAscii(record.Name);
    }
}
