namespace PokeMmo.RomExtract.Maps;

/// <summary>How wide one record of an event list is, and where its script pointer sits.</summary>
public sealed record EventShape(
    int SizeBytes, int SquareOffset, int PointerOffset, int Records, int Good)
{
    /// <summary>The share of records that read as a square on the map with a script behind it.</summary>
    public double Share => Records == 0 ? 0 : (double)Good / Records;

    public override string ToString() =>
        $"{SizeBytes,2} bytes, square at +{SquareOffset,-2} pointer at +{PointerOffset,-2}  " +
        $"{Good,5}/{Records,-5} {Share,6:P0}";
}

/// <summary>
/// Works out the shape of the two event lists this project has never read.
/// <para>
/// A map's events record holds four counts and four pointers. Two of the four have been
/// read since the beginning — the people standing about and the doors — and the other
/// two have been sitting there the whole time. They are the scripts that fire when a
/// player walks onto a square, and the signs they can read, which between them are
/// where most of a Pokémon game's story actually lives.
/// </para>
/// <para>
/// Their record sizes are not written down anywhere, so they are derived the way every
/// other width in this project has been: by trying each one against the whole cartridge
/// and asking which produces records that are true of a map. The test is strong because
/// two independent things have to come out right at once — a square inside the map's
/// own bounds, and, at some fixed offset, a pointer into the ROM with a readable script
/// at the end of it. A stride wrong by one satisfies neither, and satisfies neither
/// four hundred times over.
/// </para>
/// </summary>
public static class EventLayout
{
    /// <summary>Four counts, then four pointers.</summary>
    public const int PointersOffset = 4;

    /// <summary>Which of the four lists a kind is, counting from zero.</summary>
    public const int People = 0;
    public const int Warps = 1;
    public const int Triggers = 2;
    public const int Signs = 3;

    private const int SmallestRecord = 8;
    private const int LargestRecord = 24;

    /// <summary>
    /// A count past this means the pointer is not an events record at all, which is
    /// worth noticing rather than reading sixty thousand of anything.
    /// </summary>
    private const int MostRecords = 128;

    /// <summary>
    /// Scores every plausible shape for one of the four lists, best first.
    /// <para>
    /// Sizes are tried in fours because every one of these records is word-aligned and
    /// ends on a pointer; the pointer offset is tried at every multiple of four inside
    /// the record for the same reason.
    /// </para>
    /// </summary>
    public static List<EventShape> Derive(
        Rom rom,
        IEnumerable<(int Width, int Height, uint EventsPointer)> maps,
        int list)
    {
        List<(int Width, int Height, uint EventsPointer)> all = [.. maps];

        var scored = new List<EventShape>();

        for (int size = SmallestRecord; size <= LargestRecord; size += 4)
        {
            // The square is swept as well as the pointer, and it has to be. An object
            // event keeps its x and y at +4, not at +0, and a scan that assumed +0
            // scored the one list whose answer is already known at one per cent — which
            // is the control doing its job, and the only reason this is not still
            // confidently wrong about the two lists nobody has read.
            for (int square = 0; square + 4 <= size; square += 2)
            {
                for (int pointer = 4; pointer + 4 <= size; pointer += 4)
                {
                    if (pointer < square + 4) continue;

                    (int records, int good) = Score(rom, all, list, size, square, pointer);

                    if (records > 0) scored.Add(new EventShape(size, square, pointer, records, good));
                }
            }
        }

        return [.. scored.OrderByDescending(s => s.Share).ThenBy(s => s.SizeBytes).ThenBy(s => s.SquareOffset)];
    }

    private static (int Records, int Good) Score(
        Rom rom,
        List<(int Width, int Height, uint EventsPointer)> maps,
        int list,
        int size,
        int squareOffset,
        int pointerOffset)
    {
        int records = 0;
        int good = 0;

        foreach ((int width, int height, uint events) in maps)
        {
            // Written out rather than as `is not var (…)`, which always matches and
            // then quietly deconstructs a null into zeros — a guard that reads like one
            // and is not one.
            if (Table(rom, events, list, size) is not { } found) continue;

            (int table, int count) = found;

            for (int i = 0; i < count; i++)
            {
                int at = table + i * size;

                records++;

                int x = (short)rom.ReadU16(at + squareOffset);
                int y = (short)rom.ReadU16(at + squareOffset + 2);

                if (x < 0 || x >= width || y < 0 || y >= height) continue;

                uint script = rom.ReadU32(at + pointerOffset);

                // Zero is ordinary and neither right nor wrong: plenty of these carry
                // no script at all. Counting it as a hit would reward reading a field
                // of padding as a pointer, which is exactly the wrong stride's shape.
                if (script == 0) continue;
                if (rom.ToOffsetOrNull(script) is not { } start) continue;
                if (Scripts.ScriptReader.Read(rom, script) is not { Count: > 1 } read) continue;
                if (start >= rom.Length) continue;

                good++;
            }
        }

        return (records, good);
    }

    /// <summary>Why one record did not read as a square with a script behind it.</summary>
    public enum Miss
    {
        Fine,
        OffTheMap,
        NoPointer,
        NotAScript,
    }

    /// <summary>
    /// Every record of one list under one shape, with the reason each one missed.
    /// <para>
    /// A share of eighty-nine per cent is not an answer, it is a question: either the
    /// shape is wrong or the eleven per cent are a second kind of record. Which of those
    /// it is can only be told apart by looking at what the misses have in common.
    /// </para>
    /// </summary>
    public static List<(Miss Why, byte Kind)> Explain(
        Rom rom,
        IEnumerable<(int Width, int Height, uint EventsPointer)> maps,
        int list,
        EventShape shape,
        int kindOffset)
    {
        var found = new List<(Miss, byte)>();

        foreach ((int width, int height, uint events) in maps)
        {
            if (Table(rom, events, list, shape.SizeBytes) is not { } where) continue;

            (int table, int count) = where;

            for (int i = 0; i < count; i++)
            {
                int at = table + i * shape.SizeBytes;

                byte kind = kindOffset < shape.SizeBytes ? rom.ReadU8(at + kindOffset) : (byte)0;

                int x = (short)rom.ReadU16(at + shape.SquareOffset);
                int y = (short)rom.ReadU16(at + shape.SquareOffset + 2);

                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    found.Add((Miss.OffTheMap, kind));
                    continue;
                }

                uint script = rom.ReadU32(at + shape.PointerOffset);

                if (script == 0 || rom.ToOffsetOrNull(script) is null)
                {
                    found.Add((Miss.NoPointer, kind));
                    continue;
                }

                found.Add((Scripts.ScriptReader.Read(rom, script) is { Count: > 1 } ? Miss.Fine : Miss.NotAScript, kind));
            }
        }

        return found;
    }

    /// <summary>Where one of the four lists is and how long, or nothing.</summary>
    public static (int Table, int Count)? Table(Rom rom, uint eventsPointer, int list, int size)
    {
        if (eventsPointer == 0) return null;
        if (rom.ToOffsetOrNull(eventsPointer) is not { } events) return null;
        if (events + PointersOffset + list * 4 + 4 > rom.Length) return null;

        int count = rom.ReadU8(events + list);
        if (count is 0 or > MostRecords) return null;

        uint pointer = rom.ReadU32(events + PointersOffset + list * 4);
        if (rom.ToOffsetOrNull(pointer) is not { } table) return null;
        if (table + count * size > rom.Length) return null;

        return (table, count);
    }
}
