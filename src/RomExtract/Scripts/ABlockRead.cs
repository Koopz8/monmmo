namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One address, decoded — the bytes and what they read as, side by side, and where the read
/// stopped.
/// </summary>
/// <remarks>
/// <para>
/// <b>This project's method is "stop inferring and print the bytes", and until now there was no
/// command that printed them.</b> `--script-map` dumps a map and stops at the first `goto`;
/// `--stops` prints one command's stopped reads; `--climb` walks upwards. Reading a block the
/// scan already opens has meant a hexdump by hand and a width table copied into a scratch script
/// — milestone 190 did it, 199 did it for three widths, 228 did it for `0x0180`, and 232 did it
/// again for `0x00AB` before writing this.
/// </para>
/// <para>
/// The bytes and the decode come off the same command here, so a hexdump and a disassembly of
/// the same address cannot disagree — which is the failure a hand-copied width table invites.
/// </para>
/// </remarks>
public static class ABlockRead
{
    /// <summary>One command: where it is, what it is, and the bytes it is made of.</summary>
    public sealed record Line(int Offset, byte Code, IReadOnlyList<byte> Arguments)
    {
        /// <summary>What this project calls the command, or its number when it has no name.</summary>
        public string Name => ScriptCommands.NameOf(Code);

        /// <summary>
        /// The bytes, opcode first. <b>Off the same command as the decode</b>, so the two halves
        /// of the output cannot disagree with each other.
        /// </summary>
        public IReadOnlyList<byte> Bytes => [Code, .. Arguments];
    }

    /// <summary>
    /// One straight-line read: the commands, where it stopped if it did, and the addresses it
    /// hands control to.
    /// </summary>
    /// <param name="Address">Where the read started.</param>
    /// <param name="Lines">What it decoded, in order.</param>
    /// <param name="StoppedOn">
    /// The command code with no width that ended the read, or null when it ended properly on an
    /// <c>end</c>, a <c>return</c> or a <c>goto</c>.
    /// </param>
    /// <param name="StoppedAt">Where that byte is, or null.</param>
    /// <param name="Reaches">Every address this block hands control to, in the order it does.</param>
    public sealed record Block(
        uint Address,
        IReadOnlyList<Line> Lines,
        byte? StoppedOn,
        int? StoppedAt,
        IReadOnlyList<uint> Reaches)
    {
        /// <summary>Whether the read ran out of table rather than out of script.</summary>
        public bool Stopped => StoppedOn is not null;
    }

    /// <summary>
    /// The address a command hands control to, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four pointer forms and no others. A <c>compare</c>'s operand is a number that can look
    /// like an address and is not one, and the byte after a conditional is the fall-through
    /// rather than a hand-over — treating either as an edge would make this reading claim the
    /// block reaches places it does not.
    /// </para>
    /// <para>
    /// A <c>trainerbattle</c> carries scripts too, and they are deliberately not here: which of
    /// its pointers is a script depends on its kind, and that reading lives in
    /// <see cref="ScriptReader"/> where it is already guarded.
    /// </para>
    /// </remarks>
    public static uint? HandsOverTo(ScriptCommand command) =>
        command.Code switch
        {
            ScriptCommands.Call or ScriptCommands.Goto when command.Arguments.Length >= 4 =>
                command.Pointer(),
            ScriptCommands.CallIf or ScriptCommands.GotoIf when command.Arguments.Length >= 5 =>
                command.Pointer(1),
            _ => null,
        };

    /// <summary>One block, read straight through from an address.</summary>
    public static Block One(Rom rom, uint address)
    {
        List<ScriptCommand> commands = ScriptReader.Read(rom, address);

        List<uint> reaches = [];

        foreach (ScriptCommand command in commands)
        {
            if (HandsOverTo(command) is { } target && rom.IsRomAddress(target) && !reaches.Contains(target))
                reaches.Add(target);
        }

        // Both halves come from ScriptReader, which already returns nothing from either when a
        // read ends properly. A `stoppedOn is null ? null : stoppedAt` guard here looked like a
        // rule and was a second statement of one that already held — breaking it changed nothing
        // because nothing reached it, which is 219 again.
        return new Block(
            address,
            [.. commands.Select(c => new Line(c.Offset, c.Code, c.Arguments))],
            ScriptReader.StoppedAt(rom, address),
            ScriptReader.StoppedAtOffset(rom, address),
            reaches);
    }

    /// <summary>
    /// The block at an address and every block it reaches, each read once, entry first.
    /// </summary>
    /// <remarks>
    /// Each once, because two arms of one branch land on the same place all over this cartridge
    /// and printing that block twice would say the reading found two of something.
    /// </remarks>
    public static IReadOnlyList<Block> From(Rom rom, uint address, int mostBlocks = 24)
    {
        List<Block> found = [];
        HashSet<uint> seen = [address];
        Queue<uint> queue = new([address]);

        while (queue.Count > 0 && found.Count < mostBlocks)
        {
            Block block = One(rom, queue.Dequeue());

            found.Add(block);

            foreach (uint target in block.Reaches)
                if (seen.Add(target)) queue.Enqueue(target);
        }

        return found;
    }
}
