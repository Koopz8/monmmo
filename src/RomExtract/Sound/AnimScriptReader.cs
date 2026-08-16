namespace PokeMmo.RomExtract.Sound;

/// <summary>
/// Reading the language a move's animation is written in.
/// <para>
/// A move's animation is a program. FireRed keeps a pointer table indexed by move, and each
/// entry points at a script in a byte language of forty-eight commands: load a graphic, make
/// a sprite here, wait four frames, play this sound panned to the attacker's side, call this
/// subroutine three times, end.
/// </para>
/// <para>
/// All of that is <b>read</b> — the opcodes, every delay, every coordinate, every repeat
/// count, every sound id, every panning argument. An animation's whole rhythm comes off the
/// cartridge, which means it is not something this project has to invent.
/// </para>
/// <para>
/// <b>And here is the boundary.</b> <c>createsprite</c> names a sprite template, and a
/// sprite template is a pointer to a struct containing a pointer to a <em>callback
/// function</em>. The script says which sprite to make and where to put it; what that sprite
/// then does over time — arcs, spirals, tracks the target, fades — is compiled ARM code, and
/// this project does not read code. So this reader takes the template as an <em>identity</em>
/// and stops there. Two moves naming the same template are known to be doing the same thing;
/// what that thing is belongs to the next layer, and is modelled.
/// </para>
/// </summary>
public static class AnimScriptReader
{
    private const byte CreateSprite = 0x02;
    private const byte CreateVisualTask = 0x03;
    private const byte Delay = 0x04;
    private const byte EndOpcode = 0x08;
    private const byte CallOpcode = 0x0E;
    private const byte ReturnOpcode = 0x0F;
    private const byte GotoOpcode = 0x13;
    private const byte CreateSoundTask = 0x1F;

    /// <summary>The highest opcode the format defines. Above this is not an animation.</summary>
    public const byte HighestOpcode = 0x2F;

    /// <summary>
    /// How many commands one script may be before this reader gives up. <b>Modelled.</b>
    /// <para>
    /// A script can jump backwards, so it can loop. Unlike a music track, an animation that
    /// loops for ever is a real possibility rather than the norm — but the budget is the same
    /// idea, and it belongs to the reader rather than being a judgement about the script.
    /// </para>
    /// </summary>
    public const int MostCommands = 4_000;

    /// <summary>
    /// Fixed argument bytes for each opcode, and which group it belongs to.
    /// <para>
    /// Read off the format's own macro definitions. The three commands with a variable
    /// number of arguments carry −1 here and are handled where they are read.
    /// </para>
    /// </summary>
    private static (int Bytes, AnimCommand Command)? Shape(byte opcode) => opcode switch
    {
        0x00 or 0x01 => (2, AnimCommand.Graphic),

        CreateSprite or CreateVisualTask => (-1, AnimCommand.Creates),

        Delay => (1, AnimCommand.Waits),
        0x05 => (0, AnimCommand.Waits),

        0x06 or 0x07 => (0, AnimCommand.Nothing),

        EndOpcode => (0, AnimCommand.End),

        0x09 => (2, AnimCommand.Sound),

        0x0A or 0x0B => (1, AnimCommand.Screen),
        0x0C => (2, AnimCommand.Screen),
        0x0D => (0, AnimCommand.Screen),

        CallOpcode => (4, AnimCommand.Flow),
        ReturnOpcode => (0, AnimCommand.Flow),

        0x10 => (3, AnimCommand.Screen),
        0x11 => (8, AnimCommand.Flow),
        0x12 => (5, AnimCommand.Flow),
        GotoOpcode => (4, AnimCommand.Flow),

        0x14 => (1, AnimCommand.Screen),
        0x15 or 0x16 or 0x17 => (0, AnimCommand.Screen),
        0x18 => (1, AnimCommand.Screen),

        0x19 => (3, AnimCommand.Sound),
        0x1A => (1, AnimCommand.Sound),
        0x1B => (6, AnimCommand.Sound),
        0x1C => (5, AnimCommand.Sound),
        0x1D => (4, AnimCommand.Sound),

        0x1E => (2, AnimCommand.Screen),

        CreateSoundTask => (-1, AnimCommand.Creates),

        0x20 => (0, AnimCommand.Waits),
        0x21 => (7, AnimCommand.Flow),

        0x22 or 0x23 => (1, AnimCommand.Screen),
        0x24 => (4, AnimCommand.Flow),
        0x25 => (3, AnimCommand.Screen),

        0x26 or 0x27 => (6, AnimCommand.Sound),

        0x28 => (1, AnimCommand.Screen),
        0x29 => (0, AnimCommand.Screen),
        0x2A => (1, AnimCommand.Screen),
        0x2B or 0x2C => (1, AnimCommand.Screen),
        0x2D or 0x2E => (1, AnimCommand.Screen),
        0x2F => (0, AnimCommand.Sound),

        _ => null,
    };

    /// <summary>Reads one move's animation from where the table said it begins.</summary>
    public static AnimScript Read(Rom rom, int offset)
    {
        var events = new List<AnimEvent>();
        var returns = new Stack<int>();
        var seen = new HashSet<int>();

        int unknown = 0;
        int at = offset;

        while (events.Count < MostCommands)
        {
            if (at < 0 || at >= rom.Length) return new AnimScript(offset, events, false, unknown);

            byte opcode = rom.ReadU8(at);

            if (Shape(opcode) is not { } shape)
            {
                // An opcode the format does not define. This is where a script that was
                // never a script stops, and it is the check the whole locator leans on.
                unknown++;

                events.Add(new AnimEvent(at, opcode, AnimCommand.Unknown, []));

                return new AnimScript(offset, events, false, unknown);
            }

            int start = at + 1;

            int bytes = shape.Bytes >= 0
                ? shape.Bytes
                : VariableBytes(rom, opcode, start);

            if (bytes < 0 || start + bytes > rom.Length)
                return new AnimScript(offset, events, false, unknown);

            var arguments = new byte[bytes];

            for (int i = 0; i < bytes; i++) arguments[i] = rom.ReadU8(start + i);

            at = start + bytes;

            if (opcode == EndOpcode)
            {
                events.Add(new AnimEvent(start, opcode, AnimCommand.End, arguments));

                return new AnimScript(offset, events, true, unknown);
            }

            if (opcode is CallOpcode or GotoOpcode)
            {
                uint address = (uint)(arguments[0] | (arguments[1] << 8)
                    | (arguments[2] << 16) | (arguments[3] << 24));

                if (rom.ToOffsetOrNull(address) is not { } target)
                    return new AnimScript(offset, events, false, unknown);

                events.Add(new AnimEvent(start, opcode, AnimCommand.Flow, arguments, target));

                if (opcode == CallOpcode) returns.Push(at);

                // Been here before, which for a script means it loops. Stopped and counted
                // as an ending, exactly as a looping music track is.
                if (!seen.Add(target)) return new AnimScript(offset, events, true, unknown);

                at = target;

                continue;
            }

            events.Add(new AnimEvent(start, opcode, shape.Command, arguments));

            if (opcode == ReturnOpcode)
            {
                if (returns.Count == 0) return new AnimScript(offset, events, true, unknown);

                at = returns.Pop();
            }
        }

        return new AnimScript(offset, events, false, unknown);
    }

    /// <summary>
    /// The three commands whose length depends on a count byte inside them.
    /// <para>
    /// <c>createsprite</c> and <c>createvisualtask</c> carry a four-byte pointer, one byte of
    /// their own, a count, and then that many two-byte arguments. <c>createsoundtask</c> is
    /// the same without the byte of its own. The two-byte arguments are what the format's
    /// macros write, and getting that width wrong would not fail — it would read the next
    /// command as an argument and every animation after the first would be nonsense.
    /// </para>
    /// </summary>
    private static int VariableBytes(Rom rom, byte opcode, int start)
    {
        int countAt = opcode == CreateSoundTask ? start + 4 : start + 5;

        if (countAt >= rom.Length) return -1;

        int count = rom.ReadU8(countAt);

        return countAt - start + 1 + count * 2;
    }

    /// <summary>
    /// Everything read across a whole table of them, and what it came to.
    /// <para>
    /// The report is the point, and it is the same report shape the move-effect work uses:
    /// how many were understood, how many were not, and how many opcodes were stepped over.
    /// A count of what is not working yet is the only thing that makes it possible to tell
    /// whether it is getting better.
    /// </para>
    /// </summary>
    public static IReadOnlyList<AnimScript> All(
        Rom rom, IReadOnlyList<int> starts, Action<string>? log = null)
    {
        List<AnimScript> read = [.. starts.Select(start => Read(rom, start))];

        int ended = read.Count(s => s.EndedProperly);

        log?.Invoke($"  {read.Count} move animations, {ended} of which ran to an end");

        log?.Invoke(
            $"    {read.Sum(s => s.Events.Count)} commands, " +
            $"{read.SelectMany(s => s.Templates).Distinct().Count()} distinct sprite templates, " +
            $"{read.SelectMany(s => s.Sounds).Distinct().Count()} distinct sounds");

        log?.Invoke($"    {read.Sum(s => s.Unknown)} opcodes this reader does not account for");

        return read;
    }
}
