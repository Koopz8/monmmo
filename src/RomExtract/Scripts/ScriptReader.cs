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
/// <para>
/// <b>These lengths were written from memory of the Ruby and Emerald command set, and a
/// real FireRed image says they are not good enough.</b> Of 1584 people with a script,
/// 468 stop at a command this table does not have, and not one of the 1584 was found to
/// open a shop — in a game with a shop in every town. A count of clean reads is not
/// evidence either: a wrong length resumes inside an argument, and a stray 0x02 in the
/// middle of a pointer reads as a perfectly good <c>end</c>.
/// </para>
/// <para>
/// So the lengths that follow are believed, not known, and the ones that are missing are
/// the reason two features look broken. The fix is to read the bytes rather than to add
/// more guesses, which is what <c>--script-map</c> exists for.
/// </para>
/// </summary>
public static class ScriptCommands
{
    public const byte Nop = 0x00;
    public const byte End = 0x02;
    public const byte Return = 0x03;
    public const byte Call = 0x04;
    public const byte Goto = 0x05;

    /// <summary>Conditional jumps: a condition byte, then where to go.</summary>
    public const byte GotoIf = 0x06;

    public const byte CallIf = 0x07;
    public const byte LoadPointer = 0x0F;
    public const byte CallStandard = 0x09;
    public const byte Lock = 0x6A;
    public const byte FacePlayer = 0x6B;
    public const byte Release = 0x6C;
    public const byte Message = 0x67;
    public const byte WaitButton = 0x66;
    public const byte TrainerBattle = 0x5C;

    /// <summary>Opens a shop. The argument is a pointer to a list of what it sells.</summary>
    public const byte PokeMart = 0x86;

    /// <summary>
    /// How long a <c>trainerbattle</c> is, which depends on its first argument.
    /// <para>
    /// The only command here whose length is not fixed. Its first byte chooses a
    /// variant, and the variants differ in how many text pointers follow — a gym leader
    /// has one more than a route trainer, and the kind that cannot be fought right now
    /// has one more again.
    /// </para>
    /// <para>
    /// Every variant starts the same way: the type, the trainer id, and one more word.
    /// So the id is readable whether or not this table has the right length for the
    /// rest, and a variant this does not know stops the read rather than guessing —
    /// which loses whatever came after the fight, and never invents it.
    /// </para>
    /// <para>
    /// That third word was called a flag here and is not one. On a real image it is zero
    /// for all fifteen people on Route 8, and the flag that would have been read out of
    /// it was zero for every trainer in the game — which is a number that means "flag
    /// zero", not "no flag". Whatever the games remember a beaten trainer by, it is not
    /// written in the script.
    /// </para>
    /// </summary>
    public static int? TrainerBattleLength(byte kind) => kind switch
    {
        0 or 5 or 9 => 13,      // type, id, that word, intro, defeat
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
        // Four: a variable and a value, both two bytes. `21 60 40 01 00` is
        // compare(0x4060, 1), and the `06 04 ...` that follows it is a well-formed
        // conditional goto with a real pointer in it.
        [0x21] = 4,     // compare
        [0x22] = 4,     // comparevars
        // Two bytes: a routine number. `25 A5 00 21 73 40 00 00 06 01 ...` is
        // special(0x00A5), compare(0x4073, 0), if-goto — and the pointer that conditional
        // carries lands on a script. Reading it as no argument at all is what left 0xA5,
        // 0x73 and 0x74 looking like commands; they are routine numbers.
        [0x25] = 2,     // special

        // Four: a variable to put the answer in, and the routine to ask. This one alone
        // was 245 of the remaining stops — `26 0D 80 93 01` is specialvar(0x800D, 0x0193),
        // and the 0x80 that looked like a command every time is the top half of 0x800D.
        [0x26] = 4,     // specialvar
        [0x27] = 0,
        [0x28] = 0,
        // Two bytes, not one. Proved by the bytes: `29 A5 02 53 04 00 1A 00 80 ...`
        // reads as setflag(0x02A5) and then keeps parsing cleanly for another twenty
        // commands. Taking one byte makes the 0x02 an `end`, which is worse than a
        // failure — the script reports a clean read and quietly contains nothing.
        [0x29] = 2,     // setflag
        [0x2A] = 2,     // clearflag
        [0x2B] = 2,     // checkflag
        [0x39] = 1,
        [0x3A] = 0,
        [0x53] = 2,     // givemoney-ish
        [0x54] = 2,
        [0x55] = 2,
        // Nothing at all, and this one byte was the whole problem.
        //
        // Almost every person in FireRed opens with `6A 5A` — lock, then this. Taking an
        // argument here swallowed the next command byte, and from that point on the read
        // was one byte out of step forever. What it then hit was whatever happened to sit
        // in the middle of a pointer or a variable id: 0x80 from var 0x800D (258 scripts),
        // 0x78 from the pointer 0x081A6578, 0x60 from var 0x4060, 0x40 from var 0x4001.
        // Every one of the twenty commonest "unknown commands" on a real cartridge was
        // this, and none of them was a command.
        //
        // The proof is what follows it: `6A 5A 04 78 65 1A 08 6C 02` reads as lock,
        // this, call 0x081A6578, release, end — a textbook script, with a pointer that
        // lands exactly on a script. Reading it any other way does not.
        [0x5A] = 0,
        [WaitButton] = 0,
        [Message] = 4,
        // Both take nothing. `69 2B 25 08 06 00 91 E0 1B 08` reads as this, then
        // checkflag(0x0825), then a conditional goto whose pointer lands on a script —
        // and the 0x91, 0x23 and 0xDF that looked like commands are all the low byte of
        // whichever flag was being checked. 200 scripts, one byte, again.
        [0x68] = 0,     // close the message box
        [0x69] = 0,     // lock everybody
        [Lock] = 0,
        [FacePlayer] = 0,
        [Release] = 0,
        // Three: which slot to write into, and a two-byte id. `84 00 10 00` is followed
        // by `05 6C 78 1A 08` — a goto with a pointer that lands on a script, which only
        // works if this command is exactly three bytes wide.
        [0x84] = 3,

        [0x6D] = 0,
        [0x6E] = 1,
        [0x6F] = 1,
        [0x70] = 1,
        [0x71] = 1,
        [0x72] = 1,
        [PokeMart] = 4,
        [0x87] = 4,     // the decoration shop
        [0xC7] = 1,
        [0xCF] = 0,
        [0x88] = 4,     // and the other decoration shop
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
        PokeMart => "pokemart",
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
    /// The command that ended a read, or nothing when the script ended properly.
    /// <para>
    /// The same job <c>Explain</c> does for the located tables. A script that stops at an
    /// unknown command is not an error and looks like nothing at all — it just quietly
    /// contains less than it does, and whatever was past that point is invisible. Every
    /// shop in FireRed went missing this way, and the only way to find out which command
    /// was in the way is to count them.
    /// </para>
    /// </summary>
    public static byte? StoppedAt(Rom rom, uint address, int maxCommands = MaxCommands)
    {
        if (rom.ToOffsetOrNull(address) is not { } offset) return null;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset >= rom.Length) return null;

            byte code = rom.ReadU8(offset);
            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return code;

            offset += 1 + length;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto) return null;
        }

        return null;
    }

    /// <summary>
    /// Everything a script runs, following the ones it hands off to.
    /// <para>
    /// Most people in FireRed do their work somewhere else. A shopkeeper's own script is
    /// often four instructions long — lock, face the player, <c>call</c>, release — and
    /// everything that makes them a shopkeeper is at the other end of that call. A reader
    /// that stops at the handoff sees a person who does nothing, which is exactly what
    /// this project saw: a cartridge with a shop in every town and not one shop found.
    /// </para>
    /// <para>
    /// Branches are followed but not evaluated. Both arms of a conditional are read,
    /// because deciding which one runs needs the flags of a save this has never seen, and
    /// reading both is the difference between knowing what somebody might say and knowing
    /// nothing. What comes back is therefore everything reachable, not a transcript.
    /// </para>
    /// </summary>
    public static List<ScriptCommand> ReadAll(Rom rom, uint address, int maxScripts = 16)
    {
        var all = new List<ScriptCommand>();
        var seen = new HashSet<uint>();
        var queue = new Queue<uint>();

        queue.Enqueue(address);
        seen.Add(address);

        while (queue.Count > 0 && seen.Count <= maxScripts)
        {
            foreach (ScriptCommand command in Read(rom, queue.Dequeue()))
            {
                all.Add(command);

                uint target = command.Code switch
                {
                    ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),

                    // The conditional forms put a one-byte condition first and the
                    // destination after it.
                    ScriptCommands.CallIf or ScriptCommands.GotoIf => command.Pointer(1),

                    _ => 0,
                };

                if (target == 0 || !rom.IsRomAddress(target)) continue;
                if (!seen.Add(target)) continue;

                queue.Enqueue(target);
            }
        }

        return all;
    }

    /// <summary>Where in the image a read stopped, for printing the bytes around it.</summary>
    public static int? StoppedAtOffset(Rom rom, uint address, int maxCommands = MaxCommands)
    {
        if (rom.ToOffsetOrNull(address) is not { } offset) return null;

        for (int i = 0; i < maxCommands; i++)
        {
            if (offset >= rom.Length) return null;

            byte code = rom.ReadU8(offset);
            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return offset;

            offset += 1 + length;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto) return null;
        }

        return null;
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
        foreach (ScriptCommand command in ReadAll(rom, address))
        {
            if (command.Code == ScriptCommands.TrainerBattle) return command.Word(1);
        }

        return null;
    }

    /// <summary>
    /// What a shopkeeper sells, or nothing when this script does not open a shop.
    /// <para>
    /// The list is a run of two-byte item ids ending in a zero — no count, like almost
    /// everything else on this cartridge. A shop selling nothing and a pointer that is
    /// not a shop list look identical from one entry in, so a list whose first entry is
    /// already the terminator is treated as neither.
    /// </para>
    /// </summary>
    public static List<int> FindMart(Rom rom, uint address, int maxItems = 64)
    {
        foreach (ScriptCommand command in ReadAll(rom, address))
        {
            if (command.Code != ScriptCommands.PokeMart) continue;
            if (rom.ToOffsetOrNull(command.Pointer()) is not { } list) continue;

            var stock = new List<int>();

            for (int i = 0; i < maxItems; i++)
            {
                int at = list + i * 2;
                if (at + 2 > rom.Length) break;

                int itemId = rom.ReadU16(at);
                if (itemId == 0) break;

                stock.Add(itemId);
            }

            if (stock.Count > 0) return stock;
        }

        return [];
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

        foreach (ScriptCommand command in ReadAll(rom, address))
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
