using PokeMmo.Core.Battle;
using PokeMmo.Core.World;

namespace PokeMmo.RomExtract.Tests;

public class WildEncounterTests
{
    private static EncounterTable LandTable(int rate = 25) => new(
        EncounterKind.Land,
        rate,
        Enumerable.Range(0, 12).Select(i => new WildSlot(100 + i, 2 + i, 4 + i)).ToList());

    [Fact]
    public void EachEncounterKindHasItsOwnSlotCount()
    {
        Assert.Equal(12, WildEncounters.SlotCount(EncounterKind.Land));
        Assert.Equal(5, WildEncounters.SlotCount(EncounterKind.Water));
        Assert.Equal(5, WildEncounters.SlotCount(EncounterKind.RockSmash));
        Assert.Equal(10, WildEncounters.SlotCount(EncounterKind.Fishing));
    }

    [Fact]
    public void LandSlotWeightsSumToOneHundred()
    {
        Assert.Equal(100, WildEncounters.LandWeights.Sum());
        Assert.Equal(100, WildEncounters.WaterWeights.Sum());
    }

    [Fact]
    public void CommonSlotsComeUpFarMoreOftenThanRareOnes()
    {
        // The first slot is twenty times likelier than the last. If the weights were
        // ignored, rare encounters would appear as often as common ones and nothing
        // would look obviously broken.
        var rng = new BattleRng(4242);
        var counts = new int[12];

        for (int i = 0; i < 20000; i++)
            counts[WildEncounters.RollSlot(rng, EncounterKind.Land, 12)]++;

        Assert.True(counts[0] > counts[11] * 10);
        Assert.True(counts[0] > counts[6]);
        Assert.All(counts, c => Assert.True(c > 0));
    }

    [Fact]
    public void SlotRollsStayInsideTheTable()
    {
        var rng = new BattleRng(7);

        for (int i = 0; i < 2000; i++)
            Assert.InRange(WildEncounters.RollSlot(rng, EncounterKind.Land, 12), 0, 11);
    }

    [Fact]
    public void AShortTableNeverRollsPastItsEnd()
    {
        var rng = new BattleRng(9);

        for (int i = 0; i < 2000; i++)
            Assert.InRange(WildEncounters.RollSlot(rng, EncounterKind.Land, 3), 0, 2);
    }

    [Fact]
    public void MostStepsMeetNothing()
    {
        var rng = new BattleRng(11);
        int met = 0;

        for (int i = 0; i < 10000; i++)
            if (WildEncounters.StepMeetsSomething(rng, encounterRate: 25)) met++;

        // Roughly one step in seven at this rate; the point is that walking is mostly
        // uninterrupted.
        Assert.InRange(met, 500, 3000);
    }

    [Fact]
    public void ARateOfZeroMeansNoEncountersEver()
    {
        var rng = new BattleRng(13);

        for (int i = 0; i < 2000; i++)
            Assert.False(WildEncounters.StepMeetsSomething(rng, 0));
    }

    [Fact]
    public void AHigherRateMeetsMoreOften()
    {
        int Count(int rate)
        {
            var rng = new BattleRng(21);
            int met = 0;

            for (int i = 0; i < 10000; i++)
                if (WildEncounters.StepMeetsSomething(rng, rate)) met++;

            return met;
        }

        Assert.True(Count(50) > Count(10));
    }

    [Fact]
    public void ARolledEncounterComesFromTheTable()
    {
        var rng = new BattleRng(31);
        EncounterTable table = LandTable(rate: 100);

        WildEncounter? encounter = null;

        for (int i = 0; i < 500 && encounter is null; i++)
            encounter = WildEncounters.RollStep(rng, table);

        Assert.NotNull(encounter);

        WildSlot slot = table.Slots.Single(s => s.Species == encounter!.Species);
        Assert.InRange(encounter!.Level, slot.MinLevel, slot.MaxLevel);
        Assert.Equal(EncounterKind.Land, encounter.Kind);
    }

    [Fact]
    public void NoTableMeansNoEncounter()
    {
        var rng = new BattleRng(5);

        Assert.Null(WildEncounters.RollStep(rng, null));
        Assert.Null(WildEncounters.RollStep(rng, new EncounterTable(EncounterKind.Land, 0, [])));
    }

    [Fact]
    public void LevelsStayWithinTheSlotsRange()
    {
        var rng = new BattleRng(17);
        var slot = new WildSlot(25, MinLevel: 3, MaxLevel: 7);

        for (int i = 0; i < 1000; i++)
            Assert.InRange(slot.RollLevel(rng), 3, 7);
    }

    [Fact]
    public void AFixedLevelSlotAlwaysGivesThatLevel()
    {
        var rng = new BattleRng(19);
        var slot = new WildSlot(25, MinLevel: 5, MaxLevel: 5);

        for (int i = 0; i < 100; i++) Assert.Equal(5, slot.RollLevel(rng));
    }

    [Fact]
    public void TheSameSeedRollsTheSameEncounters()
    {
        // Encounters have to be reproducible for the same reason battles do: the
        // server decides, and the client must be able to arrive at the same answer.
        static List<string> Walk(uint seed)
        {
            var rng = new BattleRng(seed);
            var met = new List<string>();

            for (int step = 0; step < 500; step++)
                if (WildEncounters.RollStep(rng, LandTable()) is { } e)
                    met.Add($"{e.Species}@{e.Level}");

            return met;
        }

        Assert.Equal(Walk(1234), Walk(1234));
        Assert.NotEqual(Walk(1234), Walk(9876));
    }

    [Fact]
    public void AMapExposesOnlyTheTablesItHas()
    {
        var map = new MapEncounters("3.0", Land: LandTable());

        Assert.NotNull(map.For(EncounterKind.Land));
        Assert.Null(map.For(EncounterKind.Water));
        Assert.Single(map.All);
    }
}

public class MapNameMatchTests
{
    [Fact]
    public void AnExactNameBeatsALongerOneContainingIt()
    {
        // The bug this exists for: "route 1" is a substring of "ROUTE 17", so plain
        // containment hands back the wrong route.
        string[] maps = ["ROUTE 17", "ROUTE 1", "ROUTE 11"];

        Assert.Equal("ROUTE 1", MapNameMatch.Rank(maps, m => m, "route 1").First());
    }

    [Fact]
    public void ScoresExactAboveWholeWordAboveSubstring()
    {
        Assert.Equal(MapNameMatch.Exact, MapNameMatch.Score("ROUTE 1", "route 1"));
        Assert.Equal(MapNameMatch.WholeWord, MapNameMatch.Score("ROUTE 1 NORTH", "route 1"));
        Assert.Equal(MapNameMatch.Contains, MapNameMatch.Score("ROUTE 17", "route 1"));
        Assert.Equal(MapNameMatch.NoMatch, MapNameMatch.Score("PALLET TOWN", "route 1"));
    }

    [Fact]
    public void MatchingIsCaseAndWhitespaceInsensitive()
    {
        Assert.Equal(MapNameMatch.Exact, MapNameMatch.Score("PALLET TOWN", "  pallet town "));
    }

    [Fact]
    public void SizeBreaksTiesSoTheOutdoorMapWins()
    {
        (string Name, int Size)[] maps =
        [
            ("PALLET TOWN", 130),   // an interior
            ("PALLET TOWN", 480),   // the town itself
        ];

        Assert.Equal(480, MapNameMatch.Rank(maps, m => m.Name, "pallet town", m => m.Size).First().Size);
    }

    [Fact]
    public void NonMatchesAreLeftOut()
    {
        string[] maps = ["PALLET TOWN", "VIRIDIAN CITY"];

        Assert.Empty(MapNameMatch.Rank(maps, m => m, "cinnabar"));
        Assert.False(MapNameMatch.Matches("PALLET TOWN", "cinnabar"));
    }

    [Fact]
    public void AnEmptyQueryMatchesNothing()
    {
        Assert.Equal(MapNameMatch.NoMatch, MapNameMatch.Score("PALLET TOWN", ""));
        Assert.Equal(MapNameMatch.NoMatch, MapNameMatch.Score("PALLET TOWN", "   "));
    }

    [Fact]
    public void WorldDataUsesTheSameRule()
    {
        var world = new WorldData(
        [
            new MapData("3.17", "ROUTE 17", 10, 10, new byte[100]),
            new MapData("3.1", "ROUTE 1", 5, 5, new byte[25]),
        ]);

        // Route 17 is the larger map, so only exact-match ranking picks Route 1.
        Assert.Equal("3.1", world.FindByName("route 1")!.Id);
    }
}
