using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What each of the five kinds of script opens that <b>no other kind does</b> — the number that
/// would have caught 224's fault at 221.
/// <para>
/// 221 unified five copies of "every script on a map" onto a shared list with three of the five
/// kinds. Nothing anywhere printed what the missing two reached alone, so the loss showed up
/// three milestones later as twenty routines appearing out of nowhere. On this cartridge they
/// open <b>2491 byte positions</b> nothing else does — 1324 for the map's own script list, 1167
/// for what it runs on arrival, out of 24491 in all.
/// </para>
/// <para>
/// A kind whose every position is reached by some other kind can be dropped and cost nothing.
/// That is a fact about the cartridge, and it has to be possible for the number to come back
/// nought or it is not evidence when it does not.
/// </para>
/// </summary>
public sealed class WhatEachKindOpensAloneTests
{
    private static IReadOnlyDictionary<string, IReadOnlyCollection<int>> Places(
        params (string Kind, int[] At)[] kinds) =>
        kinds.ToDictionary(k => k.Kind, k => (IReadOnlyCollection<int>)k.At);

    /// <summary>
    /// A position two kinds both reach is nobody's alone; one only this kind reaches is.
    /// </summary>
    [Fact]
    public void OnlyCountsThePositionsNoOtherKindReaches()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> places = Places(
            ("person", [0x1000, 0x2000]),
            ("on load", [0x2000, 0x3000]));

        Assert.Equal(1, WhatTheScanOpens.OnlyHere(places, "person"));
        Assert.Equal(1, WhatTheScanOpens.OnlyHere(places, "on load"));
    }

    /// <summary>
    /// AND IT COMES BACK NOUGHT for a kind that reaches nothing of its own — the answer that
    /// makes the number evidence when it is not nought.
    /// </summary>
    [Fact]
    public void AKindThatReachesNothingOfItsOwnCostsNothingToDrop()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> places = Places(
            ("person", [0x1000, 0x2000, 0x3000]),
            ("sign", [0x1000, 0x2000]));

        Assert.Equal(0, WhatTheScanOpens.OnlyHere(places, "sign"));
        Assert.Equal(1, WhatTheScanOpens.OnlyHere(places, "person"));
    }

    /// <summary>
    /// A POSITION SHARED WITH A THIRD KIND IS STILL NOT ALONE. Without this, a rule that only
    /// compared against one other kind would pass everything above.
    /// </summary>
    [Fact]
    public void APositionSharedWithAnyOtherKindAtAllIsNotAlone()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> places = Places(
            ("on load", [0x1000, 0x2000]),
            ("person", [0x1000]),
            ("trigger", [0x2000]));

        Assert.Equal(0, WhatTheScanOpens.OnlyHere(places, "on load"));
    }

    /// <summary>
    /// The only kind there is opens everything alone — the shape a one-kind scan would have, and
    /// the reason the number is about a LIST of kinds rather than about a kind.
    /// </summary>
    [Fact]
    public void TheOnlyKindThereIsOpensEverythingAlone()
    {
        Assert.Equal(2, WhatTheScanOpens.OnlyHere(Places(("person", [0x1000, 0x2000])), "person"));
    }

    /// <summary>
    /// AND THE SAME RULE, ASKED OF ROUTINE NUMBERS RATHER THAN BYTE POSITIONS — it is one rule
    /// and it returns the items, not just how many.
    /// <para>
    /// On the cartridge: nine routines only the map's own script list asks and eleven only what
    /// it runs on arrival — <b>the twenty 224 found by comparing two runs of the whole
    /// instrument</b>, arrived at here directly and from a different direction.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameRuleNamesWhichRoutinesOnlyOneKindAsks()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> asked = Places(
            ("person", [0x188, 0x194]),
            ("on load", [0x0A7, 0x194]));

        Assert.Equal([0x0A7], WhatTheScanOpens.OnlyIn(asked, "on load"));
        Assert.Equal([0x188], WhatTheScanOpens.OnlyIn(asked, "person"));

        // And the count is the same rule counted, not a second one.
        Assert.Equal(
            WhatTheScanOpens.OnlyIn(asked, "on load").Count,
            WhatTheScanOpens.OnlyHere(asked, "on load"));
    }

    /// <summary>
    /// What comes back is ORDERED, because a list of routine numbers that changes order between
    /// runs cannot be read against the last milestone's.
    /// </summary>
    [Fact]
    public void WhatOnlyOneKindHasComesBackInOrder()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<int>> asked = Places(
            ("on load", [0x1B9, 0x0A7, 0x142]),
            ("person", [0x188]));

        Assert.Equal([0x0A7, 0x142, 0x1B9], WhatTheScanOpens.OnlyIn(asked, "on load"));
    }

    /// <summary>
    /// THE ROWS, ASSEMBLED — where the two ALONE columns are decided, and the thing that was
    /// unguarded while it lived inside a sweep that needs a whole cartridge.
    /// <para>
    /// A break making the routines column the kind's own set rather than what only it asks came
    /// back green against every test above. Fifth time in nine milestones that a green break
    /// meant the rule was somewhere no fixture could reach.
    /// </para>
    /// </summary>
    [Fact]
    public void ARowsAloneColumnsAreWhatNoOtherKindHas()
    {
        List<WhatTheScanOpens.AKind> rows = WhatTheScanOpens.Assemble(
            new Dictionary<string, WhatTheScanOpens.Gathered>
            {
                ["person"] = new(1584, 1250, 39446, [0x1000, 0x2000], [0x188, 0x194]),
                ["on load"] = new(234, 163, 2770, [0x2000, 0x3000], [0x0A7, 0x194]),
            });

        WhatTheScanOpens.AKind onLoad = Assert.Single(rows.Where(r => r.Kind == "on load"));

        Assert.Equal(2, onLoad.Places);
        Assert.Equal(1, onLoad.Only);
        Assert.Equal(2, onLoad.Routines);
        Assert.Equal([0x0A7], onLoad.RoutinesOnly);

        // And the row carries what was gathered rather than recomputing it.
        Assert.Equal(234, onLoad.Entries);
        Assert.Equal(163, onLoad.Addresses);
        Assert.Equal(2770, onLoad.Reads);
    }

    /// <summary>
    /// And the kinds are told apart by name, including the two whose names are two words.
    /// </summary>
    [Theory]
    [InlineData("person 3", "person")]
    [InlineData("trigger (5,15)", "trigger")]
    [InlineData("sign (9,43)", "sign")]
    [InlineData("on arrival (0x4050 == 1)", "on arrival")]
    [InlineData("on load (kind 3)", "on load")]
    public void EachKindIsNamedByWhatHangsIt(string what, string kind) =>
        Assert.Equal(kind, WhatTheScanOpens.KindOf(what));

    /// <summary>
    /// The two-word ones are not one word: a rule that took the first word alone would fold the
    /// two kinds 224 was about into a single "on", and they are the two that were missing.
    /// </summary>
    [Fact]
    public void TheTwoKindsThatWereMissingAreTwoKindsAndNotOne()
    {
        Assert.NotEqual(
            WhatTheScanOpens.KindOf("on load (kind 3)"),
            WhatTheScanOpens.KindOf("on arrival (0x4050 == 1)"));
    }
}
