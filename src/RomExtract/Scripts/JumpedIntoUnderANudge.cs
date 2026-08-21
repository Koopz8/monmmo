namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The jumped-into test, asked against the region-preserving control.
/// <para>
/// <b>"A site something jumps into" is an ADDRESS reading</b>, and its floor has been the image
/// backwards since 175. 269 showed what that floor keeps — every table, and therefore every
/// pointer table — and built the control that does not: the same pointers, in the same file,
/// aimed a few bytes off. This applies it.
/// </para>
/// <para>
/// The question the reading asks is whether a pointer with a jump opcode in front of it lands
/// within <c>slack</c> bytes BEFORE the site — on the block the site is part of. Aim every pointer
/// <c>by</c> bytes further on and ask again: a pointer aimed at <c>p + by</c> lands in
/// <c>[a - slack, a]</c> exactly when <c>p</c> lands in <c>[a - slack - by, a - by]</c>, so the
/// nudge is the same lookup with the window slid back. The opcode in front of the pointer is read
/// off the real image either way, because what a pointer is owned by is not what is in dispute.
/// </para>
/// <para>
/// <b>A nudge shorter than the window is not a control.</b> If <c>by &lt; slack</c>, the slid window
/// still overlaps the real one, so a pointer naming the block the site is in mostly still counts.
/// Those rows are printed — 269 printed the whole ladder and stability was the argument — but
/// <see cref="InsideTheWindow"/> says which of them could not have come back different.
/// </para>
/// </summary>
public static class JumpedIntoUnderANudge
{
    /// <summary>The nudges 269 used, so the two ladders are read against each other.</summary>
    public static IReadOnlyList<int> Nudges { get; } = [4, 8, 16, 64, 256, 1024, 4096];

    /// <summary>The reach the real reading uses. The control is not one unless it is the same.</summary>
    public const int Slack = 192;

    /// <summary>
    /// Whether a nudge this long leaves the slid window overlapping the real one — in which case
    /// the row it produces is about the reading and not about the cartridge.
    /// </summary>
    public static bool InsideTheWindow(int by, int slack = Slack) => Math.Abs(by) <= slack;

    /// <summary>
    /// How many of these sites have a jump-owned pointer landing within <paramref name="slack"/>
    /// bytes before them, once every pointer in the file is aimed <paramref name="by"/> bytes
    /// further on. <c>by == 0</c> is the reading as named.
    /// </summary>
    public static int Count(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<uint> sites,
        int by,
        int slack = Slack) =>
        sites.Count(site => IsJumpedInto(rom, index, site, by, slack));

    /// <summary>One site, one answer — the rule the count is made of.</summary>
    public static bool IsJumpedInto(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        uint site,
        int by,
        int slack = Slack) =>
        EverywhereInTheImage.WhoNames(rom, index, (uint)(site - by), slack).Any(n => n.AJump);

    /// <summary>
    /// The same count per group — flags, moves, anything — so "how many FLAGS have at least one
    /// jumped-into site" can be asked under the nudge as well as the site count.
    /// </summary>
    public static int GroupsWithOne<TKey>(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<(TKey Key, uint Site)> sites,
        int by,
        int slack = Slack,
        bool onTheBlock = false,
        bool orALiteral = false)
        where TKey : notnull =>
        sites
            .GroupBy(s => s.Key)
            .Count(g => g.Any(s => onTheBlock
                ? IsOnAJumpsBlock(rom, index, s.Site, by, slack, orALiteral)
                : IsJumpedInto(rom, index, s.Site, by, slack)));

    /// <summary>
    /// The stricter half of the same question: not "a jump lands within the window" but "the
    /// block a jump names, read from ITS boundary, reaches this site AS A COMMAND".
    /// <para>
    /// <b>Two predicates on one question, and only the control can say which is evidence</b>
    /// (265). The window test is satisfied by a jump pointer aimed anywhere in the 192 bytes in
    /// front of a site, which in a region full of script is most of them; this one needs the
    /// straight-line read from the jump's own target to land on the site's first byte, which a
    /// pointer aimed at somebody else's block does only when the reader resynchronises onto it.
    /// </para>
    /// </summary>
    public static bool IsOnAJumpsBlock(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        uint site,
        int by,
        int slack = Slack,
        bool orALiteral = false)
    {
        if (rom.ToOffsetOrNull(site) is not { } wanted) return false;

        foreach (NamesIt names in EverywhereInTheImage.WhoNames(rom, index, (uint)(site - by), slack))
        {
            if (!names.AJump && !(orALiteral && names.ALiteral)) continue;

            // The pointer says Points; nudged, it is aimed `by` further on — which is where the
            // window lookup already put it, so read from there.
            uint target = (uint)(names.Points + by);

            foreach (ScriptCommand command in ScriptReader.Read(rom, target))
            {
                if (command.Offset == wanted) return true;
                if (command.Offset > wanted) break;
            }
        }

        return false;
    }

    /// <summary>
    /// How many of these sites are a command of a block some jump names — or, with
    /// <paramref name="orALiteral"/>, a block an aligned literal names, which is the far side of
    /// the code boundary with an address on it. Four loose bytes never count: they are the
    /// accident the whole control exists to measure.
    /// </summary>
    public static int CountOnABlock(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<uint> sites,
        int by,
        int slack = Slack,
        bool orALiteral = false) =>
        sites.Count(site => IsOnAJumpsBlock(rom, index, site, by, slack, orALiteral));

    /// <summary>
    /// The sites that pass the strict test as named, with what names each — so a count of one
    /// is a name and not a number.
    /// </summary>
    public static IReadOnlyList<(uint Site, NamesIt By)> OnABlock(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<uint> sites,
        int slack = Slack,
        bool orALiteral = false)
    {
        var found = new List<(uint, NamesIt)>();

        foreach (uint site in sites)
        {
            if (rom.ToOffsetOrNull(site) is not { } wanted) continue;

            foreach (NamesIt names in EverywhereInTheImage.WhoNames(rom, index, site, slack))
            {
                if (!names.AJump && !(orALiteral && names.ALiteral)) continue;

                if (ScriptReader.Read(rom, names.Points).Any(c => c.Offset == wanted))
                {
                    found.Add((site, names));
                    break;
                }
            }
        }

        return found;
    }
}
