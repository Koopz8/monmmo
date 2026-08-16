using PokeMmo.Core.Sound;

namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// Every creature's noise, decoded the first time somebody asks for it.
/// <para>
/// The recordings are packed, and unpacking one is real work — a difference table walked a
/// nibble at a time. Doing it once when a creature first comes out is fine; doing it every
/// time one comes out is a hitch in a fight, and a fight is where every single one of these
/// is played.
/// </para>
/// <para>
/// Held in memory only. Nothing is written down, and what is here disappears when the client
/// closes, like every other thing this project reads off somebody's own cartridge.
/// </para>
/// </summary>
public sealed class CryLibrary
{
    private readonly Rom _rom;
    private readonly Dictionary<int, SampleRecord> _samples;
    private readonly CryTableResult? _table;
    private readonly IReadOnlyDictionary<int, int>? _bySpecies;
    private readonly Dictionary<int, Voice?> _decoded = [];

    /// <param name="bySpecies">
    /// Which entry each species uses, from <see cref="CryIndex"/>. Nothing means the species
    /// number is the entry number, which is true of no cartridge yet measured — it is the
    /// answer for a caller that has no species names to work it out from, and it is wrong
    /// after the placeholder block rather than everywhere, which is worth knowing.
    /// </param>
    public CryLibrary(
        Rom rom,
        IReadOnlyList<SampleRecord> samples,
        CryTableResult? table,
        IReadOnlyDictionary<int, int>? bySpecies = null)
    {
        _rom = rom;
        _samples = samples.GroupBy(s => s.Offset).ToDictionary(g => g.Key, g => g.First());
        _table = table;
        _bySpecies = bySpecies;
    }

    /// <summary>How many creatures this cartridge's table names.</summary>
    public int Count => _table?.Count ?? 0;

    /// <summary>How many have been unpacked so far, which is what says the store is a store.</summary>
    public int Decoded => _decoded.Count(pair => pair.Value is not null);

    /// <summary>
    /// One creature's noise, or nothing when this cartridge has none for it.
    /// <para>
    /// Nothing rather than silence, for the same reason a missing song comes back as nothing:
    /// a caller handed an empty recording would play it and believe something happened. A
    /// creature with no cry is a finding.
    /// </para>
    /// </summary>
    public Voice? For(int species)
    {
        if (_decoded.TryGetValue(species, out Voice? already)) return already;

        Voice? made = Decode(species);

        _decoded[species] = made;

        return made;
    }

    private Voice? Decode(int species)
    {
        // The entry, which is not the species number. A cartridge names more species than it
        // has cries, because a block in the middle of the numbering carries no creature —
        // see CryIndex, which is what works out by how much.
        if (Entry(species) is not { } entry) return null;

        if (_table?.SampleFor(entry) is not { } at) return null;

        if (!_samples.TryGetValue(at, out SampleRecord? record)) return null;

        sbyte[] audio = CryDecoder.Decode(_rom, record);

        if (audio.Length == 0) return null;

        // A cry does not loop. It is a noise with a beginning and an end, and a looping one
        // would ring for as long as the fight lasted.
        return new Voice(audio, record.Rate, false, 0);
    }

    /// <summary>Which entry of the table this species uses, or nothing when it has none.</summary>
    private int? Entry(int species)
    {
        if (_bySpecies is null) return species;

        return _bySpecies.TryGetValue(species, out int at) ? at : null;
    }
}
