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
