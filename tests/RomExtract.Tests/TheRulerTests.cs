using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The ruler (275): the mixture bound read off a scale whose ends were both MEASURED, and the
/// pieces that make that possible.
/// <para>
/// 268's bound is <c>1 - d(mixed, real) / d(junk, real)</c>, which puts real script at distance
/// NOUGHT from the reference. It is not there: the reference is a sample of real script and so is
/// anything scored against it, and on this cartridge two halves of the maps' own scripts sit
/// 0.178 apart. So the bound was being read off a scale whose top mark was somewhere nobody had
/// ever checked — and handed a group that is half real script by construction, it reads NOUGHT.
/// </para>
/// </summary>
public sealed class TheRulerTests
{
    private const byte SetFlag = 0x29;
    private const byte End = 0x02;
    private const byte FacePlayer = 0x5A;

    /// <summary>
    /// Blocks of two commands at 16-byte spacing: the first <paramref name="setflags"/> are
    /// <c>setflag ; end</c> and the rest <c>faceplayer ; end</c> — two kinds with nothing in
    /// common but <c>end</c>, so a distance between them is easy to reason about by hand.
    /// </summary>
    private static (Rom Rom, List<uint> Blocks) Image(int howMany, int setflags, int from = 0x100)
    {
        var image = new byte[0x8000];

        Array.Fill(image, (byte)0xFF);

        var blocks = new List<uint>();

        for (var i = 0; i < howMany; i++)
        {
            int at = from + (i * 16);

            if (i < setflags)
            {
                image[at] = SetFlag;
                image[at + 1] = (byte)(i & 0xFF);
                image[at + 2] = 0x00;
                image[at + 3] = End;
            }
            else
            {
                image[at] = FacePlayer;
                image[at + 1] = End;
            }

            blocks.Add(0x08000000 + (uint)at);
        }

        return (new Rom(image), blocks);
    }

    // ---------------------------------------------------------------- the scale

    /// <summary>
    /// The two ends read exactly what they are, which is the whole point of measuring them.
    /// </summary>
    [Fact]
    public void TheRealEndReadsAllAndTheJunkEndReadsNothing()
    {
        Assert.Equal(1.0, WhatABlockIsMadeOf.BetweenTheEnds(0.178, 0.178, 0.735), 6);
        Assert.Equal(0.0, WhatABlockIsMadeOf.BetweenTheEnds(0.735, 0.178, 0.735), 6);
    }

    /// <summary>
    /// <b>AND THAT IS WHERE IT PARTS COMPANY WITH 268'S BOUND</b>, which is the discrimination
    /// this test exists to make. A population sitting exactly where real script sits is real
    /// script and reads 100%; the old bound divides by the junk distance alone and reads 76% of
    /// the same number, because it believes real script is at nought.
    /// </summary>
    [Fact]
    public void APopulationAtTheREALENDReadsAllWhereTheOldBoundDoesNot()
    {
        const double realEnd = 0.178;
        const double junkEnd = 0.735;

        Assert.Equal(1.0, WhatABlockIsMadeOf.BetweenTheEnds(realEnd, realEnd, junkEnd), 6);

        // The old shape, written out rather than called, because what is being asserted is that
        // the two DISAGREE — and by how much.
        double asTheOldBoundWouldSayIt = 1 - (realEnd / junkEnd);

        Assert.True(asTheOldBoundWouldSayIt < 0.8);
        Assert.True(1.0 - asTheOldBoundWouldSayIt > 0.2);
    }

    /// <summary>Halfway between the ends is halfway, which is what makes a mixture readable.</summary>
    [Fact]
    public void HalfwayBetweenTheEndsReadsHalf()
    {
        Assert.Equal(0.5, WhatABlockIsMadeOf.BetweenTheEnds(0.4565, 0.178, 0.735), 3);
    }

    /// <summary>
    /// A scale with no length cannot be read, and returning something from one would be inventing
    /// it. Both directions: the ends on top of each other, and the ends the wrong way round.
    /// </summary>
    [Fact]
    public void AScaleWithNoLengthReadsNothingRatherThanSomething()
    {
        Assert.Equal(0.0, WhatABlockIsMadeOf.BetweenTheEnds(0.3, 0.5, 0.5), 6);
        Assert.Equal(0.0, WhatABlockIsMadeOf.BetweenTheEnds(0.3, 0.9, 0.5), 6);
    }

    /// <summary>Outside the ends is clamped, because a share is not negative and not above one.</summary>
    [Fact]
    public void OutsideTheEndsIsClamped()
    {
        Assert.Equal(1.0, WhatABlockIsMadeOf.BetweenTheEnds(0.05, 0.178, 0.735), 6);
        Assert.Equal(0.0, WhatABlockIsMadeOf.BetweenTheEnds(0.95, 0.178, 0.735), 6);
    }

    // ---------------------------------------------------------------- per group

    /// <summary>
    /// <b>The bound is asked of each GROUP and not of the population.</b> A population of one
    /// pure-real group and one pure-junk group must come back as two answers, 1 and 0 — the
    /// average, 0.5, is the answer to a question nobody asked and is what a whole-population
    /// reading would give.
    /// </summary>
    [Fact]
    public void EachGroupIsScoredOnItsOwn()
    {
        (Rom rom, List<uint> blocks) = Image(20, 10);

        HowOftenEachCommand real = WhatABlockIsMadeOf.In(rom, blocks.Take(10));
        HowOftenEachCommand junk = WhatABlockIsMadeOf.In(rom, blocks.Skip(10));

        IReadOnlyList<double> says =
            WhatABlockIsMadeOf.BoundPerGroup(rom, blocks, 10, real, junk);

        Assert.Equal(2, says.Count);
        Assert.Equal(0.0, says[0], 6);
        Assert.Equal(1.0, says[1], 6);
    }

    /// <summary>
    /// A mixture of known share reads its share, which is the claim the bound rests on and the
    /// reason the command builds mixtures of its own.
    /// </summary>
    [Fact]
    public void AMixtureOfKnownShareReadsThatShare()
    {
        (Rom rom, List<uint> blocks) = Image(40, 20);

        HowOftenEachCommand real = WhatABlockIsMadeOf.In(rom, blocks.Take(20));
        HowOftenEachCommand junk = WhatABlockIsMadeOf.In(rom, blocks.Skip(20));

        // Ten real and ten junk, in one group.
        List<uint> half = [.. blocks.Take(10), .. blocks.Skip(20).Take(10)];

        IReadOnlyList<double> says =
            WhatABlockIsMadeOf.BoundPerGroup(rom, half, 20, real, junk);

        Assert.Single(says);
        Assert.Equal(0.5, says[0], 2);
    }

    /// <summary>A group bigger than the population is no group, not a group of everything.</summary>
    [Fact]
    public void APopulationTooSmallForOneGroupComesBackEmpty()
    {
        (Rom rom, List<uint> blocks) = Image(8, 4);

        HowOftenEachCommand real = WhatABlockIsMadeOf.In(rom, blocks.Take(4));
        HowOftenEachCommand junk = WhatABlockIsMadeOf.In(rom, blocks.Skip(4));

        Assert.Empty(WhatABlockIsMadeOf.BoundPerGroup(rom, blocks, 9, real, junk));
    }

    /// <summary>
    /// <b>The band and the bound are cut into groups by ONE loop.</b> 258's fault was a second
    /// question given its own copy of a walk, which left the suite guarding the other copy — so
    /// this asserts the two agree about how many groups there are, at three sizes, rather than
    /// trusting that they were written the same way.
    /// </summary>
    [Fact]
    public void TheBandAndTheBoundAgreeAboutWhatAGroupIs()
    {
        (Rom rom, List<uint> blocks) = Image(30, 15);

        HowOftenEachCommand real = WhatABlockIsMadeOf.In(rom, blocks.Take(15));
        HowOftenEachCommand junk = WhatABlockIsMadeOf.In(rom, blocks.Skip(15));

        foreach (int howMany in new[] { 5, 7, 30 })
        {
            Assert.Equal(
                WhatABlockIsMadeOf.SamplingBand(rom, blocks, howMany).Count,
                WhatABlockIsMadeOf.BoundPerGroup(rom, blocks, howMany, real, junk).Count);
        }
    }

    // ---------------------------------------------------------------- the order

    /// <summary>
    /// File order is the cartridge's order, and it is the order the argument for consecutive
    /// groups is about.
    /// </summary>
    [Fact]
    public void FileOrderIsByAddress()
    {
        IReadOnlyList<uint> ordered =
            WhatABlockIsMadeOf.InFileOrder([0x08000300, 0x08000100, 0x08000200]);

        Assert.Equal([0x08000100u, 0x08000200u, 0x08000300u], ordered);
    }

    /// <summary>
    /// <b>AND THE ORDER CHANGES THE BAND</b> (275), which is why it had to be said out loud
    /// rather than assumed. Two kinds laid out so that file order groups them purely and the
    /// scrambled order does not: the file-order band reaches the two kinds' full distance and
    /// the scrambled one comes back at nought.
    /// <para>
    /// That is the direction 273's documentation predicted — neighbours in the file are alike, so
    /// a group of neighbours is FARTHER from the whole — and it is the direction that makes the
    /// band conservative. Out of a <c>HashSet</c> nobody was getting it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheOrderTheGroupsAreCutInChangesTheBand()
    {
        (Rom rom, List<uint> blocks) = Image(20, 10);

        IReadOnlyList<double> inOrder =
            WhatABlockIsMadeOf.SamplingBand(rom, WhatABlockIsMadeOf.InFileOrder(blocks), 10);

        // The same twenty blocks, alternating kinds, which is what a scattered enumeration does.
        List<uint> scrambled = [];

        for (var i = 0; i < 10; i++)
        {
            scrambled.Add(blocks[i]);
            scrambled.Add(blocks[i + 10]);
        }

        IReadOnlyList<double> scattered = WhatABlockIsMadeOf.SamplingBand(rom, scrambled, 10);

        Assert.Equal(2, inOrder.Count);
        Assert.Equal(2, scattered.Count);
        Assert.True(
            inOrder.Max() > scattered.Max() + 0.2,
            $"file order {inOrder.Max():F3} should be well above scattered {scattered.Max():F3}");
        Assert.Equal(0.0, scattered.Max(), 6);
    }

    // ---------------------------------------------------------------- the nudge

    /// <summary>
    /// The nudge as a population is the same nudge the floor counts — one loop, two questions
    /// (258). If these two could disagree, the junk model and the floor would be about different
    /// things and nothing in the output would say so.
    /// </summary>
    [Fact]
    public void TheNudgedPopulationIsWhatTheNudgedFloorCounts()
    {
        (Rom rom, List<uint> blocks) = Image(12, 6);

        foreach (int by in new[] { 0, 4, 16 })
        {
            Assert.Equal(
                EveryScriptInTheImage.NudgedFloor(rom, blocks, by),
                EveryScriptInTheImage.Nudged(rom, blocks, by).Count);
        }
    }

    /// <summary>
    /// <b>And it returns the NUDGED addresses.</b> A population that came back holding the
    /// original targets would be the real thing wearing the floor's name, and its count would be
    /// right — which is exactly the shape of break a count cannot notice.
    /// </summary>
    [Fact]
    public void TheNudgedPopulationHoldsTheMovedAddressesAndNotTheOriginals()
    {
        (Rom rom, List<uint> blocks) = Image(12, 6);

        IReadOnlyList<uint> nudged = EveryScriptInTheImage.Nudged(rom, blocks, 16);

        Assert.NotEmpty(nudged);

        // Every one of them is sixteen bytes past a target, and the FIRST target — the one no
        // nudge can land on, because nothing sits sixteen bytes before it — is not among them.
        // That second assertion is the whole discrimination: a version that handed back the
        // originals would have the right count and every other property.
        Assert.All(nudged, at => Assert.Contains(at - 16, blocks));
        Assert.DoesNotContain(blocks[0], nudged);
        Assert.Contains(blocks[0] + 16, nudged);
    }

    /// <summary>
    /// A nudge of nought is the population itself, which is the control that says the offset is
    /// doing the work rather than the filter.
    /// </summary>
    [Fact]
    public void ANudgeOfNoughtIsThePopulationItself()
    {
        (Rom rom, List<uint> blocks) = Image(12, 6);

        Assert.Equal(blocks, EveryScriptInTheImage.Nudged(rom, blocks, 0));
    }
}
