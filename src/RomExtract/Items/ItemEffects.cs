using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Items;

/// <summary>What an item clears, and where that was found.</summary>
public sealed record CureTable(int Address, int FirstItem, int LastItem, int Column, IReadOnlyDictionary<int, Ailments> Cures);

/// <summary>
/// Which condition each medicine clears.
/// <para>
/// <see cref="ItemData"/> has carried a note about this since potions worked: an
/// Antidote and a Full Heal both have zero in every field of their own record, so which
/// condition each one clears really does live somewhere else. All six of them run the
/// same field routine as well — 0x080A16E1 on this image — so even the routine, which
/// was enough to pick the stones out of three hundred items, cannot tell them apart.
/// </para>
/// <para>
/// It is a second table: one pointer per item over a stretch of the medicine pocket,
/// each pointing at a short array. Nothing points at the table from anything already
/// located, and a scan for "runs of valid pointers" finds tens of thousands.
/// </para>
/// <para>
/// So the shape is not what finds it. What finds it is a pattern that can only be one
/// thing: <b>five items each setting a single distinct bit in the same column, and the
/// item that clears everything setting all five at once.</b> Those six items are named
/// for exactly what they do, so this is the one derivation in the project that needs
/// the cartridge's own words — the same allowance the ball kinds have, and for the same
/// reason: names stop at the exporter and a number crosses into the rules file.
/// </para>
/// <para>
/// On a real FireRed that pattern occurs <b>once in sixteen megabytes</b>. It is not a
/// scan that needs a tie broken.
/// </para>
/// </summary>
public static class ItemEffects
{
    /// <summary>
    /// The five that are named for the one thing they clear.
    /// <para>
    /// Not a list of every cure — HEAL POWDER and LAVA COOKIE clear the same six things
    /// and are named for neither. These are the ones whose name <em>is</em> the answer,
    /// which is what makes them usable as anchors.
    /// </para>
    /// </summary>
    private static readonly (string Name, Ailments Means)[] Anchors =
    [
        ("ANTIDOTE", Ailments.Poison),
        ("BURN HEAL", Ailments.Burn),
        ("ICE HEAL", Ailments.Freeze),
        ("AWAKENING", Ailments.Sleep),
        ("PARLYZ HEAL", Ailments.Paralysis),
    ];

    /// <summary>The one that clears the lot, and so proves the column is a column.</summary>
    private const string Everything = "FULL HEAL";

    /// <summary>How far into a short array to look for the column.</summary>
    private const int Columns = 16;

    public static CureTable? Locate(Rom rom, IReadOnlyList<ItemRecord> items, Action<string>? log = null)
    {
        int Named(string name) =>
            items.FirstOrDefault(i => string.Equals(i.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))?.Id ?? -1;

        int[] anchors = [.. Anchors.Select(a => Named(a.Name))];
        int everything = Named(Everything);

        if (anchors.Any(a => a < 0) || everything < 0)
        {
            log?.Invoke("  cures: this cartridge does not name the six anchor items — nothing cures anything");
            return null;
        }

        int lowest = anchors.Append(everything).Min();
        int highest = anchors.Append(everything).Max();

        var found = new List<(int At, int Column, int[] Bits, int All)>();

        // The whole table is fixed relative to any one entry, so only one thing is being
        // searched for: where the first anchor's entry sits. There is no second unknown.
        for (int at = 0; at + 4 * (highest - lowest) + 4 <= rom.Length; at += 4)
        {
            int Entry(int id) => at + 4 * (id - anchors[0]);

            int?[] targets =
            [
                .. anchors.Select(a => Target(rom, Entry(a))),
                Target(rom, Entry(everything)),
            ];

            if (targets.Any(t => t is null)) continue;

            for (int column = 0; column < Columns; column++)
            {
                int[] bits = [.. targets.Take(anchors.Length).Select(t => (int)rom.ReadU8(t!.Value + column))];

                // A single bit each, no two the same. Anything else is bytes.
                if (bits.Any(b => b == 0 || (b & (b - 1)) != 0)) continue;
                if (bits.Distinct().Count() != anchors.Length) continue;

                int all = rom.ReadU8(targets[^1]!.Value + column);
                int union = bits.Aggregate(0, (a, b) => a | b);

                if ((all & union) != union) continue;

                found.Add((at, column, bits, all));
            }
        }

        if (found.Count == 0)
        {
            log?.Invoke("  cures: no column found where five named items each claim one bit");
            return null;
        }

        if (found.Count > 1)
        {
            log?.Invoke($"  cures: {found.Count} columns fit the pattern — not choosing between them");
            return null;
        }

        (int table, int found_column, int[] found_bits, int everythingBits) = found[0];

        return Read(rom, items, table, anchors[0], found_column, found_bits, everythingBits, log);
    }

    private static CureTable Read(
        Rom rom,
        IReadOnlyList<ItemRecord> items,
        int table,
        int anchorItem,
        int column,
        int[] bits,
        int everythingBits,
        Action<string>? log)
    {
        int Entry(int id) => table + 4 * (id - anchorItem);

        // How far the table runs. An entry is an entry while it points into the image,
        // and the ones on either side of the run do not — one holds 0x1E and the other
        // holds nothing at all.
        int first = anchorItem, last = anchorItem;

        while (first > 0 && Target(rom, Entry(first - 1)) is not null) first--;
        while (last < items.Count - 1 && Target(rom, Entry(last + 1)) is not null) last++;

        var meaning = new Dictionary<int, Ailments>();

        for (int i = 0; i < bits.Length; i++) meaning[bits[i]] = Anchors[i].Means;

        // The sixth. Five bits are named, the union has six, and this project models six
        // things that can be wrong with a creature — five conditions and confusion. Five
        // are spoken for and one is left, and one thing is left, so they are each other.
        //
        // What rules out its being a marker rather than an ailment is that some item sets
        // it and nothing else: a "clears everything" flag would never appear on its own.
        int spare = everythingBits & ~bits.Aggregate(0, (a, b) => a | b);
        bool alone = false;

        if (spare != 0 && (spare & (spare - 1)) == 0)
        {
            for (int id = first; id <= last && !alone; id++)
                alone = Target(rom, Entry(id)) is { } t && rom.ReadU8(t + column) == spare;

            if (alone) meaning[spare] = Ailments.Confusion;
        }

        var cures = new Dictionary<int, Ailments>();

        for (int id = first; id <= last; id++)
        {
            if (Target(rom, Entry(id)) is not { } target) continue;

            int written = rom.ReadU8(target + column);

            if (written == 0) continue;

            Ailments clears = meaning
                .Where(m => (written & m.Key) != 0)
                .Aggregate(Ailments.None, (all, m) => all | m.Value);

            if (clears != Ailments.None) cures[id] = clears;
        }

        log?.Invoke(
            $"  cures: table at 0x{Rom.BaseAddress + (uint)Entry(first):X8} for items {first}..{last}, " +
            $"column {column} — {cures.Count} of them clear something");

        log?.Invoke(alone
            ? $"  cures: bit 0x{spare:X2} is named by no item and stands alone on one, so it is the " +
              "sixth thing and confusion is the sixth thing"
            : "  cures: no leftover bit stands alone, so nothing here is read as confusion");

        foreach ((string name, Ailments means) in Anchors)
        {
            int bit = meaning.First(m => m.Value == means).Key;

            log?.Invoke($"    0x{bit:X2}  {means}   (from {name})");
        }

        return new CureTable(Entry(first), first, last, column, cures);
    }

    /// <summary>
    /// A pointer that goes somewhere in this image, as an offset — or nothing.
    /// <para>
    /// The upper bound matters as much as the lower. Half the words in a ROM look like
    /// small numbers and a great many look like addresses; requiring the target to be
    /// inside the image is what makes a run of them mean anything.
    /// </para>
    /// </summary>
    private static int? Target(Rom rom, int at)
    {
        if (at < 0 || at + 4 > rom.Length) return null;

        int? offset = rom.ToOffsetOrNull(rom.ReadU32(at));

        return offset is { } where && where + Columns <= rom.Length ? where : null;
    }
}
