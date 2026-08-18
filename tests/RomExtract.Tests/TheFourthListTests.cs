using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The signs, which the run could not see.
/// <para>
/// <b>It was never a choice.</b> "The playthrough never runs signs" has been on the owed list for
/// milestones, and the reason turned out to be that <see cref="MapData"/> — the record the walk
/// and the server both go over — carried people, triggers, warps, doors and arrival scripts and
/// <b>no signs at all</b>. `MapSign` has existed since the map work and the map scan has read all
/// 519 scripted ones for as long as it has known five kinds; nothing ever compared the two lists.
/// That is 224's fault standing in the other half of the project.
/// </para>
/// <para>
/// Putting them in moved the floor for the first time in nine milestones — 153 flags to 160 — and
/// broke the fixpoint, because a sign is the first thing this run does that can take something
/// back. See <see cref="WhereItHasBeen"/>.
/// </para>
/// </summary>
public sealed class TheFourthListTests
{
    private const uint TheSign = 0x3000;
    private const uint ThePerson = 0x4000;
    private const int Read = 0x0500;

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static Attempt Run(MapData map, Func<uint, PlayedScript> what) =>
        Autoplayer.Play(new WorldData([map]), map.Id, TestRules.All, (address, _, _) => what(address));

    /// <summary>A four-by-four room anybody can walk anywhere in.</summary>
    private static MapData Room() => new("1.0", "1.0", 4, 4, new byte[16]);

    // ------------------------------------------------------------------- it runs them

    /// <summary>
    /// THE THING: a sign beside a square the walk stands on runs, and its flag is set.
    /// </summary>
    [Fact]
    public void ASignBesideASquareTheWalkStandsOnRuns()
    {
        Attempt played = Run(
            Room() with { Signs = [new MapSign(1, 1, Kind: 0, TheSign)] },
            address => address == TheSign ? Nothing with { FlagsSet = [Read] } : Nothing);

        Assert.Contains(Read, played.Flags);
    }

    /// <summary>
    /// And a HIDDEN ITEM is not a script. One value of the cartridge's own kind tag means the
    /// record holds an item id where every other holds a pointer — 183 of the 702 — and running
    /// one would be following an item id as an address.
    /// </summary>
    [Fact]
    public void AHiddenItemIsNotAScriptToRun()
    {
        Attempt played = Run(
            Room() with { Signs = [new MapSign(1, 1, MapSign.HiddenItem, TheSign)] },
            address => address == TheSign ? Nothing with { FlagsSet = [Read] } : Nothing);

        Assert.DoesNotContain(Read, played.Flags);
    }

    /// <summary>
    /// THE DISCRIMINATION: a sign is read from BESIDE it and not from across a counter, and a
    /// person in the same place is talked to across one.
    /// </summary>
    /// <remarks>
    /// 198 derived the counter rule for a shopkeeper standing behind one. A sign is not standing
    /// anywhere, and giving it that rule would be borrowing evidence from a different question.
    /// Both halves are here, because a fixture with only the sign cannot tell "signs do not use
    /// the counter rule" from "this fixture does not reach anything at all".
    /// </remarks>
    [Fact]
    public void ASignIsNotReadAcrossACounterAndAPersonIsTalkedToAcrossOne()
    {
        // A one-wide room three squares tall. The middle square is a COUNTER and is not
        // walkable, so the walk can only ever stand on the near one and the far square is
        // reachable by nothing except the counter rule.
        var behaviours = new byte[3];

        behaviours[1] = (byte)MetatileBehaviour.Counter;

        MapData across = new("1.0", "1.0", 1, 3, [0, 1, 0]) { Behaviours = behaviours };

        Attempt sign = Run(
            across with { Signs = [new MapSign(0, 2, Kind: 0, TheSign)] },
            address => address == TheSign ? Nothing with { FlagsSet = [Read] } : Nothing);

        Assert.DoesNotContain(Read, sign.Flags);

        Attempt person = Run(
            across with
            {
                Objects = [new MapObject(1, 1, 0, 2, Direction.Down, 0, false) { ScriptAddress = ThePerson }],
            },
            address => address == ThePerson ? Nothing with { FlagsSet = [Read] } : Nothing);

        Assert.Contains(Read, person.Flags);
    }

    // --------------------------------------------------------------- and it goes round

    /// <summary>
    /// A script that sets a flag when it is absent and clears it when it is there sends the loop
    /// round in a circle, and the run says so instead of running to the backstop.
    /// </summary>
    /// <remarks>
    /// This is `9.6` at fixture size: fifteen signs sharing one block that sets and clears
    /// <c>0x0001</c> depending on the answer, so a walk that stands in front of all fifteen every
    /// pass flips one flag on and off forever. Before the cycle test every `--say-yes` row ran to
    /// twenty-four passes and reported that something never settles.
    /// </remarks>
    [Fact]
    public void AFlagToggledEveryPassIsACycleAndNotABackstop()
    {
        var on = false;

        Attempt played = Run(
            Room() with { Signs = [new MapSign(1, 1, Kind: 0, TheSign)] },
            address =>
            {
                if (address != TheSign) return Nothing;

                on = !on;

                return on
                    ? Nothing with { FlagsSet = [Read] }
                    : Nothing with { FlagsCleared = [Read] };
            });

        Assert.Equal(StoppedBecause.ItWentRoundInACircle, played.Stopped);
        Assert.True(played.Passes < Autoplayer.MostPasses);
    }

    /// <summary>
    /// And a run that genuinely settles still says so — the half without which "always report a
    /// cycle" passes the test above.
    /// </summary>
    [Fact]
    public void AndARunThatSettlesStillSaysItSettled()
    {
        Attempt played = Run(
            Room() with { Signs = [new MapSign(1, 1, Kind: 0, TheSign)] },
            address => address == TheSign ? Nothing with { FlagsSet = [Read] } : Nothing);

        Assert.Equal(StoppedBecause.NothingMoreOpened, played.Stopped);
    }

    // ------------------------------------------------------------ and they travel

    /// <summary>
    /// Signs survive the world file, kind and all — the kind is what says whether there is a
    /// script behind one, so a round trip that dropped it would turn 183 hidden items into 183
    /// scripts at address nought.
    /// </summary>
    [Fact]
    public void SignsSurviveTheWorldFileWithTheirKind()
    {
        var world = new WorldData(
        [
            Room() with
            {
                Signs =
                [
                    new MapSign(1, 2, Kind: 0, TheSign),
                    new MapSign(3, 0, MapSign.HiddenItem, ScriptAddress: 0),
                ],
            },
        ]);

        using var file = new MemoryStream();

        world.Save(file);

        file.Position = 0;

        IReadOnlyList<MapSign> signs = WorldData.Load(file).Maps.Single().Signs;

        Assert.Equal(2, signs.Count);

        Assert.Equal(new GridPosition(1, 2), signs[0].Square);
        Assert.False(signs[0].IsHiddenItem);

        Assert.Equal(new GridPosition(3, 0), signs[1].Square);
        Assert.True(signs[1].IsHiddenItem);
    }
}
