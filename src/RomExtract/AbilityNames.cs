namespace PokeMmo.RomExtract;

/// <summary>
/// What the abilities are called.
/// <para>
/// Names only, and that is the whole of what this cartridge has to say about abilities.
/// Which two a species can have is two bytes on its record — read since the species table
/// was first located, and used by nothing until now. What an ability <em>does</em> is not
/// data anywhere in this image: it is code, the same boundary <see cref="Scripts.SpecialCalls"/>
/// describes at length, and no amount of dumping crosses it.
/// </para>
/// <para>
/// So this half is read and the other half is modelled, and the two are kept in different
/// projects so nobody can confuse them. The names stay with the client, which owns the
/// cartridge; the effects are written on the server, which owns the rules.
/// </para>
/// </summary>
public static class AbilityNames
{
    /// <summary>Bytes per name, the same way the move table is a fixed stride.</summary>
    public const int NameLength = 13;

    /// <summary>
    /// How many the run has to reach before it is the table.
    /// <para>
    /// The real answer on this cartridge is seventy-seven consecutive names after the
    /// anchor. A threshold this far below it costs nothing and stops a stripped test image
    /// naming something arbitrary.
    /// </para>
    /// </summary>
    private const int MinimumRun = 40;

    /// <summary>
    /// The name of ability 1, which is where the table is found from.
    /// <para>
    /// The same technique as the move table, which anchors on POUND, and the species
    /// table, which anchors on the first species' name. It reads one English word to find
    /// an address and then reads none — what comes out is whatever the cartridge says,
    /// in whatever language it says it.
    /// </para>
    /// </summary>
    private const string Anchor = "STENCH";

    /// <summary>
    /// Where the table starts, or nothing when no run of names is found.
    /// <para>
    /// Entry nought is the placeholder every one of these tables carries, and it is kept
    /// rather than skipped: a species with one ability has nought in its second slot, and
    /// something has to be the name of nought.
    /// </para>
    /// </summary>
    public static TableLocation? Locate(Rom rom, Action<string>? log = null)
    {
        byte[] anchor = GameText.EncodeAnchor(Anchor);

        foreach (int match in rom.FindAll(anchor))
        {
            int tableStart = match - NameLength;

            if (tableStart < 0) continue;

            int valid = 0;

            for (int i = 1; i < 200; i++)
            {
                int offset = tableStart + i * NameLength;

                if (offset + NameLength > rom.Length) break;
                if (!GameText.LooksLikeName(GameText.Decode(rom.Slice(offset, NameLength)))) break;

                valid++;
            }

            if (valid < MinimumRun) continue;

            log?.Invoke($"  ability names: {valid} consecutive names decoded cleanly after {Anchor}");

            return new TableLocation(
                "AbilityNames", tableStart, NameLength, valid + 1, $"anchored on ability 1, {Anchor}");
        }

        log?.Invoke($"  no ability names: no run of {MinimumRun} decodes cleanly after {Anchor}");

        return null;
    }

    /// <summary>
    /// Every ability name, index nought first, or an empty list when the table is not
    /// found.
    /// </summary>
    public static IReadOnlyList<string> Extract(Rom rom, Action<string>? log = null)
    {
        if (Locate(rom, log) is not { } table) return [];

        var names = new List<string>(table.EntryCount);

        for (int i = 0; i < table.EntryCount; i++)
        {
            int offset = table.Offset + i * NameLength;

            if (offset + NameLength > rom.Length) break;

            names.Add(GameText.Decode(rom.Slice(offset, NameLength)));
        }

        return names;
    }
}
