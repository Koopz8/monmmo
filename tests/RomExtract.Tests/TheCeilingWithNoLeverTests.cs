using PokeMmo.Core.Scripts;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The third ceiling, and the only one with no lever.
/// <para>
/// <c>--say-yes</c> and <c>--boat</c> are both named, printed and switchable. This one arrived
/// at milestone 200 by reading two command widths <em>correctly</em>: once the reader could
/// step over <c>0x92</c>, the run walked past every money check in the game and took the arm
/// where the thing is handed over — with a purse of nought.
/// </para>
/// <para>
/// On the cartridge it is eight places wide and worth exactly one Pokémon: <c>16.0</c>
/// <c>0x0816F75F</c> asks for 500 and hands over <c>#129</c> at level 5 regardless, which
/// evolves into the <c>#130</c> at 71 that turned up in the party. At the floor it is one place
/// wide and worth nothing.
/// </para>
/// <para>
/// <b>Both halves are guarded and they are different claims.</b> How WIDE the gap is and what
/// the gap is currently WORTH can each be nought, and they mean opposite things: a run that
/// never met a money check and a run that met eight and was given nothing both carry only what
/// they earned, but only the second says the reading has got that far.
/// </para>
/// </summary>
public sealed class TheCeilingWithNoLeverTests
{
    private const uint AsksAndGives = 0x1000;

    private const uint AsksAndGivesNothing = 0x2000;

    private const uint GivesWithoutAsking = 0x3000;

    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>
    /// Three people on two maps. One is asked for money and hands something over anyway, one is
    /// asked and hands over nothing, and one hands something over having never been asked.
    /// <para>
    /// Two maps because the key is <c>(map, script)</c> and 194's rule is that a fixture needs
    /// two of whatever the key is made of.
    /// </para>
    /// </summary>
    private static WorldData ThreeShops() =>
        new(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false) { ScriptAddress = AsksAndGives },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = AsksAndGivesNothing,
                    },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = GivesWithoutAsking,
                    },
                ],
            },
        ]);

    private static Attempt Play() =>
        Autoplayer.Play(
            ThreeShops(),
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                AsksAndGives => new PlayedScript([], [], [], [], (129, 5), null)
                {
                    MoneyWalkedPast = [500],
                },
                AsksAndGivesNothing => Nothing with { MoneyWalkedPast = [350] },
                _ => new PlayedScript([], [], [], [], (25, 3), null),
            });

    /// <summary>
    /// How wide the gap is: every place that asked, whether or not anything came of it.
    /// </summary>
    [Fact]
    public void EveryPlaceThatAsksForMoneyIsCountedWhetherOrNotItGaveAnything()
    {
        Assert.Equal(2, Play().WalkedPastAMoneyCheck);
    }

    /// <summary>
    /// And what the gap is worth, which is the smaller number and the one that says the party
    /// is above the floor.
    /// <para>
    /// The discrimination this whole fixture exists for: the place that asked and gave nothing
    /// is in the count above and must NOT be in this list, or the two numbers are one number
    /// and the ceiling cannot be told from the size of the thing it is measuring.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyThePlacesThatHandedSomethingOverAreWorthAnything()
    {
        PaidForNothing free = Assert.Single(Play().TookSomethingAnyway);

        Assert.Equal("1.0", free.MapId);
        Assert.Equal(AsksAndGives, free.Address);
        Assert.Equal(500, free.Price);
        Assert.Contains("129", free.What);
    }

    /// <summary>
    /// The ordinary case, asserted: something handed over with no money check anywhere near it
    /// is not part of this at all.
    /// <para>
    /// 195's lesson applied rather than discovered. Without it, "every handover is unpaid for"
    /// passes both tests above.
    /// </para>
    /// </summary>
    [Fact]
    public void AHandoverNobodyAskedMoneyForIsNotOnTheList()
    {
        Attempt played = Play();

        Assert.DoesNotContain(played.TookSomethingAnyway, p => p.Address == GivesWithoutAsking);

        // And it is not in the wider count either — two places asked, and the third handed
        // something over without being asked, so it belongs to neither number.
        Assert.Equal(2, played.WalkedPastAMoneyCheck);
        Assert.NotEmpty(played.Party);
    }

    /// <summary>A place that asks for two different amounts inside one script.</summary>
    private const uint AsksTwoPrices = 0x4000;

    /// <summary>
    /// Three asking places across two maps, one of which asks twice and one of which is the
    /// SAME script address on both maps.
    /// <para>
    /// Kept apart from <see cref="ThreeShops"/> because the count those tests assert is the
    /// thing this fixture deliberately changes. Two of whatever the key is made of, per 194:
    /// two addresses on one map, and one address on two maps.
    /// </para>
    /// </summary>
    private static WorldData FourCounters() =>
        new(
        [
            Room("1.0") with
            {
                Warps = [new Warp(3, 1, 0, "2.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = AsksAndGivesNothing,
                    },
                    new MapObject(2, 1, 2, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = AsksTwoPrices,
                    },
                ],
            },
            Room("2.0") with
            {
                Warps = [new Warp(3, 1, 0, "1.0")],
                Objects =
                [
                    new MapObject(1, 1, 1, 1, Direction.Down, 0, false)
                    {
                        ScriptAddress = AsksAndGivesNothing,
                    },
                ],
            },
        ]);

    private static Attempt Counters() =>
        Autoplayer.Play(
            FourCounters(),
            "1.0",
            TestRules.All,
            (address, _, _) => address switch
            {
                // The flag is what makes the fixpoint run a second pass — without something
                // changing, the run settles after one and "places, not times" is untestable.
                AsksTwoPrices => new PlayedScript([7], [], [], [], null, null)
                {
                    MoneyWalkedPast = [1000, 10000],
                },
                _ => Nothing with { MoneyWalkedPast = [350] },
            });

    /// <summary>
    /// THE COUNT AND THE LIST ARE TWO CLAIMS AND THEY HAVE TO AGREE.
    /// <para>
    /// This ceiling was a number with no list for eight milestones. "8 places ask the run for
    /// money" reads the same whether those eight are eight shopkeepers or one shopkeeper and
    /// seven counters nobody has looked at — and 208 read a coin counter off the cartridge
    /// without being able to say whether the run stands in front of one. A number with no list
    /// cannot come back surprising.
    /// </para>
    /// </summary>
    [Fact]
    public void TheListIsAsLongAsTheCountAndSaysWhichPlacesAsked()
    {
        Attempt played = Counters();

        Assert.Equal(3, played.WalkedPastAMoneyCheck);
        Assert.Equal(played.WalkedPastAMoneyCheck, played.MoneyChecks.Count);

        Assert.Equal(
            new[] { ("1.0", AsksAndGivesNothing), ("1.0", AsksTwoPrices), ("2.0", AsksAndGivesNothing) },
            played.MoneyChecks.Select(m => (m.MapId, m.Address)).OrderBy(m => m.MapId).ThenBy(m => m.Address));
    }

    /// <summary>
    /// The same script on two maps is two places, which is 196's key arriving somewhere new.
    /// </summary>
    [Fact]
    public void TheSameScriptOnTwoMapsIsTwoPlacesThatAsked()
    {
        IReadOnlyList<AskedForMoney> asked =
            [.. Counters().MoneyChecks.Where(m => m.Address == AsksAndGivesNothing)];

        Assert.Equal(2, asked.Count);
        Assert.Equal(new[] { "1.0", "2.0" }, asked.Select(a => a.MapId).Order());
    }

    /// <summary>
    /// A place that asks two different amounts keeps both.
    /// <para>
    /// The cartridge has one: the coin counter offers fifty coins and five hundred inside one
    /// person's script, at two different prices. Keeping only the first would say there is one
    /// price where there are two, and would say it silently.
    /// </para>
    /// </summary>
    [Fact]
    public void APlaceThatAsksTwoDifferentAmountsKeepsBoth()
    {
        AskedForMoney twice = Assert.Single(Counters().MoneyChecks, m => m.Address == AsksTwoPrices);

        Assert.Equal(new[] { 1000, 10000 }, twice.Prices.Order());
    }

    /// <summary>
    /// PLACES AND NOT TIMES, which is 195's rule and the one a fixpoint breaks by default.
    /// <para>
    /// Every pass runs every script again, so a list that appended would grow with the number
    /// of passes and the count beside it would stop being a count of places. The assertion on
    /// the pass count is not decoration: with one pass this test cannot fail.
    /// </para>
    /// </summary>
    [Fact]
    public void AskingOnEveryPassIsStillOnePlace()
    {
        Attempt played = Counters();

        Assert.True(played.Passes > 1, $"the fixture has to run more than once; it ran {played.Passes} time(s)");

        Assert.Equal(3, played.MoneyChecks.Count);
        Assert.All(played.MoneyChecks, m => Assert.Equal(m.Prices.Distinct().Count(), m.Prices.Count));
    }

    /// <summary>
    /// And the byte-level half: a real <c>0x92</c> in a real image is what produces this at all.
    /// <para>
    /// The three tests above hand the runner its answer ready-made, which is the shape of
    /// forgiving fixture milestone 189 was caught by — a stand-in that guards the plumbing and
    /// not the thing. This one reads the command.
    /// </para>
    /// </summary>
    [Fact]
    public void ARealMoneyCheckInRealBytesIsWhatProducesIt()
    {
        var image = new byte[0x2000];

        // ask about 500, then end.
        byte[] block = [0x92, 0xF4, 0x01, 0x00, 0x00, 0x00, 0x02];

        block.CopyTo(image, 0x100);

        ScriptRun run = ScriptRunner.Run(new Rom(image), 0x08000100, new ScriptState([]));

        Assert.Equal([500], run.MoneyWalkedPast);
    }

    /// <summary>
    /// And it comes back empty when nothing asks, which is the half that lets the number above
    /// mean anything.
    /// </summary>
    [Fact]
    public void AScriptThatAsksForNoMoneyReportsNone()
    {
        var image = new byte[0x2000];

        byte[] block = [0x29, 0x55, 0x00, 0x02];

        block.CopyTo(image, 0x100);

        ScriptRun run = ScriptRunner.Run(new Rom(image), 0x08000100, new ScriptState([]));

        Assert.Empty(run.MoneyWalkedPast);
    }
}
