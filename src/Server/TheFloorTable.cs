namespace PokeMmo.Server;

/// <summary>
/// The seven lever settings the floor table is quoted at, read off seven runs, and the
/// differences between them worked out from those same seven rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because a table maintained by deltas drifts and stays self-consistent.</b> The
/// block at the top of every session's prompt was stale in five of its six rows for thirteen
/// milestones (207) and nothing anybody wrote about it was false: every <i>difference</i> it is
/// quoted for — <c>--surf</c> costs two flags, <c>--in-order</c> adds two and one and a party
/// member — stayed exactly right, because each milestone re-ran the pair it cared about and
/// pasted the delta onto a base nobody re-ran.
/// </para>
/// <para>
/// The fix is not another re-measurement. It is that <b>the absolutes and the sentences about
/// them have to come out of the same run</b>: <see cref="Render"/> prints each row's own numbers
/// and <see cref="Differences"/> subtracts two rows of the list it was handed. Neither can be
/// maintained by hand, so neither can drift away from the other.
/// </para>
/// <para>
/// Three of the levers are <b>MODELLED</b> — <c>--say-yes</c> answers every yes-or-no with yes,
/// <c>--boat</c> joins every dock to every other, and <c>--on-load</c> runs the fifth list, whose
/// entries carry no condition and whose timing is inside compiled code. <c>--surf</c> is an
/// override on something <b>READ</b>: the walk crosses water on its own when the party knows the
/// move, and the lever is what is left when it never does. <c>--in-order</c> is the one lever
/// that makes the run stricter. Every row says which.
/// </para>
/// </remarks>
public static class TheFloorTable
{
    /// <summary>One lever, and whether it is a decision or a reading.</summary>
    /// <param name="Name">What it is called on the command line.</param>
    /// <param name="Marked">MODELLED, READ, or what it is an override on — never conflated.</param>
    public sealed record Lever(string Name, string Marked);

    /// <summary>The four levers <c>--play</c> takes, each marked once, here.</summary>
    public static Lever SayYes { get; } = new("--say-yes", "MODELLED");

    /// <summary>See <see cref="SayYes"/>.</summary>
    public static Lever Boat { get; } = new("--boat", "MODELLED");

    /// <summary>See <see cref="SayYes"/>.</summary>
    public static Lever Surf { get; } = new("--surf", "MODELLED override on a READ answer");

    /// <summary>See <see cref="SayYes"/>.</summary>
    public static Lever InOrder { get; } = new("--in-order", "stricter");

    /// <summary>
    /// The fifth list — a map's own unconditional scripts (307).
    /// <para>
    /// MODELLED for one reason and it is written down in <c>MapScripts</c>: these entries carry
    /// no condition, so running one means knowing <em>when</em> the cartridge runs it, and the
    /// kind byte's meaning — on load, on transition, on the first frame — is inside compiled
    /// code. What is READ is that they are scripts on this map, that 233 of 234 decode, and that
    /// they move 61 flags of which 54 no other kind of script moves either way.
    /// </para>
    /// </summary>
    public static Lever OnLoad { get; } = new("--on-load", "MODELLED");

    /// <summary>One row's worth of levers.</summary>
    public sealed record Setting(bool SayYes, bool Boat, bool Surf, bool InOrder, bool OnLoad = false)
    {
        /// <summary>The command line that produces this row, which is what a session retypes.</summary>
        public string Command =>
            "--play"
            + (SayYes ? " " + TheFloorTable.SayYes.Name : "")
            + (Boat ? " " + TheFloorTable.Boat.Name : "")
            + (Surf ? " " + TheFloorTable.Surf.Name : "")
            + (InOrder ? " " + TheFloorTable.InOrder.Name : "")
            + (OnLoad ? " " + TheFloorTable.OnLoad.Name : "");

        /// <summary>Which levers are on, in the order they are named above.</summary>
        public IReadOnlyList<Lever> On =>
        [
            .. SayYes ? new[] { TheFloorTable.SayYes } : [],
            .. Boat ? new[] { TheFloorTable.Boat } : [],
            .. Surf ? new[] { TheFloorTable.Surf } : [],
            .. InOrder ? new[] { TheFloorTable.InOrder } : [],
            .. OnLoad ? new[] { TheFloorTable.OnLoad } : [],
        ];

        /// <summary>
        /// The lever that is on here and not on <paramref name="other"/>, when that is the only
        /// difference between the two — and null otherwise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Exactly one, and this is the rule the whole instrument turns on.</b> A delta is
        /// only a statement about a lever if the two runs it came from differ in that lever and
        /// nothing else. Two rows two levers apart also produce a number, and that number is what
        /// a table maintained by deltas fills up with.
        /// </para>
        /// </remarks>
        public Lever? OneLeverPast(Setting other)
        {
            List<Lever> added = [];
            List<Lever> lost = [];

            void Compare(bool mine, bool theirs, Lever lever)
            {
                if (mine && !theirs) added.Add(lever);
                else if (theirs && !mine) lost.Add(lever);
            }

            Compare(SayYes, other.SayYes, TheFloorTable.SayYes);
            Compare(Boat, other.Boat, TheFloorTable.Boat);
            Compare(Surf, other.Surf, TheFloorTable.Surf);
            Compare(InOrder, other.InOrder, TheFloorTable.InOrder);
            Compare(OnLoad, other.OnLoad, TheFloorTable.OnLoad);

            return added.Count == 1 && lost.Count == 0 ? added[0] : null;
        }
    }

    /// <summary>
    /// The seven settings the table is quoted at, in the order it prints them.
    /// </summary>
    /// <remarks>
    /// Named here rather than in whoever prints them, and asserted to be six distinct settings
    /// each of which is one lever past another one in the list — an orphaned row is a row no
    /// difference can ever be stated about, which is exactly what a copied table is made of.
    /// </remarks>
    public static IReadOnlyList<Setting> Settings { get; } =
    [
        new(SayYes: false, Boat: false, Surf: false, InOrder: false),
        new(SayYes: true, Boat: false, Surf: false, InOrder: false),
        new(SayYes: true, Boat: false, Surf: false, InOrder: true),
        new(SayYes: true, Boat: true, Surf: false, InOrder: false),
        new(SayYes: true, Boat: true, Surf: false, InOrder: true),
        new(SayYes: true, Boat: true, Surf: true, InOrder: true),
        new(SayYes: true, Boat: true, Surf: true, InOrder: true, OnLoad: true),
    ];

    /// <summary>
    /// How wide the command column has to be, taken off the widest command this table has.
    /// <para>
    /// <b>One name, and it is computed.</b> It was the literal <c>42</c> in eight places across
    /// two files, which is 126's fault in a formatting string: adding a fifth lever made the
    /// widest command fifty characters and every one of those eight columns broke, in eight
    /// separate lines nobody would have thought to change together. A width read off
    /// <see cref="Settings"/> cannot disagree with the rows it is printing.
    /// </para>
    /// </summary>
    public static int CommandColumn { get; } = Settings.Max(s => s.Command.Length);

    /// <summary>What one run of one setting came to.</summary>
    public sealed record Row(
        Setting At,
        int Reached,
        int Of,
        int Passes,
        int Flags,
        int Party,
        int HighestLevel,
        int HandedOver,
        int HandedTwice,
        int SurfMove,
        int LearnedToCrossOnPass,
        bool SwamAnyway)
    {
        /// <summary>
        /// Whether this run crossed water because the party learned the move, because the lever
        /// made it, or not at all — READ, MODELLED and a wall respectively.
        /// </summary>
        public string Water =>
            SurfMove == 0
                ? "this cartridge has no move by that name — READ"
                : LearnedToCrossOnPass > 0
                    ? $"READ — the party knew move {SurfMove} from pass {LearnedToCrossOnPass}"
                    : SwamAnyway
                        ? $"MODELLED — nobody ever knew move {SurfMove}; --surf swam anyway"
                        : $"nobody ever knew move {SurfMove} — a wall";
    }

    /// <summary>
    /// One row, read off one run.
    /// </summary>
    /// <remarks>
    /// Every number here comes off the attempt it was handed. There is nothing to keep up to
    /// date, which is the point: a row cannot be stale unless the run it was read from was.
    /// </remarks>
    public static Row Read(Setting at, Attempt played, int maps) =>
        new(
            at,
            played.Reached.Count,
            maps,
            played.Passes,
            played.Flags.Count,
            played.Party.Count,
            played.HighestLevel,
            played.Handovers.Count,
            played.HandedOverTwice.Count,
            played.SurfMove,
            played.LearnedToCrossOnPass,
            played.SwamAnyway);

    /// <summary>The table, one line per row, each line built out of that row.</summary>
    public static IReadOnlyList<string> Render(IReadOnlyList<Row> rows) =>
    [
        .. rows.Select(r =>
            $"{r.At.Command.PadRight(CommandColumn)} {r.Reached} / {r.Flags} in {r.Passes}, "
            + $"party of {r.Party} at {r.HighestLevel}, "
            + $"{r.HandedTwice} of {r.HandedOver} handed twice"),
    ];

    /// <summary>What one lever costs, worked out from the two rows it is the difference between.</summary>
    /// <param name="Lever">The lever that is on in <paramref name="To"/> and off in <paramref name="From"/>.</param>
    /// <param name="From">The command line of the row without it.</param>
    /// <param name="To">The command line of the row with it.</param>
    public sealed record Difference(
        Lever Lever, string From, string To, int Maps, int Flags, int Passes, int Party)
    {
        /// <summary>The sentence this project keeps quoting, with both halves of it present.</summary>
        public string Said =>
            $"{Lever.Name} ({Lever.Marked}): "
            + $"{Signed(Maps)} map(s), {Signed(Flags)} flag(s), {Signed(Passes)} pass(es), "
            + $"{Signed(Party)} in the party"
            + $"   [{From}  ->  {To}]";

        private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();
    }

    /// <summary>
    /// Every difference the table can honestly be quoted for: one per pair of rows that are one
    /// lever apart, subtracted from those two rows and no others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the half that stopped 207 from happening for thirteen milestones.</b> A
    /// difference stated on its own survives its own base going stale; a difference computed out
    /// of the rows printed beside it cannot. Both come from one list here, so if the absolutes
    /// are wrong the sentences are wrong with them, out loud.
    /// </para>
    /// <para>
    /// Pairs two levers apart are not reported at all rather than reported as one lever's doing —
    /// see <see cref="Setting.OneLeverPast"/>.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Difference> Differences(IReadOnlyList<Row> rows)
    {
        List<Difference> found = [];

        foreach (Row from in rows)
        {
            foreach (Row to in rows)
            {
                if (to.At.OneLeverPast(from.At) is not { } lever) continue;

                found.Add(
                    new Difference(
                        lever,
                        from.At.Command,
                        to.At.Command,
                        to.Reached - from.Reached,
                        to.Flags - from.Flags,
                        to.Passes - from.Passes,
                        to.Party - from.Party));
            }
        }

        return found;
    }
}
