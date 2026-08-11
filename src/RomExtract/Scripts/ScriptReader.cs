namespace PokeMmo.RomExtract.Scripts;

/// <summary>
/// One instruction: a command and the bytes that follow it.
/// <para>
/// Arguments are kept raw. Which of them is a pointer and which is a flag number
/// depends entirely on the command, and decoding them here would mean knowing all of
/// them before knowing any of them.
/// </para>
/// </summary>
public sealed record ScriptCommand(int Offset, byte Code, byte[] Arguments)
{
    /// <summary>A two-byte argument, which is how ids and flag numbers are written.</summary>
    public int Word(int at = 0) =>
        at + 2 <= Arguments.Length ? Arguments[at] | (Arguments[at + 1] << 8) : 0;

    public uint Pointer(int at = 0) =>
        at + 4 <= Arguments.Length
            ? (uint)(Arguments[at] | (Arguments[at + 1] << 8) | (Arguments[at + 2] << 16) | (Arguments[at + 3] << 24))
            : 0;

    public override string ToString() =>
        $"0x{Offset:X6}  {ScriptCommands.NameOf(Code)} ({Arguments.Length} bytes)";
}

/// <summary>
/// The commands this project knows, and how long each one's arguments are.
/// <para>
/// The full set runs past two hundred. This is the handful a conversation needs, plus
/// the control flow that gets you between them — enough to read what somebody says and
/// no more. Everything else has a length so the reader can step over it, because a
/// command of unknown length is the end of reading: there is no way to find the next
/// one.
/// </para>
/// </summary>
public static class ScriptCommands
{
    public const byte Nop = 0x00;
    public const byte End = 0x02;
    public const byte Return = 0x03;
    public const byte Call = 0x04;
    public const byte Goto = 0x05;
    public const byte LoadPointer = 0x0F;
    public const byte CallStandard = 0x09;
    public const byte Lock = 0x6A;
    public const byte FacePlayer = 0x6B;
    public const byte Release = 0x6C;
    public const byte Message = 0x67;
    public const byte WaitButton = 0x66;
    public const byte TrainerBattle = 0x5C;

    /// <summary>
    /// How long a <c>trainerbattle</c> is, which depends on its first argument.
    /// <para>
    /// The only command here whose length is not fixed. Its first byte chooses a
    /// variant, and the variants differ in how many text pointers follow — a gym leader
    /// has one more than a route trainer, and the kind that cannot be fought right now
    /// has one more again.
    /// </para>
    /// <para>
    /// Every variant starts the same way: the type, the trainer id, and a flag number.
    /// So the id is readable whether or not this table has the right length for the
    /// rest, and a variant this does not know stops the read rather than guessing —
    /// which loses whatever came after the fight, and never invents it.
    /// </para>
    /// </summary>
    public static int? TrainerBattleLength(byte kind) => kind switch
    {
        0 or 5 or 9 => 13,      // type, id, flag, intro, defeat
        3 => 9,                 // no intro text
        1 or 2 or 4 or 7 => 17, // ... and one more script or text pointer
        6 or 8 => 21,           // ... and two
        _ => null,
    };

    /// <summary>
    /// Argument lengths, by command.
    /// <para>
    /// A command whose length is unknown ends the read. Guessing would resume at some
    /// byte in the middle of an argument, and from there every instruction after it is
    /// invented — which reads as a script rather than as an error.
    /// </para>
    /// </summary>
    private static readonly Dictionary<byte, int> ArgumentLengths = new()
    {
        [Nop] = 0,
        [0x01] = 0,
        [End] = 0,
        [Return] = 0,
        [Call] = 4,
        [Goto] = 4,
        [0x06] = 5,     // if, then goto
        [0x07] = 5,     // if, then call
        [0x08] = 1,     // gotostd
        [CallStandard] = 1,
        [0x0A] = 4,     // gotostdif
        [0x0B] = 4,     // callstdif
        [0x0C] = 4,     // jumpram
        [0x0D] = 0,     // killscript
        [0x0E] = 1,     // setbyte
        [LoadPointer] = 5,
        [0x10] = 5,     // setbyte2
        [0x11] = 6,     // writebytetooffset
        [0x12] = 5,     // loadbytefrompointer
        [0x13] = 5,     // setfarbyte
        [0x14] = 8,     // copyscriptbanks
        [0x15] = 8,     // copybyte
        [0x16] = 4,     // setvar
        [0x17] = 4,     // addvar
        [0x18] = 4,     // subvar
        [0x19] = 4,     // copyvar
        [0x1A] = 4,     // copyvarifnotzero
        [0x1B] = 4,     // comparebanks
        [0x1C] = 3,     // comparebanktobyte
        [0x1D] = 6,     // comparebanktofarbyte
        [0x1E] = 6,     // comparefarbytetobank
        [0x1F] = 5,     // comparefarbytetobyte
        [0x20] = 8,     // comparefarbytes
        [0x21] = 3,     // compare
        [0x22] = 4,     // comparevars
        [0x25] = 0,     // return-ish
        [0x26] = 1,
        [0x27] = 0,
        [0x28] = 0,
        [0x29] = 1,     // setflag
        [0x2A] = 1,     // clearflag
        [0x2B] = 1,     // checkflag
        [0x39] = 1,
        [0x3A] = 0,
        [0x53] = 2,     // givemoney-ish
        [0x54] = 2,
        [0x55] = 2,
        [0x5A] = 1,
        [WaitButton] = 0,
        [Message] = 4,
        [0x68] = 1,     // closeonkeypress-ish
        [0x69] = 1,
        [Lock] = 0,
        [FacePlayer] = 0,
        [Release] = 0,
        [0x6D] = 0,
        [0x6E] = 1,
        [0x6F] = 1,
        [0x70] = 1,
        [0x71] = 1,
        [0x72] = 1,
    };

    /// <summary>
    /// Length of a command's arguments, or null when the command is unknown.
    /// <para>
    /// <paramref name="firstArgument"/> only matters for <c>trainerbattle</c>, which is
    /// the one command in this set whose size it decides.
    /// </para>
    /// </summary>
    public static int? ArgumentLength(byte code, byte firstArgument = 0) =>
        code == TrainerBattle
            ? TrainerBattleLength(firstArgument)
            : ArgumentLengths.TryGetValue(code, out int length) ? length : null;

    public static string NameOf(byte code) => code switch
    {
        TrainerBattle => "trainerbattle",
        Nop => "nop",
        End => "end",
        Return => "return",
        Call => "call",
        Goto => "goto",
        LoadPointer => "loadpointer",
        CallStandard => "callstd",
        Lock => "lock",
        FacePlayer => "faceplayer",
        Release => "release",
        Message => "message",
        WaitButton => "waitbutton",
        _ => $"0x{code:X2}",
    };
}

/// <summary>
/// Reads a script off the cartridge.
/// <para>
/// Scripts are a bytecode: a command byte followed by however many argument bytes that
/// command takes. There is no length and no table of contents — you find the second
/// instruction by knowing how long the first one is, which is why an unknown command
/// has to stop the read rather than be skipped.
/// </para>
/// </summary>
public static class ScriptReader
{
    /// <summary>Instructions read before giving up, as a guard against a runaway.</summary>
    private const int MaxCommands = 512;

    public static List<ScriptCommand> Read(Rom rom, uint address, int maxCommands = MaxCommands)
    {
        var commands = new List<ScriptCommand>();

        if (rom.ToOffsetOrNull(address) is not { } offset) return commands;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset >= rom.Length) break;

            byte code = rom.ReadU8(offset);

            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) break;
            if (offset + 1 + length > rom.Length) break;

            byte[] arguments = rom.Slice(offset + 1, length).ToArray();

            commands.Add(new ScriptCommand(offset, code, arguments));

            offset += 1 + length;

            // These end a straight-line read. Following a goto is the caller's job,
            // because doing it here would mean deciding what to do about loops.
            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto) break;
        }

        return commands;
    }

    /// <summary>
    /// Which trainer a script picks a fight with, or nothing when it does not.
    /// <para>
    /// This is the only way to find out. The object standing on the map says <em>that</em>
    /// somebody is a trainer — one field, set or not — and never says which one. The id
    /// is an argument to the <c>trainerbattle</c> command inside their script, which is
    /// why reading scripts had to come first.
    /// </para>
    /// </summary>
    public static int? FindTrainer(Rom rom, uint address)
    {
        foreach (ScriptCommand command in Read(rom, address))
        {
            if (command.Code == ScriptCommands.TrainerBattle) return command.Word(1);
        }

        return null;
    }

    /// <summary>
    /// Everything a script would say, in order.
    /// <para>
    /// The games do not have a "say this" instruction in the way you would expect.
    /// Dialogue is a pair: load a pointer into a slot, then call one of a handful of
    /// standard routines that displays whatever is in it. So the text is found by
    /// watching what gets loaded, not by looking for a message command — though the
    /// one that does exist is read too.
    /// </para>
    /// </summary>
    public static List<string> ReadDialogue(Rom rom, uint address, int maxPages = 32)
    {
        var pages = new List<string>();

        foreach (ScriptCommand command in Read(rom, address))
        {
            uint text = command.Code switch
            {
                ScriptCommands.LoadPointer => command.Pointer(1),
                ScriptCommands.Message => command.Pointer(),
                _ => 0,
            };

            if (text == 0) continue;
            if (rom.ToOffsetOrNull(text) is not { } at) continue;

            ReadOnlySpan<byte> bytes = rom.Span[at..];

            if (!GameText.LooksLikeDialogue(bytes)) continue;

            foreach (string page in GameText.DecodeDialogue(bytes))
            {
                if (pages.Count >= maxPages) return pages;
                pages.Add(page);
            }
        }

        return pages;
    }
}
