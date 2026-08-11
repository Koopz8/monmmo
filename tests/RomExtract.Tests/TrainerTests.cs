using System.Text;
using PokeMmo.Core.Data;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;
using PokeMmo.RomExtract.Trainers;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reading the trainer table off a cartridge.
/// <para>
/// Located by structure like everything else. The signature is strong — a long run of
/// forty-byte records whose party pointers all lead somewhere with a plausible creature
/// at the end — but the failure it protects against is the quiet one: a table found a
/// record early or late gives every trainer somebody else's party, and nothing about
/// that looks like an error.
/// </para>
/// </summary>
public class TrainerTableTests
{
    private static readonly SyntheticRom Fixture = new();

    private static int Table =>
        TrainerTable.Locate(Fixture.ToRom(), SyntheticRom.SpeciesCount)
        ?? throw new InvalidOperationException("The trainer table was not found.");

    [Fact]
    public void TheTableIsFoundOnThePlaceholderAndNotAfterIt()
    {
        // The first entry has no party, so it does not read as a record and the run
        // starts at trainer one. Calling that trainer zero is off-by-one across the
        // whole table — every trainer fights somebody else's creatures, and every test
        // that only counts things still passes.
        Assert.Equal(SyntheticRom.TrainerTableOffset, Table);
    }

    [Fact]
    public void EveryPartyComesBackExactlyAsItWasWritten()
    {
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        var byId = trainers.ToDictionary(t => t.Id);

        for (int id = 1; id <= SyntheticRom.TrainerCount; id++)
        {
            List<TrainerMon> expected = SyntheticRom.TrainerPartyFor(id);

            if (expected.Count == 0)
            {
                Assert.False(byId.ContainsKey(id));
                continue;
            }

            Assert.Equal(expected, byId[id].Party);
            Assert.Equal(SyntheticRom.TrainerIsDouble(id), byId[id].IsDouble);
        }
    }

    [Fact]
    public void AllFourPartyShapesAreExercised()
    {
        // Two flag bits, four shapes, two widths. Getting the width wrong reads the
        // second member out of the middle of the first, which produces a party of
        // plausible nonsense rather than an error — so all four have to be in here or
        // the test above is only checking one of them.
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        Assert.Contains(trainers, t => t.Party.All(m => m.HeldItem == 0 && m.Moves.Count == 0));
        Assert.Contains(trainers, t => t.Party.Any(m => m.HeldItem == 0 && m.Moves.Count > 0));
        Assert.Contains(trainers, t => t.Party.Any(m => m.HeldItem != 0 && m.Moves.Count == 0));
        Assert.Contains(trainers, t => t.Party.Any(m => m.HeldItem != 0 && m.Moves.Count > 0));
    }

    [Fact]
    public void AnUnusedMoveSlotIsNotAMove()
    {
        // A trainer with three moves is written as four slots with the last at zero.
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        Assert.All(trainers, t => Assert.All(t.Party, m => Assert.DoesNotContain(0, m.Moves)));
        Assert.Contains(trainers, t => t.Party.Any(m => m.Moves.Count == 3));
    }

    [Fact]
    public void AHoleInTheTableRenumbersNobody()
    {
        // Real tables have gaps — entries removed during development and never
        // renumbered, because renumbering breaks every script that names one.
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        Assert.DoesNotContain(trainers, t => t.Id == SyntheticRom.TrainerWithNoParty);

        TrainerRecord after = trainers.Single(t => t.Id == SyntheticRom.TrainerWithNoParty + 1);

        Assert.Equal(SyntheticRom.TrainerPartyFor(SyntheticRom.TrainerWithNoParty + 1), after.Party);
    }

    [Fact]
    public void ThePlaceholderIsNotSomebodyYouCanFight()
    {
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        Assert.DoesNotContain(trainers, t => t.Id == 0);
        Assert.Equal(SyntheticRom.TrainerCount - 1, trainers.Count);
    }

    [Fact]
    public void TheNamesAreReadButGoNoFurtherThanThisProcess()
    {
        // Read here so a future feature can use them client-side, and stripped on the
        // way into the rules file. Same rule as species and moves.
        List<TrainerRecord> trainers = TrainerTable.Read(Fixture.ToRom(), Table, SyntheticRom.SpeciesCount);

        Assert.Equal("TRAINER01", trainers.Single(t => t.Id == 1).Name);
    }

    [Fact]
    public void ExplainSaysWhyTheBytesBeforeTheTableAreNotARecord()
    {
        // The question a located address on its own cannot answer: whether what came
        // just before was rejected for a good reason or by a check that is too strict.
        string why = TrainerRecord.Explain(
            Fixture.ToRom(), SyntheticRom.TrainerGuardOffset, SyntheticRom.SpeciesCount);

        Assert.NotEqual("reads as a record", why);
    }

    [Fact]
    public void APartyMemberIsEightBytesOrSixteenAndNothingElse()
    {
        Assert.Equal(8, TrainerRecord.MemberSizeBytes(0));
        Assert.Equal(16, TrainerRecord.MemberSizeBytes(1));
        Assert.Equal(8, TrainerRecord.MemberSizeBytes(2));
        Assert.Equal(16, TrainerRecord.MemberSizeBytes(3));
    }
}

/// <summary>
/// Which trainer a script picks a fight with. The only place that is written down.
/// </summary>
public class TrainerScriptTests
{
    private static readonly SyntheticRom Fixture = new();

    [Fact]
    public void APersonWhoIsATrainerNamesOneInTheirScript()
    {
        uint script = SyntheticRom.ScriptAddressFor(2, SyntheticRom.TrainerObjectSlot);

        Assert.Equal(SyntheticRom.TrainerIdFor(2), ScriptReader.FindTrainer(Fixture.ToRom(), script));
    }

    [Fact]
    public void SomebodyWhoIsNotATrainerNamesNobody()
    {
        Assert.Null(ScriptReader.FindTrainer(Fixture.ToRom(), SyntheticRom.ScriptAddressFor(2, 0)));
    }

    [Fact]
    public void HowLongATrainerBattleIsDependsOnItsFirstArgument()
    {
        // The one command in this set whose size is not fixed. Its first byte chooses a
        // variant, and the variants differ in how many pointers follow.
        Assert.Equal(13, ScriptCommands.ArgumentLength(ScriptCommands.TrainerBattle, 0));
        Assert.Equal(9, ScriptCommands.ArgumentLength(ScriptCommands.TrainerBattle, 3));
        Assert.Equal(17, ScriptCommands.ArgumentLength(ScriptCommands.TrainerBattle, 1));
        Assert.Equal(21, ScriptCommands.ArgumentLength(ScriptCommands.TrainerBattle, 6));
    }

    [Fact]
    public void AVariantNobodyKnowsStopsTheReadRatherThanGuessing()
    {
        // Same rule as an unknown command, for the same reason: a guessed length
        // resumes inside an argument and invents everything after it.
        Assert.Null(ScriptCommands.ArgumentLength(ScriptCommands.TrainerBattle, 0x42));

        var image = new byte[0x200];
        image[0] = ScriptCommands.Lock;
        image[1] = ScriptCommands.TrainerBattle;
        image[2] = 0x42;

        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress);

        Assert.Single(commands);
    }

    [Fact]
    public void TheIdIsReadableEvenFromAVariantWithADifferentLength()
    {
        // Every variant starts the same way — type, id, flag — so the id survives a
        // length table that is wrong about what follows.
        var image = new byte[0x200];

        image[0] = ScriptCommands.TrainerBattle;
        image[1] = 3;                                 // the short variant
        image[2] = 0x39;
        image[3] = 0x05;                              // trainer 1337
        image[10] = ScriptCommands.End;

        Assert.Equal(1337, ScriptReader.FindTrainer(new Rom(image), Rom.BaseAddress));
    }
}

/// <summary>
/// What a trainer standing on a map can see, which is a straight line and nothing else.
/// </summary>
public class SightLineTests
{
    private static MapObject Watching(Direction facing, int range = 3) =>
        new(1, 5, 4, 4, facing, 0, true, 0, 0, 0, 12, range);

    [Fact]
    public void SomebodyStraightAheadIsSeen()
    {
        MapObject trainer = Watching(Direction.Down);

        Assert.True(trainer.CanSee(new GridPosition(4, 5)));
        Assert.True(trainer.CanSee(new GridPosition(4, 7)));
    }

    [Fact]
    public void SomebodyPastTheirRangeIsNot()
    {
        Assert.False(Watching(Direction.Down).CanSee(new GridPosition(4, 8)));
    }

    [Fact]
    public void SomebodyDiagonallyInFrontIsNot()
    {
        // Written as "same column, in front, within range" rather than as a distance,
        // because a distance has them notice people standing diagonally — which is
        // exactly the thing everybody knows they do not do.
        Assert.False(Watching(Direction.Down).CanSee(new GridPosition(5, 5)));
        Assert.False(Watching(Direction.Down).CanSee(new GridPosition(3, 6)));
    }

    [Fact]
    public void SomebodyBehindThemIsNot()
    {
        Assert.False(Watching(Direction.Down).CanSee(new GridPosition(4, 3)));
        Assert.False(Watching(Direction.Up).CanSee(new GridPosition(4, 5)));
    }

    [Fact]
    public void StandingOnTheirOwnSquareIsNotBeingSeen()
    {
        Assert.False(Watching(Direction.Down).CanSee(new GridPosition(4, 4)));
    }

    [Fact]
    public void SomebodyWithNoRangeSeesNobody()
    {
        // Most people on a map are not trainers and have no range at all. Treating zero
        // as "sees the square in front" would have every shopkeeper start a fight.
        Assert.False(Watching(Direction.Down, range: 0).CanSee(new GridPosition(4, 5)));
    }

    [Fact]
    public void EveryFacingLooksTheRightWay()
    {
        Assert.True(Watching(Direction.Up).CanSee(new GridPosition(4, 2)));
        Assert.True(Watching(Direction.Left).CanSee(new GridPosition(2, 4)));
        Assert.True(Watching(Direction.Right).CanSee(new GridPosition(6, 4)));
    }

    [Fact]
    public void TheWalkUpStopsOneSquareShort()
    {
        // Walking onto the player rather than up to them would put two characters on
        // one square, which the rest of the server spends a lot of effort preventing.
        MapObject trainer = Watching(Direction.Down);

        Assert.Equal(
            [new GridPosition(4, 5), new GridPosition(4, 6)],
            trainer.ApproachTo(new GridPosition(4, 7)));
    }

    [Fact]
    public void SomebodyRightInFrontIsNotWalkedTowards()
    {
        Assert.Empty(Watching(Direction.Down).ApproachTo(new GridPosition(4, 5)));
    }
}

/// <summary>
/// Trainers through the rules file, which is the only way the server ever sees one.
/// </summary>
public class TrainerRulesTests
{
    private static readonly SyntheticRom Fixture = new();

    private static GameRules Exported() => RulesExporter.Export(Fixture.ToRom());

    [Fact]
    public void EveryPartySurvivesASaveAndLoad()
    {
        GameRules exported = Exported();

        using var buffer = new MemoryStream();
        exported.Save(buffer);
        buffer.Position = 0;

        GameRules loaded = GameRules.Load(buffer);

        Assert.Equal(exported.TrainerCount, loaded.TrainerCount);
        Assert.True(loaded.TrainerCount > 0);

        for (int id = 1; id <= SyntheticRom.TrainerCount; id++)
        {
            List<TrainerMon> expected = SyntheticRom.TrainerPartyFor(id);

            if (expected.Count == 0)
            {
                Assert.Null(loaded.TrainerAt(id));
                continue;
            }

            TrainerParty party = loaded.TrainerAt(id)!;

            Assert.Equal(expected.Select(m => m.ToMember()), party.Members);
            Assert.Equal(SyntheticRom.TrainerIsDouble(id), party.IsDouble);
        }
    }

    [Fact]
    public void NoTrainerNameGetsIntoTheFile()
    {
        // The rule the whole format exists for. A trainer's name is cartridge text; a
        // party is a list of numbers.
        using var buffer = new MemoryStream();
        Exported().Save(buffer);

        byte[] written = buffer.ToArray();
        byte[] name = Encoding.UTF8.GetBytes("TRAINER01");

        Assert.DoesNotContain(
            Enumerable.Range(0, written.Length - name.Length),
            at => written.Skip(at).Take(name.Length).SequenceEqual(name));
    }

    [Fact]
    public void MostTrainersLeaveTheirMovesToTheLearnset()
    {
        // Which is what the games do. The server fills them in when it builds the
        // battle, exactly as it already does for a wild creature.
        GameRules rules = Exported();

        Assert.Contains(
            Enumerable.Range(1, SyntheticRom.TrainerCount),
            id => rules.TrainerAt(id)?.Members.Any(m => m.UsesLevelUpMoves) == true);
    }
}
