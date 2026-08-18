using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The floor table, as a reading rather than as a copy.
/// <para>
/// 207 found the block at the top of every session's prompt stale in five of its six rows, after
/// thirteen milestones, while every sentence written <b>about</b> it stayed exactly true — because
/// each milestone re-ran the pair it cared about and pasted that delta onto a base nobody re-ran.
/// A table maintained by deltas drifts and stays self-consistent.
/// </para>
/// <para>
/// So the rows and the deltas come out of one list here. <see cref="TheFloorTable.Render"/> prints
/// each row's own numbers; <see cref="TheFloorTable.Differences"/> subtracts two rows of the list
/// it was handed and names both of them. If the absolutes go stale the sentences go stale with
/// them, out loud, which is the property none of the last thirteen milestones had.
/// </para>
/// </summary>
public sealed class TheTableNobodyReRanTests
{
    private static TheFloorTable.Setting Floor => new(SayYes: false, Boat: false, Surf: false, InOrder: false);

    private static TheFloorTable.Setting Yes => new(SayYes: true, Boat: false, Surf: false, InOrder: false);

    private static TheFloorTable.Setting YesInOrder => new(SayYes: true, Boat: false, Surf: false, InOrder: true);

    private static TheFloorTable.Setting Boat => new(SayYes: true, Boat: true, Surf: false, InOrder: false);

    private static TheFloorTable.Setting BoatInOrder => new(SayYes: true, Boat: true, Surf: false, InOrder: true);

    /// <summary>
    /// A row with every column a different number, so nothing here can pass by two columns
    /// happening to hold the same value.
    /// </summary>
    private static TheFloorTable.Row Row(
        TheFloorTable.Setting at,
        int reached = 100,
        int flags = 50,
        int passes = 3,
        int party = 2,
        int level = 40,
        int handed = 30,
        int twice = 4) =>
        new(at, reached, 425, passes, flags, party, level, handed, twice, SurfMove: 57,
            LearnedToCrossOnPass: 0, SwamAnyway: false);

    // ------------------------------------------------------------------ the rows

    /// <summary>
    /// THE DISCRIMINATION for the printer: two rows whose numbers are different in every column,
    /// so a printer that reads any column off the wrong row prints the wrong line.
    /// <para>
    /// Two of them because one row cannot tell "reads this row" from "reads the only row"
    /// (fixtures lie, 7).
    /// </para>
    /// </summary>
    [Fact]
    public void EveryColumnOfARowComesOffThatRow()
    {
        IReadOnlyList<string> printed = TheFloorTable.Render(
        [
            Row(Floor, reached: 183, flags: 153, passes: 6, party: 6, level: 52, handed: 103, twice: 11),
            Row(Yes, reached: 243, flags: 231, passes: 5, party: 4, level: 67, handed: 155, twice: 10),
        ]);

        Assert.Equal(2, printed.Count);

        Assert.Contains("183 / 153 in 6", printed[0]);
        Assert.Contains("party of 6 at 52", printed[0]);
        Assert.Contains("11 of 103 handed twice", printed[0]);

        Assert.Contains("243 / 231 in 5", printed[1]);
        Assert.Contains("party of 4 at 67", printed[1]);
        Assert.Contains("10 of 155 handed twice", printed[1]);
    }

    /// <summary>And each line says which command line produced it, which is what a session retypes.</summary>
    [Fact]
    public void AndEachRowSaysWhichCommandLineProducedIt()
    {
        Assert.Equal("--play", Floor.Command);
        Assert.Equal("--play --say-yes", Yes.Command);
        Assert.Equal("--play --say-yes --boat --surf --in-order",
            new TheFloorTable.Setting(SayYes: true, Boat: true, Surf: true, InOrder: true).Command);
    }

    // ------------------------------------------------------------- one lever apart

    /// <summary>
    /// THE RULE THE WHOLE INSTRUMENT TURNS ON: a delta is a statement about a lever only when the
    /// two runs differ in that lever and in nothing else.
    /// </summary>
    [Fact]
    public void OneLeverApartIsOneLeverAndNotAtMostOne()
    {
        Assert.Equal(TheFloorTable.InOrder, YesInOrder.OneLeverPast(Yes));
        Assert.Equal(TheFloorTable.Boat, Boat.OneLeverPast(Yes));

        // Two levers apart. It also produces a number, and that number is not about either
        // lever — which is precisely the sort of delta a hand-kept table fills up with.
        Assert.Null(BoatInOrder.OneLeverPast(Yes));

        // And the same setting is nought levers past itself, which is not a difference either.
        Assert.Null(Yes.OneLeverPast(Yes));
    }

    /// <summary>
    /// And it is DIRECTED: a lever coming off is not the same fact as a lever going on, and a
    /// reading that treated them alike would report every pair twice with the sign flipped.
    /// </summary>
    [Fact]
    public void ALeverComingOffIsNotALeverGoingOn()
    {
        Assert.Equal(TheFloorTable.InOrder, YesInOrder.OneLeverPast(Yes));
        Assert.Null(Yes.OneLeverPast(YesInOrder));
    }

    /// <summary>
    /// The six settings the table is quoted at: six of them, all different, and none orphaned —
    /// a row no other row is one lever from is a row no difference can ever be stated about.
    /// </summary>
    [Fact]
    public void TheSixSettingsAreSixAndNoneOfThemIsOrphaned()
    {
        IReadOnlyList<TheFloorTable.Setting> settings = TheFloorTable.Settings;

        Assert.Equal(6, settings.Count);
        Assert.Equal(6, settings.Distinct().Count());

        foreach (TheFloorTable.Setting one in settings)
        {
            Assert.True(
                settings.Any(other => one.OneLeverPast(other) is not null)
                || settings.Any(other => other.OneLeverPast(one) is not null),
                $"{one.Command} is one lever from nothing else in the table");
        }
    }

    // ----------------------------------------------------------------- the deltas

    /// <summary>
    /// THE DISCRIMINATION for the deltas: two independent pairs, with different answers, and
    /// NEITHER of them the first row in the list.
    /// <para>
    /// A reading that subtracted everything from the first row it was handed — which is what a
    /// remembered base is — gets the second pair wrong, and the numbers are chosen so that it
    /// gets a different number rather than the same one by luck (fixtures lie, 10).
    /// </para>
    /// </summary>
    [Fact]
    public void EachDifferenceIsSubtractedFromTheTwoRowsItNames()
    {
        IReadOnlyList<TheFloorTable.Difference> differences = TheFloorTable.Differences(
        [
            Row(Floor, reached: 183, flags: 153, passes: 6, party: 6),
            Row(Yes, reached: 243, flags: 231, passes: 5, party: 4),
            Row(YesInOrder, reached: 243, flags: 233, passes: 5, party: 5),
            Row(Boat, reached: 381, flags: 293, passes: 6, party: 4),
            Row(BoatInOrder, reached: 381, flags: 294, passes: 6, party: 5),
        ]);

        // --in-order on the walking thread: 231 -> 233, and a party member. Nothing to do with
        // the floor row above it.
        TheFloorTable.Difference walking = Assert.Single(
            differences.Where(d => d.Lever == TheFloorTable.InOrder && d.From == Yes.Command));

        Assert.Equal(2, walking.Flags);
        Assert.Equal(1, walking.Party);
        Assert.Equal(0, walking.Maps);
        Assert.Equal(YesInOrder.Command, walking.To);

        // --in-order on the boat thread: 293 -> 294, a DIFFERENT number, off a different pair.
        TheFloorTable.Difference boat = Assert.Single(
            differences.Where(d => d.Lever == TheFloorTable.InOrder && d.From == Boat.Command));

        Assert.Equal(1, boat.Flags);
        Assert.Equal(1, boat.Party);
        Assert.Equal(0, boat.Maps);
    }

    /// <summary>
    /// And a pair two levers apart is not reported at all — the other half, which stops "report
    /// every pair" passing the test above.
    /// </summary>
    [Fact]
    public void APairTwoLeversApartIsNotADifference()
    {
        IReadOnlyList<TheFloorTable.Difference> differences = TheFloorTable.Differences(
        [
            Row(Yes, flags: 231),
            Row(BoatInOrder, flags: 294),
        ]);

        Assert.Empty(differences);
    }

    /// <summary>
    /// THE ORDINARY CASE, which is the half that goes unasserted (fixtures lie, 8): a lever one
    /// row apart that costs NOTHING is still a difference, and reporting it is the whole point of
    /// a denominator. A reading that only emitted non-zero deltas would come back empty here and
    /// look like a reading that had nothing to say.
    /// </summary>
    [Fact]
    public void ALeverThatCostsNothingIsStillADifference()
    {
        IReadOnlyList<TheFloorTable.Difference> differences = TheFloorTable.Differences(
        [
            Row(Yes, reached: 243, flags: 231, passes: 5, party: 4),
            Row(YesInOrder, reached: 243, flags: 231, passes: 5, party: 4),
        ]);

        TheFloorTable.Difference nothing = Assert.Single(differences);

        Assert.Equal(TheFloorTable.InOrder, nothing.Lever);
        Assert.Equal(0, nothing.Flags);
        Assert.Equal(0, nothing.Party);
        Assert.Contains("+0 flag(s)", nothing.Said);
    }

    /// <summary>And a difference says which two rows it came from, or it is a remembered number again.</summary>
    [Fact]
    public void ADifferenceNamesBothRowsItCameFrom()
    {
        TheFloorTable.Difference one = Assert.Single(
            TheFloorTable.Differences([Row(Yes, flags: 231), Row(YesInOrder, flags: 233)]));

        Assert.Contains(Yes.Command, one.Said);
        Assert.Contains(YesInOrder.Command, one.Said);
        Assert.Contains("+2 flag(s)", one.Said);
    }

    // ------------------------------------------------------ a row is read off a run

    /// <summary>
    /// A row is read off the attempt it was handed, and <b>handed twice is not handed at all</b>:
    /// the fixture has three hand-overs of which one happened on more than one pass, so a reading
    /// that took both columns off the same list gets 3 and 3 rather than 1 and 3.
    /// </summary>
    [Fact]
    public void ARowIsReadOffTheRunAndHandedTwiceIsNotHandedOnce()
    {
        var played = Played() with
        {
            Handovers =
            [
                new HandedOver("3.1", 0, 0x08165B10, "item 0x16E x1", [1]),
                new HandedOver("3.3", 2, 0x081664A6, "item 0x16B x1", [1, 2, 3]),
                new HandedOver("1.53", 2, 0x08161AC8, "#131 at 25", [4]),
            ],
        };

        TheFloorTable.Row row = TheFloorTable.Read(Floor, played, maps: 425);

        Assert.Equal(3, row.HandedOver);
        Assert.Equal(1, row.HandedTwice);
        Assert.Equal(425, row.Of);
        Assert.Equal(2, row.Reached);
        Assert.Equal(4, row.Flags);
        Assert.Equal(6, row.Passes);
        Assert.Equal(1, row.Party);
        Assert.Equal(52, row.HighestLevel);
    }

    /// <summary>
    /// And what the run did about the sea, which is three different facts and not one: the party
    /// learned the move (READ), the lever swam anyway (MODELLED), or the sea was a wall.
    /// </summary>
    [Fact]
    public void CrossingWaterSaysWhichOfTheThreeItWas()
    {
        TheFloorTable.Row learned = Row(Floor) with { LearnedToCrossOnPass = 3, SwamAnyway = false };
        TheFloorTable.Row lever = Row(Floor) with { LearnedToCrossOnPass = 0, SwamAnyway = true };
        TheFloorTable.Row wall = Row(Floor) with { LearnedToCrossOnPass = 0, SwamAnyway = false };

        Assert.Contains("READ", learned.Water);
        Assert.Contains("from pass 3", learned.Water);

        Assert.Contains("MODELLED", lever.Water);
        Assert.DoesNotContain("READ", lever.Water);

        Assert.Contains("a wall", wall.Water);
        Assert.DoesNotContain("MODELLED", wall.Water);
    }

    private static Attempt Played() =>
        new(
            Passes: 6,
            Stopped: StoppedBecause.NothingMoreOpened,
            Reached: ["4.1", "3.1"],
            Unreached: [],
            Flags: [1, 2, 3, 4],
            Moves: [15],
            Party: [new SavedMon(131, 52, null, 100, StatusCondition.None, Nature.Hardy, [57])],
            FightsWon: 218,
            FightsLost: 53,
            FightsSkipped: 60,
            PartiesHealed: 315,
            Specials: new Dictionary<int, int>(),
            ShutDoors: [],
            Blocked: [])
        {
            SurfMove = 57,
        };
}
