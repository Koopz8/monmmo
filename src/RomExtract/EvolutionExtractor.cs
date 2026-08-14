using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract;

/// <summary>Where the evolution table is, how it is shaped, and what it says.</summary>
/// <param name="Address">The table's first row.</param>
/// <param name="Stride">Bytes per species.</param>
/// <param name="EntrySize">Bytes per entry.</param>
/// <param name="ByLevel">The method number that means "at this level".</param>
/// <param name="Evolutions">Every entry on the table.</param>
public sealed record EvolutionTable(
    uint Address,
    int Stride,
    int EntrySize,
    int ByLevel,
    IReadOnlyList<Evolution> Evolutions)
{
    /// <summary>How many entries turn something into something stronger.</summary>
    public int Stronger { get; init; }

    /// <summary>
    /// The method number that means "somebody used this item on it", or zero.
    /// <para>
    /// Kept apart from <see cref="ByLevel"/> because they are answered by different
    /// evidence and either can be missing. A stone is the only kind of evolution a
    /// player can bring about on purpose, which makes it the only one the bag has any
    /// business in.
    /// </para>
    /// </summary>
    public int ByItem { get; init; }

    /// <summary>The items that method takes, for saying out loud what was found.</summary>
    public IReadOnlyList<int> Stones { get; init; } = [];
}

/// <summary>
/// Finds the table that says what turns into what.
/// <para>
/// Nothing in this game has ever evolved. A player levels a starter to fifty and it is
/// still the thing it hatched as, which is not a detail — it is most of what levelling
/// is <em>for</em>.
/// </para>
/// <para>
/// Located by shape, and the shape is checked against something that is true of
/// evolution rather than true of this cartridge: <b>what a thing becomes is stronger
/// than what it was</b>. Every candidate table on the image is scored by how many of
/// its entries point at a species with a higher base-stat total, and the base stats are
/// already located, so the check costs nothing and cannot be fooled by a plausible run
/// of numbers. One candidate on a real image scores 182 of 184.
/// </para>
/// <para>
/// Which method number means "at a level" is derived too, and this is the nice part.
/// Fifteen methods appear. Only one of them <em>follows itself</em> — a species that
/// evolves that way into something that evolves that way again — and it does so
/// twenty-four times, with a larger parameter every single time. A number that grows
/// along a chain of evolutions is a level. An item id has no reason to.
/// </para>
/// </summary>
public static class EvolutionExtractor
{
    /// <summary>Shapes worth trying. Every one is checked; the evidence picks.</summary>
    private static readonly (int Stride, int Entry)[] Shapes =
        [(16, 8), (20, 4), (24, 6), (24, 8), (30, 6), (32, 8), (36, 6), (40, 8), (48, 6), (48, 8)];

    /// <summary>How many entries a table must have before it can be believed.</summary>
    private const int Fewest = 100;

    /// <summary>How much of it must point at something stronger.</summary>
    private const double MustRise = 0.9;

    /// <summary>A method number bigger than this is not a method.</summary>
    private const int MostMethods = 31;

    public static EvolutionTable? Locate(
        Rom rom,
        IReadOnlyList<SpeciesData> species,
        IReadOnlyDictionary<int, uint>? itemRoutines = null,
        Action<string>? log = null)
    {
        if (species.Count == 0) return null;

        int count = species.Max(s => s.Index) + 1;

        var total = new int[count];

        foreach (SpeciesData one in species.Where(s => s.Index >= 0 && s.Index < count))
        {
            total[one.Index] =
                one.BaseHp + one.BaseAttack + one.BaseDefense +
                one.BaseSpeed + one.BaseSpAttack + one.BaseSpDefense;
        }

        ReadOnlySpan<byte> data = rom.Span;

        (EvolutionTable Table, int Rising)? best = null;
        int candidates = 0;

        foreach ((int stride, int entry) in Shapes)
        {
            if (stride % entry != 0 || entry < 4) continue;

            int per = stride / entry;

            for (int at = 0; at + stride * count <= data.Length; at += 4)
            {
                // Species zero is not a species, so its row is empty on every image that
                // numbers species from one — and the row after it is not. That pair is
                // cheap and throws away all but a couple of hundred places to look.
                if (!IsEmpty(data.Slice(at, stride))) continue;
                if (!Plausible(data, at + stride, count)) continue;

                candidates++;

                var found = new List<Evolution>();
                int rising = 0;
                int falling = 0;

                for (int index = 1; index < count; index++)
                {
                    for (int slot = 0; slot < per; slot++)
                    {
                        int where = at + index * stride + slot * entry;

                        int method = Word(data, where);
                        int parameter = Word(data, where + 2);
                        int into = Word(data, where + 4);

                        if (method == 0 && into == 0) continue;

                        if (method > MostMethods || into <= 0 || into >= count)
                        {
                            falling++;
                            continue;
                        }

                        found.Add(new Evolution(index, method, parameter, into));

                        if (total[into] > total[index]) rising++;
                        else falling++;
                    }
                }

                int entries = rising + falling;

                if (entries < Fewest || rising / (double)entries < MustRise) continue;

                if (best is null || rising > best.Value.Rising)
                {
                    best = (
                        new EvolutionTable(Rom.BaseAddress + (uint)at, stride, entry, 0, found) { Stronger = rising },
                        rising);
                }
            }
        }

        if (best is not { } winner)
        {
            log?.Invoke($"  evolutions: {candidates} tables looked right and none of them made anything stronger");
            return null;
        }

        EvolutionTable table = winner.Table;

        if (ByLevel(table.Evolutions) is not { } byLevel)
        {
            log?.Invoke(
                $"  evolutions: {table.Evolutions.Count} at 0x{table.Address:X8}, " +
                "but no method follows itself, so none of them can be read as a level");

            return table;
        }

        int chains = table.Evolutions
            .Where(e => e.Method == byLevel)
            .Count(e => table.Evolutions.Any(next => next.Species == e.Into && next.Method == byLevel));

        log?.Invoke(
            $"  evolution table: 0x{table.Address:X8}   {count} x {table.Stride}B, {table.Stride / table.EntrySize} each   " +
            $"({candidates} candidates)");

        log?.Invoke(
            $"  {table.Evolutions.Count} evolutions, {table.Stronger} of them into something stronger; " +
            $"method {byLevel} is the level one — {chains} of its {table.Evolutions.Count(e => e.Method == byLevel)} " +
            "follow themselves, every time at a higher number");

        table = table with { ByLevel = byLevel };

        if (ByItem(table.Evolutions, itemRoutines) is not { } used) return table;

        List<int> stones = [.. table.Evolutions.Where(e => e.Method == used.Method).Select(e => e.Parameter).Distinct().Order()];

        log?.Invoke(
            $"  method {used.Method} is the item one — its {stones.Count} items are exactly the " +
            $"{stones.Count} that share field routine 0x{used.Routine:X8}, and nothing else on the " +
            "cartridge uses that routine");

        return table with { ByItem = used.Method, Stones = stones };
    }

    /// <summary>
    /// Which method means "somebody used this on it".
    /// <para>
    /// An item that can be used out of a bag has a routine to run when it is; an item
    /// that is only ever held has the one that says no. So the method whose parameters
    /// are items is not enough — six evolution methods on this cartridge take a number
    /// that happens to be a valid item id, and one of them is the level method, whose
    /// "items" are potions.
    /// </para>
    /// <para>
    /// What settles it is that the set matches <em>both ways</em>: every item the method
    /// names runs the same routine, and every item on the cartridge that runs that
    /// routine is named by the method. The trade-with-an-item method fails the second
    /// half — its six items share the do-nothing routine with a hundred and fifty others.
    /// </para>
    /// </summary>
    private static (int Method, uint Routine)? ByItem(
        IReadOnlyList<Evolution> evolutions,
        IReadOnlyDictionary<int, uint>? routines)
    {
        if (routines is null || routines.Count == 0) return null;

        foreach (int method in evolutions.Select(e => e.Method).Distinct())
        {
            HashSet<int> named = [.. evolutions.Where(e => e.Method == method).Select(e => e.Parameter)];

            if (named.Count < 2) continue;
            if (named.Any(id => !routines.TryGetValue(id, out uint routine) || routine == 0)) continue;

            uint shared = routines[named.First()];

            if (named.Any(id => routines[id] != shared)) continue;

            HashSet<int> running = [.. routines.Where(p => p.Value == shared).Select(p => p.Key)];

            if (running.SetEquals(named)) return (method, shared);
        }

        return null;
    }

    /// <summary>
    /// Which method is measured in levels.
    /// <para>
    /// The one that follows itself. A three-stage line is the same method twice, and the
    /// second number is always bigger than the first — that is what a level is. Stones,
    /// trades and friendship never chain into themselves, so they never qualify however
    /// many of them there are.
    /// </para>
    /// </summary>
    private static int? ByLevel(IReadOnlyList<Evolution> evolutions)
    {
        int? best = null;
        int most = 0;

        foreach (int method in evolutions.Select(e => e.Method).Distinct())
        {
            List<Evolution> theirs = [.. evolutions.Where(e => e.Method == method)];

            var pairs = theirs
                .SelectMany(e => theirs.Where(next => next.Species == e.Into).Select(next => (First: e, Then: next)))
                .ToList();

            if (pairs.Count == 0) continue;
            if (pairs.Any(p => p.Then.Parameter <= p.First.Parameter)) continue;
            if (theirs.Any(e => e.Parameter is < 1 or > 100)) continue;

            if (pairs.Count > most)
            {
                most = pairs.Count;
                best = method;
            }
        }

        return best;
    }

    private static bool IsEmpty(ReadOnlySpan<byte> row)
    {
        foreach (byte b in row)
        {
            if (b != 0) return false;
        }

        return true;
    }

    /// <summary>Whether the row for species one begins with something that could be an entry.</summary>
    private static bool Plausible(ReadOnlySpan<byte> data, int at, int count)
    {
        if (at + 6 > data.Length) return false;

        int method = Word(data, at);
        int into = Word(data, at + 4);

        return method is > 0 and <= MostMethods && into > 0 && into < count;
    }

    private static int Word(ReadOnlySpan<byte> data, int at) => data[at] | (data[at + 1] << 8);
}
