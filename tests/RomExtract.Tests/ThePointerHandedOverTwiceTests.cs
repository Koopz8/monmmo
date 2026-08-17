using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A pointer handed to two commands in a row, and a control that says the pairing is real.
/// <para>
/// <c>0xD3</c> has two sites and both are a repeating column:
/// <c>16 06 80 00 00 | 78 &lt;ptr&gt; | D3 &lt;the same ptr&gt; | 04 &lt;ptr&gt;</c>, three times over
/// in thirty-three bytes at each. <c>0x78</c> is already known to take four, so the same address
/// appears twice inside ten bytes — which does not line up at any other width.
/// </para>
/// <para>
/// <b>Measured against a control, because "the same value twice" is exactly the kind of claim
/// that feels decisive and is not.</b> Across the whole 16 MiB the shape
/// <c>78 &lt;4&gt; D3 &lt;4&gt;</c> occurs 73 times with the two values identical in 22 of them —
/// 30.1%. The same shape with <c>0x77</c>, <c>0x79</c>, <c>0x04</c> or <c>0x05</c> in that slot
/// occurs 1400 times between them and is identical <b>once</b>.
/// </para>
/// </summary>
public class ThePointerHandedOverTwiceTests
{
    /// <summary>Two sites, six repetitions, and a 30% pairing against a 0.2% control.</summary>
    private const byte TakesTheSamePointer = 0xD3;

    /// <summary>The one in front of it, already known to take four.</summary>
    private const byte HandsItOverFirst = 0x78;

    private const byte SetFlag = 0x29;

    private const byte End = 0x02;

    /// <summary>The wall, doubling as the flag — see milestone 203 for why.</summary>
    private const int BehindIt = 0x00E6;

    private static byte[] Image()
    {
        var image = new byte[0x2000];

        // The cartridge's own shape: the pointer handed over twice, then the flag behind it.
        byte[] block =
        [
            HandsItOverFirst, 0x00, 0x03, 0x00, 0x08,
            TakesTheSamePointer, 0x00, 0x03, 0x00, 0x08,
            SetFlag, BehindIt & 0xFF, BehindIt >> 8,
            End,
        ];

        block.CopyTo(image, 0x100);

        // Something for the pointer to point at, so it is an address rather than a number.
        byte[] target = [SetFlag, 0x77, 0x00, End];

        target.CopyTo(image, 0x300);

        return image;
    }

    [Fact]
    public void TheCommandHandedAPointerIsFiveBytes()
    {
        Assert.Equal(4, ScriptCommands.ArgumentLength(TakesTheSamePointer));

        (IReadOnlyCollection<int> on, IReadOnlyCollection<int> _) = WhatItIsWaitingFor.Touches(
            new Rom(Image()), [new SetsAFlag("1.1", "on arrival (0x4001 == 0)", 0x08000100)]);

        Assert.Contains(BehindIt, on);
    }

    /// <summary>
    /// And the one in front of it has not moved. The whole argument for this width is that the
    /// two commands carry the same four bytes, which says nothing if the first one's width is
    /// not what it was when that was measured.
    /// </summary>
    [Fact]
    public void TheCommandInFrontStillTakesFour()
    {
        Assert.Equal(4, ScriptCommands.ArgumentLength(HandsItOverFirst));
    }
}
