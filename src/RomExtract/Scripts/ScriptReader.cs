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
        // Two, and this entry was WRONG rather than missing — the first of those in this
        // project, and a different animal. A missing width stops a read and says so. A wrong one
        // does not stop anything: it consumes the bytes of the commands after it and reads
        // whatever it lands on, so the block comes back full of instructions that are not there.
        //
        // Five consecutive blocks, byte for byte:
        //
        //   70 00 00 | 1F 00 00 | 05 F3 C1 16 08 | 02
        //   70 00 00 | 1F 01 00 | 05 F3 C1 16 08 | 02
        //   70 00 00 | 1F 02 00 | 05 F3 C1 16 08 | 02
        //   70 00 00 | 1F 03 00 | 05 F3 C1 16 08 | 02
        //   70 00 00 | 1F 04 00 | 05 F3 C1 16 08 | 02
        //
        // A counter and a `goto` to the same shared block, five times. At two the next command
        // is that goto at five of five; at five it swallows the goto's opcode and its pointer,
        // and the read carries on into the middle of the block the goto points at.
        //
        // Which is how it was found. It never stopped anything itself — it produced a phantom
        // stop at 0xE6, twenty-four bytes downstream, at a byte sitting INSIDE a gotoif's
        // pointer. `--stops` printing where each read STARTED, beside where it stopped, is what
        // made that visible: a stop is only a command if the reader was in step to begin with.
        //
        // Five was the width of Ruby's comparefarbytetobyte, and the note at the top of this
        // table has warned since milestone 14 that these lengths were written from memory of
        // that set and that a real FireRed image says they are not good enough.
        [0x1F] = 2,
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
        // Seven — a bank, a map, a warp id and a square — and this is the command that
        // moves somebody to another map. It sat here at one byte for forty milestones,
        // a width nobody had derived, and a wrong width does not fail: it makes every
        // script containing a warp read cleanly and quietly contain less.
        //
        // Derived by shape rather than by name. A bank and a map either name a map this
        // cartridge has or they do not, and a square is either inside that map or it is
        // not. Read that way at real command boundaries, 0x39 names a real map at a
        // square inside it at 19 of 19 sites. The next best byte on this cartridge
        // manages five per cent.
        //
        //   39 | 01 57 | 00 | 1B 00 | 15 00   -> 1.87 SEAFOAM ISLANDS at (27, 21)
        //   39 | 08 02 | 00 | 04 00 | 07 00   -> 8.2  LAVENDER TOWN   at (4, 7)
        [0x39] = 7,
        [0x3A] = 0,

        // Two, on four sites, and the block after it is what settles the width. At two the
        // third byte is a `return` and the block is four bytes long:
        //
        //   A7 16 01 | 03      <- return
        //   A7 17 01 | 03
        //   A7 3F 01 | 03
        //   A7 08 01 | 03
        //
        // A constant 0x03 could be an argument. It is not: at all four sites the byte after it
        // begins a block that something else in the image points at, and you do not fall into a
        // block that has its own pointer. Same test that settled 0xD0, and unanimous here.
        [0xA7] = 2,

        // Two. Three of its five sites are one shape, and the two that are not are a read that
        // had already drifted — both sit inside a gotoif's pointer, which is what a stop looks
        // like when the fault is upstream:
        //
        //   ... E0 7A 1A 08 | C0 00 00 | 0F 00 57 70 19 08 | 09 04
        //
        // At two the next command is `loadpointer` and the one after it `callstd`, which is how
        // every text box in this game opens, at three of three.
        [0xC0] = 2,

        // Seven, and the argument reads as plainly as the width. Twenty sites, all one shape,
        // and the only thing that varies is the numbers:
        //
        //   16 06 80 03 00 | 3F 01 2A FF 18 00 19 00 | 21 3A 40 03 00
        //   16 06 80 02 00 | 3F 01 2B FF 1C 00 10 00 | 21 ...
        //                    3F 01 2F FF 16 00 03 00 | 21 ...
        //                    3F 01 34 FF 14 00 03 00 | 21 ...
        //
        // A byte, a byte that counts up across the sites, 0xFF — which is how this cartridge
        // writes "the player" in every applymovement — and then two little-endian words whose
        // high bytes are zero at all twenty sites, which is what a pair of coordinates on a map
        // this size looks like.
        //
        // Six also parses, and the twenty sites say which: at seven the next command is
        // `compare` at TWENTY OF TWENTY, and at six it is a nop at twenty of twenty. A width
        // that lands on padding at every site has landed in the tail of an argument, which is
        // this project's own rule and it decides this one outright.
        //
        // --derive cannot: it throws both widths out for resuming on a column, because these
        // twenty sites are one idiom repeated and the correct width resumes on a column too.
        // It says "read the bytes" and that is what this is. The report now prints how much of
        // the run-up the sites share, so the next reader can see the test was worth nothing
        // here rather than having to work it out.
        [0x3F] = 7,
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
        // Four, and it was ONE — the second width in this project found wrong rather than
        // missing, and found by following the drift the first one taught us to look for.
        //
        // Five sites, all after a `setvar 0x8004`, and at four the next command is the same one
        // at every one of them:
        //
        //   16 04 80 0A 00 | 6F 14 08 3D 00 | 19 00 80 0D 80 | 21 00 80 ...
        //   16 04 80 09 00 | 6F 14 08 3D 00 | 19 00 80 0D 80 | 21 00 80 ...
        //   16 04 80 00 00 | 6F 13 05 39 00 | 19 00 80 0D 80 | 21 00 80 ...
        //   16 04 80 04 00 | 6F 00 00 2B 00 | 19 00 80 0D 80 | 21 00 80 ...
        //   16 04 80 00 00 | 6F 00 00 27 00 | 19 00 80 0D 80 | 21 00 80 ...
        //
        // `copyvar 0x8000, 0x800D` and then a `compare` on it: the cartridge's own idiom for
        // reading an answer back and branching on it, at five of five. At three the next byte
        // is a nop at five of five, which is the padding signature — a width that lands on
        // nothing but padding has landed in the tail of an argument.
        //
        // At one, which is what it was, the read is out of step from here on. That is what put
        // a phantom stop on 0xC0 thirty-seven bytes later, at a byte inside a `gotoif`'s
        // pointer — the same shape 0x1F produced, one command along.
        [0x6F] = 4,
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

        // Two, and the largest single stop on this cartridge once every kind of script was
        // being read: fifty-one blocks, more than the next three commands together. Every one
        // of the fifty-one is behind a `goto` or on a map's own script list, which is why a
        // reader that walked people's opening straight lines had never met it.
        //
        // The column, at sixteen sites:
        //
        //   D0 A4 08 | 02 | 0F 00 55 22 17 08 09 02 02
        //   D0 A5 08 | 02 | 0F 00 E0 2A 17 08 09 03 02
        //   D0 B0 08 | 02 | 0F 00 2D 96 17 08 09 03 02
        //   D0 17 08 | 0F 00 B2 D0 17 08 09 04 68
        //
        // The second byte varies and the third is 0x08 at every site, which is a word. What
        // settles the width is the byte after it: at three sites in four it is 0x02, and at
        // ELEVEN OF SIXTEEN something else in the image points at the byte after that. You do
        // not fall into a block that has its own pointer — so the 0x02 is an `end`, the
        // textbox after it is a script in its own right, and this command is two bytes wide.
        //
        // Every continuation test in --derive preferred three, because three skips the `end`
        // and reads on into a textbox that parses beautifully and is not this script. That is
        // the trap the note on 0x4F describes, from the other side, and it is why --derive now
        // counts how often a width reads on into a block something else names.
        [0xD0] = 2,

        // Four, and a pointer. Seventeen blocks, in a run of near-identical scripts where the
        // command sits between a 0x69 and a 0x6D at every one of them:
        //
        //   69 | 78 9F 92 1A 08 | 6D 6B 02
        //   69 | 78 A3 92 1A 08 | 6D 6B 02
        //   69 | 78 A7 92 1A 08 | 6D 6B 02
        //   69 | 78 AB 92 1A 08 | 6D 6B 02
        //
        // A cartridge address in plain sight, a lock either side of it, and the whole shape
        // repeating every nine bytes. No other width ends on a pointer at all.
        [0x78] = 4,

        // Two, and it travels in a pair with 0x9C carrying the same word. Three sites, on maps
        // that share nothing:
        //
        //   9C 3E 00 | 9E 3E 00 | 28 28 00 | 16 01 40 01 00 | 04 1A 65 1A 08
        //   9C 40 00 | 9E 40 00 | 26 0D 80 1E 01
        //   9C 19 00 | 9E 19 00 | 4F 0F 80 ED 75 1A 08
        //
        // 0x9C has been two bytes for milestones; this is the same word again, and at two the
        // read resumes on a known command at all three. The one at the top is the evidence
        // that matters: three commands later is `call 0x081A651A`, a pointer landing exactly
        // on a script — and that script is the `clearflag 0x009D` that puts nineteen people
        // on eleven maps. One missing width was hiding all of them.
        [0x9E] = 2,

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

        // Nothing at all, and this is the second time this entry has been rewritten.
        //
        // It was parked in milestone 14 with one script to go on, then set to five on
        // twenty-five sites that all looked like this:
        //
        //   A1 28 00 00 00 | 0F 00 61 3B 17 08     loadpointer
        //   A1 43 00 00 00 | 0F 00 44 44 17 08     loadpointer
        //   A1 96 00 02 00 | 67 9F 7F 17 08        message
        //
        // A column of 0xA1 with a zero at the end of it, twenty-five times: an argument
        // if ever there was one. It is not one. Those twenty-five are one family out of
        // two hundred sites, and the rest of the sites are not a column at all —
        //
        //   30 | 2F 26 00 ...      playse            80 sites
        //   30 | 4F FF 00 B7 ...   applymovement     43 sites
        //   30 | 6B 02 ...         faceplayer, end   15 sites
        //   30 | 0F 00 79 9E ...   loadpointer       12 sites
        //   30 | 04 BC B6 16 08    call              15 sites
        //   30 | 16 5C 40 01 00    setvar             2 sites
        //
        // — it is a catalogue of well-formed instructions, in roughly the proportions
        // the whole cartridge uses them in. Arguments do not do that. The 0xA1 family
        // was a real command being swallowed whole, and swallowing it is why 0xA1 was
        // invisible: with five bytes here, 1574 of 1584 people read to a proper end;
        // with none, 1543 do and 31 stop at 0xA1. That drop is the point. A read that
        // ends properly because it ate the command that would have stopped it is the
        // exact failure this reader exists to avoid, and it cost every one of those
        // scripts whatever 0xA1 does.
        [0x30] = 0,

        // Four, and this is the second answer this entry has had.
        //
        // Milestone 50 read it as one byte on thirty-five sites, on the strength of a
        // continuation test: one byte read on to a proper end at every site and none did
        // at a third of them. It is wrong, and the way it is wrong is the reason this
        // project keeps writing these notes. One byte leaves three nops in front of
        // every loadpointer in the game — which reads perfectly, ends properly, and
        // quietly ends the script that wakes the sleeper on ROUTE 12 three commands
        // before the flag that takes it off the map. Sixty-six maps behind a width.
        //
        // The column settles it, and it is the same column this project has trusted
        // since milestone 14 — only asked by machine this time. Every site is the same
        // shape:
        //
        //   A1 | 8F 00 02 00 | 28 28 00 ...    ROUTE 12's sleeper, species 0x8F
        //   A1 | 28 00 00 00 | 0F 00 61 3B     a loadpointer follows
        //   A1 | 61 00 02 00 | C5 B6 61 00     and here the same species again, in the
        //                                      setwildbattle three bytes later
        //
        // Byte one is a species and varies; bytes two and four are 00 at all twenty-one
        // sites. Read one byte wide, the byte it resumes on is 0x00 at 97% of sites and
        // does real work at 3% of them — it is resuming inside an argument. Read four,
        // it resumes on loadpointer, pause, setflag, message: on work, at every site.
        [0xA1] = 4,

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

        // One, and the two candidates left standing cannot be told apart by anything
        // downstream — which is itself the finding, and the reason this is safe.
        //
        // Two sites, and the scorer ruled out every width from 2 to 8 on two counts a
        // width cannot argue with. Five and seven swallow whole instructions; three and
        // four cut a loadpointer in half, and the pointer they cut lands on a page of
        // real text. Seven is the one worth naming: it reads on cleanly, it ends on
        // something that looks like a pointer, and what it swallows is the loadpointer
        // feeding the callstd immediately after it. Of the 1202 calls to standard
        // routine 4 in every script the maps can reach, 1202 have a page loaded first.
        // Zero do not. That width does not fail; it prints an empty box.
        //
        // What is left is 0 or 1, and the byte between is 0x00 at both sites — a nop
        // read as an instruction, an argument read as a number. Nothing after it can
        // tell the difference, because a nop does nothing. One is chosen over zero
        // because it is the safer of two answers that agree here: if some site elsewhere
        // ever holds a non-zero byte there, reading it as an argument is right and
        // reading it as an instruction is a command invented out of a number.
        [0x37] = 1,

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

        // Two, and this one is a VARIABLE — which is what makes it seven sites rather
        // than a column of bytes that happen to line up.
        //
        //   B3 | 01 40 | 21 01 40 1C 25 ...     five sites: 0x4001, then compare 0x4001
        //   B3 | 01 40 | 21 01 40 DE 26 ...
        //   B3 | 0D 80 | 22 0D 80 02 40 ...     two sites:  0x800D, then 0x22 on 0x800D
        //
        // Every one of the seven is followed by a command that reads THE SAME VARIABLE it
        // was just handed. An argument column can happen by accident; an argument column
        // whose value reappears as the operand of the next command cannot. The two values
        // are 0x4001 and 0x800D — one of this game's scratch pads and the standard result
        // variable — and neither is a plausible opcode.
        //
        // Read at any other width the stream desynchronises immediately: at 0 and 3 the
        // next byte is 0x01 or 0x0D, at 1 and 4 it is 0x40 or 0x80, and those are halves
        // of the variable id rather than commands.
        [0xB3] = 2,

        // Two, on two sites, and the same shape 0x94 four lines above was settled on:
        //
        //   94 00 00 | C1 00 05 | 6C 02      GAME CORNER, 0x0816C77A
        //              C1 00 00 | 6C 02      the cancel branch, 0x0816CC10
        //
        // An argument, then release and end. Widths 0 and 1 are REFUTED rather than
        // merely unpreferred: both leave site one resuming on `05` with the four bytes
        // after it reading 0x000F026C and 0x0F026C00, which are not addresses in a 16 MiB
        // cartridge. So the question is two against three.
        //
        // Three swallows the 0x6C as the last argument byte AT BOTH SITES. In the script
        // region these two live in, 688 of the 4145 bytes sitting immediately before an
        // `end` are 0x6C — 16.6%, the second commonest thing an end follows, and the pair
        // `6C 02` occurs 1030 times in the file against the 46 chance would give. Two
        // independent sites both ending with that exact byte in that exact place, as
        // data, is the coincidence.
        //
        // TWO SITES IS BELOW THIS PROJECT'S USUAL BAR, which is a column of five, and it
        // is said out loud rather than left in a commit message. What licenses it is that
        // the bar has already been met this way twice: 0x94 above is two sites of this
        // shape and 0x35 is two of three. The fixture below can separate two from three
        // and from zero and one. It cannot separate two from a width larger than four,
        // because nothing in this game's script stream is that wide.
        [0xC1] = 2,

        // Two, on five sites, and a column of round numbers.
        //
        // Three of them the instrument can see, and two of them it cannot — those sit
        // behind 0x92, which still has no width, and were read off a hexdump by hand:
        //
        //   B4 | 0A 00 | C7 03 | 0F 00 0D 6B 19 08     0x0816C811   ten
        //   B4 | 14 00 | C7 03 | 0F 00 47 6D 19 08     0x0816C8C8   twenty
        //   B4 | 14 00 | C7 03 | 0F 00 F8 6D 19 08     0x0816C928   twenty
        //   B4 | F4 01 | 91 10 27 00 00 00             0x0816C725   five hundred
        //   B4 | 32 00 | 91 E8 03 00 00 00             0x0816C753   fifty
        //
        // 10, 20, 20, 500 and 50. Arguments have columns and opcodes do not, which is the
        // test that settled 0xA1 at milestone 55 and 0x97 above.
        //
        // And the chain, which is 0xB7's test three lines of commands long: 0xC7 is
        // already known to take one argument, so read two wide the stream goes B4 -> C7
        // -> loadpointer, three commands each parsing into the next. Read three wide it
        // stops dead on a `return` with a loadpointer stranded after it; read four wide
        // the 0xC7 and the 0x03 both vanish into an argument.
        //
        // What this does NOT settle: 0x92 and 0xC7's own neighbourhood. Adopting this
        // moves the stop to 0xC7's far side rather than removing it, and the two sites
        // above that are read by hand stay unreachable until 0x92 has a width.
        [0xB4] = 2,

        // Two, and 0xB3's family — the argument is a variable, 0x4002 at all three sites.
        //
        //   16 08 02 | B5 | 02 40 | C2 00 05 7D 00 01 40 31 01 01 67 ...
        //
        // WIDTH NOUGHT IS REFUTED BY A POINTER, which is the test that settled 0xD0.
        // At nought the next byte is 0x02 and the block ENDS there — but seventeen bytes
        // further on sits `0F 00 A7 56 1A 08 | 09 05 | 21 0D 80 01 00 | 06 01 83 CD 16 08
        // | 05 10 CC 16 08 | 02`: a loadpointer carrying a real text address, a callstd, a
        // compare, an if and a goto, ending properly. That is unmistakably script, and
        // NOTHING IN THE FILE POINTS AT IT. Searched: 0x0816CDB3 has one pointer — it is a
        // block start — and 0x0816CDB4, B6, B8, BD and C7 have none between them. You do
        // not fall into a block that has its own pointer, and you do not reach one that has
        // none except by falling in. So the read does not stop at 0x0816CDB4.
        //
        // Three and four are refuted too, and by the same kind of fact rather than by
        // preference: both resume on 0x05, and the four bytes after it read 0x4001007D at
        // two sites and 0x8001001A at the third. Neither is an address in a 16 MiB file.
        //
        // That leaves one and two, and two is what makes 0x4002 a variable — the same
        // shape as 0xB3 five entries above, whose seven sites hand over 0x4001 and 0x800D.
        // The block at 0x0816CF43 does both within a dozen bytes: 0xB3 hands over 0x800D
        // and the 0x22 after it compares 0x800D against 0x4002.
        //
        // Adopting this does not unblock anything by itself: 0xC2 is immediately behind it
        // and has no width. The stops are a queue.
        [0xB5] = 2,

        // Five, on nine sites, and every one of them is money.
        //
        //   92 | 32 00 00 00 00 | 21 0D 80 00 00 ...        50
        //   92 | C8 00 00 00 00 | 05 CB C0 16 08            200
        //   92 | 2C 01 00 00 00 | 05 CB C0 16 08            300
        //   92 | 5E 01 00 00 00 | 05 CB C0 16 08            350
        //   92 | E8 03 00 00 00 | 21 0D 80 00 00 ...        1000
        //   92 | 10 27 00 00 00 | 21 0D 80 00 00 ...        10000
        //   92 | 32 00 00 00 00 | 21 0D 80 00 00 ...        50
        //   92 | F4 01 00 00 00 | 21 0D 80 00 00 ...        500
        //   92 | F4 01 00 00 00 | 21 0D 80 00 00 ...        500
        //
        // A four-byte little-endian value and a byte. The values are 50, 200, 300, 350,
        // 500, 500, 1000, 10000 and 50 — a column of prices, and the top three bytes of
        // every one of them are zero, which is what a 32-bit money field looks like in a
        // game whose largest number is 999999.
        //
        // Read five wide, all nine resume on a real command: six on `compare 0x800D 0` and
        // three on a `goto` with a valid address. The 0x800D six are the idiom whole —
        // check the money, compare the answer to nought, branch if it is not there.
        //
        // Two, three and four all resume on 0x00 at all nine sites, which is not nine
        // agreements but one: they are landing in the middle of the same run of zero bytes
        // in the same argument. A NOP SLIDE IS NOT A COLUMN, and this is what one looks
        // like from inside — the widest agreement in the table and the least evidence.
        [0x92] = 5,

        // Five, on nine sites, and it is 0x92's twin — the same shape carrying the same
        // nine values: 50, 200, 300, 350, 500, 500, 1000, 10000, 50.
        //
        // The clearest three are consecutive, and each is a one-line subroutine:
        //
        //   0x0816C0B6   91 | C8 00 00 00 00 | 03      two hundred, return
        //   0x0816C0BD   91 | 2C 01 00 00 00 | 03      three hundred, return
        //   0x0816C0C4   91 | 5E 01 00 00 00 | 03      three hundred and fifty, return
        //
        // Twenty-one bytes, three commands, three returns and three prices. A width that
        // is wrong by one cannot produce that, and the three sites are their OWN column —
        // each is jumped to separately, so the agreement is not one read repeated.
        //
        // Read five wide the other six resume on real work too: a goto with a valid
        // address at two, a return at one, and 0x95 at four. Two, three and four resume
        // on 0x00 at all nine, which is the nop slide inside the same argument rather
        // than nine agreements — the same false column 0x92 shows.
        //
        // 0x91 and 0x92 are the pair the GAME CORNER is built out of: the one that asks
        // and the one that takes. What each does is NOT claimed here; only how wide it is.
        [0x91] = 5,

        // Three, on seven sites — and the widths that LOOK best are the false column
        // milestone 200 wrote down.
        //
        // Read nought, one or two wide, all seven sites resume on 0x00. Seven agreements,
        // the widest anywhere in this table, and worth nothing: every site is landing in
        // the middle of the same run of zero bytes inside the same argument. Read three
        // wide they resume on 0x30, 0x30, 0x80, 0xC2, 0x0F, 0x31 and 0x19 — SEVEN
        // DIFFERENT BYTES, which is what a real command boundary looks like. Opcodes vary
        // between sites and arguments have columns; here the disagreement is the evidence.
        //
        // And the chains are long. 0x0816F875 reads on for eight commands:
        //
        //   95 00 00 00 | 31 01 01 | 67 <0x0819DBD3> | 66 32 | 7D 00 81 00 | 03
        //   0F 00 <0x0819DC07> | 09 04 | 94 00 00 | 6C 02
        //
        // — ending on `94 00 00 | 6C 02`, which is the exact shape 0x94 was settled on
        // eleven entries above. 0x081BF4F7 lands on `19 08 80 0D 80`, a copyvar between
        // two real variables. 0x0816D3E6 lands on a loadpointer carrying 0x08197D07.
        [0x95] = 3,

        // Two, on three sites, and the longest chain in this table.
        //
        //   C2 00 05 | 7D 00 01 40 | 31 01 01 | 67 <0x081A5DF1> | 66 32
        //   0F 00 <0x081A56A7> | 09 05 | 21 0D 80 01 00 | 06 01 <0x0816CD83>
        //
        // Eight commands, each parsing into the next, two of them carrying addresses that
        // are real and one a comparison against 0x800D. That is 0xB7's test several times
        // over, and it does not happen by accident at any other width.
        //
        // One is refuted rather than unpreferred: it resumes on 0x05 at all three sites and
        // the four bytes after read 0x4001007D twice and 0x8001001A once. Nought resumes on
        // a nop and then the same invalid goto.
        //
        // 0xC2 sits immediately after 0xB5 at all three of its sites, the way 0x95 sits
        // after 0x91 at four of nine and after 0x94 at the GAME CORNER. The pairs keep
        // pairing, and what any of them MEAN is still not claimed.
        [0xC2] = 2,

        // Nothing, on five sites — and every one of them is followed by a comparison of
        // the variable it must have just written.
        //
        //   43 | 18 0D 80 01 00 | 19 04 80 0D 80        0x081A8C27, 0x0816CD83
        //   43 | 21 0D 80 06 00 | 06 01 <0x0816891F>    0x081688BA
        //   43 | 21 0D 80 06 00 | 06 05 <0x081A77A9>    0x0816D462
        //   43 | 18 0D 80 01 00 | 7F 00 0D 80           0x081BF500
        //
        // 0x18 and 0x21 both take four arguments and both are handed 0x800D — this game's
        // standard result variable — and what comes after either reads 0x800D again or
        // branches on it. That is 0xB3's shape: an argument column is a coincidence, an
        // argument that reappears as the next command's operand is not, and here the
        // command has no arguments at all and the DEPENDENCY is still visible.
        //
        // All five are block starts. Two are goto targets and one is the far side of the
        // 0xC1 at 0x0816CD83 that milestone 199 read.
        //
        // One and two are the false column again: 0x0D at all five and then 0x80 at all
        // five, which is not ten agreements but one — the two halves of 0x800D, read as if
        // they were opcodes. Four is a nop slide. That makes three milestones running where
        // the widest-looking agreement was the wrong answer.
        [0x43] = 0,

        // Four — a POINTER, and the same one the command in front of it was just handed.
        //
        //   16 06 80 00 00 | 78 C5 92 1A 08 | D3 C5 92 1A 08 | 04 6C 92 1A 08
        //   16 06 80 00 00 | 78 D0 92 1A 08 | D3 D0 92 1A 08 | 04 6C 92 1A 08
        //   16 06 80 00 00 | 78 DC 92 1A 08 | D3 DC 92 1A 08 | 04 6C 92 1A 08
        //
        // Three times in thirty-three bytes at 0x08163BBB, three more at 0x0816442F, and the
        // 0x78 beside it is already known to take four. The same address twice within ten
        // bytes does not line up at any other width.
        //
        // MEASURED AGAINST A CONTROL, because "the same value twice" is exactly the kind of
        // claim that feels decisive and is not. Across the whole 16 MiB:
        //
        //   78 <4 bytes> D3 <4 bytes>   73 occurrences, 22 with the two values IDENTICAL  30.1%
        //   78 <4 bytes> 77 <4 bytes>  507 occurrences,  1 identical                       0.2%
        //   78 <4 bytes> 79 <4 bytes>  148 occurrences,  0 identical                       0.0%
        //   78 <4 bytes> 04 <4 bytes>  462 occurrences,  0 identical                       0.0%
        //   78 <4 bytes> 05 <4 bytes>  283 occurrences,  0 identical                       0.0%
        //
        // Thirty per cent against nought, nought, nought and a fifth of a per cent. The
        // pairing is real and it is this byte's.
        [0xD3] = 4,

        // One byte, and the column says so. Fifteen places in this game a read stops on
        // 0x97; at every one of them the byte after it is 1, 2 or 3 and nothing else,
        // which is an argument rather than an opcode — opcodes vary between sites and
        // arguments have columns, the same test that settled 0xA1 in milestone 55.
        //
        // The scorer could not split five bytes from six and preferred both to one, on
        // the continuation test. The continuation test is not to be trusted alone and
        // this project has now been shown that twice: read one wide, 0x97 resumes on
        // real work at every site — a waitbutton, a repeated 0x53, a loadpointer — and
        // read five or six wide it resumes on a column of nothing.
        //
        // And the anchor is from outside the script entirely. ROCKET HIDEOUT object 1's
        // script, read one wide, runs on through a waitbutton and two more commands and
        // lands exactly on `clearflag 0x0037` — the flag that hides the SILPH SCOPE,
        // whose address was found by a scan that knew nothing about widths.
        [0x97] = 1,
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

                // A fight carries scripts of its own, and until now nothing followed
                // them. Two of the three flags hiding the middle of this game — the LIFT
                // KEY and the SILPH SCOPE — are cleared inside one, which is why a walk
                // over every person in the world could not reach either.
                if (command.Code == ScriptCommands.TrainerBattle)
                {
                    foreach (uint after in ScriptsAfterAFight(rom, command))
                        if (seen.Add(after)) queue.Enqueue(after);

                    continue;
                }

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

    /// <summary>
    /// The pointers in a <c>trainerbattle</c> that are scripts rather than text.
    /// <para>
    /// Every variant carries a type, a trainer id and one more word, and then between
    /// one and four pointers. Some of those are lines the trainer says and some are
    /// scripts to run once the fight is over, and <b>nothing in the command says
    /// which</b> — the variant does, and the variants are the thing this project has
    /// been least sure of.
    /// </para>
    /// <para>
    /// So it is not asked. Each pointer is read as a script and kept only if it reads
    /// like one: a run of commands that ends the way a script ends, at an <c>end</c>, a
    /// <c>return</c> or a <c>goto</c>. A page of text decoded as commands runs into a
    /// byte with no length and stops, which is exactly the test — and the same test this
    /// project already uses to tell a script that finished from one that fell over.
    /// </para>
    /// </summary>
    public static IEnumerable<uint> ScriptsAfterAFight(Rom rom, ScriptCommand command)
    {
        if (command.Code != ScriptCommands.TrainerBattle) yield break;

        // Type, trainer id, and one more word: five bytes before the first pointer.
        for (int at = 5; at + 4 <= command.Arguments.Length; at += 4)
        {
            uint target = command.Pointer(at);

            if (target == 0 || !rom.IsRomAddress(target)) continue;
            if (!EndsLikeAScript(rom, target)) continue;

            yield return target;
        }
    }

    /// <summary>
    /// Whether reading from here runs to a proper end rather than into a byte that is
    /// not a command.
    /// </summary>
    private static bool EndsLikeAScript(Rom rom, uint address)
    {
        if (rom.ToOffsetOrNull(address) is not { } offset) return false;

        for (int read = 0; read < MaxCommands; read++)
        {
            if (offset >= rom.Length) return false;

            byte code = rom.ReadU8(offset);
            byte first = offset + 1 < rom.Length ? rom.ReadU8(offset + 1) : (byte)0;

            if (ScriptCommands.ArgumentLength(code, first) is not { } length) return false;

            if (code is ScriptCommands.End or ScriptCommands.Return or ScriptCommands.Goto) return true;

            offset += 1 + length;
        }

        return false;
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
    /// <summary>
    /// Every item a script could ever hand over, whichever way it does it.
    /// <para>
    /// Read rather than run, and for the usual reason: which item, if any, depends on a
    /// save this has never seen. What travels is the list of answers a client is allowed
    /// to give, and the server checks a handover against it.
    /// </para>
    /// <para>
    /// Two shapes, because this cartridge has two. <c>giveitem</c> names the item in its
    /// own arguments. The other writes the item into 0x8000 and the count into 0x8001 and
    /// calls a standard routine to say the sentence and put it in the bag — which is how
    /// the girl lost in the BERRY FOREST hands over what she was sent for, and how the
    /// list came to be missing it.
    /// </para>
    /// <para>
    /// The second shape is only counted when the script actually calls a routine. A write
    /// to 0x8000 on its own is a script passing a number to something, and a number is
    /// not an item until somebody is asked to hand it over.
    /// </para>
    /// </summary>
    public static IEnumerable<int> EverythingItCouldGive(Rom rom, uint address)
    {
        List<ScriptCommand> all = [.. ReadAll(rom, address)];

        IEnumerable<int> named = all
            .Where(c => c.Code is 0x44 or 0x46)
            .Select(c => c.Word());

        bool calls = all.Any(c => c.Code is ScriptCommands.CallStandard or 0x08);

        IEnumerable<int> buffered = calls
            ? all.Where(c => c.Code == 0x1A && c.Word() == 0x8000).Select(c => c.Word(2))
            : [];

        return named.Concat(buffered).Where(id => id > 0).Distinct();
    }

    /// <summary>
    /// Where every fight this script could ever start would pick up again.
    /// <para>
    /// Read rather than run, and only for asking the question in bulk: a run walks the
    /// branch today's save takes, and every sleeper and every one-of-a-kind creature in
    /// the game keeps its fight behind a flag a fresh save has not set. At runtime the
    /// run's own answer is the right one — it is where the script actually was.
    /// </para>
    /// </summary>
    public static IEnumerable<uint> AfterTheWildFights(Rom rom, uint address)
    {
        var after = new List<uint>();

        foreach (uint block in Reachable(rom, address))
        {
            List<ScriptCommand> commands = [.. Read(rom, block)];

            bool setUp = false;

            for (int i = 0; i < commands.Count; i++)
            {
                ScriptCommand command = commands[i];

                if (command.Code == 0xB6) setUp = true;

                // The command, and the code routine that stands where it stands. A
                // special followed by a waitstate is the script handing the screen over
                // and stopping — which is what a naming screen, a trade and a slot
                // machine all look like too, so it only counts as a fight when a creature
                // has just been set up for one. Without that it is a hundred and eighty-
                // eight scripts, most of them the CABLE CLUB.
                bool fights =
                    command.Code == 0xB7 ||
                    (setUp &&
                     command.Code == SpecialCalls.Special &&
                     i + 1 < commands.Count &&
                     commands[i + 1].Code == 0x27);

                if (!fights) continue;

                int end = command.Offset + 1 + command.Arguments.Length;

                after.Add(Rom.BaseAddress + (uint)(command.Code == 0xB7 ? end : end + 1));
            }
        }

        return after.Distinct();
    }

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
