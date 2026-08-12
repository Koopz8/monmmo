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

            CREATE TABLE IF NOT EXISTS bag_items (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                item_id    INTEGER NOT NULL,
                count      INTEGER NOT NULL,
                PRIMARY KEY (account_id, item_id)
            );

            CREATE TABLE IF NOT EXISTS defeated_trainers (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                trainer_id INTEGER NOT NULL,
                PRIMARY KEY (account_id, trainer_id)
            );

            CREATE TABLE IF NOT EXISTS items_taken (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                what       TEXT    NOT NULL,
                PRIMARY KEY (account_id, what)
            );

            CREATE TABLE IF NOT EXISTS resting_places (
                account_id INTEGER PRIMARY KEY REFERENCES characters(account_id) ON DELETE CASCADE,
                map_id     TEXT    NOT NULL,
                x          INTEGER NOT NULL,
                y          INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS script_flags (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                flag       INTEGER NOT NULL,
                PRIMARY KEY (account_id, flag)
            );

            CREATE TABLE IF NOT EXISTS script_variables (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                variable   INTEGER NOT NULL,
                value      INTEGER NOT NULL,
                PRIMARY KEY (account_id, variable)
            );

            CREATE TABLE IF NOT EXISTS party_moves (
                member_id INTEGER NOT NULL REFERENCES party_members(id) ON DELETE CASCADE,
                slot      INTEGER NOT NULL,
                move_id   INTEGER NOT NULL,
                PRIMARY KEY (member_id, slot)
            );
            """;

        command.ExecuteNonQuery();

        AddColumnIfMissing(connection, "party_members", "experience", "INTEGER NOT NULL DEFAULT 0");

        // The balls column is what the bag used to be, back when a player could carry
        // exactly one kind of thing. It is left in place and written as zero rather than
        // dropped, because dropping a column in SQLite means rebuilding the table and
        // there is nothing to gain by it.
        AddColumnIfMissing(connection, "characters", "money", $"INTEGER NOT NULL DEFAULT {SavedCharacter.StartingMoney}");
    }

    /// <summary>
    /// Adds a column to an existing table, if it is not already there.
    /// <para>
    /// <c>CREATE TABLE IF NOT EXISTS</c> does nothing to a table that already exists,
    /// so a database made before a column was added would never gain it — the schema
    /// would be right on a fresh machine and wrong on every machine that had been
    /// playing. Existing rows take the default, which for experience means their level
    /// is treated as the truth and the curve is entered at the bottom of it.
    /// </para>
    /// </summary>
    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string definition)
    {
        using (SqliteCommand check = connection.CreateCommand())
        {
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column;";
            check.Parameters.AddWithValue("$column", column);

            if (Convert.ToInt64(check.ExecuteScalar()) > 0) return;
        }

        using SqliteCommand alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
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
                INSERT INTO characters (account_id, map_id, x, y, facing, balls, money, saved_at)
                VALUES ($id, $map, $x, $y, $facing, 0, $money, $now)
                ON CONFLICT(account_id) DO UPDATE SET
                    map_id = excluded.map_id,
                    x = excluded.x,
                    y = excluded.y,
                    facing = excluded.facing,
                    money = excluded.money,
                    saved_at = excluded.saved_at;
                """;

            upsert.Parameters.AddWithValue("$id", accountId);
            upsert.Parameters.AddWithValue("$map", character.MapId);
            upsert.Parameters.AddWithValue("$x", character.X);
            upsert.Parameters.AddWithValue("$y", character.Y);
            upsert.Parameters.AddWithValue("$facing", (int)character.Facing);
            upsert.Parameters.AddWithValue("$money", character.Money);
            upsert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        // The bag is rewritten wholesale, unlike the beaten trainers: a bag genuinely
        // does shrink, and an insert-only bag would be one nothing could ever leave.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM bag_items WHERE account_id = $id;";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (BagEntry entry in character.Items)
        {
            await using SqliteCommand item = connection.CreateCommand();
            item.Transaction = transaction;
            item.CommandText = "INSERT INTO bag_items (account_id, item_id, count) VALUES ($id, $item, $count);";
            item.Parameters.AddWithValue("$id", accountId);
            item.Parameters.AddWithValue("$item", entry.ItemId);
            item.Parameters.AddWithValue("$count", entry.Count);
            await item.ExecuteNonQueryAsync(cancellationToken);
        }

        // Inserted rather than rewritten, because a beaten trainer is never unbeaten.
        // The whole set goes in on every save so a database that missed one catches up.
        foreach (int trainerId in character.DefeatedTrainers)
        {
            await using SqliteCommand beaten = connection.CreateCommand();
            beaten.Transaction = transaction;
            beaten.CommandText =
                "INSERT OR IGNORE INTO defeated_trainers (account_id, trainer_id) VALUES ($id, $trainer);";
            beaten.Parameters.AddWithValue("$id", accountId);
            beaten.Parameters.AddWithValue("$trainer", trainerId);
            await beaten.ExecuteNonQueryAsync(cancellationToken);
        }

        // Inserted rather than rewritten, for the reason the beaten trainers are: a ball
        // picked up off the ground is never put back.
        foreach (string what in character.ItemsTaken)
        {
            await using SqliteCommand taken = connection.CreateCommand();
            taken.Transaction = transaction;
            taken.CommandText = "INSERT OR IGNORE INTO items_taken (account_id, what) VALUES ($id, $what);";
            taken.Parameters.AddWithValue("$id", accountId);
            taken.Parameters.AddWithValue("$what", what);
            await taken.ExecuteNonQueryAsync(cancellationToken);
        }

        // A row rather than a column on characters, so a database made before centres
        // existed gains it without a migration. Absent means "has never rested
        // anywhere", which is a real answer and not a missing one.
        if (character.RestingAt is { } resting)
        {
            await using SqliteCommand rest = connection.CreateCommand();
            rest.Transaction = transaction;
            rest.CommandText =
                "INSERT OR REPLACE INTO resting_places (account_id, map_id, x, y) VALUES ($id, $map, $x, $y);";
            rest.Parameters.AddWithValue("$id", accountId);
            rest.Parameters.AddWithValue("$map", resting);
            rest.Parameters.AddWithValue("$x", character.RestingX);
            rest.Parameters.AddWithValue("$y", character.RestingY);
            await rest.ExecuteNonQueryAsync(cancellationToken);
        }

        // Rewritten wholesale, unlike the beaten trainers. A script can clear a flag as
        // readily as set one — that is what makes a door lock behind you — so an
        // insert-only table would be one nothing could ever come back out of.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                "DELETE FROM script_flags WHERE account_id = $id; " +
                "DELETE FROM script_variables WHERE account_id = $id;";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (int flag in character.Flags)
        {
            await using SqliteCommand row = connection.CreateCommand();
            row.Transaction = transaction;
            row.CommandText = "INSERT OR IGNORE INTO script_flags (account_id, flag) VALUES ($id, $flag);";
            row.Parameters.AddWithValue("$id", accountId);
            row.Parameters.AddWithValue("$flag", flag);
            await row.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (SavedVariable variable in character.Variables)
        {
            await using SqliteCommand row = connection.CreateCommand();
            row.Transaction = transaction;
            row.CommandText =
                "INSERT OR REPLACE INTO script_variables (account_id, variable, value) VALUES ($id, $var, $value);";
            row.Parameters.AddWithValue("$id", accountId);
            row.Parameters.AddWithValue("$var", variable.Id);
            row.Parameters.AddWithValue("$value", variable.Value);
            await row.ExecuteNonQueryAsync(cancellationToken);
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
                    INSERT INTO party_members
                        (account_id, slot, species, level, nickname, current_hp, status, nature, experience)
                    VALUES ($account, $slot, $species, $level, $nickname, $hp, $status, $nature, $experience)
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
                insert.Parameters.AddWithValue("$experience", mon.Experience);

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
        int x, y, money;
        Direction facing;

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT map_id, x, y, facing, money FROM characters WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            mapId = reader.GetString(0);
            x = reader.GetInt32(1);
            y = reader.GetInt32(2);
            facing = (Direction)reader.GetInt32(3);
            money = reader.GetInt32(4);
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
                SELECT id, species, level, nickname, current_hp, status, nature, experience
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
                    Moves: moves.GetValueOrDefault(memberId, []),
                    Experience: reader.GetInt32(7)));
            }
        }

        var defeated = new List<int>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT trainer_id FROM defeated_trainers WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) defeated.Add(reader.GetInt32(0));
        }

        var carried = new List<BagEntry>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_id, count FROM bag_items WHERE account_id = $id ORDER BY item_id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                carried.Add(new BagEntry(reader.GetInt32(0), reader.GetInt32(1)));
        }

        var flags = new List<int>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT flag FROM script_flags WHERE account_id = $id ORDER BY flag;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) flags.Add(reader.GetInt32(0));
        }

        var variables = new List<SavedVariable>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT variable, value FROM script_variables WHERE account_id = $id ORDER BY variable;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                variables.Add(new SavedVariable(reader.GetInt32(0), reader.GetInt32(1)));
        }

        var taken = new List<string>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT what FROM items_taken WHERE account_id = $id ORDER BY what;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) taken.Add(reader.GetString(0));
        }

        string? restingAt = null;
        int restingX = 0;
        int restingY = 0;

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT map_id, x, y FROM resting_places WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                restingAt = reader.GetString(0);
                restingX = reader.GetInt32(1);
                restingY = reader.GetInt32(2);
            }
        }

        return new SavedCharacter(mapId, x, y, facing, party)
        {
            ItemsTaken = taken,
            RestingAt = restingAt,
            RestingX = restingX,
            RestingY = restingY,
            DefeatedTrainers = defeated,
            Items = carried,
            Money = money,
            Flags = flags,
            Variables = variables,
        };
    }

    /// <summary>
    /// Closes the store and actually lets go of the file.
    /// <para>
    /// Disposing a connection returns it to a pool rather than closing it, so the
    /// handle outlives this object and the file stays locked. On Linux that is
    /// invisible — an open file can still be deleted or replaced. On Windows it is
    /// not, and a server that has stopped but still has its own database open is a
    /// problem for anyone trying to back it up, move it, or start a second one.
    /// </para>
    /// </summary>
    public void Dispose()
    {
        _keepAlive.Dispose();

        using var last = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(last);
    }
}
