namespace PokeMmo.RomExtract.Maps;

/// <summary>A table of place names, indexed by a map header's region-section id.</summary>
public sealed record RegionNameTable(int Offset, string Shape, IReadOnlyList<string> Names)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    public int Count => Names.Count;

    /// <summary>
    /// How many entries read like the name of a place rather than an arbitrary string.
    /// This is what separates the location table from the other runs of text pointers
    /// an image contains.
    /// </summary>
    public int PlaceWordScore => Names.Count(RegionNameLocator.ReadsLikeAPlace);

    public string this[int index] =>
        index >= 0 && index < Names.Count ? Names[index] : $"SECTION {index}";

    public override string ToString() =>
        $"0x{Address:X8}  {Count,4} names, {PlaceWordScore,3} place-like  ({Shape})" +
        (Count > 0 ? $"  e.g. {string.Join(", ", Names.Take(3))}" : "");
}

/// <summary>
/// Finds the table that turns a map's region-section id into a name.
/// <para>
/// Two shapes are searched, because the layout differs between games: a record of
/// <c>{x, y, width, height, name}</c>, and a bare array of text pointers. Candidates
/// from both are ranked together rather than one being assumed — an image contains
/// several long runs of text pointers, and the one holding place names is identified
/// by its contents, not by its shape or its address.
/// </para>
/// </summary>
public static class RegionNameLocator
{
    private const int MinimumRun = 32;

    /// <summary>Longest a place name is expected to be, used to bound a speculative read.</summary>
    private const int MaxNameBytes = 24;

    /// <summary>
    /// Words that appear in place names across the series. A run containing many of
    /// them is the location table; a run containing none is something else.
    /// </summary>
    private static readonly string[] PlaceWords =
    [
        "TOWN", "CITY", "ROUTE", "ISLAND", "CAVE", "FOREST", "MT", "MOUNT",
        "SEA", "ROAD", "TUNNEL", "PATH", "PLATEAU", "ZONE", "ISLE", "VALLEY",
    ];

    public static bool ReadsLikeAPlace(string name) =>
        PlaceWords.Any(word => name.Contains(word, StringComparison.Ordinal));

    /// <summary>
    /// Returns the best candidate, preferring the run that reads most like a list of
    /// places and falling back to the longest run when none stands out.
    /// </summary>
    public static RegionNameTable? Locate(Rom rom, Action<string>? log = null)
    {
        List<RegionNameTable> candidates = ScanCandidates(rom);

        foreach (RegionNameTable candidate in candidates.OrderByDescending(c => c.PlaceWordScore).Take(5))
            log?.Invoke($"  region names: {candidate}");

        return candidates
            .OrderByDescending(c => c.PlaceWordScore)
            .ThenByDescending(c => c.Count)
            .FirstOrDefault();
    }

    /// <summary>Every run of either shape that is long enough to be a name table.</summary>
    public static List<RegionNameTable> ScanCandidates(Rom rom)
    {
        var found = new List<RegionNameTable>();
        found.AddRange(ScanRuns(rom, stride: 8, nameFieldOffset: 4, shape: "x,y,w,h + name"));
        found.AddRange(ScanRuns(rom, stride: 4, nameFieldOffset: 0, shape: "pointer array"));
        return found;
    }

    /// <summary>
    /// Walks the image for runs of fixed-size records whose name field points at
    /// decodable text.
    /// </summary>
    private static List<RegionNameTable> ScanRuns(Rom rom, int stride, int nameFieldOffset, string shape)
    {
        var found = new List<RegionNameTable>();

        for (int offset = 0; offset + stride * MinimumRun <= rom.Length; offset += 4)
        {
            var names = new List<string>();

            while (ReadName(rom, offset + names.Count * stride, nameFieldOffset, stride) is { } name)
                names.Add(name);

            if (names.Count < MinimumRun) continue;

            found.Add(new RegionNameTable(offset, shape, names));
            offset += names.Count * stride - 4;
        }

        return found;
    }

    private static string? ReadName(Rom rom, int entryOffset, int nameFieldOffset, int stride)
    {
        if (entryOffset < 0 || entryOffset + stride > rom.Length) return null;

        // For the coordinate-bearing shape, the leading bytes must be small. For a bare
        // pointer array there are no leading bytes to check.
        if (nameFieldOffset > 0)
        {
            for (int i = 0; i < nameFieldOffset; i++)
            {
                if (rom.ReadU8(entryOffset + i) > 40) return null;
            }
        }

        uint pointer = rom.ReadU32(entryOffset + nameFieldOffset);
        if (rom.ToOffsetOrNull(pointer) is not { } nameOffset) return null;

        string name = GameText.Decode(rom.Slice(nameOffset, Math.Min(MaxNameBytes, rom.Length - nameOffset)));
        return LooksLikeLocationName(name) ? name : null;
    }

    /// <summary>
    /// Location names are short, upper-case and punctuated sparsely. Anything else
    /// means the pointer was not a name.
    /// </summary>
    public static bool LooksLikeLocationName(string name)
    {
        if (name.Length < 3) return false;

        bool hasLetter = false;

        foreach (char c in name)
        {
            if (char.IsAsciiLetterUpper(c)) { hasLetter = true; continue; }
            if (char.IsAsciiDigit(c) || c is ' ' or '.' or '-' or '’') continue;
            return false;
        }

        return hasLetter;
    }
}
