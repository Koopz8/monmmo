using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a set of blocks is made of, so "these look like scripts" is a number.
/// <para>
/// 267 counted <b>6621 blocks no map leads to</b> and could not say what they are. Its calibration
/// row said they were not behaving like scripts, and that has two very different explanations:
/// scripts using variables only compiled code writes, or bytes that are not scripts at all.
/// </para>
/// <para>
/// The command mix answers it. Measured on this cartridge, the distance from the maps' own scripts
/// is <b>0.690</b> for the outside blocks named alone, <b>0.698</b> for the ones named in a table,
/// and <b>0.711</b> for the reversed image — the outside population is no more script-like than
/// random bytes, and the mixture bound puts at most 3.1% of it on the real side.
/// </para>
/// </summary>
public sealed class WhatABlockIsMadeOfTests
{
    private static HowOftenEachCommand Mix(int blocks, params (byte Code, int Count)[] counts) =>
        new(counts.ToDictionary(c => c.Code, c => c.Count), counts.Sum(c => c.Count), blocks);

    /// <summary>
    /// THE SAME MIX AT TEN TIMES THE SIZE IS THE SAME MIX. Distance is between distributions and
    /// not between tallies — otherwise a population ten times another's size is automatically far
    /// from it, and every comparison this instrument makes is between unequal populations.
    /// </summary>
    [Fact]
    public void SizeDoesNotMakeTwoPopulationsDifferent()
    {
        Assert.Equal(
            0,
            WhatABlockIsMadeOf.Distance(
                Mix(10, (0x09, 60), (0x02, 40)),
                Mix(400, (0x09, 600), (0x02, 400))),
            3);
    }

    /// <summary>
    /// AND TWO MIXES WITH NOTHING IN COMMON ARE ONE APART, which is what fixes the scale. Without
    /// it 0.69 is a number with no ends on it.
    /// </summary>
    [Fact]
    public void NothingSharedIsADistanceOfOne()
    {
        Assert.Equal(
            1,
            WhatABlockIsMadeOf.Distance(Mix(1, (0x09, 10)), Mix(1, (0x05, 10))),
            3);
    }

    /// <summary>
    /// THE DISTANCE IS OVER EVERY CODE EITHER ONE NAMES. A population using six commands the other
    /// never uses is far from it; an intersection-only distance would call the two close and would
    /// score highest for the populations that share least.
    /// </summary>
    [Fact]
    public void ACodeOnlyOnePopulationHasStillCounts()
    {
        // They agree perfectly on 0x09 and one of them spends half its commands elsewhere.
        Assert.Equal(
            0.5,
            WhatABlockIsMadeOf.Distance(
                Mix(1, (0x09, 10)),
                Mix(1, (0x09, 10), (0x05, 10))),
            3);
    }

    /// <summary>
    /// THE MIXTURE BOUND IS ARITHMETIC AND NOT A FIT. A population that is three parts real script
    /// in ten comes back as three parts in ten, exactly, because total variation is linear in a
    /// mixture. No threshold anywhere in it.
    /// </summary>
    [Fact]
    public void AKnownMixtureComesBackAsThatMixture()
    {
        HowOftenEachCommand real = Mix(1, (0x09, 60), (0x02, 40));
        HowOftenEachCommand junk = Mix(1, (0x05, 50), (0x03, 50));

        // Three parts of the first and seven of the second, per hundred commands.
        HowOftenEachCommand mixed = Mix(1, (0x09, 18), (0x02, 12), (0x05, 35), (0x03, 35));

        Assert.Equal(0.30, WhatABlockIsMadeOf.HowMuchCouldBeReal(mixed, real, junk), 3);
    }

    /// <summary>
    /// AND THE REAL THING IS ALL OF ITSELF AND THE JUNK IS NONE OF IT. Both ends, because a bound
    /// that is right in the middle and wrong at the edges is a bound that has been fitted.
    /// </summary>
    [Fact]
    public void TheEndsOfTheBoundAreOneAndNought()
    {
        HowOftenEachCommand real = Mix(1, (0x09, 60), (0x02, 40));
        HowOftenEachCommand junk = Mix(1, (0x05, 50), (0x03, 50));

        Assert.Equal(1, WhatABlockIsMadeOf.HowMuchCouldBeReal(real, real, junk), 3);
        Assert.Equal(0, WhatABlockIsMadeOf.HowMuchCouldBeReal(junk, real, junk), 3);
    }

    /// <summary>
    /// AND SOMETHING FURTHER FROM THE REAL THING THAN THE JUNK IS NOUGHT, not a negative share.
    /// "Less real than random" is not a quantity of anything, and a negative number in that column
    /// would read as a finding.
    /// </summary>
    [Fact]
    public void FurtherAwayThanTheJunkIsNoughtRatherThanNegative()
    {
        HowOftenEachCommand real = Mix(1, (0x09, 100));
        HowOftenEachCommand junk = Mix(1, (0x09, 50), (0x05, 50));

        Assert.Equal(0, WhatABlockIsMadeOf.HowMuchCouldBeReal(Mix(1, (0x05, 100)), real, junk), 3);
    }

    private const byte SetVar = 0x16;
    private const byte End = 0x02;

    /// <summary>
    /// AND IT READS THE BLOCKS RATHER THAN BEING TOLD. Commands, blocks and the length between
    /// them, off a real reader — because a mix built from a caller's own tally would agree with
    /// whatever the caller thought a command was.
    /// </summary>
    [Fact]
    public void TheTallyComesOffTheReader()
    {
        var image = new byte[0x400];

        var at = 0x100;

        for (var i = 0; i < 3; i++)
        {
            image[at++] = SetVar;
            image[at++] = (byte)(0x01 + i);
            image[at++] = 0x40;
            image[at++] = 0x05;
            image[at++] = 0x00;
        }

        image[at] = End;

        image[0x200] = End;

        HowOftenEachCommand mix =
            WhatABlockIsMadeOf.In(new Rom(image), [0x08000100, 0x08000200]);

        Assert.Equal(3, mix.Counts[SetVar]);
        Assert.Equal(2, mix.Counts[End]);
        Assert.Equal(5, mix.Commands);
        Assert.Equal(2, mix.Blocks);
        Assert.Equal(2.5, mix.Length, 3);
    }
}
