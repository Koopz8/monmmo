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
        IReadOnlyCollection<int> looked = WhenAMapRunsSomething.LookedAt(
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
        IReadOnlyCollection<int> looked = WhenAMapRunsSomething.LookedAt(
        [
            Condition(0x407C, 1, 0x08160000),
            Terminator(),
        ]);

        Assert.Equal([0x407C], [.. looked]);
        Assert.False(WhenAMapRunsSomething.IsARead(Terminator()));
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
}
