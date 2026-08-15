using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The machines that are not tiles.
/// <para>
/// The storage machine in a healing centre is a behaviour byte and nothing else — the map
/// carries no sign, no person and no script for it. That is why milestone 68 had to find
/// it by behaviour, and it is why it is the only machine this client can open.
/// </para>
/// <para>
/// The one in the player's bedroom is the other kind: a sign, on a square whose behaviour
/// byte is zero, whose script says one line and then hands the whole of itself to special
/// routines. This measures how alone a script's routines are, which is the most that can
/// be said about code without following it.
/// </para>
/// </summary>
public class ScriptedMachineTests
{
    private static SpecialCall Call(string map, string what, int routine) =>
        new(map, what, routine, null, [], [], []);

    /// <summary>
    /// A script whose routines nobody else calls is reported; one sharing everything is
    /// not. This is the whole test — a routine with a second caller is a service.
    /// </summary>
    [Fact]
    public void AScriptCallingRoutinesNobodyElseCallsIsFound()
    {
        List<OneOfAKind> found = ScriptedMachines.Find(
        [
            Call("4.1", "sign (1,1)", 0x017D),
            Call("4.1", "sign (1,1)", 0x00D6),
            Call("4.1", "sign (1,1)", 0x0187),
            Call("7.3", "person 2", 0x0187),
            Call("9.1", "person 4", 0x0187),
        ]);

        OneOfAKind machine = Assert.Single(found);

        Assert.Equal("4.1", machine.MapId);
        Assert.Equal([0x00D6, 0x017D], machine.Alone);
    }

    /// <summary>
    /// The most alone first, because the point of the ordering is to name the script that
    /// is furthest out of reach rather than to list everything.
    /// </summary>
    [Fact]
    public void TheMostAloneComesFirst()
    {
        List<OneOfAKind> found = ScriptedMachines.Find(
        [
            Call("4.1", "sign (1,1)", 1),
            Call("4.1", "sign (1,1)", 2),
            Call("4.1", "sign (1,1)", 3),
            Call("5.0", "person 1", 4),
        ]);

        Assert.Equal(["4.1", "5.0"], found.Select(m => m.MapId));
        Assert.Equal(3, found[0].Alone.Count);
    }

    /// <summary>
    /// One script calling the same routine twice is still one caller. PC scripts do this,
    /// and counting calls rather than callers would make every routine look shared.
    /// </summary>
    [Fact]
    public void CallingTheSameRoutineTwiceIsStillOneCaller()
    {
        List<OneOfAKind> found = ScriptedMachines.Find(
        [
            Call("4.1", "sign (1,1)", 0x0190),
            Call("4.1", "sign (1,1)", 0x0190),
        ]);

        Assert.Equal([0x0190], Assert.Single(found).Alone);
    }

    /// <summary>
    /// And a world where everything is shared reports nothing, rather than reporting
    /// whatever happens to be rarest.
    /// </summary>
    [Fact]
    public void AWorldOfSharedRoutinesReportsNothing()
    {
        List<OneOfAKind> found = ScriptedMachines.Find(
        [
            Call("4.1", "sign (1,1)", 0x0187),
            Call("7.3", "person 2", 0x0187),
        ]);

        Assert.Empty(found);
    }

    /// <summary>Two sites on one map are told apart, because a map is not a script.</summary>
    [Fact]
    public void TwoSitesOnOneMapAreTwoMachines()
    {
        List<OneOfAKind> found = ScriptedMachines.Find(
        [
            Call("4.1", "sign (1,1)", 1),
            Call("4.1", "sign (6,5)", 2),
        ]);

        Assert.Equal(2, found.Count);
        Assert.Equal(["sign (1,1)", "sign (6,5)"], found.Select(m => m.What).Order());
    }
}
