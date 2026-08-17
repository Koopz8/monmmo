namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The three commands that move coins, the counters built out of them, and the one number
/// none of them contains.
/// <para>
/// <b>Milestone 199 settled how wide <c>0xB3</c>, <c>0xB4</c> and <c>0xB5</c> are and said in
/// as many words that what each one DOES is not claimed.</b> 200 did the same for <c>0x91</c>
/// and <c>0x92</c>: "the pair the GAME CORNER is built out of — the one that asks and the one
/// that takes. What each does is NOT claimed here; only how wide it is." This is that claim,
/// and it is made the way this project makes claims — by finding a shape, printing every site
/// of it, and deriving a number that is written nowhere.
/// </para>
/// <para>
/// <b>The derivation.</b> Five places in this cartridge read a count into a variable, compare
/// that variable against a bound, branch, and hand over a quantity on the fall-through:
/// </para>
/// <code>
///   B3 v ; compare v, 9500 ; if &gt;= goto ... ; B4 500      the counter, 0x0816C706
///   B3 v ; compare v, 9950 ; if &gt;= goto ... ; B4  50      the counter, 0x0816C734
///   B3 v ; compare v, 9990 ; if &gt;= goto ... ; B4  10      a person,   0x0816C803
///   B3 v ; compare v, 9980 ; if &gt;= goto ... ; B4  20      a person,   0x0816C8BA
///   B3 v ; compare v, 9980 ; if &gt;= goto ... ; B4  20      a person,   0x0816C91A
/// </code>
/// <para>
/// Four different bounds and four different gifts, and <b>every bound plus its own gift is
/// exactly ten thousand.</b> Pair any bound with somebody else's gift and the sums scatter —
/// 9550, 10450, 10490, 10000, 9990 — so the agreement is in the pairing and not in the column.
/// That is the capacity of whatever these commands count, READ, from five sites none of which
/// contains the number.
/// </para>
/// <para>
/// <b>It is the inverse of the trap this project keeps meeting.</b> Milestone 200's `0x92` had
/// nine sites agreeing on one resume byte and the agreement was worth nothing, because every
/// site was landing in the same run of zeroes. Here the sites agree on nothing you can see —
/// not the bound, not the gift, not the branch target — and only on a quantity you have to
/// compute. <b>Count what the sites agree ON, not how many agree.</b>
/// </para>
/// <para>
/// Everything here answers about the IMAGE. Nothing is keyed on a map, a person or an address
/// written down in this repository, and every list below can come back empty.
/// </para>
/// </summary>
public static class TheCoinCase
{
    /// <summary>Reads the count into a variable. Two bytes: which variable.</summary>
    public const byte HowMany = 0xB3;

    /// <summary>Adds to the count. Two bytes: how many, or a variable holding how many.</summary>
    public const byte HandOver = 0xB4;

    /// <summary>Takes from the count. Two bytes, the same shape as <see cref="HandOver"/>.</summary>
    public const byte TakeAway = 0xB5;

    /// <summary>Asks after money. Five bytes: a 32-bit amount and a byte.</summary>
    public const byte AskAfterMoney = 0x92;

    /// <summary>Takes money. Five bytes, and <see cref="AskAfterMoney"/>'s twin.</summary>
    public const byte TakeMoney = 0x91;

    /// <summary>Compares a variable against a number. Four bytes: the variable, the number.</summary>
    private const byte Compare = 0x21;

    /// <summary>Compares two variables. Four bytes.</summary>
    private const byte CompareVariables = 0x22;

    /// <summary>Puts a number in a variable. Four bytes: the variable, the number.</summary>
    private const byte SetVariable = 0x16;

    /// <summary>
    /// Hands an item over. Claimed already — <c>WhatItIsWaitingFor.GiveItem</c>,
    /// <c>WhatIsBehindAStop</c>'s "hands an item over", and <c>ScriptRunner</c>'s <c>0x46</c>
    /// case all read it that way. Nothing new is claimed by using it here.
    /// </summary>
    private const byte GiveItem = 0x46;

    /// <summary>
    /// Hands a creature over. Claimed already, the same three places over —
    /// <c>WhatItIsWaitingFor.GiveMon</c>, <c>WhatIsBehindAStop</c>'s "hands a creature over",
    /// and <c>WorldExporter</c>, which reads the starter out of one.
    /// </summary>
    private const byte GiveCreature = 0x79;

    /// <summary>One place one of the three coin commands appears.</summary>
    /// <param name="Value">Its two-byte argument, raw — a count at some sites and a variable at others.</param>
    /// <param name="Opened">Whether the map scan ever decoded this byte, which is the code boundary.</param>
    public sealed record Site(int Offset, byte Code, int Value, bool ReadsAsScript, bool Opened);

    /// <summary>
    /// A guarded hand-over: read the count, compare it against a bound, branch, hand some over.
    /// </summary>
    /// <param name="Bound">What the count is compared against.</param>
    /// <param name="Gift">What is handed over on the arm the branch does not take.</param>
    public sealed record Ceiling(int Offset, int Variable, int Bound, int Gift, int GiftAt)
    {
        /// <summary>
        /// The number the cartridge never writes down. A guard that refuses at <c>bound</c> before
        /// adding <c>gift</c> is a guard against passing <c>bound + gift</c>.
        /// </summary>
        public int Sum => Bound + Gift;
    }

    /// <summary>
    /// Money in, coins out: ask after an amount, hand some over, take the amount.
    /// </summary>
    /// <param name="Paid">
    /// What <see cref="TakeMoney"/> takes. Kept separate from <see cref="Asked"/> rather than
    /// assumed equal — a counter that asks after one number and takes another is a fact worth
    /// being able to see.
    /// </param>
    public sealed record Exchange(int Offset, long Asked, int Given, long Paid);

    /// <summary>One row of a table written as script: a thing, and what it costs.</summary>
    public sealed record PriceRow(int Offset, int Thing, int Price);

    /// <summary>
    /// A run of rows that set the same two variables and leave by the same door.
    /// <para>
    /// <b>This cartridge writes its price lists as code.</b> Each row is two <c>setvar</c>s and a
    /// <c>goto</c> to a shared routine that does the asking, the paying and the handing over
    /// once for the whole list. Which column is the price is not assumed: it is the variable a
    /// <see cref="TakeAway"/> somewhere takes.
    /// </para>
    /// </summary>
    /// <param name="HandsOverItems">
    /// Whether the shared door reaches the command this repository already reads as handing an
    /// item over. <b>This is the only thing that says what kind of number the first column
    /// holds.</b> Every id in every one of this cartridge's lists is inside the item table AND
    /// inside the species table, so reading a row against one table and falling back to the
    /// other silently answers with whichever was tried first — which is what the first version
    /// of this instrument did, and it named five creatures as berries and mail.
    /// </param>
    /// <param name="HandsOverCreatures">The same question for the command that hands a creature over.</param>
    public sealed record PriceList(
        int Offset,
        int ThingVariable,
        int PriceVariable,
        uint SharedExit,
        IReadOnlyList<PriceRow> Rows,
        bool HandsOverItems,
        bool HandsOverCreatures);

    /// <summary>Every place in the whole image one of the three coin commands sits.</summary>
    /// <param name="covered">
    /// What the map scan decoded, so each site can say whether it is inside the world or past
    /// the code boundary. Optional, because this question is about the file.
    /// </param>
    public static IReadOnlyList<Site> Everywhere(Rom rom, int[]? covered = null)
    {
        var found = new List<Site>();

        for (var offset = 0; offset + 3 <= rom.Length; offset++)
        {
            byte code = rom.ReadU8(offset);

            if (code is not (HowMany or HandOver or TakeAway)) continue;

            found.Add(new Site(
                offset,
                code,
                rom.ReadU16(offset + 1),
                ReadsAsAScript(rom, offset),
                covered is not null
                && offset < covered.Length
                && covered[offset] != EverywhereInTheImage.Nobody));
        }

        return found;
    }

    /// <summary>
    /// The same sweep on the image backwards, counted in places rather than in sites.
    /// <para>
    /// <b>Place-counted from the start, because of milestone 206.</b> Reversing a file preserves
    /// byte frequencies and it preserves SHAPE, so a table reversed still clumps exactly as hard;
    /// a floor counted in sites is comparing a clumped number against a clumped number and
    /// calling the difference signal.
    /// </para>
    /// </summary>
    public static (int Sites, int ReadsAsScript, int Places) NoiseFloor(Rom rom)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        var nowhere = new Rom(backwards);

        IReadOnlyList<Site> found = Everywhere(nowhere);

        List<int> reads = [.. found.Where(s => s.ReadsAsScript).Select(s => s.Offset)];

        return (
            found.Count,
            reads.Count,
            reads.Count - HowClustered.Clumped(nowhere, reads) + HowClustered.In(nowhere, reads).Count);
    }

    /// <summary>
    /// Every guarded hand-over in the file: the shape the capacity is derived from.
    /// <para>
    /// The straight line only. A run takes one arm of a branch and this is a question about the
    /// image, but following the branch would let any block reach any hand-over in the file and
    /// the shape would stop meaning anything.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Ceiling> Ceilings(Rom rom)
    {
        var found = new List<Ceiling>();

        foreach (int offset in Candidates(rom, HowMany))
        {
            List<ScriptCommand> block = ScriptReader.Read(rom, Rom.BaseAddress + (uint)offset);

            if (block.Count < 4 || block[0].Code != HowMany) continue;

            int variable = block[0].Word();

            // The compare has to be about the variable the count was just read into. Without
            // this the shape is "a compare happens to follow", which is three bytes of luck.
            if (block[1].Code != Compare || block[1].Word() != variable) continue;
            if (block[2].Code != ScriptCommands.GotoIf) continue;

            // The first hand-over on the fall-through, and nothing past another coin command:
            // a second read or a subtraction means the guard being read is no longer this one.
            foreach (ScriptCommand command in block.Skip(3))
            {
                if (command.Code is HowMany or TakeAway) break;

                if (command.Code != HandOver) continue;

                found.Add(new Ceiling(
                    offset, variable, block[1].Word(2), command.Word(), command.Offset));

                break;
            }
        }

        return found;
    }

    /// <summary>
    /// Whether every ceiling in a list is a guard against the same number.
    /// <para>
    /// <b>The whole finding, and it has to be able to say no.</b> Five sums that agree are a
    /// capacity; five sums that disagree are five unrelated guards and there is nothing to
    /// report. Returning the distinct sums rather than a bool keeps the disagreement printable
    /// instead of collapsing it to "no".
    /// </para>
    /// </summary>
    /// <returns>
    /// Every distinct <see cref="Ceiling.Sum"/>, and how many distinct (bound, gift) pairs
    /// produced each — because one pair repeated three times is one fact and three pairs are
    /// three.
    /// </returns>
    public static IReadOnlyList<(int Sum, int Sites, int DistinctPairs)> Capacity(
        IEnumerable<Ceiling> ceilings) =>
    [
        .. ceilings
            .GroupBy(c => c.Sum)
            .Select(g => (
                Sum: g.Key,
                Sites: g.Count(),
                DistinctPairs: g.Select(c => (c.Bound, c.Gift)).Distinct().Count()))
            .OrderByDescending(x => x.DistinctPairs)
            .ThenByDescending(x => x.Sites),
    ];

    /// <summary>
    /// The same chain hunt on the image backwards — the floor under the SHAPE, not just under
    /// the bytes.
    /// <para>
    /// <b>The pairing control this instrument was first written with could not fail, and was
    /// deleted.</b> It paired each bound with a gift it did not come with and reported that
    /// none of those sums was the answer. That is arithmetic and not evidence: if every site's
    /// bound plus its own gift is <c>S</c> and no two sites share a pair, then a bound crossed
    /// with somebody else's gift can never be <c>S</c> — the line was guaranteed before the
    /// cartridge was opened. A control with one outcome is a control that says nothing.
    /// </para>
    /// <para>
    /// This one can come back either way. Reversing the image keeps every byte and every byte's
    /// frequency and destroys every command boundary, so whatever chains it finds are what this
    /// shape finds in a file with these statistics and no scripts. If the reversal produces
    /// chains whose sums agree too, the agreement in the real image is worth nothing.
    /// </para>
    /// </summary>
    /// <returns>How many chains the reversal has, and how many different numbers they sum to.</returns>
    public static (int Chains, int Sums) CeilingFloor(Rom rom)
    {
        byte[] backwards = rom.Span.ToArray();

        Array.Reverse(backwards);

        IReadOnlyList<Ceiling> found = Ceilings(new Rom(backwards));

        return (found.Count, found.Select(c => c.Sum).Distinct().Count());
    }

    /// <summary>
    /// Money in, coins out — every place in the file that asks after money and hands some of
    /// whatever these commands count over before taking the money.
    /// </summary>
    public static IReadOnlyList<Exchange> Exchanges(Rom rom)
    {
        var found = new List<Exchange>();

        foreach (int offset in Candidates(rom, AskAfterMoney))
        {
            List<ScriptCommand> block = ScriptReader.Read(rom, Rom.BaseAddress + (uint)offset);

            if (block.Count == 0 || block[0].Code != AskAfterMoney) continue;

            var given = 0;

            foreach (ScriptCommand command in block.Skip(1))
            {
                // Another question about money means the one being read has been answered.
                if (command.Code == AskAfterMoney) break;

                if (command.Code == HandOver)
                {
                    given = command.Word();
                    continue;
                }

                if (command.Code != TakeMoney || given == 0) continue;

                found.Add(new Exchange(offset, Amount(block[0]), given, Amount(command)));

                break;
            }
        }

        return found;
    }

    /// <summary>
    /// The price lists this cartridge writes as script: rows of two <c>setvar</c>s leaving by a
    /// shared door.
    /// <para>
    /// Found by shape and grouped by what the rows have in common, so a file with no such table
    /// gives an empty list rather than a table of one row.
    /// </para>
    /// </summary>
    /// <param name="leastRows">
    /// How many rows make a list. Two, because a rule with a key needs two of whatever the key
    /// is made of — one row shares its variables and its exit with nothing.
    /// </param>
    public static IReadOnlyList<PriceList> PriceLists(Rom rom, int leastRows = 2)
    {
        var rows = new List<(int Offset, int ThingVar, int Thing, int PriceVar, int Price, uint Exit)>();

        foreach (int offset in rom.FindAll(new byte[] { SetVariable }))
        {
            if (offset + 10 > rom.Length) continue;
            if (rom.ReadU8(offset + 5) != SetVariable) continue;

            List<ScriptCommand> block = ScriptReader.Read(rom, Rom.BaseAddress + (uint)offset);

            if (block.Count < 3) continue;
            if (block[0].Code != SetVariable || block[1].Code != SetVariable) continue;

            // The shared door. A row that ends any other way is not one of a list.
            ScriptCommand? exit = block.FirstOrDefault(c => c.Code == ScriptCommands.Goto);

            if (exit is null) continue;

            rows.Add((
                offset,
                block[0].Word(), block[0].Word(2),
                block[1].Word(), block[1].Word(2),
                exit.Pointer()));
        }

        // Which variable is the price is READ rather than chosen: it is the one something
        // somewhere subtracts from the count.
        HashSet<int> spent =
        [
            .. Everywhere(rom)
                .Where(s => s.Code == TakeAway && s.ReadsAsScript)
                .Select(s => s.Value),
        ];

        var lists = new List<PriceList>();

        foreach (var group in rows.GroupBy(r => (r.ThingVar, r.PriceVar, r.Exit)))
        {
            List<PriceRow> members =
                [.. group.OrderBy(r => r.Offset).Select(r => new PriceRow(r.Offset, r.Thing, r.Price))];

            if (members.Count < leastRows) continue;
            if (!spent.Contains(group.Key.PriceVar)) continue;

            // WHAT THE SHARED DOOR HANDS OVER, which is the only thing in the file that says
            // what kind of number the first column holds. Read down every arm, because the
            // pokemon list branches on the id before giving anything and the item list does not.
            var items = false;
            var creatures = false;

            foreach (ScriptCommand command in ScriptReader.ReadAll(rom, group.Key.Exit))
            {
                if (command.Code == GiveItem) items = true;
                if (command.Code == GiveCreature) creatures = true;
            }

            lists.Add(new PriceList(
                members[0].Offset,
                group.Key.ThingVar,
                group.Key.PriceVar,
                group.Key.Exit,
                members,
                items,
                creatures));
        }

        return [.. lists.OrderByDescending(l => l.Rows.Count).ThenBy(l => l.Offset)];
    }

    /// <summary>
    /// What a five-byte money argument holds: four bytes little-endian, then a byte this
    /// project has no reading for and does not pretend to.
    /// </summary>
    private static long Amount(ScriptCommand command) =>
        command.Arguments.Length < 4
            ? 0
            : command.Arguments[0]
              | ((long)command.Arguments[1] << 8)
              | ((long)command.Arguments[2] << 16)
              | ((long)command.Arguments[3] << 24);

    /// <summary>
    /// The sites of one command worth reading a whole block from.
    /// <para>
    /// A sixteen-megabyte image holds about sixty-five thousand of any given byte and decoding
    /// a block at each of them is minutes of work for an answer that is nearly all noise. The
    /// filter is the same one the rest of this project uses — does it read as a script — and it
    /// decides nothing about which question gets asked, only which bytes are worth the cost.
    /// </para>
    /// </summary>
    private static IEnumerable<int> Candidates(Rom rom, byte code)
    {
        for (var offset = 0; offset + 6 <= rom.Length; offset++)
        {
            if (rom.ReadU8(offset) != code) continue;
            if (!ReadsAsAScript(rom, offset)) continue;

            yield return offset;
        }
    }

    /// <summary>
    /// The same weak filter the rest of this project uses: a block that ends the way blocks end.
    /// </summary>
    private static bool ReadsAsAScript(Rom rom, int offset)
    {
        List<ScriptCommand> commands = ScriptReader.Read(rom, Rom.BaseAddress + (uint)offset);

        return commands.Count > 0
               && commands[^1].Code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto;
    }

    /// <summary>
    /// Whether anything in the file compares two variables and then takes one of them away —
    /// the spending side, which has no constant in it at all.
    /// </summary>
    public static IReadOnlyList<(int Offset, int Held, int Price)> Spends(Rom rom)
    {
        var found = new List<(int, int, int)>();

        foreach (int offset in Candidates(rom, CompareVariables))
        {
            List<ScriptCommand> block = ScriptReader.Read(rom, Rom.BaseAddress + (uint)offset);

            if (block.Count < 3 || block[0].Code != CompareVariables) continue;
            if (block[1].Code != ScriptCommands.GotoIf) continue;

            int held = block[0].Word();
            int price = block[0].Word(2);

            foreach (ScriptCommand command in block.Skip(2))
            {
                if (command.Code == HowMany) break;

                if (command.Code != TakeAway || command.Word() != price) continue;

                found.Add((offset, held, price));

                break;
            }
        }

        return found;
    }
}
