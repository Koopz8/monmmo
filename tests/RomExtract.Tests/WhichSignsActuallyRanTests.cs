using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which of the map's four lists a script came off, which the run did not know.
/// <para>
/// <b>239 put 519 sign scripts into the walk and left "which of them ran" unanswerable.</b>
/// Everything the run executed was an address and a map: a flag moved by a sign and a flag moved
/// by the person standing next to it were the same record, so "the signs did this" and
/// "something on that map did this" were the same sentence. The map scan has told the five kinds
/// apart since 224.
/// </para>
/// <para>
/// And the control. 239 measured what the fourth list was worth by running the whole playthrough
/// twice, one commit apart, and writing the tables side by side — a number nobody without that
/// commit built could re-check. <see cref="Autoplayer.Play"/> now takes <c>readSigns</c>, so the
/// same process runs both and subtracts them.
/// </para>
/// </summary>
public sealed class WhichSignsActuallyRanTests
{
    private const uint TheSign = 0x3000;
    private const uint ThePerson = 0x4000;
    private const uint TheTrigger = 0x5000;
    private const uint OnArrival = 0x6000;
    private const int Moved = 0x0500;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Sets(int flag) => Nothing with { FlagsSet = [flag] };

    /// <summary>One room with something off each of the four lists in it.</summary>
    private static MapData AllFourLists() =>
        Room("1.0") with
        {
            Signs = [new MapSign(1, 1, Kind: 0, TheSign)],
            Objects =
            [
                new MapObject(1, 1, 2, 2, Direction.Down, 0, false) { ScriptAddress = ThePerson },
            ],
            Triggers = [new MapTrigger(3, 3, 0, 0, TheTrigger)],
            OnEntry = [new MapEntryScript(0, 0, OnArrival)],
        };

    private static Attempt Run(WorldData world, bool readSigns = true) =>
        Autoplayer.Play(
            world,
            world.Maps.First().Id,
            TestRules.All,
            (address, _, _) => Sets(Moved + (int)(address >> 12)),
            readSigns: readSigns);

    // ------------------------------------------------------------------- it knows which list

    /// <summary>
    /// THE THING: every flag move says which of the four lists ran the script that made it.
    /// </summary>
    [Fact]
    public void EveryFlagMoveSaysWhichListItCameOff()
    {
        Attempt played = Run(new WorldData([AllFourLists()]));

        Assert.Contains(played.FlagMoves, m => m.Address == TheSign && m.From == WhatRanIt.ASign);
        Assert.Contains(played.FlagMoves, m => m.Address == ThePerson && m.From == WhatRanIt.APerson);
        Assert.Contains(played.FlagMoves, m => m.Address == TheTrigger && m.From == WhatRanIt.ATrigger);
        Assert.Contains(played.FlagMoves, m => m.Address == OnArrival && m.From == WhatRanIt.OnArrival);
    }

    /// <summary>
    /// And the bytes after a fight are filed under whoever started it, because the continuation
    /// belongs to the battle and the battle belongs to whoever was talked to.
    /// </summary>
    /// <remarks>
    /// A sign picks the fight here, which is not something this cartridge does — the point is
    /// that the kind is carried through the enqueue rather than defaulted, and a person would
    /// pass this test by being the default.
    /// </remarks>
    [Fact]
    public void TheBytesAfterAFightBelongToWhoeverStartedIt()
    {
        const uint after = 0x7000;

        MapData start = Room("1.0") with
        {
            Signs = [new MapSign(1, 1, Kind: 0, TheSign)],
            Objects =
            [
                new MapObject(1, 1, 2, 2, Direction.Down, 0, false) { ScriptAddress = ThePerson },
            ],
        };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                // Somebody has to hand over something to fight with, or the fight is skipped
                // for having nobody to send out and the continuation never runs.
                ThePerson => new PlayedScript([], [], [], [], (1, 50), null),
                TheSign => Nothing with { Fights = TestRules.OneAlone, AfterTheFight = after },
                after => Sets(Moved),
                _ => Nothing,
            });

        MovedAFlag moved = Assert.Single(played.FlagMoves, m => m.Address == after);

        Assert.Equal(WhatRanIt.ASign, moved.From);
    }

    // --------------------------------------------------------------------------- which ones

    /// <summary>
    /// A sign the walk stands beside is recorded, with how many times it read it over the run.
    /// </summary>
    [Fact]
    public void ItRecordsWhichSignsItStoodInFrontOfAndHowOften()
    {
        Attempt played = Run(new WorldData([AllFourLists()]));

        RanASign read = Assert.Single(played.SignsRead);

        Assert.Equal("1.0", read.MapId);
        Assert.Equal(TheSign, read.Address);
        Assert.True(read.Times >= played.Passes, "a sign beside the walk is read on every pass");
    }

    /// <summary>
    /// THE DISCRIMINATION: keyed by (map, address). 519 sign scripts sit at 360 addresses
    /// because blocks are shared, so one block read in two towns is TWO signs read and ONE
    /// address — which is 224's finding standing in the run rather than in the scan.
    /// </summary>
    [Fact]
    public void OneBlockReadInTwoTownsIsTwoSignsAndOneAddress()
    {
        var world = new WorldData(
        [
            Room("1.0") with
            {
                Signs = [new MapSign(1, 1, Kind: 0, TheSign)],
                Warps = [new Warp(3, 1, 0, "2.0")],
            },
            Room("2.0") with
            {
                Signs = [new MapSign(1, 1, Kind: 0, TheSign)],
                Warps = [new Warp(3, 1, 0, "1.0")],
            },
        ]);

        Attempt played = Run(world);

        Assert.Equal(2, played.SignsRead.Count);
        Assert.Single(played.SignsRead.Select(s => s.Address).Distinct());
        Assert.Equal(["1.0", "2.0"], [.. played.SignsRead.Select(s => s.MapId).Order()]);
    }

    /// <summary>And a sign nothing can stand beside is not recorded as read.</summary>
    [Fact]
    public void ASignTheWalkNeverStandsBesideIsNotRead()
    {
        // A one-wide room three tall with the middle square solid: the walk can only ever
        // stand on the near one and the far square is beside nothing it reaches.
        var world = new WorldData(
        [
            new MapData("1.0", "1.0", 1, 3, [0, 1, 0])
            {
                Signs = [new MapSign(0, 2, Kind: 0, TheSign)],
            },
        ]);

        Assert.Empty(Run(world).SignsRead);
    }

    // ------------------------------------------------------------------------- the control

    /// <summary>
    /// THE CONTROL IS A CONTROL: with the fourth list switched off, no sign runs and nothing a
    /// sign would have done is in the answer.
    /// </summary>
    [Fact]
    public void WithTheFourthListOffNoSignRuns()
    {
        Attempt without = Run(new WorldData([AllFourLists()]), readSigns: false);

        Assert.Empty(without.SignsRead);
        Assert.DoesNotContain(without.FlagMoves, m => m.From == WhatRanIt.ASign);

        // And the other three lists are untouched, or the control is measuring something else.
        Assert.Contains(without.FlagMoves, m => m.From == WhatRanIt.APerson);
        Assert.Contains(without.FlagMoves, m => m.From == WhatRanIt.ATrigger);
        Assert.Contains(without.FlagMoves, m => m.From == WhatRanIt.OnArrival);
    }

    /// <summary>
    /// And on a world with no signs in it the two runs agree exactly — the half without which
    /// "the control always comes back smaller" would pass the test above.
    /// </summary>
    [Fact]
    public void OnAWorldWithNoSignsTheControlIsTheSameRun()
    {
        var world = new WorldData([AllFourLists() with { Signs = [] }]);

        Attempt with = Run(world);
        Attempt without = Run(world, readSigns: false);

        Assert.Equal([.. with.Flags.Order()], [.. without.Flags.Order()]);
        Assert.Equal(with.Reached.Count, without.Reached.Count);
        Assert.Equal(with.Passes, without.Passes);
    }
}
