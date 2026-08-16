namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// Unpacking the recordings that are stored as differences rather than as audio.
/// <para>
/// Most of what a cry is would be wasted as flat eight-bit audio: a creature's noise is
/// short, loud and mostly smooth, so consecutive samples are usually close together. The
/// cartridge exploits that by storing the <em>difference</em> between one sample and the
/// next, four bits at a time, which halves it.
/// </para>
/// <para>
/// A block is sixty-five samples' worth in thirty-three bytes:
/// </para>
/// <code>
/// byte 0        the first sample, signed, stored outright
/// byte 1        one nibble; the high half is padding
/// bytes 2..32   two nibbles each
/// </code>
/// <para>
/// Each nibble indexes <see cref="Deltas"/>, and each value is added to the sample before
/// it. Sixty-four samples come out of every block.
/// </para>
/// <para>
/// <b>Read or modelled?</b> The recordings are read — they are audio on the player's own
/// file. The sixteen numbers in the table are read too, in the sense this project uses:
/// they are the format's own, documented, and they are the same sixteen everywhere. What is
/// <em>modelled</em> is only the decision about wrap-around described on the loop below.
/// </para>
/// <para>
/// This is why the recordings this locator finds now include ones it could not see before.
/// A packed recording carries its marker in the <em>first</em> byte of its header rather
/// than the fourth, and a reader insisting on three leading zeroes — which is what the
/// format documentation describes and what this project implemented — could never find one.
/// Every cry on the cartridge was invisible until this was looked at properly.
/// </para>
/// </summary>
public static class CryDecoder
{
    /// <summary>
    /// What each of the sixteen nibbles is worth.
    /// <para>
    /// Not evenly spaced, and that is the cleverness: the eight steps up are the squares of
    /// nought to seven, so small differences are recorded precisely and large ones coarsely.
    /// Audio spends almost all of its time making small differences.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<sbyte> Deltas =
    [
        0, 1, 4, 9, 16, 25, 36, 49,
        -64, -49, -36, -25, -16, -9, -4, -1,
    ];

    /// <summary>Samples that come out of one block.</summary>
    public const int SamplesPerBlock = 64;

    /// <summary>And bytes that go into one.</summary>
    public const int BytesPerBlock = 33;

    /// <summary>
    /// How many bytes on the cartridge a packed recording of this many samples occupies.
    /// <para>
    /// Whole blocks only. A recording whose length is not a multiple of sixty-four has a
    /// remainder that is not stored at all and comes back as silence, which is the format's
    /// own behaviour rather than a shortcut here.
    /// </para>
    /// </summary>
    public static int PackedBytesFor(int samples) => samples / SamplesPerBlock * BytesPerBlock;

    /// <summary>
    /// Unpacks a recording into plain signed eight-bit audio.
    /// <para>
    /// Returns as many samples as the header said, with any tail past the last whole block
    /// left silent.
    /// </para>
    /// </summary>
    public static sbyte[] Decode(Rom rom, SampleRecord record)
    {
        var audio = new sbyte[Math.Max(0, record.Length)];

        if (!record.Compressed)
        {
            // Not packed at all: the bytes after the header are already the answer.
            for (int i = 0; i < audio.Length && record.DataOffset + i < rom.Length; i++)
                audio[i] = unchecked((sbyte)rom.ReadU8(record.DataOffset + i));

            return audio;
        }

        int blocks = audio.Length / SamplesPerBlock;

        for (int block = 0; block < blocks; block++)
        {
            int at = record.DataOffset + block * BytesPerBlock;
            int into = block * SamplesPerBlock;

            if (at + BytesPerBlock > rom.Length) break;

            // The first sample of a block is stored outright, which is what stops an error
            // anywhere in a recording from ruining everything after it: a block is at most
            // sixty-four samples of damage.
            sbyte sample = unchecked((sbyte)rom.ReadU8(at));

            audio[into] = sample;

            // Then one lone nibble, because sixty-four samples need sixty-three differences
            // and sixty-three is odd. The high half of that byte is padding.
            sample = Step(sample, rom.ReadU8(at + 1) & 0x0F);

            audio[into + 1] = sample;

            for (int pair = 1; pair < 32; pair++)
            {
                byte both = rom.ReadU8(at + pair + 1);

                sample = Step(sample, both >> 4);
                audio[into + pair * 2] = sample;

                sample = Step(sample, both & 0x0F);
                audio[into + pair * 2 + 1] = sample;
            }
        }

        return audio;
    }

    /// <summary>
    /// One difference applied to one sample.
    /// <para>
    /// <b>The one modelled decision in this file.</b> The sum can leave the range a signed
    /// byte holds, and what the hardware does then is wrap round rather than stop at the
    /// edge — so that is what happens here. Clamping instead would be the obvious choice and
    /// the wrong one: it would quietly flatten the loudest part of every cry, which is the
    /// part a player recognises.
    /// </para>
    /// </summary>
    private static sbyte Step(sbyte from, int nibble) =>
        unchecked((sbyte)(from + Deltas[nibble & 0x0F]));
}
