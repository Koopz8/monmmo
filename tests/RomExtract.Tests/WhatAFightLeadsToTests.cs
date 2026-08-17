using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The instrument that settled which of a <c>trainerbattle</c>'s two exits is the one.
/// <para>
/// It sorts the bytes after each command into three shapes, and the whole argument rests on
/// telling a guard from a line: a line after the command is a shape both readings agree
/// about, and a guard is not. So the discrimination this file has to make is exactly that
/// one, on bytes rather than on a stand-in — and it has to be able to come back saying
/// "nothing of this kind skips a guard", because that is the answer for six of the eight
/// kinds in this cartridge and an instrument that cannot say it is not measuring.
/// </para>
/// </summary>
public class WhatAFightLeadsToTests
{
    private const byte TrainerBattle = 0x5C;
    private const byte LoadPointer = 0x0F;
    private const byte CallStandard = 0x09;
    private const byte CheckFlag = 0x2B;
    private const byte GotoIf = 0x06;
    private const byte Goto = 0x05;
    private const byte SetFlag = 0x29;
    private const byte Release = 0x6C;
    private const byte End = 0x02;

    private static void Put(byte[] image, int at, params byte[] bytes) => bytes.CopyTo(image, at);

    private static void Pointer(byte[] image, int at, uint address)
    {
        for (int i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    [Fact]
    public void AConditionalReadsAsAGuard()
    {
        var image = new byte[0x1000];

        Put(image, 0x200, CheckFlag, 0x54, 0x02);
        Put(image, 0x203, GotoIf, 0x00);
        Pointer(image, 0x205, 0x08000300);

        Assert.Equal(WhatFollows.AGuard, WhatAFightLeadsTo.Reads(new Rom(image), 0x08000200));
    }

    [Fact]
    public void SomethingSaidAndThenAnEndReadsAsALine()
    {
        var image = new byte[0x1000];

        Put(image, 0x200, LoadPointer, 0x00);
        Pointer(image, 0x202, 0x08000400);
        Put(image, 0x206, CallStandard, 0x04, Release, End);

        Assert.Equal(WhatFollows.JustALine, WhatAFightLeadsTo.Reads(new Rom(image), 0x08000200));
    }

    [Fact]
    public void AnEndOnItsOwnReadsAsNothingAtAll()
    {
        var image = new byte[0x1000];

        Put(image, 0x200, Release, End);

        Assert.Equal(WhatFollows.NothingAtAll, WhatAFightLeadsTo.Reads(new Rom(image), 0x08000200));
    }

    /// <summary>
    /// And bytes that are not commands say so rather than being sorted into one of the
    /// three. A misread that lands on a plausible shape is how a column gets invented.
    /// </summary>
    [Fact]
    public void BytesWithNoWidthReadAsNotCommands()
    {
        var image = new byte[0x1000];

        Put(image, 0x200, 0xFE, 0xFE, 0xFE, 0xFE);

        Assert.Equal(WhatFollows.NotCommands, WhatAFightLeadsTo.Reads(new Rom(image), 0x08000200));
    }

    /// <summary>
    /// A gym leader's shape: the command, a guard after it, and a continuation that ends
    /// somewhere else entirely. Both exits are reported and they do not meet.
    /// </summary>
    private static byte[] TheGymShape()
    {
        var image = new byte[0x2000];

        Put(image, 0x100, TrainerBattle, 1, 0x9E, 0x01, 0x00, 0x00);
        Pointer(image, 0x106, 0x08000400);
        Pointer(image, 0x10A, 0x08000400);
        Pointer(image, 0x10E, 0x08000600);

        Put(image, 0x112, CheckFlag, 0x54, 0x02);
        Put(image, 0x115, GotoIf, 0x00);
        Pointer(image, 0x117, 0x08000700);
        Put(image, 0x11B, Release, End);

        Put(image, 0x400, 0xFE, 0xFE, 0xFE, 0xFE);
        Put(image, 0x600, SetFlag, 0x2E, 0x00, Release, End);
        Put(image, 0x700, SetFlag, 0x54, 0x02, Release, End);

        return image;
    }

    private static AFight Only(byte[] image) =>
        Assert.Single(WhatAFightLeadsTo.In(
            new Rom(image), [new SetsAFlag("1.0", "person 1", 0x08000100)]));

    [Fact]
    public void BothExitsAreReported()
    {
        AFight fight = Only(TheGymShape());

        Assert.Equal((byte)1, fight.Variant);
        Assert.Equal(0x019E, fight.TrainerId);
        Assert.Equal(0x08000112u, fight.After);
        Assert.Equal(0x08000600u, fight.Jump);
        Assert.Equal(WhatFollows.AGuard, fight.Follows);
        Assert.False(fight.JumpRejoins);
    }

    /// <summary>
    /// AND THE ANSWER THAT MEANS THERE IS NOTHING HERE. A continuation that comes back to
    /// the bytes after the command is not skipping them, whatever is in them — so the
    /// instrument has to say so rather than counting it. Six of this cartridge's eight
    /// kinds come back empty on this question and the reading rests on that being real.
    /// </summary>
    [Fact]
    public void AContinuationThatComesBackIsNotSkippingAnything()
    {
        byte[] image = TheGymShape();

        // The continuation ends by rejoining the fall-through instead of stopping.
        Put(image, 0x600, SetFlag, 0x2E, 0x00, Goto);
        Pointer(image, 0x604, 0x08000112);

        Assert.True(Only(image).JumpRejoins);
    }

    /// <summary>
    /// And a fight whose pointers are all text has no second exit at all, so the question
    /// does not arise — which is what 385 of this cartridge's 729 sites look like.
    /// </summary>
    [Fact]
    public void AFightWithNoScriptPointerHasOnlyOneExit()
    {
        byte[] image = TheGymShape();

        Pointer(image, 0x10E, 0x08000400);

        Assert.Equal(0u, Only(image).Jump);
    }
}
