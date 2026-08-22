using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which operands name the move the script they sit in is about (290).
/// <para>
/// <b><c>0x82</c>'s word is a MOVE ID.</b> It takes seven values on this cartridge and every one
/// is a named move — ICE BEAM, IRON TAIL, THUNDERBOLT, SHADOW BALL and FLAMETHROWER in one run of
/// five, CUT and ROCK SMASH in two of the three obstacle scripts. Seven values inside a 355-wide
/// table is worth little on its own, which is why 238 wrote <i>two of two is not a column, do not
/// build on it</i>.
/// </para>
/// <para>
/// The floor says otherwise: of the <b>32 operand positions</b> that appear inside the three
/// scripts which ask who knows a move, <c>0x82 arg1</c> is the ONLY one that ever names that
/// script's own move — twice out of twice, for two different moves. The asking command itself is
/// the only other, and it is itself by construction.
/// </para>
/// </summary>
public sealed class WhatElseNamesTheMoveTests
{
    private const byte Asks = ObstacleMoves.FindMove;   // 0x7C, a word
    private const byte ByteThenWord = 0x82;             // a byte, then a word
    private const byte End = 0x02;

    private const int Cut = 15;
    private const int RockSmash = 249;

    private static uint Where(int at) => Rom.BaseAddress + (uint)at;

    /// <summary>An image with one script at 0x200, built from the bytes given.</summary>
    private static Rom Image(params byte[] script)
    {
        var image = new byte[0x1000];

        script.CopyTo(image, 0x200);

        return new Rom(image);
    }

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    private static byte[] Asking(int move) => [Asks, .. Word(move)];

    private static byte[] Naming(int move) => [ByteThenWord, 1, .. Word(move)];

    private static NamesItsOwnMove? Of(Rom rom, byte code, int at) =>
        WhatElseNamesTheMove.In(rom, [Where(0x200)])
            .FirstOrDefault(o => o.Code == code && o.At == at);

    /// <summary>
    /// <b>THE THING.</b> A script that asks who knows CUT and then holds CUT's own id in another
    /// command's word: that operand names the script's own move.
    /// </summary>
    [Fact]
    public void AnOperandHoldingTheScriptsOwnMoveIsCounted()
    {
        Rom rom = Image([.. Asking(Cut), .. Naming(Cut), End]);

        NamesItsOwnMove? found = Of(rom, ByteThenWord, 1);

        Assert.NotNull(found);
        Assert.Equal(1, found.Places);
        Assert.Equal(1, found.Matches);
    }

    /// <summary>
    /// <b>AND THE WORD STARTS AT BYTE ONE.</b> <c>0x82</c> is a byte then a word, so an operand
    /// sweep stepping in halfwords reads <c>0x0F01</c> where the cartridge has 15 — and reports
    /// NOUGHT matches, which is exactly what "not a column" looks like. The first version of this
    /// instrument did that and agreed with 238's guess for the wrong reason.
    /// </summary>
    [Fact]
    public void TheWordIsFoundAtByteOneAndNotAtByteNought()
    {
        Rom rom = Image([.. Asking(Cut), .. Naming(Cut), End]);

        Assert.Equal(1, Of(rom, ByteThenWord, 1)?.Matches);

        // Byte nought of the same command is the 1 and the low half of the move: 0x0F01, which is
        // not the move and must not be counted as it.
        Assert.Equal(0, Of(rom, ByteThenWord, 0)?.Matches);
    }

    /// <summary>
    /// An operand holding some OTHER move is a place and not a match — the denominator is what
    /// makes "one of thirty-two" mean anything.
    /// </summary>
    [Fact]
    public void AnOperandHoldingAnotherMoveIsAPlaceAndNotAMatch()
    {
        Rom rom = Image([.. Asking(Cut), .. Naming(RockSmash), End]);

        NamesItsOwnMove? found = Of(rom, ByteThenWord, 1);

        Assert.NotNull(found);
        Assert.Equal(1, found.Places);
        Assert.Equal(0, found.Matches);
    }

    /// <summary>
    /// The asking command names its own move by construction, and the reading says so rather than
    /// counting it as evidence. Without it in the list a reader cannot see that it is excluded.
    /// </summary>
    [Fact]
    public void TheAskingCommandNamesItsOwnMoveByConstruction()
    {
        Rom rom = Image([.. Asking(Cut), End]);

        NamesItsOwnMove? found = Of(rom, Asks, 0);

        Assert.NotNull(found);
        Assert.Equal(1, found.Matches);
        Assert.Equal(1, found.Places);
    }

    /// <summary>
    /// A script that asks about TWO moves is thrown away rather than credited to either — "its
    /// own move" would then be a choice made by this code, and a reading that has to choose is
    /// not a reading (67).
    /// </summary>
    [Fact]
    public void AScriptAskingTwoMovesIsNotCountedAtAll()
    {
        Rom rom = Image([.. Asking(Cut), .. Asking(RockSmash), .. Naming(Cut), End]);

        Assert.Null(Of(rom, ByteThenWord, 1));
    }

    /// <summary>And a script that asks about no move is nothing to do with this question.</summary>
    [Fact]
    public void AScriptAskingNoMoveIsNotCounted()
    {
        Rom rom = Image([.. Naming(Cut), End]);

        Assert.Empty(WhatElseNamesTheMove.In(rom, [Where(0x200)]));
    }

    /// <summary>
    /// The same script handed in twice is one script. The map scan lists an address once per
    /// object that uses it and 200 obstacle objects sit on THREE addresses (224), so counting
    /// them per object would report the same two matches two hundred times.
    /// </summary>
    [Fact]
    public void TheSameScriptTwiceIsOneScript()
    {
        Rom rom = Image([.. Asking(Cut), .. Naming(Cut), End]);

        NamesItsOwnMove found = Assert.Single(
            WhatElseNamesTheMove.In(rom, [Where(0x200), Where(0x200)])
                .Where(o => o.Code == ByteThenWord && o.At == 1));

        Assert.Equal(1, found.Places);
    }
}
