using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Maps;

/// <summary>One sign record of the buried kind, as bytes and as fields.</summary>
/// <param name="MapId">Which map it is on.</param>
/// <param name="At">Where the twelve-byte record sits in the file.</param>
/// <param name="Word">The four bytes every other sign uses for a script pointer.</param>
public sealed record Buried(string MapId, int At, int X, int Y, uint Word)
{
    /// <summary>The first halfword of the four — an item id if this reading is right.</summary>
    public int Item => (int)(Word & 0xFFFF);

    /// <summary>
    /// Whether the first halfword resolves to a NAMED item, which entry nought does not.
    /// </summary>
    /// <remarks>
    /// <b>A placeholder is not a name.</b> 248 reported "183 of 183 resolve to a name in the item
    /// table's 308 entries" and made that the evidence the field is an item id — and twelve of
    /// the 183 resolve to entry nought, which this cartridge's table calls <c>????????</c>. The
    /// reading is stronger stated honestly: every id that is not the blank resolves, 171 of 171.
    /// Counting a placeholder as a hit is how a test that could have failed stops being able to.
    /// </remarks>
    public bool NamesAnItem(IReadOnlyDictionary<int, string> names) =>
        Item != 0 && names.ContainsKey(Item);

    /// <summary>The third byte.</summary>
    public int Third => (int)((Word >> 16) & 0xFF);

    /// <summary>The fourth byte.</summary>
    public int Fourth => (int)((Word >> 24) & 0xFF);

    public override string ToString() =>
        $"{MapId} ({X},{Y}) 0x{At:X6}  {Word & 0xFF:X2} {(Word >> 8) & 0xFF:X2}"
        + $" {Third:X2} {Fourth:X2}   item {Item}, third {Third}, fourth {Fourth}";
}

/// <summary>
/// The four bytes a buried item keeps where every other sign keeps a script pointer.
/// <para>
/// <b>183 of this cartridge's 702 signs are this kind</b> and nothing in this project has ever
/// read their word. <see cref="MapLinkExtractor.ReadSigns"/> deliberately does not follow it as a
/// pointer — that was 239's finding and it was the right call — and then throws it away.
/// </para>
/// <para>
/// <b>Why it is worth reading.</b> A buried item is picked up and stays picked up, so something
/// remembers it, and a flag is how this cartridge remembers anything. If these records carry an
/// INDEX rather than a flag number, the flag has to be computed from a base — which is precisely
/// the blind spot 246 printed and could not close, and it would be a fourth kind of read that is
/// not a command.
/// </para>
/// <para>
/// <b>The fields are not asserted.</b> This reads the four bytes and offers three splittings of
/// them; which one is right is a question for the caller's own evidence, and the raw word is
/// carried so a reader can disagree.
/// </para>
/// </summary>
public static class WhatIsBuried
{
    /// <summary>The record is twelve bytes and the word is the last four.</summary>
    public const int SignSizeBytes = 12;

    private const int KindOffset = 5;

    private const int WordOffset = 8;

    /// <summary>Every buried sign on every map, with the bytes.</summary>
    public static List<Buried> In(Rom rom, IEnumerable<LoadedMap> maps) =>
    [
        .. maps.SelectMany(
            map => On(rom, WorldExporter.MapId(map.Bank, map.Number), map.EventsPointer)),
    ];

    /// <summary>
    /// The buried signs in one map's sign list, taking the pointer rather than a loaded map.
    /// <para>
    /// Split out so a fixture can reach it with a Rom and an address. A rule that needs a whole
    /// cartridge is a rule no test can ask, which is the fault four milestones running found by
    /// breaking a guard and getting green back.
    /// </para>
    /// </summary>
    public static IEnumerable<Buried> On(Rom rom, string mapId, uint eventsPointer)
    {
        if (EventLayout.Table(rom, eventsPointer, EventLayout.Signs, SignSizeBytes)
            is not { } list)
        {
            yield break;
        }

        (int table, int count) = list;

        for (var i = 0; i < count; i++)
        {
            int at = table + i * SignSizeBytes;

            // ONLY THE BURIED KIND. Every other sign keeps a script pointer in these four
            // bytes, and reading one as an item and an index would put four hundred and forty
            // invented indices into a list whose whole claim is that its indices are distinct.
            if (rom.ReadU8(at + KindOffset) != MapSign.HiddenItem) continue;

            yield return new Buried(
                mapId,
                at,
                (short)rom.ReadU16(at),
                (short)rom.ReadU16(at + 2),
                rom.ReadU32(at + WordOffset));
        }
    }

    /// <summary>
    /// Every sign's KIND byte on every map, tallied — so "there is no third kind" is measured.
    /// </summary>
    /// <remarks>
    /// <b>A hole in the index is a slot nothing claims</b> (279), and the first thing to ask is
    /// whether some OTHER kind of sign record claims it. That is a count, and counting it beats
    /// asserting it: this project has twice found a second kind of record inside a table it
    /// thought held one (259's clones, 248's buried signs themselves).
    /// </remarks>
    public static (IReadOnlyDictionary<int, int> Kinds, IReadOnlyDictionary<int, int> Pointers)
        KindsOfSign(Rom rom, IEnumerable<LoadedMap> maps)
    {
        var kinds = new Dictionary<int, int>();
        var pointers = new Dictionary<int, int>();

        foreach (LoadedMap map in maps)
        {
            if (EventLayout.Table(rom, map.EventsPointer, EventLayout.Signs, SignSizeBytes)
                is not { } list)
            {
                continue;
            }

            (int table, int count) = list;

            for (var i = 0; i < count; i++)
            {
                int kind = rom.ReadU8(table + (i * SignSizeBytes) + KindOffset);

                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;

                uint word = rom.ReadU32(table + (i * SignSizeBytes) + WordOffset);

                if (word >> 24 == 0x08) pointers[kind] = pointers.GetValueOrDefault(kind) + 1;
            }
        }

        return (kinds, pointers);
    }

    /// <summary>One sign's kind byte and where it stands.</summary>
    public sealed record ASign(string MapId, int At, int X, int Y, int Kind, uint Word);

    /// <summary>Every sign on every map, with its kind — not just the buried ones.</summary>
    public static List<ASign> EverySign(Rom rom, IEnumerable<LoadedMap> maps)
    {
        var found = new List<ASign>();

        foreach (LoadedMap map in maps)
        {
            if (EventLayout.Table(rom, map.EventsPointer, EventLayout.Signs, SignSizeBytes)
                is not { } list)
            {
                continue;
            }

            (int table, int count) = list;

            for (var i = 0; i < count; i++)
            {
                int at = table + (i * SignSizeBytes);

                found.Add(
                    new ASign(
                        WorldExporter.MapId(map.Bank, map.Number),
                        at,
                        (short)rom.ReadU16(at),
                        (short)rom.ReadU16(at + 2),
                        rom.ReadU8(at + KindOffset),
                        rom.ReadU32(at + WordOffset)));
            }
        }

        return found;
    }

    /// <summary>
    /// Whether a set of numbers is a dense index: every value from nought to the largest, each
    /// exactly once.
    /// <para>
    /// <b>The test that decides whether a flag has to be computed.</b> A field holding each of
    /// 0..N-1 once is an index into something; a field holding flag numbers would be scattered
    /// across whatever range the flags occupy and would have gaps. The two are not close.
    /// </para>
    /// </summary>
    public static bool IsADenseIndex(IReadOnlyCollection<int> values) =>
        values.Count > 0
        && values.Distinct().Count() == values.Count
        && values.Min() == 0
        && values.Max() == values.Count - 1;

    /// <summary>
    /// How far a set of numbers is from being one, so the answer is never a bare yes or no.
    /// </summary>
    /// <param name="Values">How many there are.</param>
    /// <param name="Distinct">How many are distinct — equal to <paramref name="Values"/> for an index.</param>
    /// <param name="Low">The smallest.</param>
    /// <param name="High">The largest.</param>
    /// <param name="Missing">Values in [Low, High] that nothing holds.</param>
    public sealed record HowDense(int Values, int Distinct, int Low, int High, int Missing)
    {
        public override string ToString() =>
            $"{Values} value(s), {Distinct} distinct, {Low} to {High}, {Missing} gap(s) in the range";
    }

    /// <summary>
    /// The buried kinds no script anywhere names — the ones a player can only get by digging.
    /// </summary>
    /// <param name="buried">The records.</param>
    /// <param name="namedElsewhere">
    /// Every item id a script the maps open names, in any of the five ways
    /// <c>ItemMentions</c> reads: handed over, taken away, asked for, loaded for a routine, sold.
    /// <b>The buried records themselves must not be in it</b> — a population that counted them
    /// would report every buried kind as having another source, and would do it silently.
    /// </param>
    /// <remarks>
    /// <b>Item nought is not an item.</b> Twelve of this cartridge's 183 buried records name it,
    /// carry a count of 10, 20, 40 or 100 where every other one carries 1, and sit on the one map
    /// holding all five of 208's coin chains. Whatever they hand over is not in the item table,
    /// and listing "????????" as a thing only found underground would be a reading of a
    /// placeholder.
    /// </remarks>
    public static IReadOnlyList<int> OnlyBuried(
        IEnumerable<Buried> buried, IReadOnlyCollection<int> namedElsewhere) =>
    [
        .. buried.Select(b => b.Item)
            .Where(id => id != 0 && !namedElsewhere.Contains(id))
            .Distinct()
            .Order(),
    ];

    /// <summary>The buried things on maps that are not in <paramref name="reached"/>.</summary>
    /// <remarks>
    /// Keyed by map, which is all a buried record offers — it has a square and no script, so
    /// there is nothing finer to key on and nothing for a walk to run. Standing on the map is as
    /// close as this project can say the run gets.
    /// </remarks>
    public static IReadOnlyList<Buried> NeverStoodOn(
        IEnumerable<Buried> buried, IReadOnlyCollection<string> reached) =>
        [.. buried.Where(b => !reached.Contains(b.MapId))];

    /// <summary>A run of consecutive numbers nothing in the file names.</summary>
    /// <param name="From">The first number of the run.</param>
    /// <param name="Length">How many consecutive numbers it covers.</param>
    public sealed record Window(int From, int Length)
    {
        public int To => From + Length - 1;

        public override string ToString() => $"0x{From:X4}-0x{To:X4} ({Length} wide)";
    }

    /// <summary>
    /// Every run of at least <paramref name="least"/> consecutive numbers below
    /// <paramref name="ceiling"/> that <paramref name="named"/> does not contain.
    /// <para>
    /// <b>Where a computed flag range could live, derived rather than known.</b> If a buried
    /// item's flag is a base plus an index, the base plus every index has to land somewhere no
    /// script ever names — a hidden item's flag is the pickup routine's own business and nothing
    /// in the data says it. So the range is a gap in the flag number line wide enough to hold
    /// every index, and the number of such gaps is the answer's error bar: one is a finding and
    /// six is unanswerable.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Window> Gaps(IReadOnlyCollection<int> named, int ceiling, int least)
    {
        var found = new List<Window>();

        HashSet<int> taken = [.. named];

        var run = 0;

        for (var n = 0; n <= ceiling; n++)
        {
            if (!taken.Contains(n))
            {
                run++;
                continue;
            }

            if (run >= least) found.Add(new Window(n - run, run));

            run = 0;
        }

        if (run >= least) found.Add(new Window(ceiling + 1 - run, run));

        return found;
    }

    /// <summary>The same question with its working shown.</summary>
    public static HowDense Density(IReadOnlyCollection<int> values)
    {
        if (values.Count == 0) return new HowDense(0, 0, 0, 0, 0);

        HashSet<int> held = [.. values];

        int low = values.Min();
        int high = values.Max();

        return new HowDense(
            values.Count,
            held.Count,
            low,
            high,
            Enumerable.Range(low, high - low + 1).Count(n => !held.Contains(n)));
    }
}
