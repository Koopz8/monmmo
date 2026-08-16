namespace PokeMmo.RomExtract;

/// <summary>
/// How many boxes there are, found by looking for what they are called.
/// <para>
/// <see cref="BoxCapacity"/> reads how many one box holds out of the sentence the man in the
/// Pokémon Center says, and then stops: it says, in its own comment, that how many boxes
/// there are is not written anywhere, so there is one. That was true of everything that had
/// been looked at and it was not true of the image.
/// </para>
/// <para>
/// Box storage lives in the save file, whose layout this project does not read — but a box
/// has a <em>name</em>, and a name a player has never changed has to come from somewhere.
/// The defaults are in the image as a run of consecutive fixed-width strings: BOX 1, BOX 2,
/// and so on. Counting them is counting the boxes.
/// </para>
/// <para>
/// Nothing here knows the answer is fourteen. It looks for the first, works out the stride
/// from where the second is, and then walks until the numbering stops — so a cartridge with
/// eight boxes or twenty answers eight or twenty, and this project does not have to remember
/// which game had which.
/// </para>
/// </summary>
public static class BoxNames
{
    /// <summary>The most a run may be, so a false positive cannot walk the whole image.</summary>
    private const int Most = 64;

    /// <summary>
    /// The widest a fixed-width name entry may be. Nine on this cartridge, and the point of
    /// a bound rather than a constant is that the stride is measured rather than assumed.
    /// </summary>
    private const int WidestEntry = 32;

    /// <summary>
    /// How many boxes this cartridge has, and where their names are, or nothing.
    /// </summary>
    public static (int At, int Count, int Stride)? Locate(Rom rom, Action<string>? log = null)
    {
        byte[] first = Named(1);
        byte[] second = Named(2);

        var runs = new List<(int At, int Count, int Stride)>();

        for (int at = 0; at + first.Length <= rom.Length; at++)
        {
            if (!Matches(rom, at, first)) continue;

            // The stride is measured from where the second name actually is rather than
            // assumed, because a table of fixed-width strings can be padded to any width and
            // guessing nine would be remembering another game again.
            int stride = 0;

            for (int width = second.Length; width <= WidestEntry; width++)
            {
                if (Matches(rom, at + width, second))
                {
                    stride = width;
                    break;
                }
            }

            if (stride == 0) continue;

            // And then walk until the numbering stops. A run of two is a coincidence in a
            // sixteen-megabyte image; a run of fourteen is a table.
            int count = 2;

            while (count < Most && Matches(rom, at + count * stride, Named(count + 1))) count++;

            runs.Add((at, count, stride));
        }

        if (runs.Count == 0)
        {
            // A negative worth evidence rather than a shrug. If the word is not in the
            // image at all, that is one finding; if it is there dozens of times and never
            // numbered, that is a different one, and only the second says the names are
            // built at run time out of a word and a counter.
            Report(rom, log);

            return null;
        }

        // The longest, and every one of them printed. Two runs of the same length would be
        // a reason to look rather than to pick, and saying so is cheaper than finding out
        // later that something chose quietly.
        (int At, int Count, int Stride) longest = runs.OrderByDescending(r => r.Count).First();

        foreach ((int found, int howMany, int wide) in runs)
        {
            log?.Invoke(
                $"  rules: {howMany} box names at 0x{found:X8}, {wide} bytes apart" +
                (found == longest.At ? " (taken)" : ""));
        }

        return longest;
    }

    /// <summary>
    /// What was actually there, when no run was. Printed rather than summarised, because a
    /// negative result in this project is a finding and a finding has to be checkable.
    /// </summary>
    private static void Report(Rom rom, Action<string>? log)
    {
        byte[] word = [.. GameText.EncodeAnchor("BOX").TakeWhile(b => b != 0xFF)];

        var found = new List<int>();

        for (int at = 0; at + word.Length <= rom.Length; at++)
        {
            bool same = true;

            for (int i = 0; i < word.Length && same; i++) same = rom.ReadU8(at + i) == word[i];

            if (same) found.Add(at);
        }

        log?.Invoke(
            $"  rules: the word BOX is in this image {found.Count} times and never followed by a " +
            "numbered run, so the names are built at run time and there is one box");

        foreach (int at in found.Take(4))
        {
            byte[] after = [.. Enumerable.Range(0, 16).Select(i => rom.ReadU8(at + i))];

            log?.Invoke($"    0x{at:X8}  \"{GameText.Decode(after).Replace('\n', ' ')}\"");
        }
    }

    /// <summary>What the cartridge calls the nth box before anybody renames it.</summary>
    private static byte[] Named(int number) =>
        [.. GameText.EncodeAnchor($"BOX {number}").TakeWhile(b => b != 0xFF)];

    private static bool Matches(Rom rom, int at, byte[] wanted)
    {
        if (at < 0 || at + wanted.Length > rom.Length) return false;

        for (int i = 0; i < wanted.Length; i++)
        {
            if (rom.ReadU8(at + i) != wanted[i]) return false;
        }

        // The byte after has to end the string, or "BOX 1" has matched the front of
        // "BOX 12" and the count would be one short of wherever it stopped.
        byte after = rom.ReadU8(at + wanted.Length);

        return after is 0xFF or 0x00;
    }
}
