using Microsoft.Data.Sqlite;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;

namespace PokeMmo.Server.Storage;

/// <summary>
/// Accounts and saves in a SQLite file.
/// <para>
/// One file, no service to install, and it is the same SQL a bigger engine would take
/// if this ever outgrows it. Write-ahead logging is on because the alternative locks
/// readers out during every save, and saves happen while people are playing.
/// </para>
/// </summary>
public sealed class SqlitePlayerStore : IPlayerStore, IDisposable
{
    /// <summary>Where the database lives unless told otherwise.</summary>
    public const string DefaultFileName = "players.db";

    private readonly string _connectionString;

    /// <summary>
    /// A connection held open for the lifetime of the store.
    /// <para>
    /// SQLite discards an in-memory database when its last connection closes, so
    /// without this the tests that use <c>:memory:</c> would lose the schema between
    /// calls. For a file database it costs nothing and saves reopening.
    /// </para>
    /// </summary>
    private readonly SqliteConnection _keepAlive;

    public SqlitePlayerStore(string path)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        Migrate();
    }

    /// <summary>A store that exists only while it is open, for tests.</summary>
    public static SqlitePlayerStore InMemory() => new($"file:{Guid.NewGuid():N}?mode=memory");

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// Creates the schema. Written to be safe to run every start, so there is no
    /// separate migration step to forget.
    /// </summary>
    private void Migrate()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS accounts (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                username       TEXT    NOT NULL,
                username_folded TEXT   NOT NULL UNIQUE,
                password_hash  TEXT    NOT NULL,
                created_at     TEXT    NOT NULL,
                last_login_at  TEXT
            );

            CREATE TABLE IF NOT EXISTS characters (
                account_id INTEGER PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
                map_id     TEXT    NOT NULL,
                x          INTEGER NOT NULL,
                y          INTEGER NOT NULL,
                facing     INTEGER NOT NULL,
                balls      INTEGER NOT NULL,
                saved_at   TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS party_members (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                slot       INTEGER NOT NULL,
                species    INTEGER NOT NULL,
                level      INTEGER NOT NULL,
                nickname   TEXT,
                current_hp INTEGER NOT NULL,
                status     INTEGER NOT NULL,
                nature     INTEGER NOT NULL,
                UNIQUE (account_id, slot)
            );

            CREATE TABLE IF NOT EXISTS party_moves (
                member_id INTEGER NOT NULL REFERENCES party_members(id) ON DELETE CASCADE,
                slot      INTEGER NOT NULL,
                move_id   INTEGER NOT NULL,
                PRIMARY KEY (member_id, slot)
            );
            """;

        command.ExecuteNonQuery();
    }

    public async Task<AuthOutcome> RegisterAsync(
        string username, string password, SavedCharacter fresh, CancellationToken cancellationToken = default)
    {
        if (UsernameRules.Problem(username) is { } nameProblem) return new AuthOutcome.Failed(nameProblem);
        if (UsernameRules.PasswordProblem(password) is { } wordProblem) return new AuthOutcome.Failed(wordProblem);

        string trimmed = username.Trim();
        string hash = PasswordHasher.Hash(password);

        await using SqliteConnection connection = Open();
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long accountId;

        try
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO accounts (username, username_folded, password_hash, created_at)
                VALUES ($username, $folded, $hash, $now)
                RETURNING id;
                """;

            insert.Parameters.AddWithValue("$username", trimmed);
            insert.Parameters.AddWithValue("$folded", UsernameRules.Fold(trimmed));
            insert.Parameters.AddWithValue("$hash", hash);
            insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            accountId = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // The unique index is what actually decides this, not a prior lookup —
            // two registrations racing would both pass a check-then-insert.
            return new AuthOutcome.Failed("That name is taken.");
        }

        await WriteCharacterAsync(connection, transaction, accountId, fresh, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AuthOutcome.Success(new Account(accountId, trimmed), fresh);
    }

    public async Task<AuthOutcome> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = "SELECT id, username, password_hash FROM accounts WHERE username_folded = $folded;";
        find.Parameters.AddWithValue("$folded", UsernameRules.Fold(username));

        long accountId;
        string storedName;
        string storedHash;

        await using (SqliteDataReader reader = await find.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return new AuthOutcome.Failed("Wrong name or password.");

            accountId = reader.GetInt64(0);
            storedName = reader.GetString(1);
            storedHash = reader.GetString(2);
        }

        if (!PasswordHasher.Verify(password, storedHash))
            return new AuthOutcome.Failed("Wrong name or password.");

        if (PasswordHasher.NeedsRehash(storedHash))
        {
            // The one moment the password is available in the clear, so the one moment
            // an old hash can be upgraded.
            await using SqliteCommand upgrade = connection.CreateCommand();
            upgrade.CommandText = "UPDATE accounts SET password_hash = $hash WHERE id = $id;";
            upgrade.Parameters.AddWithValue("$hash", PasswordHasher.Hash(password));
            upgrade.Parameters.AddWithValue("$id", accountId);
            await upgrade.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand touch = connection.CreateCommand())
        {
            touch.CommandText = "UPDATE accounts SET last_login_at = $now WHERE id = $id;";
            touch.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            touch.Parameters.AddWithValue("$id", accountId);
            await touch.ExecuteNonQueryAsync(cancellationToken);
        }

        SavedCharacter? character = await ReadCharacterAsync(connection, accountId, cancellationToken);

        return character is null
            ? new AuthOutcome.Failed("That account has no character.")
            : new AuthOutcome.Success(new Account(accountId, storedName), character);
    }

    public async Task SaveAsync(long accountId, SavedCharacter character, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await WriteCharacterAsync(connection, transaction, accountId, character, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteCharacterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long accountId,
        SavedCharacter character,
        CancellationToken cancellationToken)
    {
        await using (SqliteCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO characters (account_id, map_id, x, y, facing, balls, saved_at)
                VALUES ($id, $map, $x, $y, $facing, $balls, $now)
                ON CONFLICT(account_id) DO UPDATE SET
                    map_id = excluded.map_id,
                    x = excluded.x,
                    y = excluded.y,
                    facing = excluded.facing,
                    balls = excluded.balls,
                    saved_at = excluded.saved_at;
                """;

            upsert.Parameters.AddWithValue("$id", accountId);
            upsert.Parameters.AddWithValue("$map", character.MapId);
            upsert.Parameters.AddWithValue("$x", character.X);
            upsert.Parameters.AddWithValue("$y", character.Y);
            upsert.Parameters.AddWithValue("$facing", (int)character.Facing);
            upsert.Parameters.AddWithValue("$balls", character.Balls);
            upsert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        // The party is rewritten wholesale rather than diffed. Six rows is nothing,
        // and a diff is where an ordering bug would hide.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM party_members WHERE account_id = $id;";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        for (int slot = 0; slot < character.Party.Count; slot++)
        {
            SavedMon mon = character.Party[slot];
            long memberId;

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText =
                    """
                    INSERT INTO party_members (account_id, slot, species, level, nickname, current_hp, status, nature)
                    VALUES ($account, $slot, $species, $level, $nickname, $hp, $status, $nature)
                    RETURNING id;
                    """;

                insert.Parameters.AddWithValue("$account", accountId);
                insert.Parameters.AddWithValue("$slot", slot);
                insert.Parameters.AddWithValue("$species", mon.Species);
                insert.Parameters.AddWithValue("$level", mon.Level);
                insert.Parameters.AddWithValue("$nickname", (object?)mon.Nickname ?? DBNull.Value);
                insert.Parameters.AddWithValue("$hp", mon.CurrentHp);
                insert.Parameters.AddWithValue("$status", (int)mon.Status);
                insert.Parameters.AddWithValue("$nature", (int)mon.Nature);

                memberId = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
            }

            for (int moveSlot = 0; moveSlot < mon.Moves.Count; moveSlot++)
            {
                await using SqliteCommand move = connection.CreateCommand();
                move.Transaction = transaction;
                move.CommandText = "INSERT INTO party_moves (member_id, slot, move_id) VALUES ($member, $slot, $move);";
                move.Parameters.AddWithValue("$member", memberId);
                move.Parameters.AddWithValue("$slot", moveSlot);
                move.Parameters.AddWithValue("$move", mon.Moves[moveSlot]);

                await move.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    private static async Task<SavedCharacter?> ReadCharacterAsync(
        SqliteConnection connection, long accountId, CancellationToken cancellationToken)
    {
        string mapId;
        int x, y, balls;
        Direction facing;

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT map_id, x, y, facing, balls FROM characters WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            mapId = reader.GetString(0);
            x = reader.GetInt32(1);
            y = reader.GetInt32(2);
            facing = (Direction)reader.GetInt32(3);
            balls = reader.GetInt32(4);
        }

        var moves = new Dictionary<long, List<int>>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT m.member_id, m.move_id
                FROM party_moves m
                JOIN party_members p ON p.id = m.member_id
                WHERE p.account_id = $id
                ORDER BY m.member_id, m.slot;
                """;

            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                long memberId = reader.GetInt64(0);

                if (!moves.TryGetValue(memberId, out List<int>? list))
                    moves[memberId] = list = [];

                list.Add(reader.GetInt32(1));
            }
        }

        var party = new List<SavedMon>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT id, species, level, nickname, current_hp, status, nature
                FROM party_members
                WHERE account_id = $id
                ORDER BY slot;
                """;

            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                long memberId = reader.GetInt64(0);

                party.Add(new SavedMon(
                    Species: reader.GetInt32(1),
                    Level: reader.GetInt32(2),
                    Nickname: reader.IsDBNull(3) ? null : reader.GetString(3),
                    CurrentHp: reader.GetInt32(4),
                    Status: (StatusCondition)reader.GetInt32(5),
                    Nature: (Nature)reader.GetInt32(6),
                    Moves: moves.GetValueOrDefault(memberId, [])));
            }
        }

        return new SavedCharacter(mapId, x, y, facing, balls, party);
    }

    public void Dispose() => _keepAlive.Dispose();
}
