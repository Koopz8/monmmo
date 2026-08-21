namespace PokeMmo.RomExtract;

/// <summary>
/// The image with something taken out of it, for use as a floor.
/// <para>
/// <b>Every control in this project has been the image reversed</b>, and the reasoning written
/// down for it is sound as far as it goes: reversing keeps every byte and every byte's frequency
/// and destroys every command boundary, so what a sweep finds there is what it would find in a
/// file with these statistics and no scripts in it.
/// </para>
/// <para>
/// <b>268 found the half that sentence leaves out.</b> Reversing also keeps every TABLE. A
/// reversed table of pointers is still a run of four-byte words holding addresses into the image,
/// and this cartridge's accidents come from its tables rather than from its byte frequencies —
/// 456 blocks predicted where about 6300 turned up. A control that destroys the thing the sweep
/// is not measuring and keeps the thing that produces the accidents is not a control at all.
/// </para>
/// <para>
/// So there are two here, and the difference between them is the measurement.
/// </para>
/// </summary>
public static class AControlImage
{
    /// <summary>The image backwards — the control this project has always used.</summary>
    /// <remarks>
    /// Keeps: every byte, every frequency, every table's shape. Destroys: every command boundary,
    /// every alignment, and the direction of everything.
    /// </remarks>
    public static Rom Backwards(Rom rom)
    {
        byte[] bytes = rom.Span.ToArray();

        Array.Reverse(bytes);

        return new Rom(bytes);
    }

    /// <summary>
    /// The image rotated, so that the content at every address is somebody else's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Keeps everything the reversal keeps and one thing more: the file still reads
    /// forwards.</b> Tables are still tables, alignment is still alignment, a pointer is still a
    /// pointer and still names an address inside the image — and what it finds there is a
    /// different part of this same cartridge.
    /// </para>
    /// <para>
    /// That is the right null for every question of the shape <em>does this pointer name a
    /// script?</em> The pointers are exactly the real ones; only the correspondence between a
    /// pointer and its target is gone. A hit here is a pointer landing on script-looking bytes it
    /// does not name, which is precisely the accident being counted.
    /// </para>
    /// <para>
    /// <b>By a multiple of four, always.</b> A rotation of one byte would move every table off its
    /// alignment, and a sweep that filters on alignment would then be measuring the rotation
    /// rather than the file. The whole point of this control is that the structure survives.
    /// </para>
    /// </remarks>
    public static Rom Rotated(Rom rom, int by)
    {
        int length = rom.Length;
        int shift = ((by / 4 * 4) % length + length) % length;

        var bytes = new byte[length];
        ReadOnlySpan<byte> from = rom.Span;

        for (var i = 0; i < length; i++) bytes[i] = from[(i + shift) % length];

        return new Rom(bytes);
    }

    /// <summary>
    /// A spread of rotations, so that one lucky offset is not the answer.
    /// </summary>
    /// <remarks>
    /// Fractions of the file rather than anything drawn at random: a control that cannot be
    /// reproduced from the file alone is a control nobody can check, and this project has no
    /// source of randomness it is willing to put in a measurement.
    /// </remarks>
    public static IReadOnlyList<int> Offsets(Rom rom, int howMany = 3) =>
        [.. Enumerable.Range(1, howMany).Select(i => rom.Length / (howMany + 1) * i / 4 * 4)];
}
