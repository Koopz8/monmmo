using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;

namespace PokeMmo.RomExtract.Tests;

public class DialogueTextTests
{
    private static byte[] Encoded(params byte[] bytes) => bytes;

    [Fact]
    public void ControlBytesBreakPagesRatherThanBecomingQuestionMarks()
    {
        // Running dialogue through the name decoder turns every control byte into a
        // question mark, which is what makes a text box unreadable rather than absent.
        var rom = new SyntheticRom();
        Rom image = rom.ToRom();

        List<string> pages = ScriptReader.ReadDialogue(
            image, Rom.BaseAddress + (uint)SyntheticRom.ScriptFor(1, 0));

        Assert.All(pages, page => Assert.DoesNotContain('?', page));
    }

    [Fact]
    public void ALineBreakStaysInsideThePage()
    {
        List<string> pages = GameText.DecodeDialogue(
            Encoded(0xBB, GameText.NewLine, 0xBC, GameText.Terminator));

        Assert.Single(pages);
        Assert.Equal("A\nB", pages[0]);
    }

    [Fact]
    public void APageBreakStartsANewPage()
    {
        List<string> pages = GameText.DecodeDialogue(
            Encoded(0xBB, GameText.Paragraph, 0xBC, GameText.Terminator));

        Assert.Equal(2, pages.Count);
        Assert.Equal("A", pages[0]);
        Assert.Equal("B", pages[1]);
    }

    [Fact]
    public void ReadingStopsAtTheTerminator()
    {
        // Dialogue carries no length. Reading past the end gives whatever data follows,
        // decoded into something that looks like speech.
        List<string> pages = GameText.DecodeDialogue(
            Encoded(0xBB, GameText.Terminator, 0xBC, 0xBD, 0xBE));

        Assert.Equal("A", Assert.Single(pages));
    }

    [Fact]
    public void SomethingThatIsNotTextIsRecognisedAsSuch()
    {
        // Used to decide whether a pointer leads to speech at all. Anything decodes;
        // the question is whether the result is mostly letters or mostly nonsense.
        Assert.True(GameText.LooksLikeDialogue(Encoded(0xBB, 0xBC, 0xBD, 0xBE, 0xBF, GameText.Terminator)));
        Assert.False(GameText.LooksLikeDialogue(Encoded(0x30, 0x31, 0x32, 0x33, 0x34, GameText.Terminator)));

        // And a couple of stray bytes are not enough to call it speech either way.
        Assert.False(GameText.LooksLikeDialogue(Encoded(0xBB, GameText.Terminator)));

        // Empty memory is the important case. A zero byte decodes as a space, so a run
        // of padding reads as flawless text under any check that counts spaces.
        Assert.False(GameText.LooksLikeDialogue(new byte[64]));
    }

    [Fact]
    public void TheCurlyApostropheSurvivesTheTripToAPlainFont()
    {
        // The cartridge's apostrophe is a curly one and it turns up in roughly every
        // other sentence. A font with no glyph for it draws nothing at all, so "I'm"
        // comes out as "Im" — text that looks subtly wrong everywhere without ever
        // looking wrong enough to go and investigate.
        Assert.Equal("I'm \"here\"", GameText.ToAscii("I’m “here”"));
        Assert.Equal("NIDORAN(M)", GameText.ToAscii("NIDORAN♂"));

        // And anything already plain is left exactly as it is, line breaks included.
        Assert.Equal("Hello!\nHow are you?", GameText.ToAscii("Hello!\nHow are you?"));
    }
}

public class ScriptReaderTests
{
    private static readonly SyntheticRom Fixture = new();

    private static uint ScriptAt(int mapIndex, int slot) =>
        Rom.BaseAddress + (uint)SyntheticRom.ScriptFor(mapIndex, slot);

    [Fact]
    public void ReadsTheInstructionsInOrder()
    {
        List<ScriptCommand> commands = ScriptReader.Read(Fixture.ToRom(), ScriptAt(1, 0));

        Assert.Equal(
            new byte[]
            {
                ScriptCommands.Lock,
                ScriptCommands.FacePlayer,
                ScriptCommands.LoadPointer,
                ScriptCommands.CallStandard,
                ScriptCommands.Release,
                ScriptCommands.End,
            },
            commands.Select(c => c.Code));
    }

    [Fact]
    public void ArgumentLengthsDecideWhereTheNextCommandStarts()
    {
        // There is no length and no table of contents. The second instruction is found
        // by knowing how long the first one is, so a wrong length does not fail — it
        // resumes in the middle of an argument and invents every instruction after it.
        List<ScriptCommand> commands = ScriptReader.Read(Fixture.ToRom(), ScriptAt(1, 0));

        ScriptCommand loadPointer = commands.Single(c => c.Code == ScriptCommands.LoadPointer);

        Assert.Equal(5, loadPointer.Arguments.Length);
        Assert.True(Fixture.ToRom().IsRomAddress(loadPointer.Pointer(1)));
    }

    /// <summary>Builds a tiny image holding two scripts, the first calling the second.</summary>
    private static Rom TwoScripts(byte[] first, byte[] second, int secondAt = 0x100)
    {
        var image = new byte[0x400];

        first.CopyTo(image, 0);
        second.CopyTo(image, secondAt);

        return new Rom(image);
    }

    [Fact]
    public void AScriptThatHandsOffIsFollowed()
    {
        // Most people in FireRed do their work somewhere else: lock, face the player,
        // call, release, end — and everything that makes them who they are is at the
        // other end of that call. A reader that stops at the handoff sees somebody who
        // does nothing, which is what a cartridge with a shop in every town looked like.
        uint target = Rom.BaseAddress + 0x100;

        byte[] caller =
        [
            ScriptCommands.Lock,
            ScriptCommands.Call,
            (byte)target, (byte)(target >> 8), (byte)(target >> 16), (byte)(target >> 24),
            ScriptCommands.Release,
            ScriptCommands.End,
        ];

        byte[] called = [ScriptCommands.WaitButton, ScriptCommands.End];

        List<ScriptCommand> commands = ScriptReader.ReadAll(TwoScripts(caller, called), Rom.BaseAddress);

        Assert.Contains(commands, c => c.Code == ScriptCommands.WaitButton);
    }

    [Fact]
    public void AScriptThatCallsItselfIsReadOnce()
    {
        // Real scripts loop. Following a handoff without remembering where you have been
        // is a reader that never comes back.
        uint self = Rom.BaseAddress;

        byte[] script =
        [
            ScriptCommands.Lock,
            ScriptCommands.Call,
            (byte)self, (byte)(self >> 8), (byte)(self >> 16), (byte)(self >> 24),
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.ReadAll(TwoScripts(script, []), self);

        Assert.Equal(3, commands.Count);
    }

    [Fact]
    public void BothArmsOfAConditionalAreRead()
    {
        // Deciding which arm runs needs the flags of a save this has never seen. Reading
        // both is the difference between knowing what somebody might say and knowing
        // nothing at all.
        uint target = Rom.BaseAddress + 0x100;

        byte[] caller =
        [
            ScriptCommands.GotoIf,
            0x01,
            (byte)target, (byte)(target >> 8), (byte)(target >> 16), (byte)(target >> 24),
            ScriptCommands.End,
        ];

        byte[] called = [ScriptCommands.WaitButton, ScriptCommands.End];

        List<ScriptCommand> commands = ScriptReader.ReadAll(TwoScripts(caller, called), Rom.BaseAddress);

        Assert.Contains(commands, c => c.Code == ScriptCommands.WaitButton);
    }

    [Fact]
    public void TheCommandEveryPersonOpensWithTakesNoArgument()
    {
        // One byte, 468 broken scripts. Taking an argument here swallows the next command
        // and puts the read one byte out of step forever — after which what it hits is
        // whatever sat in the middle of a pointer or a variable id. Every one of the
        // twenty commonest "unknown commands" on a real cartridge was this, and none of
        // them was a command.
        Assert.Equal(0, ScriptCommands.ArgumentLength(0x5A));

        // The bytes that prove it: lock, this, call <a real pointer>, release, end.
        uint target = Rom.BaseAddress + 0x100;

        byte[] script =
        [
            ScriptCommands.Lock,
            0x5A,
            ScriptCommands.Call,
            (byte)target, (byte)(target >> 8), (byte)(target >> 16), (byte)(target >> 24),
            ScriptCommands.Release,
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(
            new byte[] { ScriptCommands.Lock, 0x5A, ScriptCommands.Call, ScriptCommands.Release, ScriptCommands.End },
            commands.Select(c => c.Code));

        Assert.Equal(target, commands[2].Pointer());
    }

    [Fact]
    public void SettingAFlagTakesTwoBytes()
    {
        // One byte here does not stop the read — it makes the next byte an `end`. The
        // script then reports a clean read and quietly contains nothing, which is worse
        // than failing.
        Assert.Equal(2, ScriptCommands.ArgumentLength(0x29));

        byte[] script = [0x29, 0xA5, 0x02, ScriptCommands.WaitButton, ScriptCommands.End];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(3, commands.Count);
        Assert.Equal(0x02A5, commands[0].Word());
    }

    [Fact]
    public void AskingTheGameSomethingTakesARoutineNumber()
    {
        // These two were the whole of the second round. Neither stops a read where it
        // sits — they misalign it, and what turns up a byte later looks like a command
        // nobody has heard of. 0x80 "stopped" 245 scripts and is the top half of variable
        // 0x800D; 0xA5, 0x73 and 0x74 are routine numbers.
        Assert.Equal(2, ScriptCommands.ArgumentLength(0x25));
        Assert.Equal(4, ScriptCommands.ArgumentLength(0x26));

        byte[] script =
        [
            0x25, 0xA5, 0x00,               // special 0x00A5
            0x26, 0x0D, 0x80, 0xA3, 0x00,   // specialvar 0x800D, 0x00A3
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(3, commands.Count);
        Assert.Equal(0x00A5, commands[0].Word());
        Assert.Equal(0x800D, commands[1].Word());
        Assert.Equal(0x00A3, commands[1].Word(2));
    }

    [Fact]
    public void LockingAndClosingABoxTakeNothing()
    {
        // Third round, same shape a third time. `69 2B 25 08 06 00 ...` is this, then
        // checkflag(0x0825), then a conditional goto — and 0x91, 0x23 and 0xDF, which
        // between them "stopped" 200 scripts, are the low bytes of the flags being
        // checked. None of the three was ever a command.
        Assert.Equal(0, ScriptCommands.ArgumentLength(0x69));
        Assert.Equal(0, ScriptCommands.ArgumentLength(0x68));

        uint target = Rom.BaseAddress + 0x100;

        byte[] script =
        [
            0x69,
            0x2B, 0x25, 0x08,                                                   // checkflag 0x0825
            ScriptCommands.GotoIf, 0x00,
            (byte)target, (byte)(target >> 8), (byte)(target >> 16), (byte)(target >> 24),
            0x68,
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(
            new byte[] { 0x69, 0x2B, ScriptCommands.GotoIf, 0x68, ScriptCommands.End },
            commands.Select(c => c.Code));

        Assert.Equal(0x0825, commands[1].Word());
        Assert.Equal(target, commands[2].Pointer(1));
    }

    [Fact]
    public void TheObstacleFamilyReadsAsOneScript()
    {
        // Two hundred reads, three scripts, five widths — the trees, the boulders and
        // the rubble. This is the cut script from a real image, byte for byte from
        // 0x081BDF2B, with its pointers moved to fit a fixture.
        //
        // The value in the first command is 15, and 15 in the move table read off that
        // same image is CUT. 70 is STRENGTH and 249 is ROCK SMASH, and those are the
        // only three values this command is ever given.
        Assert.Equal(2, ScriptCommands.ArgumentLength(0x7C));
        Assert.Equal(3, ScriptCommands.ArgumentLength(0x9D));
        Assert.Equal(3, ScriptCommands.ArgumentLength(0x7F));
        Assert.Equal(3, ScriptCommands.ArgumentLength(0x82));
        Assert.Equal(2, ScriptCommands.ArgumentLength(0x9C));

        uint target = Rom.BaseAddress + 0x100;

        byte[] script =
        [
            0x7C, 0x0F, 0x00,               // findmove CUT
            0x21, 0x0D, 0x80, 0x06, 0x00,   // compare 0x800D, 6
            ScriptCommands.GotoIf, 0x01,
            (byte)target, (byte)(target >> 8), (byte)(target >> 16), (byte)(target >> 24),
            0x9D, 0x00, 0x0D, 0x80,
            0x7F, 0x00, 0x0D, 0x80,
            0x82, 0x01, 0x0F, 0x00,
            0x68,                           // closemessage
            0x9C, 0x02, 0x00,
            0x27,
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(
            new byte[] { 0x7C, 0x21, ScriptCommands.GotoIf, 0x9D, 0x7F, 0x82, 0x68, 0x9C, 0x27, ScriptCommands.End },
            commands.Select(c => c.Code));

        // The move id, twice: once to ask who can use it and once to say so.
        Assert.Equal(0x000F, commands[0].Word());
        Assert.Equal(0x000F, commands[5].Word(1));

        // Six, because a party has six slots and the sixth answer is "none of them".
        Assert.Equal(0x0006, commands[1].Word(2));

        // The jump lands on a command boundary, which is the check that actually catches
        // a width wrong by one: a desynchronised stream's own jumps land mid-argument.
        Assert.Equal(target, commands[2].Pointer(1));
    }

    [Fact]
    public void TheOneThatWasStoppingTheStory()
    {
        // Pallet Town's north exit, byte for byte from 0x08165612 on a real image, with
        // the message pointer moved to fit a fixture.
        //
        // 0x28 was zero-width here since the beginning, with nothing written down for it.
        // Read that way the stream walks one byte into the message command and stops on
        // the middle of its pointer — so the trigger ran, set its variables, and said
        // nothing. Standing at the edge of town, the professor let you leave.
        Assert.Equal(2, ScriptCommands.ArgumentLength(0x28));

        uint text = Rom.BaseAddress + 0x100;

        byte[] script =
        [
            0x25, 0x74, 0x01,               // special 0x0174
            0xC7, 0x00,
            0x28, 0x1E, 0x00,
            0x33, 0x2E, 0x01, 0x00,
            ScriptCommands.Message, (byte)text, (byte)(text >> 8), (byte)(text >> 16), (byte)(text >> 24),
            ScriptCommands.WaitButton,
            ScriptCommands.End,
        ];

        List<ScriptCommand> commands = ScriptReader.Read(TwoScripts(script, []), Rom.BaseAddress);

        Assert.Equal(
            new byte[] { 0x25, 0xC7, 0x28, 0x33, ScriptCommands.Message, ScriptCommands.WaitButton, ScriptCommands.End },
            commands.Select(c => c.Code));

        // The message, landing exactly where it should. One byte out and this is a
        // pointer read from the middle of itself.
        Assert.Equal(text, commands[4].Pointer());
    }

    [Fact]
    public void AnUnknownCommandStopsTheRead()
    {
        // Guessing a length would resume at some byte inside an argument, and from
        // there everything read is invented — which looks like a script, not an error.
        Assert.Null(ScriptCommands.ArgumentLength(0xE4));

        var rom = new Rom(BuildScript(0x6A, 0xE4, 0x02));

        List<ScriptCommand> commands = ScriptReader.Read(rom, Rom.BaseAddress);

        Assert.Single(commands);
        Assert.Equal(ScriptCommands.Lock, commands[0].Code);
    }

    [Fact]
    public void ReadingStopsAtTheEnd()
    {
        var rom = new Rom(BuildScript(ScriptCommands.Lock, ScriptCommands.End, ScriptCommands.Lock));

        Assert.Equal(2, ScriptReader.Read(rom, Rom.BaseAddress).Count);
    }

    [Fact]
    public void FindsWhatSomebodyWouldSay()
    {
        // The games have no "say this" instruction: dialogue is a pointer loaded into a
        // slot, then a call to a routine that shows whatever is in it.
        List<string> pages = ScriptReader.ReadDialogue(Fixture.ToRom(), ScriptAt(2, 1));

        Assert.Equal(SyntheticRom.DialogueFor(2, 2), pages);
    }

    [Fact]
    public void APointerToSomethingThatIsNotTextIsIgnored()
    {
        // Plenty of loaded pointers are not speech — they are movement scripts, item
        // ids, tables. Decoding one anyway produces a page of question marks.
        var script = new byte[64];

        script[0] = ScriptCommands.LoadPointer;
        script[1] = 0;

        // Point at the map collision data, which decodes as nothing readable.
        uint target = Rom.BaseAddress + (uint)SyntheticRom.MapBlocksOffset;

        script[2] = (byte)target;
        script[3] = (byte)(target >> 8);
        script[4] = (byte)(target >> 16);
        script[5] = (byte)(target >> 24);
        script[6] = ScriptCommands.End;

        byte[] image = Fixture.Bytes.ToArray();
        script.CopyTo(image, SyntheticRom.ScriptsOffset);

        List<string> pages = ScriptReader.ReadDialogue(
            new Rom(image), Rom.BaseAddress + SyntheticRom.ScriptsOffset);

        Assert.Empty(pages);
    }

    [Fact]
    public void EveryPersonWithAScriptHasOne()
    {
        Rom rom = Fixture.ToRom();

        MapBankTable banks = MapBankLocator.Locate(rom)!;

        (int bank, int map, MapHeaderRecord header) = banks.AllMaps.First(m => m.Bank == 0 && m.Map == 1);

        List<MapObject> objects = MapLinkExtractor.ReadObjects(
            rom, header, SyntheticRom.MapWidth, SyntheticRom.MapHeight);

        Assert.All(objects, o => Assert.True(o.HasScript));

        MapObject first = objects[0];

        Assert.Equal(SyntheticRom.DialogueFor(1, first.LocalId), ScriptReader.ReadDialogue(rom, first.ScriptAddress));
    }

    /// <summary>A tiny image holding just a script, for the cases the fixture cannot express.</summary>
    private static byte[] BuildScript(params byte[] commands)
    {
        var image = new byte[0x400];
        commands.CopyTo(image, 0);
        return image;
    }
}

/// <summary>
/// Running a script rather than reading it.
/// <para>
/// The reader answers "what could this person possibly say" and has to, because
/// choosing between the arms of a conditional needs the flags of a save. The runner is
/// given those flags and walks one path — so what comes back is a transcript rather
/// than an inventory, and a person stops saying every version of their line at once.
/// </para>
/// </summary>
public class ScriptRunnerTests
{
    private const uint Start = Rom.BaseAddress;
    private const uint Elsewhere = Rom.BaseAddress + 0x100;
    private const uint SaysA = Rom.BaseAddress + 0x200;
    private const uint SaysB = Rom.BaseAddress + 0x220;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    /// <summary>Six of the same letter, which is enough to read as speech and not as data.</summary>
    private static byte[] Speech(char letter) =>
    [
        .. Enumerable.Repeat((byte)(0xBB + (letter - 'A')), 6),
        GameText.Terminator,
    ];

    private static Rom Image(params (uint Address, byte[] Bytes)[] chunks)
    {
        var image = new byte[0x400];

        foreach ((uint address, byte[] bytes) in chunks)
            bytes.CopyTo(image, (int)(address - Rom.BaseAddress));

        return new Rom(image);
    }

    /// <summary>A script that says one letter and stops.</summary>
    private static byte[] Says(uint text) => [ScriptCommands.Message, .. At(text), ScriptCommands.End];

    [Fact]
    public void AByteWithNoLetterBehindItSaysWhichByteItWas()
    {
        // A question mark is a lie: it reads as punctuation somebody typed, so the one
        // character this cannot decode is also the one it can never notice. POKéMON read
        // as POK?MON in every line on Route 1 and looked like a sentence.
        // 0x50 rather than 0x1B: this test is about the ones still unknown, and 0x1B is
        // é now precisely because printing it that way gave it away.
        List<string> pages = GameText.DecodeDialogue([0xBB, 0x50, 0xBC, GameText.Terminator]);

        Assert.Equal("A{50}B", Assert.Single(pages));

        // And the three that printing the byte identified, each from the sentence it
        // turned up in on the real cartridge.
        Assert.Equal("PokéMON…:", Assert.Single(GameText.DecodeDialogue(
            [0xCA, 0xE3, 0xDF, 0x1B, 0xC7, 0xC9, 0xC8, 0xB0, 0xF0, GameText.Terminator])));

        // A real question mark is still a real question mark.
        Assert.Equal("A?", Assert.Single(GameText.DecodeDialogue([0xBB, 0xAC, GameText.Terminator])));
    }

    [Fact]
    public void OnlyTheArmThatRunsIsRead()
    {
        // checkflag then "goto if less" is the commonest pair on the cartridge, and it
        // means "if they have not done this yet". A flag is one or nothing compared
        // against one, so clear reads as less and set reads as equal.
        Rom rom = Image(
            (Start, [0x2B, .. Word(0x828), ScriptCommands.GotoIf, 0x00, .. At(Elsewhere), .. Says(SaysB)]),
            (Elsewhere, Says(SaysA)),
            (SaysA, Speech('A')),
            (SaysB, Speech('B')));

        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start).Pages));

        Assert.Equal(
            "BBBBBB",
            Assert.Single(ScriptRunner.Run(rom, Start, new ScriptState([0x828])).Pages));
    }

    [Fact]
    public void AQuestionStopsTheRunRatherThanAnsweringItself()
    {
        // Standard routine 5 asks. Nothing in a save can answer it, so the run stops and
        // says where to carry on from — and running past it instead reads whatever 0x800D
        // happens to hold, which on a fresh save is nought, and nought is no. Every offer
        // in this game was being declined before anybody saw it.
        Rom rom = Image(
            (Start,
            [
                ScriptCommands.LoadPointer, 0x00, .. At(SaysA),
                ScriptCommands.CallStandard, 0x05,
                0x16, .. Word(0x4055), .. Word(3),
                ScriptCommands.End,
            ]),
            (SaysA, Speech('A')));

        ScriptRun asked = ScriptRunner.Run(rom, Start);

        Assert.NotNull(asked.Question);
        Assert.Equal("AAAAAA", Assert.Single(asked.Pages));
        Assert.Empty(asked.VariablesWritten);

        // And carrying on from where it said finds the rest.
        ScriptRun rest = ScriptRunner.Run(rom, asked.Question!.Value);

        Assert.Equal(3, Assert.Single(rest.VariablesWritten).Value);
    }

    [Fact]
    public void AnOrdinaryPageDoesNotStopTheRun()
    {
        // The control. Routine 4 is 1967 of this game's calls and not one of them is
        // followed by a compare on 0x800D; stopping on those would stop most of the
        // dialogue in the world.
        Rom rom = Image(
            (Start,
            [
                ScriptCommands.LoadPointer, 0x00, .. At(SaysA),
                ScriptCommands.CallStandard, 0x04,
                0x16, .. Word(0x4055), .. Word(3),
                ScriptCommands.End,
            ]),
            (SaysA, Speech('A')));

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Null(run.Question);
        Assert.Equal(3, Assert.Single(run.VariablesWritten).Value);
    }
    [Fact]
    public void ACallComesBackAndCarriesOn()
    {
        // The difference between call and goto is the whole of a script's structure:
        // most people in FireRed say nothing themselves and call somebody who does.
        Rom rom = Image(
            (Start, [ScriptCommands.Call, .. At(Elsewhere), ScriptCommands.Message, .. At(SaysB), ScriptCommands.End]),
            (Elsewhere, [ScriptCommands.Message, .. At(SaysA), ScriptCommands.Return]),
            (SaysA, Speech('A')),
            (SaysB, Speech('B')));

        Assert.Equal(["AAAAAA", "BBBBBB"], ScriptRunner.Run(rom, Start).Pages);
    }

    /// <summary>
    /// Enough letters in front of the gap for the run to read as dialogue at all, then
    /// the codes. Say refuses anything that does not look like words, rightly — a
    /// pointer that is not text would otherwise be read to the end of the cartridge.
    /// </summary>
    private static byte[] Gap(params byte[] codes) =>
    [
        .. Enumerable.Repeat((byte)0xBB, Math.Max(6, codes.Length * 6)),
        .. codes.SelectMany<byte, byte>(c => [0xFD, c]),
        GameText.Terminator,
    ];

    [Fact]
    public void TheGapsInASentenceAreFilledIn()
    {
        // 0xFD marks a gap and the byte after it says what goes there. Derived by
        // counting sites and reading sentences: 0x01 is the player at 109 of them and
        // 0x06 is the rival at 33, always as a speaker's label before a colon.
        byte[] text = Gap(0x06, 0x01);

        Rom rom = Image((Start, [ScriptCommands.Message, .. At(SaysA), ScriptCommands.End]), (SaysA, text));

        ScriptRun run = ScriptRunner.Run(rom, Start, new ScriptState { PlayerName = "KOOP" });

        Assert.Equal("AAAAAAAAAAAARIVALKOOP", Assert.Single(run.Pages));
    }

    [Fact]
    public void AGapWithNothingBehindItIsLeftExactlyAsFound()
    {
        // The trades fill 0x03 through a special routine, which this project cannot
        // follow. Substituting an empty string would turn "trade it for my {FD}{03}?"
        // into "trade it for my ?" — a sentence that looks like the cartridge's own and
        // is not, which is the one failure everything here is arranged against.
        byte[] text = Gap(0x03);

        Rom rom = Image((Start, [ScriptCommands.Message, .. At(SaysA), ScriptCommands.End]), (SaysA, text));

        Assert.Equal("AAAAAA{FD}{03}", Assert.Single(ScriptRunner.Run(rom, Start).Pages));
    }

    [Fact]
    public void NamingASpeciesFillsTheGapThatFollowsIt()
    {
        // 0x7D sits between a handover and a text box at every gift site. Its first
        // argument picks the gap and the word after it is a species — or a variable
        // holding one, as the starter is, because which ball was pressed is what decides
        // it. The pairing is off by two and the ball script is what says so: it writes
        // buffer 0 and then asks about {FD}{02}.
        byte[] text = Gap(0x02);

        Rom rom = Image(
            (Start,
            [
                0x7D, 0x00, .. Word(4),
                ScriptCommands.Message, .. At(SaysA),
                ScriptCommands.End,
            ]),
            (SaysA, text));

        ScriptRun run = ScriptRunner.Run(
            rom, Start, new ScriptState { NameOfSpecies = species => species == 4 ? "CHARMANDER" : "?" });

        Assert.Equal("AAAAAACHARMANDER", Assert.Single(run.Pages));
    }

    [Fact]
    public void AScriptThatNamesTheRivalSaysSo()
    {
        // The battle screen called him TERRY and his own script called him GREEN. One of
        // those is a placeholder and it is the trainer table's: --rival-fights finds
        // thirty fights picked by scripts that say {FD}{06}, twenty-seven of them wearing
        // that one name, and not one trainer anywhere else in the game wearing it.
        //
        // So the fight and the sentence that names him are the same script, which makes
        // this the cheap half of that instrument and exact.
        Rom rom = Image(
            (Start, [ScriptCommands.Message, .. At(SaysA), ScriptCommands.End]),
            (SaysA, Gap(0x06)));

        Assert.True(ScriptRunner.Run(rom, Start, new ScriptState { RivalName = "GREEN" }).NamesRival);
    }

    [Fact]
    public void AScriptThatNamesNobodyDoesNot()
    {
        Rom rom = Image((Start, Says(SaysA)), (SaysA, Speech('A')));

        Assert.False(ScriptRunner.Run(rom, Start).NamesRival);
    }

    [Fact]
    public void ANumberGoesIntoAGapToo()
    {
        // The professor's aides. Five of them across five maps, each running
        // `83 00 <n> | 80 01 <item>` in front of "complete data on {FD}{02} species...
        // entrusted me with the {FD}{03} for you", with n running ten, twenty, thirty,
        // forty, fifty and the item being the one that same script hands over.
        //
        // This was read as one command of seven for two rounds, and the reason it
        // survived is that at three these five scripts derailed on the 0x80 behind it —
        // one unknown standing behind another.
        byte[] text = Gap(0x02, 0x03);

        Rom rom = Image(
            (Start,
            [
                0x83, 0x00, .. Word(10),
                0x80, 0x01, .. Word(0x0157),
                ScriptCommands.Message, .. At(SaysA),
                ScriptCommands.End,
            ]),
            (SaysA, text));

        ScriptRun run = ScriptRunner.Run(
            rom, Start, new ScriptState { NameOfItem = item => item == 0x0157 ? "HM05" : "?" });

        Assert.Equal("AAAAAAAAAAAA10HM05", Assert.Single(run.Pages));
    }

    [Fact]
    public void ANumberCanComeOutOfAVariable()
    {
        // The professor's own rating: the two commands in front of "{FD}{02} POKeMON
        // seen and {FD}{03} POKeMON owned" copy the counts into 0x8008 and 0x8009, and
        // the two after that name those variables into the two gaps.
        byte[] text = Gap(0x02);

        Rom rom = Image(
            (Start, [0x83, 0x00, .. Word(0x8008), ScriptCommands.Message, .. At(SaysA), ScriptCommands.End]),
            (SaysA, text));

        var save = new ScriptState();
        save.Write(0x8008, 42);

        Assert.Equal("AAAAAA42", Assert.Single(ScriptRunner.Run(rom, Start, save).Pages));
    }

    [Fact]
    public void ASpeciesNamedFromAVariableIsResolvedFirst()
    {
        // The starters are one script. The species is not written down anywhere in it —
        // 0x4002 holds whichever ball was pressed, and the gift and the sentence both
        // read it.
        byte[] text = Gap(0x02);

        Rom rom = Image(
            (Start,
            [
                0x7D, 0x00, .. Word(0x4002),
                ScriptCommands.Message, .. At(SaysA),
                ScriptCommands.End,
            ]),
            (SaysA, text));

        var save = new ScriptState { NameOfSpecies = species => species == 7 ? "SQUIRTLE" : "?" };
        save.Write(0x4002, 7);

        Assert.Equal("AAAAAASQUIRTLE", Assert.Single(ScriptRunner.Run(rom, Start, save).Pages));
    }

    [Fact]
    public void ACallIntoSomethingUnreadableComesBackToo()
    {
        // The naming screen found this. Answering yes to "give a nickname to
        // BULBASAUR?" runs `call 0x081A74EB`, and that address is not script — it is
        // the game's own code, unreadable by anything that decodes bytes as commands.
        // Giving up there threw away the return address, and the return address is
        // where the rival walks over to take his own.
        //
        // 0xEE is a byte with no width, standing in for that code here.
        Rom rom = Image(
            (Start, [ScriptCommands.Call, .. At(Elsewhere), ScriptCommands.Message, .. At(SaysB), ScriptCommands.End]),
            (Elsewhere, [0xEE, 0xEE, 0xEE]),
            (SaysB, Speech('B')));

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Equal(["BBBBBB"], run.Pages);
        Assert.Equal(Elsewhere, Assert.Single(run.CodeCalled));

        // And it is not a stop. The two findings stay apart, because a width we have
        // not adopted and a routine we can never adopt need different answers.
        Assert.Null(run.StoppedAt);
    }

    [Fact]
    public void DerailingWithNothingOnTheStackIsStillAStop()
    {
        // The other half, and the important one. If an unreadable byte at the top level
        // were also forgiven, every width still missing would go quiet — and the whole
        // method of this project is that a script which finishes saying nothing is how
        // a missing width announces itself.
        Rom rom = Image((Start, [ScriptCommands.Message, .. At(SaysA), 0xEE, 0xEE]), (SaysA, Speech('A')));

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Equal((byte)0xEE, run.StoppedAt);
        Assert.Empty(run.CodeCalled);
    }

    [Fact]
    public void ABeatenTrainerSaysWhatComesAfterTheFightInstead()
    {
        // The command is its own conditional. Having beaten them does not skip a branch
        // — it makes the fight do nothing and lets the script carry on to the line they
        // say afterwards. Confirmed on a real image: all fifteen people on Route 8 have
        // a second line and it is a different sentence.
        //
        // By id. The word after the id in this command is not a flag number, whatever
        // else it is — the fixture plants one here and nothing reads it.
        byte[] script =
        [
            ScriptCommands.TrainerBattle, 0x00, .. Word(41), .. Word(0x4F1), .. At(SaysA), .. At(SaysB),
            ScriptCommands.Message, .. At(SaysB), ScriptCommands.End,
        ];

        Rom rom = Image((Start, script), (SaysA, Speech('A')), (SaysB, Speech('B')));

        ScriptRun first = ScriptRunner.Run(rom, Start);

        Assert.Equal(41, first.TrainerId);

        // In its own list rather than among the pages: the line belongs to the fight,
        // and whoever is showing a conversation is not showing a fight.
        Assert.Empty(first.Pages);
        Assert.Equal("AAAAAA", Assert.Single(first.Challenge));

        ScriptRun again = ScriptRunner.Run(rom, Start, new ScriptState(beaten: [41]));

        Assert.Null(again.TrainerId);
        Assert.Equal("BBBBBB", Assert.Single(again.Pages));
    }

    [Fact]
    public void AFightCarriesTheLineTheTrainerOpensWith()
    {
        // Four hundred and fifty trainers on this cartridge, and every one of them used
        // to open with a sentence this project wrote. Theirs is the third argument of
        // the command that starts the fight, next to the one this project has been
        // reading all along.
        byte[] fight =
        [
            ScriptCommands.TrainerBattle, 0x00, .. Word(41), .. Word(0), .. At(SaysA), .. At(SaysB),
            ScriptCommands.End,
        ];

        Rom rom = Image((Start, fight), (SaysA, Speech('A')), (SaysB, Speech('B')));

        Assert.Equal(SaysA, ScriptReader.BeforeTheFight(rom, Start, 41));

        // And nothing for somebody else's fight: the line belongs to one command, not
        // to the script it sits in.
        Assert.Null(ScriptReader.BeforeTheFight(rom, Start, 42));

        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Speech(rom, SaysA)));
    }

    [Fact]
    public void TheVariantWithNoIntroTextIsGivenNoWords()
    {
        // Variant 3 is nine bytes rather than thirteen because it has no intro pointer.
        // Reading one anyway would take whatever follows the command and print it, and a
        // sentence that looks like the cartridge's and is not is the one failure this
        // whole project is arranged against.
        byte[] fight =
        [
            ScriptCommands.TrainerBattle, 0x03, .. Word(41), .. Word(0), .. At(SaysA),
            ScriptCommands.End,
        ];

        Rom rom = Image((Start, fight), (SaysA, Speech('A')));

        Assert.Null(ScriptReader.BeforeTheFight(rom, Start, 41));
    }

    [Fact]
    public void AGymLeadersFightCarriesTheScriptToRunOnWinning()
    {
        // The longer variants of the command carry a pointer past the two every variant
        // has, and at 27 sites on a real image that pointer is script while at 54 it is
        // text. So it is decided by decoding what is there — the same way every text
        // pointer in this project is decided — rather than by variant number.
        //
        // BROCK is the one that matters. His badge, his TM and five flags are all at the
        // end of that pointer, and running his script again from the top instead lands
        // on the line he says on a later visit and hands over nothing.
        byte[] gym =
        [
            ScriptCommands.TrainerBattle, 0x01, .. Word(414), .. Word(0),
            .. At(SaysA), .. At(SaysB), .. At(Elsewhere),
            ScriptCommands.End,
        ];

        Rom rom = Image(
            (Start, gym),
            (SaysA, Speech('A')),
            (SaysB, Speech('B')),
            (Elsewhere, [0x29, .. Word(0x4B0), ScriptCommands.End]));

        Assert.Equal(Elsewhere, ScriptReader.AfterTheFight(rom, Start, 414));

        // And nothing for the trainer who is not there, because the pointer belongs to
        // one fight rather than to the script.
        Assert.Null(ScriptReader.AfterTheFight(rom, Start, 415));
    }

    [Fact]
    public void APointerToWordsIsNotAScriptToRunAfterwards()
    {
        // Fifty-four sites carry text in that slot. Handing one of those to the client
        // as a script would run a sentence as instructions, which is the same failure as
        // a wrong argument width and looks just as much like a game.
        byte[] fight =
        [
            ScriptCommands.TrainerBattle, 0x04, .. Word(41), .. Word(0),
            .. At(SaysA), .. At(SaysB), .. At(SaysB),
            ScriptCommands.End,
        ];

        Rom rom = Image((Start, fight), (SaysA, Speech('A')), (SaysB, Speech('B')));

        Assert.Null(ScriptReader.AfterTheFight(rom, Start, 41));
    }

    [Fact]
    public void OneVariableCanBeCopiedIntoAnother()
    {
        // Winning a gym runs a shared routine that opens `copyvar 0x8000, 0x8008` and
        // then compares 0x8000 against one through eight — the badge number, which the
        // leader's own script wrote into 0x8008 two commands earlier. With this command
        // doing nothing, 0x8000 stayed nought, all eight comparisons failed, and the
        // badge was never reached.
        Rom rom = Image(
            (Start,
            [
                0x16, .. Word(0x8008), .. Word(3),
                0x19, .. Word(0x8000), .. Word(0x8008),
                0x21, .. Word(0x8000), .. Word(3),
                ScriptCommands.GotoIf, 0x01, .. At(Elsewhere),
                ScriptCommands.End,
            ]),
            (Elsewhere, Says(SaysA)),
            (SaysA, Speech('A')));

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Equal("AAAAAA", Assert.Single(run.Pages));
        Assert.Equal(3, run.VariablesWritten.Single(v => v.Key == 0x8000).Value);
    }

    [Fact]
    public void WhatAScriptWritesIsReportedRatherThanApplied()
    {
        // A run has to be repeatable: the client runs one to find out whether there is
        // anything to open a box for, and would otherwise set every flag in it twice.
        // What was written comes back as a list for somebody else to persist.
        Rom rom = Image((Start, [0x29, .. Word(0x2A5), 0x16, .. Word(0x4001), .. Word(3), ScriptCommands.End]));

        var save = new ScriptState();

        ScriptRun run = ScriptRunner.Run(rom, Start, save);

        Assert.Equal([0x2A5], run.FlagsSet);
        Assert.Equal(3, run.VariablesWritten[0x4001]);

        Assert.False(save.Has(0x2A5));
        Assert.Equal([0x2A5], ScriptRunner.Run(rom, Start, save).FlagsSet);
    }

    [Fact]
    public void AVariableDecidesABranchTheSameWayAFlagDoes()
    {
        Rom rom = Image(
            (Start, [0x21, .. Word(0x4001), .. Word(2), ScriptCommands.GotoIf, 0x04, .. At(Elsewhere), .. Says(SaysB)]),
            (Elsewhere, Says(SaysA)),
            (SaysA, Speech('A')),
            (SaysB, Speech('B')));

        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start).Pages));

        var far = new ScriptState(variables: [new KeyValuePair<int, int>(0x4001, 7)]);

        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start, far).Pages));
    }

    [Fact]
    public void AScriptThatLoopsForeverStillComesBack()
    {
        // Real scripts loop — a "which one do you want?" prompt waits for an answer this
        // has no way to give. Following jumps without a budget is a client that hangs on
        // somebody saying hello.
        Rom rom = Image((Start, [ScriptCommands.Goto, .. At(Start)]));

        Assert.True(ScriptRunner.Run(rom, Start).IsEmpty);
    }

    [Fact]
    public void AnUnknownCommandStopsTheRunAndSaysWhich()
    {
        // Per run rather than per script, because a script can now stop somewhere it
        // only reaches on one branch: the same person reads cleanly today and stops
        // tomorrow, and the difference is a flag.
        // 0xEE rather than a byte off the real command set. This test used 0x30, which
        // was unknown when it was written and is five bytes wide now — so the test broke
        // the moment the thing it was standing for got derived. A fixture that borrows a
        // real unknown is a fixture with a fuse in it.
        Rom rom = Image((Start, [ScriptCommands.Message, .. At(SaysA), 0xEE, ScriptCommands.End]), (SaysA, Speech('A')));

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Equal((byte)0xEE, run.StoppedAt);
        Assert.Equal("AAAAAA", Assert.Single(run.Pages));

        // And where, so the bytes around it can be printed. A run follows jumps, so this
        // is almost never inside the script the map named — printing the bytes there
        // shows a handoff, which is what makes an unknown command hard to identify.
        Assert.Equal(Rom.BaseAddress + 5, Rom.BaseAddress + (uint)run.StoppedAtOffset!.Value);
    }
}

/// <summary>
/// The two hundred things standing in the way, and the one question they all ask.
/// <para>
/// A cut tree, a strength boulder and a heap of rock-smash rubble are the same script
/// written three times with a different move id in it. Each opens by asking which party
/// slot knows that move, and the answer — a slot, or six for nobody — decides between
/// two conversations that have nothing in common.
/// </para>
/// </summary>
public class ObstacleTests
{
    private const int Cut = 15;
    private const int Strength = 70;

    private const uint Start = Rom.BaseAddress;
    private const uint CanShift = Rom.BaseAddress + 0x200;
    private const uint Cannot = Rom.BaseAddress + 0x220;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    private static byte[] Speech(char letter) =>
    [
        .. Enumerable.Repeat((byte)(0xBB + (letter - 'A')), 6),
        GameText.Terminator,
    ];

    /// <summary>
    /// The cut script's shape: ask, and go somewhere else if the answer is six.
    /// </summary>
    private static Rom Tree(int moveId)
    {
        var image = new byte[0x400];

        byte[] script =
        [
            0x7C, (byte)moveId, (byte)(moveId >> 8),        // findmove
            0x21, 0x0D, 0x80, 0x06, 0x00,                   // compare 0x800D, 6
            ScriptCommands.GotoIf, 0x01, .. At(Cannot),     // if equal, nobody can
            ScriptCommands.Message, .. At(CanShift),
            ScriptCommands.End,
        ];

        byte[] refusal = [ScriptCommands.Message, .. At(Cannot + 0x10), ScriptCommands.End];

        script.CopyTo(image, 0);
        refusal.CopyTo(image, (int)(Cannot - Rom.BaseAddress));

        Speech('A').CopyTo(image, (int)(CanShift - Rom.BaseAddress));
        Speech('B').CopyTo(image, (int)(Cannot + 0x10 - Rom.BaseAddress));

        return new Rom(image);
    }

    private static ScriptState Party(params int[][] moves) =>
        new(partyMoves: moves.Select(m => (IReadOnlyList<int>)m));

    [Fact]
    public void TheFirstSlotThatKnowsItIsTheAnswer()
    {
        // The first, because the games use the answer to decide who steps forward and
        // there is only room for one to.
        Assert.Equal(1, Party([1, 2], [3, Cut]).SlotKnowing(Cut));
        Assert.Equal(0, Party([Cut], [Cut]).SlotKnowing(Cut));
    }

    [Fact]
    public void SixMeansNobody()
    {
        // Six because a party has six slots, so the sixth index is the first that cannot
        // be a member. Read off the cartridge rather than chosen: every one of these
        // scripts compares against exactly this and branches to "nobody can do that".
        Assert.Equal(ScriptState.NoSlot, Party([1, 2], [3, 4]).SlotKnowing(Cut));
        Assert.Equal(ScriptState.NoSlot, Party().SlotKnowing(Cut));

        // And an empty party does not accidentally know move zero, which is what a bare
        // "is this in the list" would say about six empty move slots.
        Assert.Equal(ScriptState.NoSlot, Party([0, 0, 0, 0]).SlotKnowing(0));
    }

    [Fact]
    public void ARunWithoutAPartyIsNotARunWithAWillingParty()
    {
        // The whole reason the party has to reach the runner. Left unanswered, the
        // variable reads zero — "the first one in your party can do it" — and every
        // obstacle in the game offers to move itself for a player with nothing.
        Rom rom = Tree(Cut);

        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start).Pages));
        Assert.Equal("BBBBBB", Assert.Single(ScriptRunner.Run(rom, Start, Party([1, 2])).Pages));
        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(rom, Start, Party([1, Cut])).Pages));
    }

    [Fact]
    public void WhatShiftsItIsReportedWhicheverWayTheScriptGoes()
    {
        // Both arms, because what a thing *is* does not depend on whether this player
        // can get at it. Every rock-smash rock in the game sits behind a badge check,
        // and an exporter that only believed the arm a fresh save takes would have found
        // none of them.
        Assert.Equal(Cut, ScriptRunner.Run(Tree(Cut), Start).ShiftedBy);
        Assert.Equal(Cut, ScriptRunner.Run(Tree(Cut), Start, Party([Cut])).ShiftedBy);
        Assert.Equal(Strength, ScriptRunner.Run(Tree(Strength), Start).ShiftedBy);
    }
}

/// <summary>
/// The movement lists a cutscene is made of, and the one thing about them that can be
/// checked without a screen: where the steps stop.
/// </summary>
public class MovementListTests
{
    private static Rom Image(params (uint Address, byte[] Bytes)[] chunks)
    {
        var image = new byte[0x400];

        foreach ((uint address, byte[] bytes) in chunks)
            bytes.CopyTo(image, (int)(address - Rom.BaseAddress));

        return new Rom(image);
    }

    [Fact]
    public void AListEndsAtTheTerminatorAndNotAtTheNextOne()
    {
        // The lists sit packed one after another with nothing between them, so a reader
        // that does not stop at 0xFE reads the next scene's steps as part of this one —
        // and a cutscene that walks somebody twice as far as it should ends with them
        // standing inside a building.
        Rom rom = Image((Rom.BaseAddress, [0x11, 0x11, 0x11, MovementLists.End, 0x10, 0x10, MovementLists.End]));

        Assert.Equal(new byte[] { 0x11, 0x11, 0x11 }, MovementLists.Read(rom, Rom.BaseAddress));
        Assert.Equal(new byte[] { 0x10, 0x10 }, MovementLists.Read(rom, Rom.BaseAddress + 4));
    }

    [Fact]
    public void SomethingWithNoTerminatorIsNotAList()
    {
        // applymovement's second argument is only a pointer; nothing says it leads to a
        // list. Returning whatever is there would put sixty-four bytes of somebody
        // else's data into a scene.
        var image = new byte[0x400];
        Array.Fill(image, (byte)0x11);

        Assert.Empty(MovementLists.Read(new Rom(image), Rom.BaseAddress));
    }

    [Fact]
    public void AnEmptyListIsStillAList()
    {
        // A terminator on its own. Ordinary — a scene that applies one to somebody who
        // is already where they should be.
        Assert.Empty(MovementLists.Read(Image((Rom.BaseAddress, [MovementLists.End])), Rom.BaseAddress));
    }
}

/// <summary>
/// The calls into the game's own code — the one boundary in this project that reading
/// harder does not cross.
/// </summary>
public class SpecialCallTests
{
    private const uint Start = Rom.BaseAddress;
    private const uint Elsewhere = Rom.BaseAddress + 0x100;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    private static Rom Image(byte[] script)
    {
        var image = new byte[0x400];

        script.CopyTo(image, 0);
        new byte[] { ScriptCommands.End }.CopyTo(image, (int)(Elsewhere - Rom.BaseAddress));

        return new Rom(image);
    }

    [Fact]
    public void ARunRecordsWhatItCouldNotAsk()
    {
        // The difference between "this person has nothing to say" and "this person asked
        // something we cannot ask". Both look identical from outside, and one of them is
        // this project's fault while the other is a boundary.
        Rom rom = Image([0x25, 0x87, 0x01, 0x26, 0x0D, 0x80, 0xAB, 0x00, ScriptCommands.End]);

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Equal([0x0187, 0x00AB], run.SpecialsCalled);
    }

    [Fact]
    public void SteppingOverOneStillLeavesAZeroBehind()
    {
        // The part that is not harmless. Nothing answers, so the answer variable keeps
        // its zero — and at 174 sites on a real cartridge the script reads that zero and
        // branches away from whatever it was about to do.
        //
        // This fixture is that shape: ask, compare the answer against zero, and jump if
        // equal. The run takes the jump, and takes it for a reason that is not an answer.
        Rom rom = Image(
        [
            0x26, 0x0D, 0x80, 0xAB, 0x00,               // specialvar 0x800D, 0x00AB
            0x21, 0x0D, 0x80, 0x00, 0x00,               // compare 0x800D, 0
            ScriptCommands.GotoIf, 0x01, .. At(Elsewhere),
            ScriptCommands.Message, .. At(Rom.BaseAddress + 0x200),
            ScriptCommands.End,
        ]);

        ScriptRun run = ScriptRunner.Run(rom, Start);

        Assert.Empty(run.Pages);
        Assert.Equal(0x00AB, Assert.Single(run.SpecialsCalled));
    }
}

/// <summary>
/// Reading a routine from what the script does about each answer.
/// <para>
/// What a special does cannot be read. What the two arms after it say can be, and those
/// are the cartridge's own words — which is evidence in a way that recalling another game
/// is not. On a real image the arms after 0x0174 differ by exactly one word, "bud" and
/// "lady", which says what the routine distinguishes without anybody having to remember.
/// </para>
/// </summary>
public class SpecialBranchTests
{
    private const uint Start = Rom.BaseAddress;
    private const uint Yes = Rom.BaseAddress + 0x100;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    [Fact]
    public void OnlyTheVeryNextCommandCounts()
    {
        // A looser rule finds forks near a command rather than forks about it. Swept over
        // every opcode it ranked `compare` and `goto_if` themselves among the best
        // evidenced answerers in the game, which is retraction 2 wearing a different hat.
        //
        // Here: a call, then something in between, then a compare. The compare is not
        // about the call, and a reader that says it is will name the wrong thing.
        var image = new byte[0x400];

        byte[] script =
        [
            0x25, 0x74, 0x01,                               // special 0x0174
            0x6B,                                           // faceplayer — anything at all
            0x21, 0x0D, 0x80, 0x00, 0x00,                   // compare 0x800D, 0
            ScriptCommands.GotoIf, 0x01, .. At(Yes),
            ScriptCommands.End,
        ];

        script.CopyTo(image, 0);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Start);

        // The gap is the whole point: one command between the call and the compare is
        // enough for the compare to be about something else.
        Assert.Equal(0x6B, commands[1].Code);
        Assert.Equal(0x21, commands[2].Code);
    }

    [Fact]
    public void AnAnswerBelongsToWhoeverAnsweredLast()
    {
        // The mistake this guard exists for, and it cost a wrong claim in a commit
        // message. In BILL's house the call to 0x0174 is followed immediately by 0xA0 —
        // which answers into the result variable itself — and only then by the compare.
        // Reading forward without stopping credits 0x0174 with 0xA0's reply, and the two
        // arms of that fork say "bud" and "lady", so the wrong routine gets identified
        // from perfectly good evidence.
        var image = new byte[0x400];

        byte[] script =
        [
            0x25, 0x74, 0x01,                               // special 0x0174
            0xA0,                                           // answers into 0x800D itself
            0x21, 0x0D, 0x80, 0x00, 0x00,                   // compare 0x800D, 0
            ScriptCommands.GotoIf, 0x01, .. At(Yes),
            ScriptCommands.End,
        ];

        script.CopyTo(image, 0);
        new byte[] { ScriptCommands.End }.CopyTo(image, (int)(Yes - Rom.BaseAddress));

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Start);

        // Both are there to be read; what matters is which one the compare is about, and
        // it is the nearer.
        Assert.Equal(0x25, commands[0].Code);
        Assert.Equal(0xA0, commands[1].Code);
        Assert.Equal(0x21, commands[2].Code);
    }

    [Fact]
    public void BothArmsOfTheAnswerAreFound()
    {
        var image = new byte[0x400];

        byte[] script =
        [
            0x25, 0x74, 0x01,                               // special 0x0174
            0x21, 0x0D, 0x80, 0x01, 0x00,                   // compare 0x800D, 1
            ScriptCommands.GotoIf, 0x01, .. At(Yes),        // if equal, the other arm
            ScriptCommands.End,
        ];

        script.CopyTo(image, 0);
        new byte[] { ScriptCommands.End }.CopyTo(image, (int)(Yes - Rom.BaseAddress));

        // Not through a map: this is about the shape of the fork, and a fixture map would
        // only be a longer way to write the same four commands.
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Start);

        Assert.Equal(
            new byte[] { 0x25, 0x21, ScriptCommands.GotoIf, ScriptCommands.End },
            commands.Select(c => c.Code));

        // The arm taken when the answer matches, and the one taken when it does not —
        // the second being simply where the read carries on.
        Assert.Equal(Yes, commands[2].Pointer(1));
        // 3 + 5 + 6 bytes of command in front of it, counted rather than eyeballed.
        Assert.Equal(Start + 14, Rom.BaseAddress + (uint)commands[3].Offset);
    }
}

/// <summary>
/// Which of the two sets of words a character reads.
/// <para>
/// Command 0xA0 takes nothing and answers into the result variable, and the arms after
/// it are the cartridge's own words at every site: "Waiter"/"Waitress", "little
/// brother"/"little sister", "All boys leave home someday"/"All girls dream of
/// traveling", "dear boy"/"dear girl". Seven scripts on six maps, agreeing.
/// </para>
/// <para>
/// The first identification in this project of a command by what the script says about
/// its answer, and it took two retracted attempts to get the instrument honest enough to
/// make it: one that counted branches without judging them, and one that credited a
/// special with this command's reply.
/// </para>
/// </summary>
public class PlayerGenderTests
{
    private const uint Start = Rom.BaseAddress;
    private const uint Girl = Rom.BaseAddress + 0x100;
    private const uint BoyText = Rom.BaseAddress + 0x200;
    private const uint GirlText = Rom.BaseAddress + 0x220;

    private static byte[] At(uint address) =>
        [(byte)address, (byte)(address >> 8), (byte)(address >> 16), (byte)(address >> 24)];

    private static byte[] Speech(char letter) =>
    [
        .. Enumerable.Repeat((byte)(0xBB + (letter - 'A')), 6),
        GameText.Terminator,
    ];

    /// <summary>The shape every one of these forks has on a real image.</summary>
    private static Rom Fork()
    {
        var image = new byte[0x400];

        byte[] script =
        [
            0xA0,                                           // playergender
            0x21, 0x0D, 0x80, 0x01, 0x00,                   // compare 0x800D, 1
            ScriptCommands.GotoIf, 0x01, .. At(Girl),
            ScriptCommands.Message, .. At(BoyText),
            ScriptCommands.End,
        ];

        script.CopyTo(image, 0);

        byte[] otherArm = [ScriptCommands.Message, .. At(GirlText), ScriptCommands.End];

        otherArm.CopyTo(image, (int)(Girl - Rom.BaseAddress));

        Speech('A').CopyTo(image, (int)(BoyText - Rom.BaseAddress));
        Speech('B').CopyTo(image, (int)(GirlText - Rom.BaseAddress));

        return new Rom(image);
    }

    [Fact]
    public void TheZeroArmIsTheOneThatSaysBoy()
    {
        // Unwritten reads as zero, which is why this has been the boy's line since
        // before anybody knew the command existed.
        Assert.Equal("AAAAAA", Assert.Single(ScriptRunner.Run(Fork(), Start).Pages));
    }

    [Fact]
    public void ACharacterWhoSaysSoGetsTheOtherArm()
    {
        Assert.Equal(
            "BBBBBB",
            Assert.Single(ScriptRunner.Run(Fork(), Start, new ScriptState { IsGirl = true }).Pages));
    }

    [Fact]
    public void ItSurvivesBeingCopiedAndGivenAParty()
    {
        // Both of those make a new state, and a run copies before it walks. A field that
        // did not survive either would read as a boy on every script in the game while
        // testing green on the one above.
        var state = new ScriptState { IsGirl = true };

        Assert.True(state.Copy().IsGirl);
        Assert.True(state.WithParty([]).IsGirl);
    }
}

/// <summary>
/// The pair that was stopping the opening of the game.
/// <para>
/// The professor's scene runs, walks you to his lab, and then hits 0xAC. Past it is
/// 0xAD, which has the same shape, and past that the bookkeeping that spends the trigger.
/// </para>
/// <para>
/// Two sites each, which is thin by this project's standard. What makes them worth
/// adopting is that the width and the command after it are confirmed by the same bytes,
/// that the two commands are identical in shape to each other, and that the wrongness
/// detector did not move — while twenty-six pages of dialogue appeared.
/// </para>
/// </summary>
public class TheOpeningsPairTests
{
    [Fact]
    public void BothTakeTwoWordsAndTheOneBetweenThemTakesNothing()
    {
        Assert.Equal(4, ScriptCommands.ArgumentLength(0xAC));
        Assert.Equal(4, ScriptCommands.ArgumentLength(0xAD));
        Assert.Equal(0, ScriptCommands.ArgumentLength(0xAE));
    }

    [Fact]
    public void ReadThatWayTheSceneCarriesOnIntoItsOwnBookkeeping()
    {
        // Byte for byte from 0x08165694 on a real image: the tail of the scene, where it
        // writes the variable that stops it happening a second time. Read one byte
        // narrower and that write is never reached.
        var image = new byte[0x400];

        byte[] script =
        [
            0xAD, 0x10, 0x00, 0x0D, 0x00,
            0xAE,
            0x16, 0x55, 0x40, 0x01, 0x00,   // setvar 0x4055, 1
            0x2A, 0x2B, 0x00,               // clearflag 0x002B
            ScriptCommands.End,
        ];

        script.CopyTo(image, 0);

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress);

        Assert.Equal(
            new byte[] { 0xAD, 0xAE, 0x16, 0x2A, ScriptCommands.End },
            commands.Select(c => c.Code));

        // The write itself, which is the whole point of reading this far.
        Assert.Equal(0x4055, commands[2].Word());
        Assert.Equal(1, commands[2].Word(2));
    }

}

/// <summary>
/// The three commands standing between a player and the S.S. ANNE.
/// <para>
/// The machine in BILL's cottage is a sign, and its script is what sets the flag that
/// makes him hand over the ticket. Every byte here is taken from a real image, at
/// 0x081706FA and 0x08160C30, because the shapes are the evidence: what matters is not
/// that these widths are in the table but that reading with them reaches the flag, and
/// that reading with the old ones did not.
/// </para>
/// </summary>
public class TheMachineTests
{
    private static List<ScriptCommand> Read(params byte[] script)
    {
        var image = new byte[0x400];

        script.CopyTo(image, 0);

        return ScriptReader.Read(new Rom(image), Rom.BaseAddress);
    }

    [Fact]
    public void TheSeparatorReachesTheFlagItSets()
    {
        // 0x081706FA, up to the flag. Before 0x37 had a width the read stopped on the
        // first byte, so the machine set nothing, so BILL never had anything to thank
        // anybody for — and nothing about that failed. The sign just said its line.
        List<ScriptCommand> commands = Read(
            0x37, 0x00,                             // the unknown one, one byte wide
            0x0F, 0x00, 0x2D, 0x04, 0x1A, 0x08,     // loadpointer
            0x09, 0x04,                             // callstd 4
            0x68,                                   // closemessage
            0x2A, 0x02, 0x00,                       // clearflag 0x0002
            0x29, 0x33, 0x02,                       // setflag 0x0233
            ScriptCommands.End);

        Assert.Equal(
            new byte[] { 0x37, 0x0F, 0x09, 0x68, 0x2A, 0x29, ScriptCommands.End },
            commands.Select(c => c.Code));

        Assert.Equal(0x0233, commands[5].Word());
    }

    [Fact]
    public void WaitingForASoundTakesNothingAndTheCommandAfterItIsReal()
    {
        // 0x08160C30. Read with five bytes here — which is what this project believed
        // for two milestones — the whole of the next command is swallowed, and what
        // follows is read from the middle of it.
        List<ScriptCommand> commands = Read(
            0x6A,                                   // lock
            0x5A,                                   // faceplayer2
            0x30,                                   // wait for the sound: nothing
            0xA1, 0x28,                             // one byte, not five
            0x00, 0x00, 0x00,                       // three nops the old width ate
            0x0F, 0x00, 0x61, 0x3B, 0x17, 0x08,     // loadpointer
            0x09, 0x04,                             // callstd 4
            ScriptCommands.End);

        Assert.Equal(
            new byte[]
            {
                0x6A, 0x5A, 0x30, 0xA1, 0x00, 0x00, 0x00, 0x0F, 0x09, ScriptCommands.End,
            },
            commands.Select(c => c.Code));
    }

    [Fact]
    public void APageIsAlwaysLoadedBeforeTheRoutineThatPrintsIt()
    {
        // The habit the widths above were decided against, stated as a rule so it is
        // written down somewhere other than a comment: of the 1202 calls to standard
        // routine 4 in every script this cartridge's maps can reach, 1202 have a page
        // loaded first and none do not. A width that leaves one of these standing alone
        // has eaten the words.
        List<ScriptCommand> commands = Read(
            0x0F, 0x00, 0x2D, 0x04, 0x1A, 0x08,
            0x09, 0x04,
            ScriptCommands.End);

        int printing = commands.FindIndex(c => c.Code == ScriptCommands.CallStandard);

        Assert.True(printing > 0);
        Assert.Equal(ScriptCommands.LoadPointer, commands[printing - 1].Code);
    }
}
