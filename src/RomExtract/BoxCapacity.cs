using System.Text.RegularExpressions;

namespace PokeMmo.RomExtract;

/// <summary>
/// How many a box holds, in the cartridge's own words.
/// <para>
/// A seventh catch used to disappear. The battle said "Gotcha!", the party was already
/// six, and the line that adds it checked for room and quietly did nothing — a loss a
/// player could see happen and could not prove.
/// </para>
/// <para>
/// So there had to be somewhere to put it, and somewhere has a size. That size is not in
/// any table: box storage lives in the save file, whose layout this project does not
/// read. But the game <em>says</em> it, to anybody who asks the man in the Pokémon
/// Center:
/// </para>
/// <code>
/// Each BOX can hold up to
/// 30 POKéMON.
/// </code>
/// <para>
/// So it is read out of the sentence. That is a stranger derivation than the usual sort
/// and a better one than the alternative, which is writing thirty here because another
/// game had thirty. The phrase occurs once in the image and there is one number after
/// it.
/// </para>
/// <para>
/// <b>How many boxes there are is not said anywhere</b>, in text or in data. That used to
/// be an assumption in this comment and is now a measurement: <see cref="BoxNames"/> looks
/// for a run of default box names, finds the word BOX forty-six times in this image and
/// never once numbered, and prints the occurrences so anybody can check. They are built at
/// run time out of a word and a counter, so there is nothing here to read.
/// </para>
/// <para>
/// So the count is modelled, which is the one honest option left, and it is marked as such
/// where it lives rather than smuggled in here. Fourteen would still be a number this
/// project was remembering rather than reading, and the standing rule against exactly that
/// is the reason half the things in here are right.
/// </para>
/// </summary>
public static class BoxCapacity
{
    /// <summary>The words to look for, which are the cartridge's not this project's.</summary>
    private const string Says = "BOX can hold up to";

    /// <summary>
    /// Bounds on the answer. A box of nothing is not a box, and a sentence that yields
    /// nine hundred was a different sentence.
    /// </summary>
    private const int Fewest = 1;

    private const int Most = 255;

    /// <summary>How far past the phrase the number is allowed to be.</summary>
    private const int Within = 64;

    public static int? Locate(Rom rom, Action<string>? log = null)
    {
        byte[] needle = [.. GameText.EncodeAnchor(Says).TakeWhile(b => b != 0xFF)];

        var found = new List<(int At, int Holds)>();

        for (int at = 0; at + needle.Length <= rom.Length; at++)
        {
            var same = true;

            for (int i = 0; i < needle.Length && same; i++) same = rom.ReadU8(at + i) == needle[i];

            if (!same) continue;

            string sentence = string.Join(
                " ", GameText.DecodeDialogue(rom.Slice(at, Math.Min(Within, rom.Length - at))));

            Match number = Regex.Match(sentence, @"\d+");

            if (!number.Success) continue;
            if (!int.TryParse(number.Value, out int holds) || holds < Fewest || holds > Most) continue;

            found.Add((at, holds));
        }

        if (found.Count == 0)
        {
            log?.Invoke("  box: this cartridge never says how many a box holds — there will be no box");
            return null;
        }

        // Two sentences saying different numbers is the cartridge disagreeing with
        // itself, which it does not, so it would mean the phrase was matched somewhere
        // it does not belong.
        if (found.Select(f => f.Holds).Distinct().Count() > 1)
        {
            log?.Invoke(
                $"  box: {found.Count} sentences say different numbers " +
                $"({string.Join(", ", found.Select(f => f.Holds).Distinct())}) — not believing any of them");

            return null;
        }

        log?.Invoke(
            $"  box: \"{Says} {found[0].Holds}\" at 0x{Rom.BaseAddress + (uint)found[0].At:X8} — " +
            "one box, because nothing says how many there are");

        return found[0].Holds;
    }
}
