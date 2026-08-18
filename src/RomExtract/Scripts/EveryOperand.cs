namespace PokeMmo.RomExtract.Scripts;

/// <summary>One operand of one command, and what the numbers it names look like.</summary>
/// <param name="Code">The command.</param>
/// <param name="At">Which argument byte the halfword starts at.</param>
/// <param name="Numbers">How many distinct numbers it names.</param>
/// <param name="Places">How many byte positions name one.</param>
/// <param name="Written">How many of its numbers a known WRITING operand ever writes.</param>
public sealed record OneOperand(byte Code, int At, int Numbers, int Places, int Written)
{
    /// <summary>The numbers it names, most-named first — so a candidate can be looked at.</summary>
    public IReadOnlyList<(int Number, int Places)> Named { get; init; } = [];

    /// <summary>
    /// How many of its places are immediately followed by a <c>compare</c> on the very number
    /// this operand just named.
    /// </summary>
    /// <remarks>
    /// <b>The direction test, and it is separate from the naming test on purpose.</b> Written-ness
    /// says an operand names a variable; it says nothing about which way the number goes. A
    /// command whose operand is compared in the next breath left something there — that is how
    /// every reading in this project treats <c>specialvar</c>'s answer, and the same shape asked
    /// of an operand nobody has named. It needs a floor and the caller prints one.
    /// </remarks>
    public int ComparedNext { get; init; }

    /// <summary>The share of its places that are.</summary>
    public double ComparedShare => Places == 0 ? 0 : (double)ComparedNext / Places;

    /// <summary>The share of what it names that something writes.</summary>
    public double Share => Numbers == 0 ? 0 : (double)Written / Numbers;

    public string Name => EveryOperand.NameOf(Code, At);

    public override string ToString() =>
        $"{Name}: {Numbers,4} number(s) at {Places,5} place(s), {Written,4} written — {Share,6:P0}"
        + $", compared next at {ComparedNext,4} of them — {ComparedShare,6:P0}";
}

/// <summary>
/// Every operand of every command, asked the one question that tells a variable from a value.
/// <para>
/// <b>251 found <c>copyvar</c>'s destination missing from both of this repository's write
/// tables.</b> Two tables, written separately, wrong in the same place — so having two of them
/// caught nothing. The obvious next question is whether there is a third operand nobody has
/// noticed, and the obvious way to ask it is to stop reading tables and sweep.
/// </para>
/// <para>
/// <b>The test needs no outside knowledge and no band boundary.</b> A variable something looks at
/// is a variable something writes (244). So: take every halfword-aligned operand position of
/// every command the map scan reads, tally the numbers each one names, and ask how many of them
/// a known WRITING operand ever writes. An operand that names variables comes in near a hundred
/// per cent; one that names items, values, coordinates, movement types or text ids comes in near
/// nought, because nothing in the game ever <c>setvar</c>s an item id.
/// </para>
/// <para>
/// <b>It is allowed to come back empty and that is the useful answer.</b> If nothing outside the
/// known tables scores like a variable operand, the tables are complete — and the distribution of
/// every operand's score is printed so that "near a hundred" can be read rather than asserted.
/// </para>
/// <para>
/// The known writers seed the question and are the one thing taken on trust: a <c>setvar</c>'s
/// first word is a variable id or nothing in this project means anything. Everything else is
/// measured against them.
/// </para>
/// </summary>
public static class EveryOperand
{
    private const byte Compare = 0x21;

    /// <summary>What an operand is called, in one place so a seed can be filtered by name.</summary>
    public static string NameOf(byte code, int at) => $"0x{code:X2} arg{at}";

    /// <summary>
    /// A seed with the operands that name VALUES taken out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this the mirror is unusable.</b> Seeding on the writers can only find operands
    /// naming variables something writes, so 252 could not see a read of a variable only compiled
    /// code ever writes. The obvious fix is to seed on the readers instead — and the reader list
    /// contains <c>0x1A arg2</c>, which names 149 numbers of which three are ever written (244).
    /// Seed on that and "is this number a variable?" becomes "is this number small?", so
    /// <c>giveitem</c>'s item id scores a hundred per cent.
    /// </para>
    /// <para>
    /// Measured on this cartridge: 27 candidates with it in, <b>one</b> with it out. Which
    /// operands name values is not asserted here — it comes from
    /// <see cref="BothNamespaces.NameValues"/>, which decides by how much of what each one names
    /// is ever written.
    /// </para>
    /// </remarks>
    public static IReadOnlyCollection<(byte Code, int At)> Without(
        IEnumerable<(byte Code, int At)> operands, IReadOnlyCollection<string> namingValues) =>
        [.. operands.Where(o => !namingValues.Contains(NameOf(o.Code, o.At)))];

    /// <summary>
    /// Every operand of every command the given scripts read, with its written-ness.
    /// </summary>
    /// <param name="rom">The cartridge.</param>
    /// <param name="from">Script addresses to start from — the map scan's own list.</param>
    /// <param name="writing">
    /// The operands taken as certainly writing a variable, as <c>(code, at)</c> pairs. What
    /// counts as written is defined by these and by nothing else, so the answer for every other
    /// operand is measured rather than assumed.
    /// </param>
    /// <param name="leastNumbers">
    /// Operands naming fewer numbers than this are dropped. A pair naming one number is 0% or
    /// 100% by arithmetic and neither figure is about the cartridge.
    /// </param>
    public static IReadOnlyList<OneOperand> In(
        Rom rom,
        IEnumerable<uint> from,
        IReadOnlyCollection<(byte Code, int At)> writing,
        int leastNumbers = 4)
    {
        var named = new Dictionary<(byte Code, int At), Dictionary<int, int>>();
        var compared = new Dictionary<(byte Code, int At), int>();
        var seen = new HashSet<uint>();

        foreach (uint start in from)
        {
            foreach (uint block in ScriptReader.Reachable(rom, start))
            {
                // Each block once. A block reached from two maps is one block, and counting it
                // twice inflates every operand by the same factor — which changes no share and
                // makes every place count a lie.
                if (!seen.Add(block)) continue;

                List<ScriptCommand> read = [.. ScriptReader.Read(rom, block)];

                for (var i = 0; i < read.Count; i++)
                {
                    ScriptCommand command = read[i];
                    ScriptCommand? next = i + 1 < read.Count ? read[i + 1] : null;

                    // EVERY halfword-aligned position, not the ones somebody wrote down. The
                    // whole point is to look where no table says to look.
                    for (var at = 0; at + 2 <= command.Arguments.Length; at += 2)
                    {
                        if (!named.TryGetValue((command.Code, at), out Dictionary<int, int>? of))
                            named[(command.Code, at)] = of = [];

                        int number = command.Word(at);

                        of[number] = of.GetValueOrDefault(number) + 1;

                        // AND WHETHER THE NEXT COMMAND COMPARES THAT VERY NUMBER, which is the
                        // only direction evidence available from one pass. Counted per place.
                        if (next is { Code: Compare } test
                            && test.Arguments.Length >= 2
                            && test.Word() == number)
                        {
                            compared[(command.Code, at)] =
                                compared.GetValueOrDefault((command.Code, at)) + 1;
                        }
                    }
                }
            }
        }

        HashSet<int> written =
        [
            .. writing.Where(named.ContainsKey).SelectMany(w => named[w].Keys),
        ];

        return
        [
            .. named.Where(o => o.Value.Count >= leastNumbers)
                .Select(o => new OneOperand(
                    o.Key.Code,
                    o.Key.At,
                    o.Value.Count,
                    o.Value.Values.Sum(),
                    o.Value.Keys.Count(written.Contains))
                {
                    Named = [.. o.Value.OrderByDescending(n => n.Value).Select(n => (n.Key, n.Value))],
                    ComparedNext = compared.GetValueOrDefault(o.Key),
                })
                .OrderByDescending(o => o.Share)
                .ThenByDescending(o => o.Numbers),
        ];
    }

    /// <summary>
    /// The operands that score like a variable operand and are in neither of the known tables.
    /// </summary>
    /// <param name="all">Every operand, from <see cref="In"/>.</param>
    /// <param name="known">Every operand either table already names.</param>
    /// <param name="least">
    /// The share above which an operand is a candidate. Deliberately doing no work if the
    /// cartridge's own distribution is bimodal, which is the thing the caller prints.
    /// </param>
    public static IReadOnlyList<OneOperand> Unknown(
        IEnumerable<OneOperand> all,
        IReadOnlyCollection<(byte Code, int At)> known,
        double least = 0.5) =>
        [.. all.Where(o => o.Share >= least && !known.Contains((o.Code, o.At)))];

    /// <summary>
    /// How the scores spread, in tenths — so "near a hundred and near nought" is READ.
    /// </summary>
    /// <remarks>
    /// A threshold with nothing behind it is a number that decides the answer. If the operands of
    /// this cartridge come in at the two ends with nothing in between, the threshold is doing no
    /// work and the histogram says so; if they are spread evenly, the whole method is wrong and
    /// the histogram says that instead.
    /// </remarks>
    public static IReadOnlyList<(int Tenth, int Operands)> Spread(IEnumerable<OneOperand> all) =>
    [
        .. all.GroupBy(o => Math.Min(9, (int)(o.Share * 10)))
            .Select(g => (Tenth: g.Key, Operands: g.Count()))
            .OrderBy(g => g.Tenth),
    ];
}
