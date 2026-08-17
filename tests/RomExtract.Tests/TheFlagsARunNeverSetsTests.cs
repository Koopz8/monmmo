using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A run reports how many flags it set and nothing about what any of them hold up.
/// <para>
/// <b>"153 flags" cannot come back surprising.</b> A run that set a hundred and fifty marks on a
/// character and a run that opened a hundred and fifty doors print the same line, and diffing
/// two runs means comparing one number rather than two lists — which is why 207 had to
/// hand-patch a print into two worktrees to find out which three flags a milestone had added.
/// </para>
/// <para>
/// The denominator lives here rather than in whoever prints it, for the seventh time: a rule
/// about the world inside <c>Program.cs</c> is a rule nothing can fail.
/// </para>
/// </summary>
public sealed class TheFlagsARunNeverSetsTests
{
    private const int HidesSomebody = 0x0011;

    private const int HidesSomebodyElse = 0x0012;

    private const int OpensTheBoat = 0x0030;

    private const int GatesNothing = 0x4444;

    /// <summary>
    /// A world with three gating flags of TWO different kinds, and nothing else.
    /// <para>
    /// Two kinds on purpose. A world whose only gates are people cannot tell "it asked what
    /// gates something" from "it counted the people", and this cartridge has both kinds.
    /// </para>
    /// </summary>
    private static WorldData Gated() =>
        new(
        [
            new MapData("1.0", "1.0", 4, 4, new byte[16])
            {
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { HiddenBy = HidesSomebody },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false) { HiddenBy = HidesSomebodyElse },
                    new MapObject(3, 1, 3, 1, Direction.Down, 0, false),
                ],
            },
        ])
        {
            FerryPasses = [new FerryPass(OpensTheBoat, 1)],
        };

    /// <summary>
    /// The count is of flags that gate something, not of flags.
    /// <para>
    /// The set handed in holds one of each kind of gate and one flag that gates nothing, so an
    /// implementation that counted what it was given rather than what gates something answers
    /// three where the answer is two.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheFlagsThatGateSomethingAreCounted()
    {
        Assert.Equal(2, new FlagGates(Gated()).HowManyOf([HidesSomebody, OpensTheBoat, GatesNothing]));
    }

    /// <summary>
    /// And the half that says what is left: the gating flags the run never set, in order.
    /// </summary>
    [Fact]
    public void TheGatingFlagsARunNeverSetAreTheOnesItIsMissing()
    {
        Assert.Equal(
            new[] { HidesSomebodyElse, OpensTheBoat },
            new FlagGates(Gated()).NotIn([HidesSomebody, GatesNothing]));
    }

    /// <summary>
    /// IT HAS TO BE ABLE TO COME BACK EMPTY, or "110 gating flags it never set" is a number
    /// with only one direction.
    /// <para>
    /// A run that had set every gate would print a sentence nothing in this project has ever
    /// printed, and the only way to know it would is to make it happen.
    /// </para>
    /// </summary>
    [Fact]
    public void ARunThatSetEveryGateHasNothingLeft()
    {
        Assert.Empty(new FlagGates(Gated()).NotIn([HidesSomebody, HidesSomebodyElse, OpensTheBoat]));
    }

    /// <summary>
    /// And a flag that gates nothing is not made into a gate by being handed in — the ordinary
    /// case, asserted, which is what stops "everything is a gate" passing the three above.
    /// </summary>
    [Fact]
    public void AFlagThatGatesNothingIsNeverOnEitherSide()
    {
        var gates = new FlagGates(Gated());

        Assert.Equal(3, gates.Count);
        Assert.DoesNotContain(GatesNothing, gates.NotIn([]));
        Assert.Equal(0, gates.HowManyOf([GatesNothing]));
    }
}
