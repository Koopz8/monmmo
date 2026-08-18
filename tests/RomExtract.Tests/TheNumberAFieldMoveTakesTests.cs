using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The number <c>dofieldeffect</c> takes, against the move the same block asked about.
/// <para>
/// The command had a name in this repository and nothing outside one file knew it: 191 wrote
/// <c>private const byte DoFieldEffect = 0x9C</c> inside the who-knows-a-move sweep and used it to
/// say what a block offers; every other reading printed a bare number, and 232 measured it as an
/// unnamed argument column forty-one milestones later.
/// </para>
/// <para>
/// So the question the name implies gets asked: seven blocks in this cartridge pair a move with a
/// number, six moves and six numbers, and <b>the only move that appears twice gets the same number
/// twice</b> — which is one agreement and is said to be one. The four numbers no move drives are
/// all larger than all six, which chance would do one time in two hundred and ten.
/// </para>
/// </summary>
public sealed class TheNumberAFieldMoveTakesTests
{
    private static FieldEffectNumbers.Offer Offer(int move, int effect, int at = 0x1000) =>
        new(move, effect, at);

    // ---------------------------------------------------------------- one number per move

    /// <summary>
    /// THE DISCRIMINATION: two moves and two numbers reads the same whether each move has its own
    /// or one move has both, and those are opposite findings. A reading that compared the two
    /// counts would call this fixture clean.
    /// </summary>
    [Fact]
    public void TwoMovesAndTwoNumbersIsNotOneNumberPerMove()
    {
        FieldEffectNumbers.OneEach each = FieldEffectNumbers.PerMove(
            [Offer(15, 2), Offer(15, 9), Offer(249, 2)]);

        Assert.Equal(2, each.Moves);
        Assert.Equal(2, each.Effects);

        Assert.False(each.Holds);
        Assert.Equal([15], each.WithTwoNumbers);
    }

    /// <summary>
    /// And the ordinary case, which is the half that stops "always say no" passing: distinct moves
    /// with distinct numbers hold, and the one repeated move agreeing is counted as an agreement.
    /// </summary>
    [Fact]
    public void AMoveThatAppearsTwiceWithTheSameNumberIsOneAgreement()
    {
        FieldEffectNumbers.OneEach each = FieldEffectNumbers.PerMove(
            [Offer(291, 44), Offer(291, 44), Offer(15, 2), Offer(57, 9)]);

        Assert.True(each.Holds);
        Assert.Equal(4, each.Offers);
        Assert.Equal(3, each.Moves);
        Assert.Equal(3, each.Effects);

        Assert.Equal(1, each.Repeated);
        Assert.Equal(1, each.RepeatedAgreeing);
    }

    /// <summary>
    /// A repeated move that DISAGREES is a repeat and not an agreement — without this, counting
    /// every repeat as an agreement passes the test above.
    /// </summary>
    [Fact]
    public void ARepeatedMoveThatDisagreesIsNotAnAgreement()
    {
        FieldEffectNumbers.OneEach each = FieldEffectNumbers.PerMove(
            [Offer(291, 44), Offer(291, 43), Offer(15, 2)]);

        Assert.Equal(1, each.Repeated);
        Assert.Equal(0, each.RepeatedAgreeing);
        Assert.False(each.Holds);
    }

    // -------------------------------------------------------------------- the two bands

    /// <summary>
    /// The split is EVERY one below EVERY one, not most of them: an interleaved pair of sets is
    /// not two bands however tidy it looks.
    /// </summary>
    [Fact]
    public void TheSplitIsEveryOneBelowEveryOne()
    {
        FieldEffectNumbers.TheSplit clean =
            FieldEffectNumbers.AreTheLowest([2, 9, 37, 40, 43, 44], [62, 64, 68, 69]);

        Assert.True(clean.Cleanly);
        Assert.Equal(10, clean.Of);
        Assert.Equal(6, clean.Taken);
        Assert.Equal(210, clean.OneIn);

        // One of the low set is above one of the high set. The smallest is still smallest.
        FieldEffectNumbers.TheSplit mixed =
            FieldEffectNumbers.AreTheLowest([2, 9, 66], [40, 64]);

        Assert.False(mixed.Cleanly);
    }

    /// <summary>
    /// With nothing on one side there is no split to be surprised by, and the floor says one
    /// rather than a large number about nothing.
    /// </summary>
    [Fact]
    public void NothingOnOneSideIsNotASplit()
    {
        FieldEffectNumbers.TheSplit none = FieldEffectNumbers.AreTheLowest([2, 9, 37], []);

        Assert.False(none.Cleanly);
        Assert.Equal(1, none.OneIn);
    }

    /// <summary>
    /// And the floor is off the two sets TOGETHER — how many ways the whole set of numbers could
    /// have been divided, not how many the smaller side has.
    /// </summary>
    /// <remarks>
    /// Note what this test cannot do: <c>C(n, k)</c> equals <c>C(n, n - k)</c>, so a reading that
    /// used the other side's size is arithmetically identical and no fixture can separate them.
    /// Written down rather than left to be discovered — a test named for a discrimination it does
    /// not make is worse than no test.
    /// </remarks>
    [Fact]
    public void TheFloorIsOffBothSetsTogether()
    {
        Assert.Equal(35, FieldEffectNumbers.AreTheLowest([1, 2, 3], [4, 5, 6, 7]).OneIn);
        Assert.Equal(6, FieldEffectNumbers.AreTheLowest([1], [2, 3, 4, 5, 6]).OneIn);
    }

    // --------------------------------------------------------------------- the raw sweep

    /// <summary>
    /// The whole-image sweep and its control. Reversing preserves every byte frequency, so the
    /// site counts must match — what can differ is how many of them read on to a proper end.
    /// </summary>
    [Fact]
    public void TheReversedImageHasTheSameBytesAndNotTheSameReads()
    {
        var data = new byte[0x1000];

        // A byte this project has no width for, so a read that drifts stops at once rather than
        // sliding through nops to somewhere convenient.
        Array.Fill(data, (byte)0xFF);

        // dofieldeffect 1 ; end — reads on forwards. Reversed it becomes `02 00 01 9C` and the
        // 0x9C there runs into the filler.
        new byte[] { ScriptCommands.DoFieldEffect, 0x01, 0x00, 0x02 }.CopyTo(data, 0x100);

        var rom = new Rom(data);

        (int Sites, int ReadsOn, int Words) real = FieldEffectNumbers.Sweep(rom);
        (int Sites, int ReadsOn, int Words) floor = FieldEffectNumbers.NoiseFloor(rom);

        Assert.Equal(1, real.Sites);
        Assert.Equal(1, real.ReadsOn);

        Assert.Equal(real.Sites, floor.Sites);
        Assert.Equal(0, floor.ReadsOn);
    }

    /// <summary>And the command has ONE name, in one place, which is the whole point of 233.</summary>
    [Fact]
    public void TheCommandHasAName()
    {
        Assert.Equal(0x9C, ScriptCommands.DoFieldEffect);
        Assert.Equal("dofieldeffect", ScriptCommands.NameOf(ScriptCommands.DoFieldEffect));
    }
}
