using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// Every place this cartridge looks at a variable without executing a command.
/// <para>
/// <b>246's fault, given somewhere to live so it cannot be found a third time.</b> Every sweep
/// in this project walks a script stream and decides what a number is by which operand of which
/// command named it. That is the right shape for a question about a script stream and the wrong
/// shape for a question about the cartridge, because two of a map's own records name a variable
/// in a field:
/// </para>
/// <list type="bullet">
/// <item>
/// the <b>arrival condition</b> in the map's header — <em>run this script when this variable
/// holds this value</em> — which 246 found by way of <c>0x407C</c> being reported as looked at
/// nowhere in sixteen megabytes while nineteen maps consulted it;
/// </item>
/// <item>
/// the <b>trigger condition</b> on a square — the same question asked of somebody standing
/// somewhere rather than arriving — which is the same field, on a different list, and was
/// missed by the same reasoning.
/// </item>
/// </list>
/// <para>
/// <b>One place rather than five.</b> 224's rule is that a shared wrong list is worse than five
/// private ones, and that is about a list nobody compares. This is the other half of the same
/// rule: when a fault is that a KIND of thing was never enumerated, five private enumerations
/// means finding it five times, and the one that gets missed is the one nobody thinks of. Adding
/// a third kind here reaches every caller at once.
/// </para>
/// <para>
/// <b>The flag half is already done</b> and is not repeated here: a person's record carries the
/// flag that hides them, and <c>FlagGates</c> has read it since long before this. What has never
/// been enumerated is the variable half.
/// </para>
/// </summary>
public static class ReadsThatAreNotCommands
{
    /// <summary>What the map header's own list is called in the output.</summary>
    public const string OnArrival = "a map header, on arrival";

    /// <summary>What a square's condition is called in the output.</summary>
    public const string OnASquare = "a trigger, on the square";

    /// <summary>
    /// Whether one header entry looks at anything — it does, unless it runs nothing.
    /// </summary>
    /// <remarks>
    /// The list in a map's header ends with an all-zero record, and its variable field is a
    /// nought that names nothing. Counting it would rescue variable 0 from every deaf list in
    /// this project for free, on all four hundred and twenty-five maps — a correction that can
    /// only make findings disappear, which is the direction nobody notices.
    /// </remarks>
    public static bool IsARead(MapEntryScript entry) => entry.ScriptAddress != 0;

    /// <summary>
    /// Whether one trigger looks at anything: it needs a script to run and a variable to name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves are needed and neither is the same rule as the header's.</b> A trigger
    /// with no script is a record that does nothing, exactly like the header terminator. But a
    /// trigger with a script and a variable field of nought is ordinary and common — it is a
    /// square that always fires — and this cartridge has far more of those than of conditional
    /// ones. Counting them would put variable 0 in every reader's list from hundreds of squares.
    /// </para>
    /// <para>
    /// Nought is used rather than a band boundary because a band is a thing this project READS
    /// and does not assert. The distribution of what this field actually holds is printed beside
    /// the count, so the rule can be looked at rather than trusted.
    /// </para>
    /// </remarks>
    public static bool IsARead(MapTrigger trigger) => trigger.ScriptAddress != 0 && trigger.Variable != 0;

    /// <summary>The variables a set of header entries looks at.</summary>
    public static IReadOnlyCollection<int> LookedAt(IEnumerable<MapEntryScript> entries) =>
        [.. entries.Where(IsARead).Select(e => e.Variable).Distinct()];

    /// <summary>The variables a set of triggers looks at.</summary>
    public static IReadOnlyCollection<int> LookedAt(IEnumerable<MapTrigger> triggers) =>
        [.. triggers.Where(IsARead).Select(t => t.Variable).Distinct()];

    /// <summary>
    /// Both kinds at once, keyed by what does the looking — the shape
    /// <see cref="BothNamespaces.LookedAtBySomethingElse"/> takes.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<int>> Of(
        IEnumerable<MapEntryScript> entries, IEnumerable<MapTrigger> triggers) =>
        new Dictionary<string, IReadOnlyCollection<int>>
        {
            [OnArrival] = LookedAt(entries),
            [OnASquare] = LookedAt(triggers),
        };

    /// <summary>
    /// What a trigger's variable field actually holds, by band — so the nought rule is READ.
    /// </summary>
    /// <remarks>
    /// A rule with a number in it that nobody can check is a number nothing computes (231). The
    /// caller prints this beside the count, and if the field turned out to hold something other
    /// than nought and the story's own variables, the rule above would be visibly wrong rather
    /// than quietly so.
    /// </remarks>
    public static IReadOnlyList<(int From, int Triggers)> WhatTheFieldHolds(
        IEnumerable<MapTrigger> triggers, int width = 0x1000) =>
    [
        .. triggers
            .GroupBy(t => t.Variable / width * width)
            .Select(g => (From: g.Key, Triggers: g.Count()))
            .OrderBy(g => g.From),
    ];
}
