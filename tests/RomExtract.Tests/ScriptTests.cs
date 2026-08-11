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
