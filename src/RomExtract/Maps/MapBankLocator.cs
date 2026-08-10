namespace PokeMmo.RomExtract.Maps;

/// <summary>One bank of maps, addressed by the game as (bank, map).</summary>
public sealed record MapBank(int Index, int Offset, IReadOnlyList<MapHeaderRecord> Maps)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;
}

/// <summary>The two-level table that gives every map its (bank, map) address.</summary>
public sealed record MapBankTable(int Offset, IReadOnlyList<MapBank> Banks)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    public int MapCount => Banks.Sum(b => b.Maps.Count);

    /// <summary>Every map, paired with the bank and map numbers the game addresses it by.</summary>
    public IEnumerable<(int Bank, int Map, MapHeaderRecord Header)> AllMaps =>
        Banks.SelectMany(bank => bank.Maps.Select((header, index) => (bank.Index, index, header)));

    public override string ToString() =>
        $"MapBanks         0x{Address:X8}  {Banks.Count} banks, {MapCount} maps";
}

/// <summary>
/// Finds the map bank table, and with it the game's own map numbering.
/// <para>
/// The structure is two levels of indirection: a table of pointers, each to an array
/// of pointers, each to a map header. That shape is its own signature — the leaves
/// have to be records whose layout pointer resolves to a valid layout, which is
/// specific enough that nothing else in the image satisfies it at length.
/// </para>
/// </summary>
public static class MapBankLocator
{
    /// <summary>Banks a candidate table must have before it is believed.</summary>
    private const int MinimumBanks = 8;

    /// <summary>Maps a candidate table must total before it is believed.</summary>
    private const int MinimumTotalMaps = 32;

    public static MapBankTable? Locate(Rom rom, Action<string>? log = null)
    {
        var headerCache = new Dictionary<int, MapHeaderRecord?>();

        MapHeaderRecord? HeaderAt(uint pointer)
        {
            if (rom.ToOffsetOrNull(pointer) is not { } offset) return null;

            if (!headerCache.TryGetValue(offset, out MapHeaderRecord? header))
            {
                header = MapHeaderRecord.TryParse(rom, offset);
                headerCache[offset] = header;
            }

            return header;
        }

        // Every position holding a pointer to a valid map header. A bank's array is a
        // run of these, and the bank table points at the start of each run.
        var headerPointerOffsets = new HashSet<int>();

        for (int offset = 0; offset + 4 <= rom.Length; offset += 4)
        {
            if (HeaderAt(rom.ReadU32(offset)) is not null)
                headerPointerOffsets.Add(offset);
        }

        log?.Invoke($"  map banks: {headerPointerOffsets.Count} pointers to valid map headers");

        if (headerPointerOffsets.Count < MinimumTotalMaps) return null;

        MapBankTable? best = null;

        for (int offset = 0; offset + MinimumBanks * 4 <= rom.Length; offset += 4)
        {
            var starts = new List<int>();

            for (int i = 0; offset + (i + 1) * 4 <= rom.Length; i++)
            {
                uint pointer = rom.ReadU32(offset + i * 4);
                if (rom.ToOffsetOrNull(pointer) is not { } bankOffset) break;
                if (!headerPointerOffsets.Contains(bankOffset)) break;

                starts.Add(bankOffset);
            }

            if (starts.Count < MinimumBanks) continue;

            // Bank arrays are laid out back-to-back, so walking one until its pointers
            // stop resolving would swallow every bank that follows. The bank table is
            // itself the boundary: each bank ends where the next one begins.
            var banks = new List<MapBank>();

            for (int i = 0; i < starts.Count; i++)
            {
                int limit = starts
                    .Where(start => start > starts[i])
                    .DefaultIfEmpty(rom.Length)
                    .Min();

                banks.Add(new MapBank(i, starts[i], ReadBank(rom, starts[i], limit, HeaderAt)));
            }

            var candidate = new MapBankTable(offset, banks);
            if (candidate.MapCount < MinimumTotalMaps) continue;

            log?.Invoke($"  map banks: {banks.Count} banks, {candidate.MapCount} maps at 0x{candidate.Address:X8}");

            if (best is null || candidate.MapCount > best.MapCount)
                best = candidate;

            offset += banks.Count * 4 - 4;
        }

        return best;
    }

    /// <summary>
    /// Walks one bank's array of header pointers, stopping at <paramref name="limit"/>
    /// — the next bank's start — or at the first entry that is not a header.
    /// </summary>
    private static List<MapHeaderRecord> ReadBank(
        Rom rom, int offset, int limit, Func<uint, MapHeaderRecord?> headerAt)
    {
        var maps = new List<MapHeaderRecord>();

        for (int i = 0; offset + (i + 1) * 4 <= Math.Min(limit, rom.Length); i++)
        {
            if (headerAt(rom.ReadU32(offset + i * 4)) is not { } header) break;
            maps.Add(header);
        }

        return maps;
    }

}
