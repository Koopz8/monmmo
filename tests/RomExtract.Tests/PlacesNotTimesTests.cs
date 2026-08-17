using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The error bars this project quotes were counting its own passes.
/// <para>
/// <c>N calls to M routines it could not answer</c> and <c>N script runs stopped at M commands
/// this project has no width for</c> are the two numbers that say how much of a run was decided
/// by something unreadable. Both counted every run of every script on every pass — and the run
/// is a fixpoint that talks to everybody again each time round. On a real cartridge the floor
/// run asked 5047 times at 319 places, and stopped 399 times at 40.
/// </para>
/// <para>
/// Both are true and they answer different questions. Only one of them is about the cartridge.
/// </para>
/// </summary>
public class PlacesNotTimesTests
{
    private const int Routine = 0x0187;

    private static MapData Room() => new("1.0", "1.0", 4, 4, new byte[16]);

    private static MapObject Person(int localId, int x, int y, uint script) =>
        new(localId, 1, x, y, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    private static PlayedScript Asks => new([], [], [], [Routine], null, null);

    /// <summary>
    /// One person who keeps something opening so the loop runs several passes, and one who asks
    /// a routine nothing can answer every time they are spoken to.
    /// </summary>
    [Fact]
    public void OnePlaceAskedOnEveryPassIsOnePlaceAndSeveralTimes()
    {
        MapData start = Room() with { Objects = [Person(1, 0, 0, 0x1000), Person(2, 1, 1, 0x2000)] };

        var opened = 0x100;
        var asked = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? new PlayedScript(asked++ < 3 ? [opened++] : [], [], [], [], null, null)
                : Asks);

        Assert.Equal(1, played.Specials.Values.Sum());
        Assert.True(
            played.AskedSpecials > 1,
            $"it asked {played.AskedSpecials} time(s), so the fixpoint only went round once and this proves nothing");

        // AND NONE OF THAT FOLDING WAS A DOOR. This is the same script on a later pass, which
        // is the ordinary case and by far the commonest — 5047 asks folding to 319 places, six
        // of them doors. Without this the two kinds are one number and the finding cannot be
        // stated at all, which is how the break came back green the first time.
        Assert.Equal(0, played.FoldedByDoor);
    }

    /// <summary>
    /// AND THE DENOMINATOR HAS TO BE ABLE TO BE ONE. A run that asks once reports one place and
    /// one ask, so "counted per place" and "there was only one pass" are different answers.
    /// </summary>
    [Fact]
    public void OnePlaceAskedOnceIsOnePlaceAndOneTime()
    {
        MapData start = Room() with { Objects = [Person(2, 1, 1, 0x2000)] };

        var asked = 0;

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => asked++ == 0 ? Asks : Nothing);

        Assert.Equal(1, played.Specials.Values.Sum());
        Assert.Equal(1, played.AskedSpecials);
        Assert.Equal(0, played.FoldedByDoor);
    }

    /// <summary>
    /// And two doors into one scene are one place, with the door folding counted apart from the
    /// pass folding — because the prediction that doors were what inflated these numbers was
    /// wrong, and a number nobody can separate cannot say so.
    /// </summary>
    [Fact]
    public void TwoDoorsIntoOneSceneAreOnePlaceAndTheFoldIsCountedAsADoor()
    {
        MapData start = Room() with { Objects = [Person(1, 0, 0, 0x1000), Person(2, 1, 1, 0x2000)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (_, _, _) => Asks,
            doorsTo: new Dictionary<uint, uint> { [0x1000] = 0x9000, [0x2000] = 0x9000 });

        Assert.Equal(1, played.Specials.Values.Sum());
        Assert.True(played.FoldedByDoor > 0, "the second door was not counted as a door");
    }

    /// <summary>And without the doors, the same two scripts are two places.</summary>
    [Fact]
    public void TwoScriptsThatShareNothingAreTwoPlaces()
    {
        MapData start = Room() with { Objects = [Person(1, 0, 0, 0x1000), Person(2, 1, 1, 0x2000)] };

        Attempt played = Autoplayer.Play(
            new WorldData([start]), "1.0", TestRules.All, (_, _, _) => Asks);

        Assert.Equal(2, played.Specials.Values.Sum());
        Assert.Equal(0, played.FoldedByDoor);
    }
}
