using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Server;
using PokeMmo.Server.Storage;

namespace PokeMmo.RomExtract.Tests;

public class ExperienceCurveTests
{
    /// <summary>
    /// The published total for each curve at level 100.
    /// <para>
    /// These are the anchors worth having. A curve that is subtly wrong does not throw
    /// or look broken — it produces a species that levels at the wrong pace forever,
    /// and the only way to notice is to check it against a number somebody else
    /// derived.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(GrowthRate.Erratic, 600_000)]
    [InlineData(GrowthRate.Fast, 800_000)]
    [InlineData(GrowthRate.MediumFast, 1_000_000)]
    [InlineData(GrowthRate.MediumSlow, 1_059_860)]
    [InlineData(GrowthRate.Slow, 1_250_000)]
    [InlineData(GrowthRate.Fluctuating, 1_640_000)]
    public void MatchesThePublishedTotalAtLevelOneHundred(GrowthRate rate, int expected)
    {
        Assert.Equal(expected, Experience.TotalForLevel(rate, 100));
    }

    [Theory]
    [InlineData(GrowthRate.Erratic)]
    [InlineData(GrowthRate.Fast)]
    [InlineData(GrowthRate.MediumFast)]
    [InlineData(GrowthRate.MediumSlow)]
    [InlineData(GrowthRate.Slow)]
    [InlineData(GrowthRate.Fluctuating)]
    public void EveryCurveStartsAtZeroAndOnlyRises(GrowthRate rate)
    {
        Assert.Equal(0, Experience.TotalForLevel(rate, 1));

        // Two of these are piecewise, and a piece that joins badly would show up as a
        // level that costs less than the one before it.
        for (int level = 2; level <= 100; level++)
        {
            Assert.True(
                Experience.TotalForLevel(rate, level) > Experience.TotalForLevel(rate, level - 1),
                $"{rate} went backwards at level {level}");
        }
    }

    [Theory]
    [InlineData(GrowthRate.Erratic)]
    [InlineData(GrowthRate.MediumSlow)]
    [InlineData(GrowthRate.Fluctuating)]
    public void LevelAndTotalAgreeWithEachOther(GrowthRate rate)
    {
        for (int level = 1; level <= 100; level++)
        {
            Assert.Equal(level, Experience.LevelAt(rate, Experience.TotalForLevel(rate, level)));

            // And a point short of the next level is still the current one.
            if (level < 100)
                Assert.Equal(level, Experience.LevelAt(rate, Experience.TotalForLevel(rate, level + 1) - 1));
        }
    }

    [Fact]
    public void ExperienceIsCappedAtTheTop()
    {
        Assert.Equal(100, Experience.LevelAt(GrowthRate.Slow, 99_999_999));
        Assert.Equal(0, Experience.ToNextLevel(GrowthRate.Slow, 100, 1_250_000));
    }

    [Fact]
    public void BeatingSomethingBiggerIsWorthMore()
    {
        Assert.True(Experience.ForDefeating(64, 20) > Experience.ForDefeating(64, 5));
        Assert.True(Experience.ForDefeating(200, 10) > Experience.ForDefeating(64, 10));
    }

    [Fact]
    public void AVictoryIsAlwaysWorthSomething()
    {
        // The formula rounds down, and against a level 1 with a tiny yield it rounds
        // to nothing — which would read as the game failing to award anything.
        Assert.True(Experience.ForDefeating(1, 1) > 0);
    }
}

public class ProgressionTests
{
    private static readonly Progression Levelling = new(TestRules.All);

    private static SavedMon Member(int level = 5, int experience = 0, params int[] moves) =>
        new(1, level, null, 20, StatusCondition.None, Nature.Hardy,
            moves.Length > 0 ? moves : [TestRules.FirstMove], experience);

    [Fact]
    public void WinningPaysOutExperience()
    {
        (SavedMon grown, List<BattleEvent> events) = Levelling.Award(Member(), faintedSpecies: 16, faintedLevel: 3);

        BattleEvent.ExperienceGained gained = Assert.IsType<BattleEvent.ExperienceGained>(events[0]);

        Assert.True(gained.Amount > 0);
        Assert.True(grown.Experience > 0);
    }

    [Fact]
    public void EnoughExperienceRaisesTheLevel()
    {
        // Entered a point short of level 6, against something small enough that the
        // payout crosses that threshold and not the one after it.
        int nextLevel = Experience.TotalForLevel(GrowthRate.MediumFast, 6);

        (SavedMon grown, List<BattleEvent> events) =
            Levelling.Award(Member(level: 5, experience: nextLevel - 1), 16, 3);

        Assert.Equal(6, grown.Level);
        Assert.Contains(events, e => e is BattleEvent.LevelledUp { Level: 6 });
    }

    [Fact]
    public void OneVictoryCanCrossMoreThanOneLevel()
    {
        // A level is a threshold, not an increment, so a large payout has to keep
        // crossing them rather than granting one and stopping.
        (SavedMon grown, List<BattleEvent> events) = Levelling.Award(Member(level: 2), 16, 100);

        Assert.True(grown.Level > 3, $"only reached level {grown.Level}");
        Assert.True(events.Count(e => e is BattleEvent.LevelledUp) >= 2);
    }

    [Fact]
    public void ALevelUpTeachesWhatTheSpeciesLearnsThere()
    {
        // TestRules gives every species a move at level 3.
        (SavedMon grown, List<BattleEvent> events) = Levelling.Award(Member(level: 2), 16, 100);

        Assert.Contains(events, e => e is BattleEvent.MoveLearned);
        Assert.Contains(2, grown.Moves);
    }

    [Fact]
    public void AFullMoveListIsNotOverwritten()
    {
        // The games ask which move to forget. Until something can ask, nothing is
        // forgotten — silently dropping a move a player chose is the worse mistake.
        SavedMon full = Member(level: 2, experience: 0, 10, 11, 12, 13);

        (SavedMon grown, List<BattleEvent> events) = Levelling.Award(full, 16, 100);

        Assert.Equal(4, grown.Moves.Count);
        Assert.Equal(new[] { 10, 11, 12, 13 }, grown.Moves);
        Assert.Contains(events, e => e is BattleEvent.MoveNotLearned);
    }

    [Fact]
    public void AMemberSavedBeforeExperienceExistedIsNotSentBackToLevelOne()
    {
        // Anything caught before this milestone has no recorded experience. Its level
        // is the truth, so the curve is entered at the bottom of that level rather
        // than at zero — otherwise a level 30 would drop to 1 on its next win.
        (SavedMon grown, _) = Levelling.Award(Member(level: 30, experience: 0), 16, 3);

        Assert.True(grown.Level >= 30);
        Assert.True(grown.Experience >= Experience.TotalForLevel(GrowthRate.MediumFast, 30));
    }

    [Fact]
    public void NothingGrowsPastTheCap()
    {
        SavedMon capped = Member(level: 100, experience: Experience.TotalForLevel(GrowthRate.MediumFast, 100));

        (SavedMon grown, List<BattleEvent> events) = Levelling.Award(capped, 16, 100);

        Assert.Equal(100, grown.Level);
        Assert.Empty(events);
    }
}

public class ExperiencePersistenceTests
{
    [Fact]
    public async Task ExperienceSurvivesASaveAndLoad()
    {
        using SqlitePlayerStore store = SqlitePlayerStore.InMemory();

        var member = new SavedMon(1, 12, null, 20, StatusCondition.None, Nature.Hardy, [1, 2], 4321);
        var character = new SavedCharacter("3.0", 1, 1, PokeMmo.Core.World.Direction.Down, [member]);

        await store.RegisterAsync("Mason", "a-good-password", character);

        var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

        Assert.Equal(4321, Assert.Single(login.Character.Party).Experience);
    }

    [Fact]
    public async Task ADatabaseMadeBeforeExperienceExistedStillOpens()
    {
        // CREATE TABLE IF NOT EXISTS does nothing to a table that already exists, so
        // without a real migration the column would appear on fresh machines and never
        // on one that had been played on.
        string path = TempDatabase.Path();

        try
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();

                using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();

                command.CommandText =
                    """
                    CREATE TABLE accounts (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, username TEXT NOT NULL,
                        username_folded TEXT NOT NULL UNIQUE, password_hash TEXT NOT NULL,
                        created_at TEXT NOT NULL, last_login_at TEXT);

                    CREATE TABLE characters (
                        account_id INTEGER PRIMARY KEY, map_id TEXT NOT NULL,
                        x INTEGER NOT NULL, y INTEGER NOT NULL, facing INTEGER NOT NULL,
                        balls INTEGER NOT NULL, saved_at TEXT NOT NULL);

                    CREATE TABLE party_members (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, account_id INTEGER NOT NULL,
                        slot INTEGER NOT NULL, species INTEGER NOT NULL, level INTEGER NOT NULL,
                        nickname TEXT, current_hp INTEGER NOT NULL, status INTEGER NOT NULL,
                        nature INTEGER NOT NULL, UNIQUE (account_id, slot));

                    CREATE TABLE party_moves (
                        member_id INTEGER NOT NULL, slot INTEGER NOT NULL,
                        move_id INTEGER NOT NULL, PRIMARY KEY (member_id, slot));
                    """;

                await command.ExecuteNonQueryAsync();
            }

            using var store = new SqlitePlayerStore(path);

            var member = new SavedMon(1, 5, null, 20, StatusCondition.None, Nature.Hardy, [1], 99);
            var character = new SavedCharacter("3.0", 1, 1, PokeMmo.Core.World.Direction.Down, [member]);

            await store.RegisterAsync("Mason", "a-good-password", character);

            var login = Assert.IsType<AuthOutcome.Success>(await store.LoginAsync("Mason", "a-good-password"));

            Assert.Equal(99, Assert.Single(login.Character.Party).Experience);
        }
        finally
        {
            TempDatabase.Delete(path);
        }
    }
}
