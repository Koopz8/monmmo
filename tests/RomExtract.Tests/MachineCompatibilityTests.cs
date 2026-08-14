using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Items;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Who a machine works on.
/// <para>
/// A PIDGEY that knows STRENGTH is not what the cartridge says, and this project let one
/// exist for its whole life because the table that forbids it had not been found. It is
/// one eight-byte word per species, one bit per machine — a shape so weak that sixteen
/// megabytes hold seven thousand runs of bytes that fit it.
/// </para>
/// <para>
/// So the shape is not what finds it. What finds it is agreement with a table located
/// separately and for another reason: a machine teaches a move, and something that learns
/// that move by growing up is something the machine can teach. That is the behaviour
/// test, and on a real image it separates the answer from every decoy by thirty points.
/// </para>
/// </summary>
public class MachineCompatibilityTests
{
    private const int Species = 40;

    /// <summary>Enough level-up evidence that a wrong table cannot agree by luck.</summary>
    private static readonly int[] Teaches =
        [.. Enumerable.Range(1, MachineMoves.Count)];

    private static void Put(byte[] image, int at, IEnumerable<ulong> words)
    {
        int i = at;

        foreach (ulong word in words)
            for (int b = 0; b < 8; b++) image[i++] = (byte)(word >> (b * 8));
    }

    /// <summary>Which machine a species is built around, so the two tables can agree.</summary>
    private static int Machine(int species) => species % MachineMoves.Count;

    /// <summary>
    /// Two bits each, one of them the species' own machine and one scattered.
    /// <para>
    /// Deliberately not nested. The first version of this gave species <c>n</c> the
    /// machines <c>0</c> through <c>n</c>, which reads perfectly and is useless: every
    /// word contains the one before it, so the same bytes read eight early agree with
    /// the level-up lists just as well as the real thing and the locator correctly
    /// refuses to choose. A decoy is only a decoy if it can be told apart.
    /// </para>
    /// </summary>
    private static List<ulong> Masks() =>
    [
        .. Enumerable.Range(0, Species).Select(s => s == 0
            ? 0UL
            : (1UL << Machine(s)) | (1UL << ((s * 17 + 3) % MachineMoves.Count)))
    ];

    /// <summary>And the level-up lists say the same thing about the same species.</summary>
    private static Dictionary<int, Learnset> Learnsets() =>
        Enumerable.Range(1, Species - 1).ToDictionary(
            s => s,
            s => new Learnset(s, [new LevelUpMove(5, Teaches[Machine(s)])]));

    private static MachineSets? Locate(byte[] image) =>
        MachineCompatibility.Locate(new Rom(image), Species, Teaches, Learnsets());

    [Fact]
    public void TheTableIsFoundByAgreeingWithTheLevelUpLists()
    {
        var image = new byte[0x2000];
        Put(image, 0x400, Masks());

        MachineSets? found = Locate(image);

        Assert.NotNull(found);
        Assert.Equal(0x400, found.Address);
        Assert.Equal(1.0, found.Agreement);
    }

    /// <summary>
    /// The shape alone is not enough, and this is the whole reason the behaviour test
    /// exists. A second run of words with the same quiet top bits sits right beside the
    /// real one and agrees with nothing.
    /// </summary>
    [Fact]
    public void SomethingWithTheRightShapeAndTheWrongBitsLoses()
    {
        var image = new byte[0x2000];

        // Every species allowed exactly one machine, and never the right one.
        Put(image, 0x400, Enumerable.Range(0, Species).Select(_ => 1UL << (MachineMoves.Count - 1)));
        Put(image, 0x1000, Masks());

        MachineSets? found = Locate(image);

        Assert.NotNull(found);
        Assert.Equal(0x1000, found.Address);
    }

    /// <summary>
    /// A run of zeros has a quiet top byte in every word of it and says nothing about
    /// anything. Half the species have to be able to learn something.
    /// </summary>
    [Fact]
    public void AnEmptyRunIsNotATable()
    {
        var image = new byte[0x2000];

        Assert.Null(Locate(image));
    }

    /// <summary>
    /// Sixty-four bits and fifty-eight machines: the top six are nothing on the real
    /// table, and a word that uses them is a word that is something else.
    /// </summary>
    [Fact]
    public void AWordThatUsesTheSpareBitsIsNotAWord()
    {
        var image = new byte[0x2000];

        List<ulong> masks = Masks();
        masks[7] |= 1UL << 63;

        Put(image, 0x400, masks);

        Assert.Null(Locate(image));
    }

    /// <summary>
    /// Two tables that score alike are not a tie to break carefully — they are the
    /// locator having no way to tell which one the cartridge means, and it says so by
    /// finding nothing rather than by taking the first.
    /// </summary>
    [Fact]
    public void TwoTablesThatScoreAlikeAreNoAnswer()
    {
        var image = new byte[0x2000];

        Put(image, 0x400, Masks());
        Put(image, 0x1000, Masks());

        Assert.Null(Locate(image));
    }

    /// <summary>
    /// And a best candidate that only half agrees is refused rather than used. A wrong
    /// table does not make machines behave oddly; it refuses moves that should be
    /// allowed, which reads to a player as the game being broken.
    /// </summary>
    [Fact]
    public void AWeakBestIsRefused()
    {
        var image = new byte[0x2000];

        List<ulong> half =
        [
            .. Masks().Select((word, index) => index % 2 == 0 ? word : 1UL << (MachineMoves.Count - 1))
        ];

        Put(image, 0x400, half);

        Assert.Null(Locate(image));
    }

    /// <summary>
    /// The real table has three exceptions on it — species that know a machine's move
    /// from birth and still cannot be taught it — so demanding perfect agreement would
    /// throw the right answer away.
    /// </summary>
    [Fact]
    public void AFewExceptionsDoNotSinkIt()
    {
        var image = new byte[0x2000];

        List<ulong> masks = Masks();

        // Three species that know the move and are refused the machine anyway. Given
        // some other machine rather than none, so the run still looks like a table.
        foreach (int s in new[] { 11, 22, 33 }) masks[s] = 1UL << ((s + 1) % MachineMoves.Count);

        Put(image, 0x400, masks);

        MachineSets? found = Locate(image);

        Assert.NotNull(found);
        Assert.Equal(0x400, found.Address);
        Assert.Equal(3, found.Disagreed);
    }

    // ---- what the rules file does with it -------------------------------------------

    [Fact]
    public void AMachineWorksOnWhatTheWordSaysAndNothingElse()
    {
        Assert.True(TestRules.All.CanBeTaught(1, TestRules.DiscItem));
        Assert.True(TestRules.All.CanBeTaught(1, TestRules.HiddenMachineItem));

        Assert.False(TestRules.All.CanBeTaught(TestRules.LearnsNothing, TestRules.DiscItem));
        Assert.False(TestRules.All.CanBeTaught(TestRules.LearnsNothing, TestRules.HiddenMachineItem));
    }

    /// <summary>Something that is not a machine is not taught by anything.</summary>
    [Fact]
    public void NothingIsTaughtByAPotion()
    {
        Assert.False(TestRules.All.CanBeTaught(1, TestRules.PotionItem));
    }

    /// <summary>
    /// A file written before this table was located carries no words at all, and on
    /// such a file every machine works on everything — which is what this project did
    /// for its whole life and is a better failure than refusing the entire party.
    /// </summary>
    [Fact]
    public void AFileWithNoWordsAllowsEverything()
    {
        var rules = new GameRules([], [], [], items:
        [
            new ItemData(TestRules.DiscItem, 3000, Pocket.Machines, 0, 0, 0, 0, 0),
        ]);

        Assert.True(rules.CanBeTaught(1, TestRules.DiscItem));
        Assert.Equal(0, rules.MachineSetCount);
    }

    [Fact]
    public void TheWordsSurviveBeingWrittenDown()
    {
        using var buffer = new MemoryStream();

        TestRules.All.Save(buffer);
        buffer.Position = 0;

        GameRules again = GameRules.Load(buffer);

        Assert.Equal(TestRules.All.MachineSetCount, again.MachineSetCount);
        Assert.True(again.CanBeTaught(1, TestRules.DiscItem));
        Assert.False(again.CanBeTaught(TestRules.LearnsNothing, TestRules.DiscItem));
    }

    /// <summary>
    /// Which bit belongs to which machine is worked out from the pocket rather than
    /// written to the file, so the two cannot drift apart.
    /// </summary>
    [Fact]
    public void TheMachinesAreListedInPocketOrder()
    {
        Assert.Equal([TestRules.DiscItem, TestRules.HiddenMachineItem], TestRules.All.MachinesFor(1));
        Assert.Empty(TestRules.All.MachinesFor(TestRules.LearnsNothing));
    }

    // ---- and what the server does with it -------------------------------------------

    /// <summary>A player standing somewhere with one creature of a chosen species.</summary>
    private static (GameWorld World, ServerPlayer Player) Holding(int species)
    {
        const string town = "1.0";

        MapData map = new(town, "PALLET TOWN", 8, 8, new byte[64]);

        var world = new GameWorld(new WorldData([map]), town, TestRules.All);

        (ServerPlayer player, _) = world.Join(1, "Mason", SavedCharacter.Fresh(town, 3, 4));

        player.Party =
        [
            new SavedMon(species, 10, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove]),
        ];

        return (world, player);
    }

    [Fact]
    public void TheServerRefusesAMachineTheSpeciesCannotTake()
    {
        (GameWorld world, ServerPlayer player) = Holding(TestRules.LearnsNothing);

        player.Bag.Add(TestRules.DiscItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.DiscItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "It can't learn that move." });

        // Nothing learned, and nothing spent. Being told no is not using a machine.
        Assert.DoesNotContain(TestRules.TaughtMove, player.Party[0].Moves);
        Assert.Equal(1, player.Bag.CountOf(TestRules.DiscItem));
    }

    [Fact]
    public void TheServerStillTeachesOneItCanTake()
    {
        (GameWorld world, ServerPlayer player) = Holding(3);

        player.Bag.Add(TestRules.DiscItem, 1);

        world.UseItem(player.Id, TestRules.DiscItem, 0);

        Assert.Contains(TestRules.TaughtMove, player.Party[0].Moves);
    }

    /// <summary>
    /// Refused before "it already knows that" and before the four-move question, because
    /// those two are about this member and this one is about what it is. A member of a
    /// species that cannot take the machine should hear the same thing whether its moves
    /// happen to be full or not.
    /// </summary>
    [Fact]
    public void WhatItIsIsAskedBeforeWhatItKnows()
    {
        (GameWorld world, ServerPlayer player) = Holding(TestRules.LearnsNothing);

        player.Party[0] = player.Party[0] with { Moves = [1, 2, 3, 4] };
        player.Bag.Add(TestRules.DiscItem, 1);

        List<Outgoing> said = world.UseItem(player.Id, TestRules.DiscItem, 0);

        Assert.Contains(said, o => o.Message is BagUpdated { Message: "It can't learn that move." });
        Assert.DoesNotContain(said, o => o.Message is MoveOffered);
        Assert.Empty(player.MovesOffered);
    }
}
