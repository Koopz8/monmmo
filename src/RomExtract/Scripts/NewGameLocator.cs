namespace PokeMmo.RomExtract.Scripts;

/// <summary>What a brand new save already knows, before the player has done anything.</summary>
/// <param name="Address">Where the script that says so lives.</param>
/// <param name="Flags">Flags the cartridge sets before the first frame.</param>
/// <param name="Variables">Variables it writes, as (id, value).</param>
public sealed record NewGameState(
    uint Address,
    IReadOnlyList<int> Flags,
    IReadOnlyList<(int Variable, int Value)> Variables);

/// <summary>
/// Finds the script a new game runs before the player has taken a step.
/// <para>
/// This one was found by a hole rather than by looking for it. MR. FUJI is meant to be
/// at the top of the POKéMON TOWER, and his house in LAVENDER TOWN is meant to be empty
/// until he is rescued; the house object carries a flag that hides him when it is set.
/// On a save with no flags in it that flag is not set, so he was standing in his own
/// front room on turn one, holding the POKé FLUTE, and the whole tower was scenery.
/// </para>
/// <para>
/// The mistake underneath it is a nice one: a fresh save is not an empty save. The
/// cartridge starts a new game by <em>setting</em> forty-nine flags, and every one of
/// them hides somebody who has not been met yet. An empty save is not the beginning of
/// the story — it is a world where every ending has already happened at once.
/// </para>
/// <para>
/// Located by shape. A run of back-to-back <c>setflag</c> commands is a distinctive
/// thing for a script to be, and the longest such run on the cartridge is forty-nine
/// long. Three longer-looking runs exist and are all stretches of graphics that happen
/// to be full of 0x29 bytes — they are told apart by two things a script has and a
/// picture does not: something in the image points at it, and the flags it sets are not
/// all the same number.
/// </para>
/// <para>
/// Nothing here is hardcoded, and deliberately so: the flag numbers are the answer, and
/// a locator that knew any of them in advance would only be able to find the cartridge
/// it was written against.
/// </para>
/// </summary>
public static class NewGameLocator
{
    private const byte SetFlag = 0x29;
    private const byte SetVar = 0x16;
    private const byte End = 0x02;
    private const byte Return = 0x03;

    /// <summary>
    /// Short runs are ordinary. Plenty of scripts set three or four flags on their way
    /// out; the opening of a game is the only thing that sets dozens.
    /// </summary>
    private const int ShortestRun = 8;

    /// <summary>
    /// What a new save starts with, or nothing when no run on this image stands out.
    /// </summary>
    public static NewGameState? Locate(Rom rom, Action<string>? log = null)
    {
        ReadOnlySpan<byte> data = rom.Span;

        (int Offset, int Count)? best = null;
        int runners = 0;

        for (int offset = 0; offset + 3 <= data.Length;)
        {
            if (data[offset] != SetFlag)
            {
                offset++;
                continue;
            }

            int start = offset;
            int count = 0;

            while (offset + 3 <= data.Length && data[offset] == SetFlag)
            {
                offset += 3;
                count++;
            }

            if (count < ShortestRun) continue;

            runners++;

            // A picture cannot be jumped to and does not vary. Both tests are about the
            // run being a script rather than about which script it is.
            if (!IsPointedAt(rom, start)) continue;
            if (Distinct(data, start, count) < 2) continue;

            if (best is null || count > best.Value.Count) best = (start, count);
        }

        if (best is not { } run)
        {
            log?.Invoke($"  new game: no run of {ShortestRun} or more setflags is pointed at");
            return null;
        }

        var flags = new List<int>(run.Count);

        for (int i = 0; i < run.Count; i++)
            flags.Add(data[run.Offset + i * 3 + 1] | (data[run.Offset + i * 3 + 2] << 8));

        var variables = new List<(int, int)>();
        int after = run.Offset + run.Count * 3;

        // Whatever the same script goes on to write. In FireRed that is one variable,
        // and taking it here rather than assuming there is none costs nothing and keeps
        // the answer to "what does a new game start with" in one place.
        while (after + 5 <= data.Length && data[after] == SetVar)
        {
            variables.Add((data[after + 1] | (data[after + 2] << 8), data[after + 3] | (data[after + 4] << 8)));
            after += 5;
        }

        uint address = Rom.BaseAddress + (uint)run.Offset;

        log?.Invoke(
            $"  new game: 0x{address:X8} sets {flags.Count} flags and {variables.Count} variables " +
            $"({runners} runs of {ShortestRun}+ setflags on this image, {(data[after] is End or Return ? "ends cleanly" : "runs on")})");

        return new NewGameState(address, flags, variables);
    }

    /// <summary>Whether any four-byte-aligned word in the image is a pointer to here.</summary>
    private static bool IsPointedAt(Rom rom, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;
        Span<byte> pattern =
        [
            (byte)address,
            (byte)(address >> 8),
            (byte)(address >> 16),
            (byte)(address >> 24),
        ];

        return rom.FindAll(pattern.ToArray(), alignment: 4).Any();
    }

    /// <summary>How many different flags a run sets, which for a picture is one.</summary>
    private static int Distinct(ReadOnlySpan<byte> data, int offset, int count)
    {
        var seen = new HashSet<int>();

        for (int i = 0; i < count; i++)
            seen.Add(data[offset + i * 3 + 1] | (data[offset + i * 3 + 2] << 8));

        return seen.Count;
    }
}
