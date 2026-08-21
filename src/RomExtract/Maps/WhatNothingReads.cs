namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// One byte position inside an event record that no reader in this project ever looks at.
/// </summary>
/// <param name="List">Which of the four lists.</param>
/// <param name="Offset">Which byte of the record.</param>
/// <param name="Values">Every value it takes across every record of that list, with how many.</param>
public sealed record UnreadByte(
    string List, int Offset, IReadOnlyDictionary<int, int> Values)
{
    /// <summary>How many records were asked.</summary>
    public int Records => Values.Values.Sum();

    /// <summary>
    /// True when the byte is nought in every record — spare, and nothing is hiding in it.
    /// </summary>
    public bool AlwaysNought => Values.Count == 1 && Values.ContainsKey(0);

    /// <summary>
    /// How many records DON'T carry the commonest value — the size of whatever is in there.
    /// </summary>
    public int Unusual => Records - (Values.Count == 0 ? 0 : Values.Values.Max());
}

/// <summary>
/// Which bytes of a map's event records nothing in this project reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>259 found the object table's kind byte by hand.</b> Nine records had <c>0xFF</c> where 1639
/// had nought, in a byte no reader consumed, and it took a hexdump and a hunch. Signs have a kind
/// byte too, read since 248. The question nobody has asked is whether warps and triggers have
/// one — and asking it record by record is the same hunch again.
/// </para>
/// <para>
/// <b>The reader says which bytes it reads.</b> The list of consumed offsets is not written down
/// anywhere and must not be: a hand-kept list goes stale the first time a field is added, which is
/// the fault this project has fixed at 220, 224, 251 and 258. <see cref="Rom.WatchReads"/> records
/// what the reader actually touched, so this cannot disagree with it.
/// </para>
/// <para>
/// A byte nothing reads is not a finding. A byte nothing reads that takes MORE THAN ONE VALUE is,
/// and the difference is the whole instrument.
/// </para>
/// </remarks>
public static class WhatNothingReads
{
    /// <summary>
    /// Every byte position of every event record, minus the ones a reader touched, with what each
    /// of the remainder holds across the whole cartridge.
    /// </summary>
    /// <param name="read">
    /// Runs every reader over one map. Whatever it touches counts as read — which is the point:
    /// the caller supplies the readers rather than this supplying a list of offsets.
    /// </param>
    public static List<UnreadByte> In(
        Rom rom, IEnumerable<MapHeaderRecord> headers, Action<Rom, MapHeaderRecord> read)
    {
        var touched = new HashSet<int>();
        var values = new Dictionary<(string List, int Offset), Dictionary<int, int>>();
        var records = new List<(string List, int At, int Size)>();

        foreach (MapHeaderRecord header in headers)
        {
            using (rom.WatchReads(touched)) read(rom, header);

            foreach ((string list, int table, int count, int size) in
                     MapLinkExtractor.EventTables(rom, header))
            {
                for (var i = 0; i < count; i++) records.Add((list, table + i * size, size));
            }
        }

        // The tables are enumerated OUTSIDE the watch on purpose — finding a table is not
        // reading a record, and counting the events header's own bytes as read would answer a
        // different question. Only the record's own bytes are asked about.
        foreach ((string list, int at, int size) in records)
        {
            for (var k = 0; k < size; k++)
            {
                if (touched.Contains(at + k)) continue;

                if (!values.TryGetValue((list, k), out Dictionary<int, int>? seen))
                    values[(list, k)] = seen = [];

                int value = rom.Span[at + k];

                seen[value] = seen.GetValueOrDefault(value) + 1;
            }
        }

        return
        [
            .. values
                .OrderBy(v => v.Key.List)
                .ThenBy(v => v.Key.Offset)
                .Select(v => new UnreadByte(v.Key.List, v.Key.Offset, v.Value)),
        ];
    }
}
