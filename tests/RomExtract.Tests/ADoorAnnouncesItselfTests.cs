using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Which variable a door may announce itself in.
/// <para>
/// 173 found door numbers in the scratch pads and 194 built this instrument on them, so the rule
/// has been "below the scratch cliff" ever since. A door can announce itself in an ARGUMENT
/// variable just as well, and this cartridge has a twenty-two-door scene that does: <c>10.14</c>'s
/// slot machines say <c>0x8004 = 0</c> through <c>21</c>. Cut at the cliff alone, every one of
/// those stubs reads as a block doing something of its own and the scene is invisible — which it
/// was, for forty-three milestones.
/// </para>
/// <para>
/// Admitting the argument band takes <c>--entries</c> from 22 scenes and 68 doors to <b>26 and
/// 112</b>, and moves nothing the run reports at any lever setting.
/// </para>
/// </summary>
public sealed class ADoorAnnouncesItselfTests
{
    /// <summary>The cliff this project cut the scratch pads at, MODELLED at 173.</summary>
    private const int ScratchBelow = 0x4010;

    /// <summary>
    /// THE DISCRIMINATION: a scratch variable and an argument variable are both places a door can
    /// say which door it is, and the story's own memory is not.
    /// </summary>
    [Fact]
    public void ScratchAndArgumentBothAnnounceAndTheStoryDoesNot()
    {
        // The pads 173 found the door numbers in.
        Assert.True(EntriesToAScene.AnnouncesItself(0x4001, ScratchBelow));
        Assert.True(EntriesToAScene.AnnouncesItself(0x4002, ScratchBelow));

        // The argument band — 10.14's slot machines and TRAINER TOWER's doors.
        Assert.True(EntriesToAScene.AnnouncesItself(0x8004, ScratchBelow));
        Assert.True(EntriesToAScene.AnnouncesItself(0x8008, ScratchBelow));

        // THE STORY'S OWN MEMORY. A block that writes this before handing over is moving the
        // story on, not saying which door you came in by, and folding those together would fold
        // two scenes into one because they share an exit.
        Assert.False(EntriesToAScene.AnnouncesItself(0x4055, ScratchBelow));
        Assert.False(EntriesToAScene.AnnouncesItself(0x4010, ScratchBelow));
        Assert.False(EntriesToAScene.AnnouncesItself(0x406F, ScratchBelow));
    }

    /// <summary>
    /// The argument band has a far end. Everything from <c>0x8000</c> upwards would take in
    /// whatever else lives above it, and this project has read nothing up there — a rule with one
    /// edge is a rule that admits everything on one side of it.
    /// </summary>
    [Fact]
    public void TheArgumentBandHasBothEnds()
    {
        Assert.True(EntriesToAScene.AnnouncesItself(EntriesToAScene.FirstArgument, ScratchBelow));
        Assert.True(EntriesToAScene.AnnouncesItself(EntriesToAScene.LastArgument, ScratchBelow));

        Assert.False(EntriesToAScene.AnnouncesItself(EntriesToAScene.LastArgument + 1, ScratchBelow));
        Assert.False(EntriesToAScene.AnnouncesItself(0x8020, ScratchBelow));
        Assert.False(EntriesToAScene.AnnouncesItself(0x9000, ScratchBelow));
    }

    /// <summary>
    /// And the cliff is handed in rather than written here, so the one MODELLED number in this
    /// rule stays in the one place that derived it.
    /// </summary>
    [Fact]
    public void TheScratchCliffIsHandedIn()
    {
        Assert.True(EntriesToAScene.AnnouncesItself(0x4001, 0x4010));
        Assert.False(EntriesToAScene.AnnouncesItself(0x4001, 0x4000));
    }
}
