using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The read a script stream cannot see, and what counting it costs the deaf list.
/// <para>
/// <b>245 reported twelve variables looked at NOWHERE IN SIXTEEN MEGABYTES.</b> One of them,
/// <c>0x407C</c>, is the condition on an arrival script on nineteen maps — the map's own header
/// says "run this when 0x407C holds 1", which names a variable, is a read, and involves no
/// command anywhere in the file. Every sweep in this project walks a script stream and decides
/// what a number is by which operand of which command named it, so not one of them could see it.
/// </para>
/// <para>
/// Trap 1 one level down: <b>before believing any "nothing in the world does X", check what the
/// scan is enumerating.</b> It was enumerating commands and the sentence was about the cartridge.
/// </para>
/// </summary>
public sealed class AReadThatIsNotACommandTests
{
    private static MapEntryScript Condition(int variable, int value, uint runs) =>
        new(variable, value, runs);

    /// <summary>A header record with no script — the all-zero terminator the list ends with.</summary>
    private static MapEntryScript Terminator() => new(0, 0, 0);

    private static BothNamespaces Deaf(int written, params int[] readByAHeader) =>
        new(
            Flags: new Dictionary<int, int>(),
            Variables: new Dictionary<int, int>(),
            Commands: 1)
        {
            ByOperand = new Dictionary<string, IReadOnlyDictionary<int, int>>
            {
                ["0x16 arg0"] = new Dictionary<int, int> { [written] = 1 },
            },
            LookedAtBySomethingElse = new Dictionary<string, IReadOnlyCollection<int>>
            {
                ["a map header, on arrival"] = readByAHeader,
            },
        };

    // ------------------------------------------------------ what a header entry looks at

    /// <summary>THE THING: an arrival condition names a variable, and that is a read.</summary>
    [Fact]
    public void AnArrivalConditionLooksAtTheVariableItNames()
    {
        IReadOnlyCollection<int> looked = ReadsThatAreNotCommands.LookedAt(
        [
            Condition(0x407C, 1, 0x08160000),
            Condition(0x4075, 2, 0x08160100),
        ]);

        Assert.Equal([0x4075, 0x407C], [.. looked.Order()]);
    }

    /// <summary>
    /// AND THE HALF THAT MAKES IT MEAN ANYTHING: an entry that runs nothing is not a read.
    /// </summary>
    /// <remarks>
    /// The list in a map's header ends with an all-zero record, and its variable field is a nought
    /// that names nothing. Counting it would rescue variable 0 from every deaf list in this
    /// project for free, on all four hundred and twenty-five maps — a correction that can only
    /// make findings disappear, which is the direction nobody notices.
    /// </remarks>
    [Fact]
    public void AHeaderEntryThatRunsNothingIsNotARead()
    {
        IReadOnlyCollection<int> looked = ReadsThatAreNotCommands.LookedAt(
        [
            Condition(0x407C, 1, 0x08160000),
            Terminator(),
        ]);

        Assert.Equal([0x407C], [.. looked]);
        Assert.False(ReadsThatAreNotCommands.IsARead(Terminator()));
    }

    // -------------------------------------------------- and what it does to the deaf list

    /// <summary>
    /// THE CONSEQUENCE: a variable only a map header reads is not written and never looked at.
    /// </summary>
    [Fact]
    public void AVariableOnlyAMapHeaderReadsIsNotWrittenAndNeverLookedAt()
    {
        Assert.Empty(Deaf(0x407C, 0x407C).WrittenAndNeverLookedAt);
    }

    /// <summary>
    /// And a variable NO non-command reader names is still reported — without this, subtracting
    /// everything passes the test above and the whole list goes empty.
    /// </summary>
    [Fact]
    public void AVariableNoHeaderReadsIsStillWrittenAndNeverLookedAt()
    {
        Assert.Equal([0x407D], [.. Deaf(0x407D, 0x407C).WrittenAndNeverLookedAt]);
    }

    /// <summary>
    /// And the commands-only list still holds it, so the SIZE of the correction stays printable.
    /// </summary>
    /// <remarks>
    /// A number that changes for reasons the reader cannot see is a number they have to take on
    /// trust. 245's answer and this one differ by seven, and the seven are named in the output
    /// with what reads each — which is only possible because both lists survive.
    /// </remarks>
    [Fact]
    public void TheCommandsOnlyListStillReportsItSoTheCorrectionHasASize()
    {
        BothNamespaces both = Deaf(0x407C, 0x407C);

        Assert.Equal([0x407C], [.. both.WrittenAndNeverLookedAtByACommand]);
        Assert.Empty(both.WrittenAndNeverLookedAt);
    }

    /// <summary>
    /// And the raw reading — the one that counts a literal as a look — is corrected too, or the
    /// "0 were hidden by a literal" line compares two lists that were not asked the same question.
    /// </summary>
    [Fact]
    public void TheRawReadingIsCorrectedTheSameWay()
    {
        Assert.Empty(Deaf(0x407C, 0x407C).WrittenAndNeverReadRaw);
    }

    // --------------------------------------------------- and the second copy, found at 247

    private static MapTrigger Square(int variable, int value, uint runs) =>
        new(0, 0, variable, value, runs);

    /// <summary>
    /// THE SECOND COPY OF THE SAME FAULT: a trigger's condition names a variable too.
    /// </summary>
    /// <remarks>
    /// 246 found the header. 247 went looking for another record with the same shape and there
    /// was one, on a different list, missed by the same reasoning — and it is the bigger of the
    /// two: it takes the deaf list from 19 to 5.
    /// </remarks>
    [Fact]
    public void ATriggerConditionLooksAtTheVariableItNames()
    {
        IReadOnlyCollection<int> looked = ReadsThatAreNotCommands.LookedAt(
        [
            Square(0x4088, 3, 0x08163000),
            Square(0x400F, 1, 0x08163100),
        ]);

        Assert.Equal([0x400F, 0x4088], [.. looked.Order()]);
    }

    /// <summary>
    /// A DECOY: a trigger with no script names nothing, and this cartridge has not one of them.
    /// </summary>
    /// <remarks>
    /// <b>All 228 triggers in this game have a script and a variable in the story's own band</b>,
    /// which the output prints, so neither half of the rule fires on the real image. That makes
    /// both of these fixtures decoys by this project's own definition, and a decoy is the stated
    /// alternative to deleting a guard nothing can fail. They are kept rather than deleted
    /// because the rule is what makes the sweep's population defensible: without it, a record
    /// that runs nothing would put its variable field into a reader's list, and nothing about
    /// the count would look wrong.
    /// </remarks>
    [Fact]
    public void ATriggerThatRunsNothingIsNotARead()
    {
        Assert.Empty(ReadsThatAreNotCommands.LookedAt([Square(0x4088, 3, 0)]));
        Assert.False(ReadsThatAreNotCommands.IsARead(Square(0x4088, 3, 0)));
    }

    /// <summary>
    /// AND THE OTHER DECOY: a trigger whose variable field is nought names nothing.
    /// </summary>
    /// <remarks>
    /// Nought is used rather than a band boundary because a band is READ in this project and not
    /// asserted. On this cartridge the field is in <c>0x4000+</c> at all 228 triggers, so this
    /// rule fires nowhere and the distribution is printed beside the count saying so.
    /// </remarks>
    [Fact]
    public void ATriggerWithNoVariableIsNotARead()
    {
        Assert.Empty(ReadsThatAreNotCommands.LookedAt([Square(0, 0, 0x08163000)]));
        Assert.False(ReadsThatAreNotCommands.IsARead(Square(0, 0, 0x08163000)));
    }

    /// <summary>
    /// And the distribution the rule is checked against is a real reading, not a restatement —
    /// it counts every trigger, including the ones the rule throws away.
    /// </summary>
    /// <remarks>
    /// A rule with a number in it that nobody can check is a number nothing computes (231). If
    /// this only reported what the rule kept, "0x4000+ at all of them" would be true by
    /// construction and would say nothing about the cartridge.
    /// </remarks>
    [Fact]
    public void TheFieldDistributionCountsTheTriggersTheRuleThrowsAway()
    {
        IReadOnlyList<(int From, int Triggers)> held = ReadsThatAreNotCommands.WhatTheFieldHolds(
        [
            Square(0x4088, 3, 0x08163000),
            Square(0x4089, 1, 0x08163100),
            Square(0, 0, 0x08163200),
            Square(0x408A, 1, 0),
        ]);

        Assert.Equal([(0x0000, 1), (0x4000, 3)], [.. held]);
    }

    /// <summary>
    /// And both kinds are gathered under names, so a subtraction can always say what did the
    /// reading.
    /// </summary>
    [Fact]
    public void BothKindsAreGatheredUnderTheirOwnNames()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> of = ReadsThatAreNotCommands.Of(
            [Condition(0x407C, 1, 0x08160000)],
            [Square(0x4088, 3, 0x08163000)]);

        Assert.Equal([0x407C], [.. of[ReadsThatAreNotCommands.OnArrival]]);
        Assert.Equal([0x4088], [.. of[ReadsThatAreNotCommands.OnASquare]]);
    }
}
