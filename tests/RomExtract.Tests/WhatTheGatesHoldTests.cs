using PokeMmo.Core.World;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Every line this project prints about flag gates counts GATES. Four numbers about what those
/// gates HOLD have been quoted in the prompt since milestone 190 and <b>no instrument printed one
/// of them</b> — 231 marked the debt and this is it being paid.
/// <para>
/// Two of the four came back exact after seventy-three milestones of being uncheckable: <b>146</b>
/// objects behind the fifteen tree-and-rock gates, and <b>158</b> behind all twenty-seven obstacle
/// gates. One is withdrawn — no split of this cartridge produces "62 gates hold 240 people".
/// </para>
/// </summary>
public sealed class WhatTheGatesHoldTests
{
    private static MapObject Person(int id, int hiddenBy) =>
        new(id, 1, id, 1, Direction.Down, 0, false, 0, 0, 0x08160000, 0, 0, null)
        {
            HiddenBy = hiddenBy,
        };

    private static FlagGates Gates(params (int Flag, int People)[] behind)
    {
        var objects = new List<MapObject>();
        var id = 1;

        foreach ((int flag, int people) in behind)
        {
            for (var i = 0; i < people; i++) objects.Add(Person(id++, flag));
        }

        return new FlagGates(new WorldData([
            new MapData("1.0", "SOMEWHERE", 8, 8, new byte[64]) { Objects = objects },
        ]));
    }

    /// <summary>
    /// THE TOTAL AND THE GATE COUNT ARE DIFFERENT NUMBERS. One gate holding thirty-two people and
    /// thirty-two gates holding one each are the same total and opposite facts, which is exactly
    /// what 190's withdrawn number was a claim about.
    /// </summary>
    [Fact]
    public void GatesAndWhatTheyHoldAreCountedSeparately()
    {
        WhatGatesHold held = WhatTheGatesHold.Of(Gates((1, 3), (2, 1)), [1, 2]);

        Assert.Equal(2, held.Gates);
        Assert.Equal(4, held.Objects);
    }

    /// <summary>
    /// A FLAG THAT HOLDS NOTHING IS STILL A GATE. The boat's two are, and folding them out would
    /// make "322 gating flags" and "320 gate somebody standing there" one number instead of two
    /// facts.
    /// </summary>
    [Fact]
    public void AFlagHoldingNothingIsStillCountedAsAGate()
    {
        WhatGatesHold held = WhatTheGatesHold.Of(Gates((1, 2)), [1, 999]);

        Assert.Equal(2, held.Gates);
        Assert.Equal(2, held.Objects);
        Assert.Equal(1, held.HoldingNothing);
    }

    /// <summary>
    /// AND HOLDING MORE THAN ONE IS ITS OWN COUNT, with the objects in those gates beside it —
    /// two numbers, because "21 gates hold more than one" and "175 objects between them" are the
    /// pair 190's claim was made of and either alone says nothing.
    /// </summary>
    [Fact]
    public void HoldingSeveralIsCountedBothWays()
    {
        WhatGatesHold held = WhatTheGatesHold.Of(Gates((1, 5), (2, 1), (3, 2)), [1, 2, 3]);

        Assert.Equal(2, held.HoldingSeveral);
        Assert.Equal(7, held.InTheSeveral);

        // …and the one holding exactly one is not one of them, which is the discrimination.
        Assert.Equal(8, held.Objects);
    }

    /// <summary>
    /// THE SHAPE PUTS EACH GATE IN EXACTLY ONE BAND, so the bands add up to the gates. A gate in
    /// two bands is how a total stops being a split.
    /// </summary>
    [Fact]
    public void EveryGateLandsInExactlyOneBand()
    {
        FlagGates gates = Gates((1, 0), (2, 1), (3, 3), (4, 9), (5, 20));

        IReadOnlyList<(string Band, int Gates)> shape =
            WhatTheGatesHold.Shape(gates, [1, 2, 3, 4, 5]);

        Assert.Equal(5, shape.Sum(s => s.Gates));
        Assert.Equal(5, shape.Count);
    }

    /// <summary>
    /// AND THE BANDS ARE NAMED, all five of them, so "five bands" cannot be satisfied by whatever
    /// five the code happened to produce — 251's lesson.
    /// </summary>
    [Fact]
    public void AllFiveBandsAreProducedAndNamedHere()
    {
        FlagGates gates = Gates((1, 0), (2, 1), (3, 3), (4, 9), (5, 20));

        Assert.Equal(
            ["hold 2-4", "hold 5-16", "hold more than 16", "hold nothing", "hold one"],
            WhatTheGatesHold.Shape(gates, [1, 2, 3, 4, 5]).Select(s => s.Band).Order());
    }

    /// <summary>
    /// AND ASKING ABOUT NO FLAGS AT ALL ANSWERS NOUGHT RATHER THAN THROWING. The obstacle split
    /// asks about a set that could be empty on another cartridge, and an instrument that falls
    /// over on the empty case is one nobody can point at a different image.
    /// </summary>
    [Fact]
    public void AskingAboutNothingAnswersNought()
    {
        WhatGatesHold held = WhatTheGatesHold.Of(Gates((1, 2)), []);

        Assert.Equal(0, held.Gates);
        Assert.Equal(0, held.Objects);
        Assert.Empty(WhatTheGatesHold.Shape(Gates((1, 2)), []));
    }
}
