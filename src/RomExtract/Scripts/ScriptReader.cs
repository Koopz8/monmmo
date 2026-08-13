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
        // Two, a word, and it was zero here since the beginning with nothing to say
        // for itself. The column is unanswerable — the byte above is different at every
        // site and the one below it is 00 at all of them:
        //
        //   28 | 0F 00 | 05 E7 BB 1B 08     goto 0x081BBBE7
        //   28 | 2D 00 | 21 01 40 00 00     compare 0x4001, 0
        //   28 | 12 00 | 4F 03 00 ED 75 1A 08   applymovement
        //   28 | 14 00 | 25 87 01           special 0x0187
        //
        // Read as nothing, that first site makes a loadpointer whose pointer is
        // 0x1BBBE705, which is not an address. Read as a word, every site lands on a
        // known command carrying sensible arguments.
        //
        // This is the one that was stopping the story. Pallet Town's north exit reads
        // `C7 00 | 28 1E 00 | 33 2E 01 00 | 67 2C D7 17 08` — and that last command is
        // the message the professor stops you with. One byte out and the read walked
        // into the middle of it, so the script ran, set its variables, and said nothing.
        [0x28] = 2,
        // Two bytes, not one. Proved by the bytes: `29 A5 02 53 04 00 1A 00 80 ...`
        // reads as setflag(0x02A5) and then keeps parsing cleanly for another twenty
        // commands. Taking one byte makes the 0x02 an `end`, which is worse than a
        // failure — the script reports a clean read and quietly contains nothing.
        [0x29] = 2,     // setflag
        [0x2A] = 2,     // clearflag
        [0x2B] = 2,     // checkflag
        // Two bytes, on two sites that share nothing and both resume on something
        // unmistakable:
        //
        //   34 00 00 | 35 | 0F 00 94 DC 18 08 | 09 04 | 68     4.3   arriving
        //   34 10 01 | 29 01 40 | 29 A2 00 | 28 14 00          12.5  person 7
        //
        // The first resumes on a loadpointer, a callstd and a waitbutton — which is the
        // shape of every text box in this game — and the second on two setflags. At one
        // byte or three, neither does.
        [0x34] = 2,
        // Nothing, and on one site only, which is thin and is the whole argument: the
        // byte after it is 0x0F, and what follows that is a text pointer into the region
        // every other message in this game points into, a callstd 04 and a waitbutton.
        // Any argument at all here eats the 0x0F and the box with it.
        [0x35] = 0,
        // Three bytes, on four sites that are byte-for-byte identical:
        //
        //   5B FF 00 02 | 02 | 55 40 01 00 3E 92 16 08 ...
        //
        // The argument is what settles it. At three the `02` is an `end` and the script
        // is over; at four the read carries on into `55 40 01 00 <pointer>`, which is not
        // code at all — it is the next record in the map's own script list, a variable, a
        // value and a script, whose shape was derived from the other end entirely. A
        // width that reads one record as another is wrong however cleanly it parses.
        [0x5B] = 3,
        // Fourteen. Four sites, no two of them on the same map, and at fourteen every
        // one resumes on a real command where thirteen and fifteen resume on none:
        //
        //   79 | 02 40 05 00 00 ... | 19 31 40 01 40      4.3   person 5
        //   79 | 01 40 19 00 00 ... | 21 0D 80 00 00      14.2  person 6
        //   79 | 83 00 19 00 00 ... | 21 0D 80 00 00      1.53  person 2
        //   79 | 85 00 19 00 00 ... | 21 0D 80 00 00      10.11 person 2
        //
        // It was adopted as fifteen first, and the reason is worth keeping: the four
        // addresses were taken from a report that names where a command's *arguments*
        // begin, and then used as though they named the command. Every width came out
        // one too many, and fifteen passed the same test fourteen passes because the
        // test was being run one byte to the left. What caught it was not the test — it
        // was the professor's ball reading `givemon` and then `0x31`, which is not a
        // command, in the middle of the one script this project has read most.
        //
        // And the arguments say what it is without being asked. The first word is a
        // species or a variable holding one, the second is a level: 131 at level 25 and
        // 133 at level 25 are LAPRAS and EEVEE, at the levels this game gives them, on
        // Silph's top floor and in Celadon. The starter is the one whose species comes
        // out of a variable, at level 5.
        [0x79] = 14,     // gives a monster
        // Three, and the argument says what it is. Thirteen sites, and at every one the
        // last two bytes are a species or a variable holding one — the same numbers the
        // command above it hands over:
        //
        //   7D 00 | 02 40 | 67 0D E3 18 08     4.3   the starter, from 0x4002
        //   7D 00 | 83 00 | 0F 00 A7 56 1A 08  1.53  LAPRAS
        //   7D 00 | 85 00 | 0F 00 A7 56 1A 08  10.11 EEVEE
        //   7D 00 | 09 80 | 0F 00 18 5B 1A 08  1.30
        //
        // Eight of the thirteen resume on a loadpointer, which is the first half of every
        // text box in this game, and it sits immediately after the handover at every gift
        // site. Whatever it is called, it is the game about to say which one you got.
        [0x7D] = 3,
        [0x39] = 1,
        [0x3A] = 0,
        // Eight bytes — four words — and the cleanest column this project has seen. The
        // command sits in a packed run of itself, so the evidence is that the byte eight
        // along is another one, at every one of eighty sites:
        //
        //   A2 | 05 00 08 00 35 03 00 00 | A2 | 06 00 08 00 ...
        //   A2 | 09 00 0B 00 47 03 01 00 | A2 | 0A 00 0B 00 ...
        //
        // A hundred and thirty-nine scripts stopped here, which was the largest single
        // stop on the cartridge once the fifth list was being read at all.
        [0xA2] = 8,
        // Two, and it is not about money. Every one of its 224 sites holds either a
        // number between 1 and 10 or a variable, and nothing else — which is an object
        // number. The rival walks out of the professor's lab through `53 08 00`, and
        // person 8 on that map is the rival.
        [0x53] = 2,     // takes an object off the map
        [0x54] = 2,
        [0x55] = 2,
        // Six bytes — three words — on ten sites across four maps, and the argument is
        // legible as well as the length:
        //
        //   08 00 17 00 00 00 | 05 CC 64 16 08 02      3.3   person 8 is at (22, 0)
        //   01 00 19 00 05 00 | 05 BE 82 16 08 02      3.41  person 1 is at (25, 4)
        //   08 00 05 00 0A 00 | 55 08 00               4.3   person 8 exists
        //   02 00 05 00 00 00 | 55 02 00               1.120 person 2 exists
        //
        // Every site resumes on a goto or a 0x55, which five bytes or seven do not. And
        // the first word names a person who is really on that map, every time, with the
        // two words after it landing on or beside where the cartridge put them — near
        // enough to be about that person, not near enough to be their placement, which
        // is what a command that moves somebody would look like. Not named here: what it
        // does is a guess, what it takes is not.
        [0x63] = 6,
        // Three bytes, on two sites that disagree about what four would mean:
        //
        //   65 02 00 08 | 65 03 00 08 | 03            1.97 trigger (37, 43)
        //   65 04 00 08 | 2A 2B 00 | 4F FF 00 ...     4.3  arriving with 0x4055 at 1
        //
        // At three, the first pair reads as two of the same command in a row and then a
        // return, and the second reads as a clearflag followed by an applymovement. At
        // four, the second swallows the clearflag's opcode and the read is one byte out
        // from there on. The first word is a person id again — 2, 3 and 4, all of whom
        // are on their map — which is the same shape as the command above it.
        [0x65] = 3,
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

        // Two, on one site and the site says so twice. MISTY's badge script:
        //
        //   29 B1 04     setflag 0x04B1
        //   29 21 08     setflag 0x0821
        //   9F 04 00     <- this
        //   16 08 80 02 00   setvar 0x8008, 2
        //   04 18 6B 1A 08   call the routine every gym shares
        //
        // At two, what follows is a setvar putting *two* into the variable the shared
        // badge routine compares — and CERULEAN is the second gym. At any other width
        // that setvar is eaten and the badge number is nonsense. BROCK's script has an
        // ordinary setvar in the same slot, which is the same shape from the other side.
        [0x9F] = 2,

        // Two: one word, and small numbers in it. Six sites, and three of them are read
        // by what comes next rather than by what comes before:
        //
        //   64 | 01 00 | 29 02 00 ...      setflag
        //   64 | 01 00 | 4F FF 00 ...      applymovement
        //   64 | 02 00 | 0F 00 8E 45 ...   loadpointer
        //
        // The other three run into commands that are themselves known, which is the same
        // agreement from the other direction. This is what stopped both fossils in MT.
        // MOON: the two of them are one script each, and each of those stopped four
        // commands after handing the fossil over — so the words the cartridge has for
        // taking one were never read.
        [0x64] = 2,
        [0x88] = 4,     // and the other decoration shop

        // Six: a word and then a pointer. Derived twice and the two agree.
        //
        // By eye, at 0x08164D84: `4F 01 00 E5 75 1A 08 51 00 00 6C 02` reads as this
        // command taking (1, 0x081A75E5), then 0x51 taking a word, then release, end.
        // One clean parse, with a cartridge address sitting in plain sight.
        //
        // By counting, across all twenty places it stops a run: six is the only width
        // where a real pointer ends exactly on the argument boundary.
        //
        // Worth knowing how nearly this went the other way. Every test based on what
        // follows the command preferred four, because at four the read skips over 0x51 —
        // which is also unknown — and lands on a copyvarifnotzero that parses beautifully
        // and is not there. The correct width scored *worst* on continuation, because
        // the correct width stops at the next thing this project cannot read.
        [0x4F] = 6,

        // Two: a word. Read from three sites, and all three agree — which is what the
        // scorer could not do here, because no width of this one ends on a pointer and
        // the continuation test is the one that prefers skipping unknowns.
        //
        //   4F 01 00 E5 75 1A 08 | 51 00 00 | 6C 02        release, end
        //   4F 01 00 E1 75 1A 08 | 51 00 00 | 0F 00 C8 ... loadpointer
        //   4F 03 00 E5 75 1A 08 | 51 00 00 | 6C 02        release, end
        //
        // The pair is the evidence. 0x4F takes a word and a pointer, 0x51 follows it
        // immediately with a word of its own, and what comes after that is a real
        // command every time. Deriving either alone was impossible; they only make
        // sense together, which is exactly the entanglement that made the scoring tie.
        [0x51] = 2,

        // Five, and this overturns a decision milestone 14 made deliberately.
        //
        // That round parked 0x30 because two readings of its bytes both reached the
        // same next command, and refused to guess between them. Right call on the
        // evidence it had, which was one script. Twenty-five sites side by side say
        // something one site cannot:
        //
        //   A1 28 00 00 00 | 0F 00 61 3B 17 08     loadpointer
        //   A1 43 00 00 00 | 0F 00 44 44 17 08     loadpointer
        //   A1 96 00 02 00 | 67 9F 7F 17 08        message
        //   ... twenty more, every one the same shape
        //
        // The fifth byte is zero at every site without exception. Read as four, that
        // byte is a separate nop instruction — and it would be a nop sitting in front
        // of twenty-two of twenty-five loadpointers, which is not something anything
        // emits. A column that never changes is an argument.
        //
        // Read as five it is a byte and two words, and the words hold small sensible
        // numbers. The first byte is usually 0xA1 and is not always, so it is an
        // argument too rather than a second opcode.
        [0x30] = 5,

        // Nothing at all, and it only became visible once 0x30 was five bytes wide —
        // the same twenty-four scripts moved from stopping at one to stopping at the
        // other, which is the loop doing its job. Every site is followed immediately by
        // a real command: release-end at most of them, loadpointer at several, a call at
        // one. Taking an argument here would swallow the first byte of all of those.
        [0xC5] = 0,

        // Two. Three sites, and the second argument byte is 0x01 at all three:
        //
        //   31 | 00 01 | 32 ...
        //   31 | 3E 01 | 67 82 BD 17 08     message 0x0817BD82
        //   31 | 01 01 | 67 F6 51 1A 08     message 0x081A51F6
        //
        // Two of the three continue straight into a message carrying a real cartridge
        // address, which is the sharpest test this project has. The third runs into
        // 0x32, still unknown, which is where the next round starts.
        [0x31] = 2,

        // Nothing, exposed by fixing 0x31 — the same three scripts moved from one to
        // the other. Two of the three continue straight into a command carrying a real
        // address:
        //
        //   32 | 04 75 66 1A 08 ...     call 0x081A6675
        //   32 | 66 84 02 18 00 0F 00 18 52 1A 08 09
        //                                waitbutton, 0x84, loadpointer 0x081A5218, callstd
        //
        // The third runs into 0x63, which is the next unknown along and the next round.
        [0x32] = 0,

        // Four: an item and a count. Eight of nine sites read the same shape, and the
        // shape says what the command is:
        //
        //   46 | 55 01  01 00 | 21 0D 80 00 00 | 06 01 ...
        //   46 | 0D 00  01 00 | 21 0D 80 00 00 | 06 01 ...
        //   46 | 6E 00  01 00 | 21 0D 80 00 00 | 06 01 ...
        //
        // An item id, then one of them, then compare 0x800D against zero and branch —
        // which is a command handing something over and the script asking whether it
        // fitted. This is the one that stopped Route 1 one line after "I know, I'll
        // give you a sample. Here you go!", and the sample is item 0x0155.
        [0x46] = 4,     // giveitem

        // Nothing. Seven sites out of seven, every one followed immediately by
        // `21 0D 80 00 00` — compare 0x800D against zero — and then a branch. A command
        // that answers into the result variable and is asked about on the next line
        // takes no arguments; anything swallowed here would eat the compare.
        [0xA0] = 0,

        // Three: a gap and a number to put in it. This was read as seven for two rounds
        // and the reason it survived one of them is worth keeping, because the way it
        // fell is the whole method.
        //
        // Seven fitted five sites exactly — `00 0A 00 80 01 57 01` and then `return`,
        // three times over with only the middle bytes changing — and three was tried
        // and rejected, because at three those same five scripts derailed on a 0x80 and
        // had to be rescued by returning from a call. A width that costs five reads and
        // buys nothing is not the width, and that was the right call on what was known.
        //
        // What was missing is that 0x80 is also three, and the five derails were one
        // unknown standing behind another. Read with both:
        //
        //   83 00 0A 00 | 80 01 57 01 | 03      gap 0 <- 10,  gap 1 <- item 0x157
        //   83 00 14 00 | 80 01 C3 00 | 03      gap 0 <- 20,  gap 1 <- item 0x0C3
        //   83 00 1E 00 | 80 01 05 01 | 03      gap 0 <- 30,  gap 1 <- item 0x105
        //   83 00 28 00 | 80 01 BD 00 | 03      gap 0 <- 40,  gap 1 <- item 0x0BD
        //   83 00 32 00 | 80 01 B6 00 | 03      gap 0 <- 50,  gap 1 <- item 0x0B6
        //
        // Ten, twenty, thirty, forty, fifty, on five different maps, each paired with a
        // different item — and each of those five scripts hands over that exact item a
        // few commands later. The line they run in front of is the professor's aides:
        // "If your POKeDEX has complete data on {FD}{02} species... PROF. OAK entrusted
        // me with the {FD}{03} for you."
        //
        // The professor's own rating says the same thing about a variable rather than a
        // number: `83 00 08 80 83 01 09 80` in front of "{FD}{02} POKeMON seen and
        // {FD}{03} POKeMON owned", where the two commands before it copy the seen and
        // owned counts into 0x8008 and 0x8009.
        //
        // So 0x83 fills a gap with a number, from a literal or from a variable, and the
        // gap it names is off by two exactly as 0x7D's is.
        [0x83] = 3,

        // Three, and the same shape: a gap and an item to name into it. Every site is
        // the aide pair above, and what identifies it is not the shape but the argument
        // — the item named is the item that same script gives, at all five.
        [0x80] = 3,

        // Four: two variables. One site only, and it needs no more than one:
        //
        //   42 | 04 80 05 80 | 21 04 80 09 00 | 06 04 ...
        //
        // Those are var 0x8004 and var 0x8005, and the very next command compares
        // 0x8004 against nine. A command taking two variables and then being asked
        // about one of them is not a coincidence that four bytes could produce twice.
        [0x42] = 4,

        // Five. All five sites, and only readable because 0x30 is known now — every one
        // of them is followed by a 0x30 and then a message:
        //
        //   B6 | 90 00 32 00 00 | 30 A1 90 00 02 00 | 67 ...
        //   B6 | 91 00 32 00 00 | 30 A1 91 00 02 00 | 67 ...
        //   B6 | 65 00 22 00 00 | 30 A1 65 00 02 00 | 28 ...
        //
        // Word, word, byte, with the first word repeated inside the 0x30 that follows
        // it. Two commands sharing a number is a pair working together, and the shape
        // only appeared once the second of them could be read.
        [0xB6] = 5,

        // Two, and every one of the four sites is followed by a conditional branch:
        //
        //   60 | AA 00 | 06 01 11 07 16 08     gotoif -> 0x08160711
        //   60 | 19 02 | 06 01 9E 39 16 08     gotoif -> 0x0816399E
        //
        // A command asked about on the very next line takes its argument and no more.
        [0x60] = 2,

        // Three. Bytes zero, one and two are 00 at all four sites, and three of the four
        // then continue into something known — a message, an 0xA0, a specialvar. The
        // fourth runs into 0xC0, which is where this goes next.
        [0x93] = 3,

        // Three. Bytes one and two are `01 00` at all four sites, and all four continue
        // into something known — waitstate twice, a compare, and a 0x4F.
        [0x33] = 3,

        // Two. Three sites, and every one continues into the same pair:
        //
        //   8F | 03 00 | 19 08 80 0D 80 | 21 08 80 00 00
        //   8F | 04 00 | 19 08 80 0D 80 | 21 08 80 00 00
        //
        // copyvar 0x8008 <- 0x800D, then compare 0x8008 against zero. Reading a result
        // out and testing it, which is what follows a command that produces one.
        [0x8F] = 2,

        // Four, and the same pairing 0xB6 has. Bytes two and three are `0A 03` at all
        // three sites, and the word in front of them turns up again immediately:
        //
        //   75 | 6A 00 0A 03 | 16 01 40 6A 00 | 4F ...   setvar 0x4001 <- 0x006A
        //   75 | 6B 00 0A 03 | 16 01 40 6B 00 | 4F ...   setvar 0x4001 <- 0x006B
        //
        // Two commands sharing a number is a pair, and a pair is not a coincidence.
        [0x75] = 4,

        // Two. Three sites and the twelve bytes after each of them are identical:
        // `15 00 4F 01 00 DB 75 1A 08 51 00 00`. Read as two it is an argument followed
        // by the 0x4F and 0x51 pair derived earlier in this run.
        [0x2F] = 2,

        // Four, and the same shape as giveitem: an id, a count, then the result
        // variable tested on the very next line.
        //
        //   47 | 1A 00 01 00 | 21 0D 80 01 00 | 07 01 ...
        //   47 | 50 00 01 00 | 21 0D 80 01 00 | 06 01 ...
        //   47 | 68 00 01 00 | 21 0D 80 01 00 | 07 01 ...
        //
        // Compared against one rather than zero, so it asks a different question than
        // 0x46 does — which is as far as the bytes go, and further than a name would
        // have been earned.
        [0x47] = 4,

        // Nothing. All three sites are followed immediately by `6C 02` — release, end —
        // or by a loadpointer. A command that sits at the tail of a script and takes no
        // arguments; anything swallowed here eats the release.
        [0x76] = 0,

        // Four, two words, and the one that was stopping the opening of the game. The
        // professor's scene runs, walks you to his lab, and then hits this.
        //
        //   AC | 10 00 0D 00 | AE | 4F 03 00 2E 57 16 08
        //   AC | 0A 00 03 00 | AE | 4F 02 00 DB 06 17 08
        //
        // Two sites, on two maps, with the same shape: two small words, then a command
        // taking nothing, then an applymovement carrying a pointer that lands on a real
        // movement list. Read any narrower and the first site puts a killscript in the
        // middle of a cutscene with the rest of the scene unreachable behind it.
        //
        // Two sites is thin by this project's own standard and it is worth saying so.
        // What makes it worth adopting anyway is that both the width *and* the command
        // after it are confirmed by the same bytes, and the wrongness detector — scripts
        // that finish saying nothing — did not move when they were adopted.
        [0xAC] = 4,

        // Four, and 0xAC's twin — same two words, same 0xAE after it, at the same two
        // sites on the same two maps:
        //
        //   AD | 10 00 0D 00 | AE | 16 55 40 01 00   setvar 0x4055, 1
        //   AD | 0A 00 03 00 | AE | 29 02 00 6C 02   setflag 0x0002, release, end
        //
        // Read this way both sites land on ordinary bookkeeping — a variable written, a
        // flag set, a release, an end — which is exactly what the tail of a cutscene
        // looks like. The pair being identical in shape is worth more than either alone:
        // whatever 0xAC and 0xAD are, they are two of the same kind of thing.
        [0xAD] = 4,

        // Nothing, and it is the other half of that pair. At every site it sits between
        // one of the two and something whose first bytes are plainly a command — an
        // applymovement with a good pointer, a setvar, a setflag — so anything it
        // swallowed would eat the front of that.
        [0xAE] = 0,

        // Two, a word, and the largest single unknown this project had: it stopped two
        // hundred reads. Only three sites, and they sit within three hundred bytes of
        // each other, so they are much closer to one piece of evidence than to three —
        // but the three of them carry the whole answer:
        //
        //   7C | 0F 00 | 21 0D 80 06 00 | 06 01 87 DF 1B 08
        //   7C | 46 00 | 21 0D 80 06 00 | 06 01 85 E1 1B 08
        //   7C | F9 00 | 21 0D 80 06 00 | 06 01 91 E0 1B 08
        //        ^^^^^   compare 0x800D, 6   goto if equal
        //
        // Read as one, the second byte is a nop standing between this and a compare, at
        // every site — the same thing that settled loadpointer, and nothing emits it.
        // Read as two, it answers into the result variable and the script immediately
        // asks whether the answer is six.
        //
        // Six is the tell. A party has six slots, so an answer of six is "none of them"
        // — this hands back a slot number. And the three arguments, looked up in the
        // move table read off this same image rather than recalled, are CUT (15),
        // STRENGTH (70) and ROCK SMASH (249). Ninety-seven people share the rock-smash
        // script, fifty-four the strength one, forty-nine the cut one, across forty-seven
        // maps: the trees, the boulders and the rubble, asking who in the party can
        // shift them.
        [0x7C] = 2,

        // Three, and it is the command 0x7C was hiding. Same three sites, same two
        // hundred people, and the first three bytes are the same at all of them:
        //
        //   9D | 00 0D 80 | 7F 00 0D 80 82 01 F9 00 0F ...
        //   9D | 00 0D 80 | 0F 00 9A E1 1B 08 09 05 21 ...    loadpointer 0x081BE19A
        //   9D | 00 0D 80 | 7F 00 0D 80 82 01 0F 00 0F ...
        //
        // A byte and then 0x800D — the slot 0x7C has just answered with, handed
        // straight on to this. Three is the only width that leaves every site pointing
        // at something: the middle one lands exactly on a loadpointer carrying a good
        // script pointer, and every shorter width makes the read walk into a killscript
        // (0x0D) that is plainly the low half of 0x800D.
        //
        // Not named. What it does with the slot is not written down here.
        [0x9D] = 3,

        // Three, and the same shape as the one before it — a byte and 0x800D again,
        // the slot handed along a second time:
        //
        //   7F | 00 0D 80 | 82 01 F9 00 | 0F 00 9D E0 1B 08
        //   7F | 00 0D 80 | 82 01 0F 00 | 0F 00 94 DF 1B 08
        //
        [0x7F] = 3,

        // Three: a constant byte and then a move id — and it is the *same* move id the
        // script asked about six commands earlier. The cut script says CUT twice, the
        // rock-smash script says ROCK SMASH twice, and both land exactly on a
        // loadpointer carrying a good script pointer.
        //
        //   82 | 01 0F 00 | 0F 00 94 DF 1B 08     CUT
        //   82 | 01 F9 00 | 0F 00 9D E0 1B 08     ROCK SMASH
        //
        [0x82] = 3,

        // Two. Three sites, three different words, and the byte above them never
        // changes:
        //
        //   9C | 02 00 | 27 05 76 DF 1B 08 02     cut
        //   9C | 25 00 | 27 05 6F E0 1B 08 02     rock smash
        //   9C | 28 00 | 27 05 79 E1 1B 08 02     strength
        //
        // Each appears twice inside its own script, byte for byte, once after a
        // closemessage and once after a lockall.
        //
        // The confirmation is the jumps rather than the column: read this way, every
        // goto target in all three scripts lands exactly on a command boundary — the
        // 0x081BDF76 that 0x081BDF65 jumps to is a 0x4F, the 0x081BDF87 the top of the
        // script jumps to is a loadpointer. A width that is wrong by one desynchronises
        // the stream, and a desynchronised stream's own jumps land mid-argument.
        [0x9C] = 2,

        // Nothing, and the three sites make a chain of known commands the moment it is
        // read that way:
        //
        //   B7 | 2A 07 08 | 25 88 01 | 26 0D 80 B4 00 | 21 ...
        //        clearflag  special    specialvar        compare
        //
        // Four commands in a row, each one parsing into the next. That does not happen
        // by accident at any other width.
        [0xB7] = 0,

        // Nothing, on two of three sites:
        //
        //   35 | 53 02 00 | 2A 50 00 | 6C 02      clearflag, release, end
        //   35 | 53 05 00 | 2A 2E 00 | 6C 02      clearflag, release, end
        //
        // The third site does not fit that shape and is not claimed by it.
        [0x35] = 0,

        // Four, and giveitem's shape a third time — an id and a count. Both sites are
        // followed by a loadpointer carrying a real address, which is a script about to
        // say something about whatever it just handed over.
        [0x44] = 4,
        // Four, and the same shape as the two beside it: an item and a count. Oak takes
        // the parcel with `45 5D 01 01 00` — item 0x015D, one of them — and the byte
        // after those four is 0x0F, which is the first half of every text box in this
        // game. A width that resumes on a loadpointer at the one site the whole story
        // runs through is a width worth having.
        [0x45] = 4,     // takes an item away

        // Two. Both sites read `94 00 00 | 6C 02` — an argument of zero, then release
        // and end.
        [0x94] = 2,
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

        // The ones the runner acts on. A dump that prints these as numbers makes the
        // reader hold a table in their head while looking for the one command that is
        // not in it, which is the whole job.
        0x5A => "faceplayer2",
        0x16 => "setvar",
        0x17 => "addvar",
        0x21 => "compare",
        0x25 => "special",
        0x26 => "specialvar",
        0x29 => "setflag",
        0x2A => "clearflag",
        0x2B => "checkflag",
        // Named for what the bytes show it doing rather than for what it might be
        // called: it is handed a move id and answers a party slot, or six for nobody.
        0x79 => "givemon",
        0x7C => "findmove",

        // Named from the words on the two arms after it, at seven sites on six maps.
        0xA0 => "playergender",

        0x68 => "closemessage",
        0x69 => "lockall",
        0x6D => "waitstate",
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
    /// <summary>
    /// Every script start reachable from one address, following calls and jumps.
    /// <para>
    /// Written because the tool that finds unknown widths could not see them. It asked
    /// where a script's *linear* read stopped, and a linear read stops at the first
    /// <c>goto</c> — so a command sitting behind one was invisible to the very instrument
    /// built to find it. The command blocking the opening of the game was behind two.
    /// </para>
    /// </summary>
    public static List<uint> Reachable(Rom rom, uint address, int maxScripts = 64)
    {
        var found = new List<uint>();
        var seen = new HashSet<uint>();
        var queue = new Queue<uint>();

        queue.Enqueue(address);

        while (queue.Count > 0 && found.Count < maxScripts)
        {
            uint at = queue.Dequeue();

            if (!seen.Add(at)) continue;
            if (rom.ToOffsetOrNull(at) is null) continue;

            found.Add(at);

            foreach (ScriptCommand command in Read(rom, at))
            {
                uint target = command.Code switch
                {
                    ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),
                    ScriptCommands.GotoIf or ScriptCommands.CallIf => command.Pointer(1),
                    _ => 0,
                };

                if (target != 0 && rom.IsRomAddress(target)) queue.Enqueue(target);
            }
        }

        return found;
    }

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
    /// <para>
    /// <paramref name="maxScripts"/> was sixteen and sixteen was not enough. The rival's
    /// challenge in the professor's lab branches three ways on which starter was taken
    /// and three ways again inside each of those, which is thirteen blocks before the
    /// first fight is even queued — so the traversal ran out one block short of the only
    /// thing anybody wanted from it, and the square recorded no trainer at all. A limit
    /// that silently truncates is the same failure as a wrong width: the read comes back
    /// clean and quietly contains less. Ninety-six is past the largest script in this
    /// cartridge; <see cref="ReadAllTruncated"/> is how we know that.
    /// </para>
    /// </summary>
    public static List<ScriptCommand> ReadAll(Rom rom, uint address, int maxScripts = 96)
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
    public static int? FindTrainer(Rom rom, uint address) =>
        FindTrainers(rom, address) is [int first, ..] ? first : null;

    /// <summary>
    /// Every trainer a script can pick a fight with, in the order they are reached.
    /// <para>
    /// One is not always the answer. The rival's challenge at the lab door is three
    /// fights behind one square: the script compares 0x4031 — which starter was taken —
    /// and picks the one holding the type yours is weak to. Recording the first of them
    /// and calling it the trainer would field the wrong boy two times in three.
    /// </para>
    /// <para>
    /// Which one it is cannot be decided here, because it is a fact about a save this has
    /// never seen. What can be decided here is the set it must come from, and that is
    /// exactly what a server with no cartridge needs: not the answer, but the list of
    /// answers a client is allowed to give.
    /// </para>
    /// </summary>
    public static List<int> FindTrainers(Rom rom, uint address)
    {
        var found = new List<int>();

        foreach (ScriptCommand command in ReadAll(rom, address))
        {
            if (command.Code != ScriptCommands.TrainerBattle) continue;

            int id = command.Word(1);
            if (id != 0 && !found.Contains(id)) found.Add(id);
        }

        return found;
    }

    /// <summary>
    /// What a trainer says on the way into the fight.
    /// <para>
    /// Every trainer in this game has said the same sentence — "CALE wants to fight!" —
    /// and that sentence is this project's, not the cartridge's. Theirs is right there,
    /// as the third argument of the command that starts the fight: "People call this the
    /// NUGGET BRIDGE!", "You're going to see BILL? First, we battle!", "I saw your feat
    /// from the grass!". Four hundred and fifty of them, all thrown away.
    /// </para>
    /// <para>
    /// Thrown away twice over, and for two different reasons. A trainer with a line of
    /// sight never has their script run at all — the server starts the fight from the
    /// geometry. A trainer who has to be talked to does have it run, and the words are
    /// in the box that the battle screen then opens on top of. Reading them here, from
    /// the fight rather than from the conversation, covers both.
    /// </para>
    /// <para>
    /// Variant 3 is the exception the length table already knew about: nine bytes rather
    /// than thirteen, because it has no intro text. Those are the fights that begin
    /// without anybody saying anything, and giving them a line would be inventing one.
    /// </para>
    /// </summary>
    public static uint? BeforeTheFight(Rom rom, uint address, int trainerId)
    {
        foreach (ScriptCommand command in ReadAll(rom, address))
        {
            if (command.Code != ScriptCommands.TrainerBattle) continue;
            if (command.Word(1) != trainerId) continue;
            if (command.Arguments.Length < 13) continue;
            if (command.Arguments[0] == 3) continue;

            uint said = command.Pointer(5);

            if (rom.ToOffsetOrNull(said) is not { } at) continue;
            if (!GameText.LooksLikeDialogue(rom.Span[at..])) continue;

            return said;
        }

        return null;
    }

    /// <summary>
    /// The script a fight runs when it is won, if it carries one.
    /// <para>
    /// BROCK is why this exists. Beating a gym leader used to run his script again from
    /// the top with the fight marked as done, which reads the line he says on a later
    /// visit — "There are all kinds of TRAINERS in this huge world of ours" — and never
    /// touches the badge. The badge, the TM and five flags are at the end of a pointer
    /// the <c>trainerbattle</c> command carries and nothing followed.
    /// </para>
    /// <para>
    /// Which variants carry one is measured rather than remembered. Every variant has an
    /// intro and a defeat pointer; the longer ones have a third, and across this
    /// cartridge that third is script at 27 sites and text at 54 — so it is decided the
    /// same way every text pointer in this project is decided, by decoding what is there
    /// and asking whether it reads as speech. The eight that read as script and are
    /// reached by talking are the eight gym leaders.
    /// </para>
    /// </summary>
    public static uint? AfterTheFight(Rom rom, uint address, int trainerId)
    {
        foreach (ScriptCommand command in ReadAll(rom, address))
        {
            if (command.Code != ScriptCommands.TrainerBattle) continue;
            if (command.Word(1) != trainerId) continue;

            // The pointer past the two every variant has. Shorter variants have none,
            // and a script that carries on inline after the fight is the ordinary case.
            if (command.Arguments.Length < 17) continue;

            uint after = command.Pointer(13);

            if (rom.ToOffsetOrNull(after) is not { } at) continue;
            if (GameText.LooksLikeDialogue(rom.Span[at..])) continue;

            return after;
        }

        return null;
    }

    /// <summary>
    /// Whether reading everything reachable from here ran into its own limit.
    /// <para>
    /// The instrument for the limit, kept because a cap nobody measures is a cap that
    /// will one day be wrong quietly. Across this cartridge it fires for nothing, which
    /// is the only reason ninety-six is defensible as a number.
    /// </para>
    /// </summary>
    public static bool ReadAllTruncated(Rom rom, uint address, int maxScripts = 96)
    {
        var seen = new HashSet<uint> { address };
        var queue = new Queue<uint>([address]);

        while (queue.Count > 0)
        {
            if (seen.Count > maxScripts) return true;

            foreach (ScriptCommand command in Read(rom, queue.Dequeue()))
            {
                uint target = command.Code switch
                {
                    ScriptCommands.Call or ScriptCommands.Goto => command.Pointer(),
                    ScriptCommands.CallIf or ScriptCommands.GotoIf => command.Pointer(1),
                    _ => 0,
                };

                if (target == 0 || !rom.IsRomAddress(target)) continue;
                if (seen.Add(target)) queue.Enqueue(target);
            }
        }

        return false;
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
