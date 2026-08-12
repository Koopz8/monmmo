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
        Assert.Equal("AAAAAA", Assert.Single(first.Pages));

        ScriptRun again = ScriptRunner.Run(rom, Start, new ScriptState(beaten: [41]));

        Assert.Null(again.TrainerId);
        Assert.Equal("BBBBBB", Assert.Single(again.Pages));
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
