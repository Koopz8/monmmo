using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A trainer beaten stays beaten. A trainer lost to does not.
/// <para>
/// The run marked one fought before the fight happened, so a loss was final: it met GIOVANNI on
/// its first pass with whatever it had, lost, and never went back — while every pass after that
/// made the party stronger. A player who loses wakes up in a centre and walks in again, which
/// is what the healing was already modelling one step too late.
/// </para>
/// <para>
/// And the other half: <b>nothing ever told the reader a trainer had been beaten.</b> A
/// <c>trainerbattle</c> is its own conditional — beaten, the fight does nothing and the script
/// carries on into whatever the victory was for — so with <c>HasBeaten</c> false at every site,
/// every script containing a fight stopped at the fight on every pass, forever. That is SILPH
/// CO.'s <c>setflag 0x003E</c>, eleven commands past GIOVANNI, and two sessions were spent on it.
/// </para>
/// </summary>
public class GoingBackForARematchTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// A run that keeps opening something for a few passes, so the loop keeps going and the
    /// same trainer gets every chance to be met again.
    /// </summary>
    private static Attempt Run(int trainer, int level, ISet<int>? beaten = null)
    {
        MapData start = Room("1.0") with { Objects = [Person(1, 0x1000), Person(2, 0x2000)] };

        var opened = 0x100;
        var asked = 0;

        return Autoplayer.Play(
            new WorldData([start]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? new PlayedScript(asked++ < 4 ? [opened++] : [], [], [], [], (1, level), null)
                : Nothing with { Fights = trainer },
            beaten: beaten);
    }

    /// <summary>
    /// The one it cannot beat is met again and again; the count of trainers that stopped it is
    /// still one. Both halves matter — the retry is the point, and reporting seven attempts as
    /// seven walls is how a party closing the gap looks like one falling further behind.
    /// </summary>
    [Fact]
    public void ATrainerItLosesToIsMetAgainOnALaterPass()
    {
        Attempt played = Run(TestRules.ThreeStrong, level: 2);

        Assert.Equal(1, played.FightsLost);
        Assert.True(
            played.FightAttemptsLost > 1,
            $"it lost {played.FightAttemptsLost} time(s), so it never went back");
    }

    /// <summary>And one it beats is not fought a second time, which is what a flag means.</summary>
    [Fact]
    public void ATrainerItBeatsIsNotFoughtAgain()
    {
        Attempt played = Run(TestRules.OneAlone, level: 50);

        Assert.Equal(1, played.FightsWon);
        Assert.Equal(0, played.FightAttemptsLost);
    }

    /// <summary>
    /// And whoever reads the scripts is told. Without this a <c>trainerbattle</c> is in front of
    /// the script forever and everything the victory unlocks is behind it.
    /// </summary>
    [Fact]
    public void WhoeverRunsTheScriptsIsToldWhoHasBeenBeaten()
    {
        var beaten = new HashSet<int>();

        Run(TestRules.OneAlone, level: 50, beaten);

        Assert.Contains(TestRules.OneAlone, beaten);
    }

    /// <summary>
    /// And not told about one it lost to, which would open every door a victory opens for
    /// nothing — the same error in the direction nobody notices.
    /// </summary>
    [Fact]
    public void AndNotToldAboutOneItLostTo()
    {
        var beaten = new HashSet<int>();

        Run(TestRules.ThreeStrong, level: 2, beaten);

        Assert.DoesNotContain(TestRules.ThreeStrong, beaten);
    }
}
