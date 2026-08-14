using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The scripts a fight leads to.
/// <para>
/// A <c>trainerbattle</c> carries between one and four pointers, and until now nothing
/// followed any of them. Two of the three flags hiding the middle of this game are
/// cleared inside one — which is why a walk over every person, sign, trigger and entry
/// script in the world reached neither.
/// </para>
/// <para>
/// Some of those pointers are lines the trainer says and some are scripts to run once
/// the fight is over, and nothing in the command says which. So it is not asked: each
/// one is read as a script and kept only if it reads like one — a run of commands ending
/// at an <c>end</c>, a <c>return</c> or a <c>goto</c>. Text decoded as commands runs into
/// a byte with no length and stops, which is exactly the test.
/// </para>
/// </summary>
public class AfterAFightTests
{
    private const byte TrainerBattle = 0x5C;
    private const byte End = 0x02;
    private const byte Release = 0x6C;
    private const byte SetFlag = 0x29;

    /// <summary>The variant with three pointers, which is the one the hideout uses.</summary>
    private const byte ThreePointers = 2;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>
    /// A fight with three pointers: two at text, one at a script that sets a flag.
    /// </summary>
    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // The fight itself, at 0x100.
        Put(image, 0x100, TrainerBattle, ThreePointers, 0x70, 0x01, 0x00, 0x00);
        Pointer(image, 0x106, 0x08000400);   // text
        Pointer(image, 0x10A, 0x08000500);   // text
        Pointer(image, 0x10E, 0x08000600);   // the script afterwards
        Put(image, 0x112, Release, End);

        // Two pages of something that is not script. 0x97 has no known length, which is
        // what a page of text looks like to a reader of commands.
        Put(image, 0x400, 0x97, 0x97, 0x97, 0x97);
        Put(image, 0x500, 0x97, 0x97, 0x97, 0x97);

        // And the script the fight leads to.
        Put(image, 0x600, SetFlag, 0x36, 0x00, End);

        return image;
    }

    [Fact]
    public void TheScriptAFightLeadsToIsFollowed()
    {
        var rom = new Rom(Image());

        List<ScriptCommand> all = ScriptReader.ReadAll(rom, 0x08000100);

        Assert.Contains(all, c => c.Code == SetFlag && c.Arguments[0] == 0x36);
    }

    /// <summary>
    /// And the pointers that are text are not followed, because reading them as commands
    /// runs into a byte with no length. Without this the reader would walk into every
    /// line of dialogue in the game and report it as script.
    /// </summary>
    [Fact]
    public void ThePointersThatAreTextAreLeftAlone()
    {
        var rom = new Rom(Image());

        List<ScriptCommand> fight = ScriptReader.Read(rom, 0x08000100);

        List<uint> followed = [.. ScriptReader.ScriptsAfterAFight(rom, fight[0])];

        Assert.Equal([0x08000600u], followed);
    }

    /// <summary>
    /// A pointer going nowhere on this image is not followed either. Argument bytes look
    /// like addresses all the time.
    /// </summary>
    [Fact]
    public void APointerOffTheImageIsNotFollowed()
    {
        byte[] image = Image();

        Pointer(image, 0x10E, 0x0F000000);

        var rom = new Rom(image);

        Assert.Empty(ScriptReader.ScriptsAfterAFight(rom, ScriptReader.Read(rom, 0x08000100)[0]));
    }

    /// <summary>Anything that is not a fight has no scripts after it.</summary>
    [Fact]
    public void NothingElseCarriesScriptsAfterAFight()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, SetFlag, 0x36, 0x00, End);

        var rom = new Rom(image);

        Assert.Empty(ScriptReader.ScriptsAfterAFight(rom, ScriptReader.Read(rom, 0x08000100)[0]));
    }

    /// <summary>
    /// And the read still ends. A fight whose pointers loop back on themselves would
    /// otherwise be read for ever.
    /// </summary>
    [Fact]
    public void AFightPointingAtItselfIsReadOnce()
    {
        byte[] image = Image();

        Pointer(image, 0x10E, 0x08000100);

        var rom = new Rom(image);

        Assert.NotEmpty(ScriptReader.ReadAll(rom, 0x08000100));
    }
}
