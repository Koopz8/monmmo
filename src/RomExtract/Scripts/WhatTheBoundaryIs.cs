namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// The gating flags nothing in the world moves, sorted by what NAMES the script that moves them.
/// <para>
/// <c>--flags</c> has said since 175 that sixty of the boundary's flags are "moved by something
/// reading as script that the maps never open", and 270 showed that the promotion it offered —
/// "jumped into" — measures the region a site sits in and not the block. This is the sort that
/// survives that: a site is either a command of the script a new game runs before the first
/// frame, or a command of a block a jump names, or a command of a block an aligned literal names,
/// or nothing names it — and the buckets are in that order because each is a stronger claim than
/// the next, and a flag goes in the FIRST it satisfies.
/// </para>
/// <para>
/// <b>"Named by nothing" is the bucket that matters</b>, because a site that reads as a script
/// and that nothing in sixteen megabytes names is what 269 showed an accident looks like.
/// </para>
/// </summary>
public static class WhatTheBoundaryIs
{
    public enum Named
    {
        /// <summary>A command of the script a new game runs — set before the first frame.</summary>
        TheOpening,

        /// <summary>A command of a block a jump names, read from the jump's own target.</summary>
        AJumpsBlock,

        /// <summary>A command of a block an aligned word no command owns names — code or a table.</summary>
        ALiteralsBlock,

        /// <summary>Reads as a script and nothing names the block it is on.</summary>
        Nothing,
    }

    public sealed record Sorted(int Flag, Named By, FlagSite Site, NamesIt? What);

    /// <summary>
    /// Each flag once, by the strongest claim any of its unopened sites supports.
    /// </summary>
    /// <param name="opening">Where the new-game script starts, or null when it was not found.</param>
    public static IReadOnlyList<Sorted> Sort(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        IEnumerable<EverywhereInTheImage.OutsideTheWorld> boundary,
        uint? opening,
        int slack = JumpedIntoUnderANudge.Slack)
    {
        var openingCommands = new HashSet<int>();

        if (opening is { } at)
        {
            foreach (ScriptCommand command in ScriptReader.Read(rom, at)) openingCommands.Add(command.Offset);
        }

        var found = new List<Sorted>();

        foreach (EverywhereInTheImage.OutsideTheWorld flag in boundary)
        {
            Sorted? best = null;

            foreach (FlagSite site in flag.Unopened)
            {
                Sorted here = One(rom, index, flag.Flag, site, openingCommands, slack);

                if (best is null || here.By < best.By) best = here;
                if (best.By == Named.TheOpening) break;
            }

            if (best is not null) found.Add(best);
        }

        return found;
    }

    private static Sorted One(
        Rom rom,
        IReadOnlyDictionary<uint, IReadOnlyList<int>> index,
        int flag,
        FlagSite site,
        HashSet<int> openingCommands,
        int slack)
    {
        if (openingCommands.Contains(site.Offset)) return new Sorted(flag, Named.TheOpening, site, null);

        if (JumpedIntoUnderANudge.WhatNamesTheBlock(rom, index, site.Address, 0, slack) is { } jump)
            return new Sorted(flag, Named.AJumpsBlock, site, jump);

        if (JumpedIntoUnderANudge.WhatNamesTheBlock(rom, index, site.Address, 0, slack, orALiteral: true) is { } literal)
            return new Sorted(flag, Named.ALiteralsBlock, site, literal);

        return new Sorted(flag, Named.Nothing, site, null);
    }
}
