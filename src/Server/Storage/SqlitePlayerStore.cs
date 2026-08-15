using Microsoft.Data.Sqlite;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Cosmetics;
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
public sealed class SqlitePlayerStore : IPlayerStore, IMarketStore, IFriendStore, IDisposable
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

            -- With a write-ahead log, NORMAL means a commit does not wait for the disk
            -- to confirm it. The trade is exact and worth writing down: a power cut or a
            -- kernel panic can lose the last few transactions, and cannot corrupt the
            -- database — that is the guarantee WAL gives and the reason FULL is not
            -- needed here. What it buys was measured: a save cost 21 ms on average with
            -- FULL and 458 ms at worst, and a thousand players doing one thing every two
            -- seconds is five hundred saves a second, which is ten times more writing
            -- than that allows.
            PRAGMA synchronous = NORMAL;

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

            CREATE TABLE IF NOT EXISTS cosmetics_owned (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                cosmetic   INTEGER NOT NULL,
                PRIMARY KEY (account_id, cosmetic)
            );

            CREATE TABLE IF NOT EXISTS cosmetics_worn (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                slot       INTEGER NOT NULL,
                cosmetic   INTEGER NOT NULL,
                PRIMARY KEY (account_id, slot)
            );

            CREATE TABLE IF NOT EXISTS script_variables (
                account_id INTEGER NOT NULL REFERENCES characters(account_id) ON DELETE CASCADE,
                variable   INTEGER NOT NULL,
                value      INTEGER NOT NULL,
                PRIMARY KEY (account_id, variable)
            );

            CREATE TABLE IF NOT EXISTS friends (
                account_id INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                friend_id  INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                added_at   TEXT NOT NULL,

                -- The pair, so adding the same person twice is refused by the table rather
                -- than by a check that has to be remembered at every call site.
                PRIMARY KEY (account_id, friend_id)
            );

            CREATE TABLE IF NOT EXISTS market_listings (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                seller_id  INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,

                -- The escrowed row while it is for sale, and nothing once it is sold. No
                -- foreign key on purpose: after a sale that row belongs to its buyer and
                -- is theirs to rewrite, and a cascade from it would delete the listing —
                -- taking the seller's unpaid money with it the first time the buyer saved.
                member_id  INTEGER NULL,

                -- Copied at the moment of listing rather than looked up, so a sold listing
                -- still says what it was. This is also what a market is searched by.
                species    INTEGER NOT NULL,
                level      INTEGER NOT NULL,
                sex        INTEGER NOT NULL DEFAULT 0,
                iv_hp      INTEGER NOT NULL DEFAULT 0,
                iv_attack  INTEGER NOT NULL DEFAULT 0,
                iv_defense INTEGER NOT NULL DEFAULT 0,
                iv_speed   INTEGER NOT NULL DEFAULT 0,
                iv_spattack INTEGER NOT NULL DEFAULT 0,
                iv_spdefense INTEGER NOT NULL DEFAULT 0,

                price      INTEGER NOT NULL,

                -- Nought is for sale, one is sold and owing. There is no third: a listing
                -- whose money has been collected is deleted, because nothing here is kept
                -- for its own sake.
                state      INTEGER NOT NULL DEFAULT 0,
                buyer_id   INTEGER NULL,
                listed_at  TEXT NOT NULL,
                sold_at    TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS market_for_sale ON market_listings (state, id DESC);
            CREATE INDEX IF NOT EXISTS market_by_seller ON market_listings (seller_id);

            CREATE TABLE IF NOT EXISTS party_moves (
                member_id INTEGER NOT NULL REFERENCES party_members(id) ON DELETE CASCADE,
                slot      INTEGER NOT NULL,
                move_id   INTEGER NOT NULL,
                PRIMARY KEY (member_id, slot)
            );
            """;

        command.ExecuteNonQuery();

        AddColumnIfMissing(connection, "party_members", "experience", "INTEGER NOT NULL DEFAULT 0");

        // What it is carrying. Zero for everything that already existed, which is right:
        // nothing could be carrying anything before there was anything to carry.
        AddColumnIfMissing(connection, "party_members", "held_item", "INTEGER NOT NULL DEFAULT 0");

        // Which list this row is in. Added to an existing database rather than only to a
        // fresh one, for the same reason the held item was: a schema that is right on a
        // new machine and wrong on every machine that has been playing is the worst of
        // both. Everything already stored is in the party, which is where it was.
        AddColumnIfMissing(connection, "party_members", "in_box", "INTEGER NOT NULL DEFAULT 0");

        // What is left of each move. Minus one for every row written before moves could
        // run out, and read as "full" — which is what those creatures were.
        AddColumnIfMissing(connection, "party_moves", "pp", "INTEGER NOT NULL DEFAULT -1");

        // What a creature has to show for its fights, one column per stat and in the
        // order the six stats are in everywhere else. Added to the existing table rather
        // than only to a fresh one, so a party saved yesterday reads back as having
        // earned nothing — which is exactly what it had earned.
        foreach (string stat in EffortColumns)
            AddColumnIfMissing(connection, "party_members", stat, "INTEGER NOT NULL DEFAULT 0");

        // And what each was born with. Defaulting to the best of everything, because
        // that is what every creature saved before this column existed actually was:
        // every stat in the project was computed with the argument at its default.
        foreach (string stat in GeneColumns)
            AddColumnIfMissing(connection, "party_members", stat, $"INTEGER NOT NULL DEFAULT {Genes.Best}");

        // And which sex each one is. Nought is both "genderless" and "written down
        // before anybody asked", which this column cannot tell apart and does not try to.
        AddColumnIfMissing(connection, "party_members", "sex", "INTEGER NOT NULL DEFAULT 0");

        // And which of its two abilities it was born with. Nought for everything already
        // stored, which is the first slot — what every creature written before this column
        // existed effectively had, since nothing was asking.
        AddColumnIfMissing(connection, "party_members", "ability_slot", "INTEGER NOT NULL DEFAULT 0");

        // The balls column is what the bag used to be, back when a player could carry
        // exactly one kind of thing. It is left in place and written as zero rather than
        // dropped, because dropping a column in SQLite means rebuilding the table and
        // there is nothing to gain by it.
        AddColumnIfMissing(connection, "characters", "money", $"INTEGER NOT NULL DEFAULT {SavedCharacter.StartingMoney}");

        // How far this character has walked, and the step their egg is due on. Nought for
        // everything already stored, which is the truthful answer rather than a convenient
        // one: nothing written before this column existed had walked a step that anything
        // was counting, and no egg was owed to anybody.
        AddColumnIfMissing(connection, "characters", "steps", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "characters", "egg_at", "INTEGER NOT NULL DEFAULT 0");
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
    /// <summary>The six effort columns, in the six-stat order.</summary>
    /// <summary>
    /// How much writing this store has actually done, for the report.
    /// <para>
    /// A save is the one thing in this server that touches a disk, and a disk is the one
    /// thing in it that can be slow for reasons nothing here controls. Counting is what
    /// turns "the server felt sticky" into a number.
    /// </para>
    /// </summary>
    private long _saves;

    private long _saveMilliseconds;

    private double _slowestSave;

    /// <summary>How many characters have been written down.</summary>
    public long Saves => Interlocked.Read(ref _saves);

    /// <summary>The average time one save took, in milliseconds.</summary>
    public double AverageSave => Saves == 0 ? 0 : Interlocked.Read(ref _saveMilliseconds) / (double)Saves;

    /// <summary>And the worst one.</summary>
    public double SlowestSave => Volatile.Read(ref _slowestSave);

    /// <summary>
    /// What the <c>in_box</c> column means, and where this character's own lists stop.
    /// <para>
    /// Nought is the party, one is the box, two is the daycare, and three is the market.
    /// The first three are lists a character has; the fourth is somewhere a creature goes
    /// when it stops being in any of them, which is what listing it for sale means.
    /// </para>
    /// <para>
    /// <see cref="LastOwnList"/> is the line between those two ideas, and it is load-bearing
    /// in exactly two places: the delete that rewrites a character's creatures wholesale,
    /// and the select that reads them back. Without it the first destroys anything on the
    /// market and the second shows it to its seller as though it were still theirs.
    /// </para>
    /// </summary>
    public const int InTheParty = 0;

    public const int InTheBox = 1;

    public const int AtTheDaycare = 2;

    public const int OnTheMarket = 3;

    /// <summary>The highest <c>in_box</c> value that is still one of this character's lists.</summary>
    public const int LastOwnList = AtTheDaycare;

    /// <summary>
    /// The slot number an escrowed row carries.
    /// <para>
    /// A slot is only row order within a character's own lists, and a creature on the
    /// market is in none of them — so the number means nothing here and is parked well
    /// clear of any real one rather than left at nought, where it would collide with
    /// somebody's lead creature under the table's own uniqueness rule.
    /// </para>
    /// </summary>
    private const int MarketSlot = 9000;

    /// <summary>Nought is for sale; one is sold and the price is owed to its seller.</summary>
    private const int ForSale = 0;

    private const int Sold = 1;

    /// <summary>The six effort columns, in the six-stat order.</summary>
    private static readonly string[] EffortColumns =
        ["ev_hp", "ev_attack", "ev_defense", "ev_speed", "ev_spattack", "ev_spdefense"];

    /// <summary>The six gene columns, in the same order.</summary>
    private static readonly string[] GeneColumns =
        ["iv_hp", "iv_attack", "iv_defense", "iv_speed", "iv_spattack", "iv_spdefense"];

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

    public async Task<int> ForgetStoryAsync(
        string username, SavedCharacter start, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM accounts WHERE username_folded = $folded;";
        find.Parameters.AddWithValue("$folded", UsernameRules.Fold(username));

        if (await find.ExecuteScalarAsync(cancellationToken) is not long accountId) return -1;

        await using SqliteCommand forget = connection.CreateCommand();
        forget.CommandText =
            "DELETE FROM script_flags WHERE account_id = $id; " +
            "DELETE FROM script_variables WHERE account_id = $id;";
        forget.Parameters.AddWithValue("$id", accountId);

        int forgotten = await forget.ExecuteNonQueryAsync(cancellationToken);

        // And put back what a new game already knows. Deleting the flags and stopping
        // there is what a character who had never played would look like, and no such
        // character exists — the cartridge hands out forty-nine of them before anybody
        // has taken a step.
        foreach (int flag in start.Flags)
        {
            await using SqliteCommand set = connection.CreateCommand();
            set.CommandText =
                "INSERT OR IGNORE INTO script_flags (account_id, flag) VALUES ($id, $flag);";
            set.Parameters.AddWithValue("$id", accountId);
            set.Parameters.AddWithValue("$flag", flag);

            await set.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (SavedVariable variable in start.Variables)
        {
            await using SqliteCommand write = connection.CreateCommand();
            write.CommandText =
                "INSERT OR REPLACE INTO script_variables (account_id, variable, value) VALUES ($id, $var, $value);";
            write.Parameters.AddWithValue("$id", accountId);
            write.Parameters.AddWithValue("$var", variable.Id);
            write.Parameters.AddWithValue("$value", variable.Value);

            await write.ExecuteNonQueryAsync(cancellationToken);
        }

        return forgotten;
    }

    public async Task<bool> GiveAsync(
        string username, int species, int level, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM accounts WHERE username_folded = $folded;";
        find.Parameters.AddWithValue("$folded", UsernameRules.Fold(username));

        if (await find.ExecuteScalarAsync(cancellationToken) is not long accountId) return false;

        await using SqliteCommand slots = connection.CreateCommand();
        slots.CommandText = "SELECT COALESCE(MAX(slot) + 1, 0) FROM party_members WHERE account_id = $id;";
        slots.Parameters.AddWithValue("$id", accountId);

        int slot = Convert.ToInt32(await slots.ExecuteScalarAsync(cancellationToken) ?? 0);

        // Health, nature and moves are left for the server to work out when it loads
        // this, the same as any other party member — a shortcut that produces a creature
        // the rest of the game could not have made would test the wrong thing. Zero
        // health here means "as much as it has", which is what a fresh one gets.
        await using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO party_members (account_id, slot, species, level, nickname, current_hp, status, nature, experience, held_item) " +
            "VALUES ($id, $slot, $species, $level, NULL, 0, 0, 0, 0, 0);";

        insert.Parameters.AddWithValue("$id", accountId);
        insert.Parameters.AddWithValue("$slot", slot);
        insert.Parameters.AddWithValue("$species", species);
        insert.Parameters.AddWithValue("$level", level);

        await insert.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    public async Task<bool> WipeAsync(
        string username, SavedCharacter fresh, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM accounts WHERE username_folded = $folded;";
        find.Parameters.AddWithValue("$folded", UsernameRules.Fold(username));

        if (await find.ExecuteScalarAsync(cancellationToken) is not long accountId) return false;

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // Everything keyed on the account except the account. WriteCharacterAsync clears
        // and rewrites most of these itself; the three it does not are the ones that only
        // ever grow — what has been beaten, what has been picked up, where they last
        // rested — and those are exactly what makes a wiped character not a new one.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                "DELETE FROM defeated_trainers WHERE account_id = $id; " +
                "DELETE FROM items_taken WHERE account_id = $id; " +
                "DELETE FROM resting_places WHERE account_id = $id;";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteCharacterAsync(connection, transaction, accountId, fresh, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task SaveAsync(
        long accountId,
        SavedCharacter character,
        CancellationToken cancellationToken = default,
        SavedCharacter? previous = null)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();

        await using (SqliteConnection connection = Open())
        await using (SqliteTransaction transaction =
                     (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken))
        {
            await WriteCharacterAsync(connection, transaction, accountId, character, cancellationToken, previous);
            await transaction.CommitAsync(cancellationToken);
        }

        double took = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        Interlocked.Increment(ref _saves);
        Interlocked.Add(ref _saveMilliseconds, (long)took);

        if (took > _slowestSave) Volatile.Write(ref _slowestSave, took);
    }

    /// <summary>
    /// Writes a character, skipping the parts of it that have not changed.
    /// <para>
    /// <paramref name="previous"/> is what this store last wrote for this account, or
    /// nothing when it does not know — and not knowing means writing everything, which is
    /// what every save did until this argument existed. A save is a full rewrite: the row,
    /// the bag, every flag, every party member and every move each of them knows. For the
    /// commonest save of all — somebody did something and is standing one square further
    /// along — every one of those is identical to what is already there.
    /// </para>
    /// <para>
    /// The comparison is by section and never by row. A section is skipped whole or
    /// written whole, so there is nowhere for a half-written party to exist, and the
    /// ordering bug a row-level diff would hide has nowhere to hide.
    /// </para>
    /// </summary>
    private static async Task WriteCharacterAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long accountId,
        SavedCharacter character,
        CancellationToken cancellationToken,
        SavedCharacter? previous = null)
    {
        // What has changed since this store last wrote this account. Everything is
        // "changed" when there is nothing to compare against.
        bool bagChanged = previous is null || !character.Items.SequenceEqual(previous.Items);

        bool scriptChanged = previous is null
            || !character.Flags.SequenceEqual(previous.Flags)
            || !character.Variables.SequenceEqual(previous.Variables)
            || !character.Cosmetics.SequenceEqual(previous.Cosmetics)
            || character.Looks != previous.Looks;

        bool partyChanged = previous is null
            || !character.Party.SequenceEqual(previous.Party)
            || !character.Box.SequenceEqual(previous.Box)
            || !character.Daycare.SequenceEqual(previous.Daycare);

        await using (SqliteCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO characters (account_id, map_id, x, y, facing, balls, money, steps, egg_at, saved_at)
                VALUES ($id, $map, $x, $y, $facing, 0, $money, $steps, $egg, $now)
                ON CONFLICT(account_id) DO UPDATE SET
                    map_id = excluded.map_id,
                    x = excluded.x,
                    y = excluded.y,
                    facing = excluded.facing,
                    money = excluded.money,
                    steps = excluded.steps,
                    egg_at = excluded.egg_at,
                    saved_at = excluded.saved_at;
                """;

            upsert.Parameters.AddWithValue("$id", accountId);
            upsert.Parameters.AddWithValue("$map", character.MapId);
            upsert.Parameters.AddWithValue("$x", character.X);
            upsert.Parameters.AddWithValue("$y", character.Y);
            upsert.Parameters.AddWithValue("$facing", (int)character.Facing);
            upsert.Parameters.AddWithValue("$money", character.Money);
            upsert.Parameters.AddWithValue("$steps", character.Steps);
            upsert.Parameters.AddWithValue("$egg", character.EggAt);
            upsert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        if (bagChanged)
        {
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

        if (scriptChanged)
        {
        // Rewritten wholesale, unlike the beaten trainers. A script can clear a flag as
        // readily as set one — that is what makes a door lock behind you — so an
        // insert-only table would be one nothing could ever come back out of.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                "DELETE FROM script_flags WHERE account_id = $id; " +
                "DELETE FROM script_variables WHERE account_id = $id; " +
                "DELETE FROM cosmetics_owned WHERE account_id = $id; " +
                "DELETE FROM cosmetics_worn WHERE account_id = $id;";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        // Wholesale for the same reason as the flags: a hat can come off.
        foreach (int owned in character.Cosmetics)
        {
            await using SqliteCommand row = connection.CreateCommand();
            row.Transaction = transaction;
            row.CommandText =
                "INSERT OR IGNORE INTO cosmetics_owned (account_id, cosmetic) VALUES ($id, $what);";
            row.Parameters.AddWithValue("$id", accountId);
            row.Parameters.AddWithValue("$what", owned);
            await row.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach ((CosmeticSlot slot, int what) in character.Looks.Worn)
        {
            await using SqliteCommand row = connection.CreateCommand();
            row.Transaction = transaction;
            row.CommandText =
                "INSERT OR REPLACE INTO cosmetics_worn (account_id, slot, cosmetic) VALUES ($id, $slot, $what);";
            row.Parameters.AddWithValue("$id", accountId);
            row.Parameters.AddWithValue("$slot", (int)slot);
            row.Parameters.AddWithValue("$what", what);
            await row.ExecuteNonQueryAsync(cancellationToken);
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
        }

        if (partyChanged)
        {
        // The party is rewritten wholesale rather than diffed. Six rows is nothing,
        // and a diff is where an ordering bug would hide.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            // Bounded to this character's own lists, and that bound is the whole of what
            // keeps a market safe. The wholesale rewrite above is what makes saving safe —
            // there is nowhere for a half-written party to exist — and it would destroy a
            // creature that is on the market, because listing one means it is in none of
            // these lists and a rewrite of "everything belonging to this account" would
            // take it with the rest.
            //
            // A silent loss is this project's worst known class of bug, and this is the
            // line that would cause one. Its counterpart is on the SELECT that reads these
            // rows back; forgetting either is invisible until somebody has lost something,
            // so both have their own test.
            clear.CommandText =
                $"DELETE FROM party_members WHERE account_id = $id AND in_box <= {LastOwnList};";
            clear.Parameters.AddWithValue("$id", accountId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        // Party first, then the box, numbering straight through. The slot is only row
        // order — which list a row belongs to is the column that says so — and keeping
        // them distinct is what the table's own uniqueness rule wants.
        // Nought is the party, one is the box, two is the daycare. A third value rather
        // than a third table: the daycare holds at most two rows and needs every one of
        // the things this table already does.
        List<(SavedMon Mon, int Where)> stored =
        [
            .. character.Party.Select(m => (m, InTheParty)),
            .. character.Box.Select(m => (m, InTheBox)),
            .. character.Daycare.Select(m => (m, AtTheDaycare)),
        ];

        for (int slot = 0; slot < stored.Count; slot++)
        {
            (SavedMon mon, int where) = stored[slot];

            await WriteMemberAsync(connection, transaction, accountId, slot, mon, where, cancellationToken);
        }
        }
    }

    /// <summary>
    /// Writes one creature and its moves, and says which row it became.
    /// <para>
    /// Its own method because the market needs exactly this and nothing else around it:
    /// escrowing a creature is writing one of these rows in the fourth state, inside
    /// somebody else's transaction. Before this existed the only way to write a creature
    /// was to write a whole character, which is not a thing a market has.
    /// </para>
    /// </summary>
    private static async Task<long> WriteMemberAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long accountId,
        int slot,
        SavedMon mon,
        int where,
        CancellationToken cancellationToken)
    {
        long memberId;

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO party_members
                    (account_id, slot, species, level, nickname, current_hp, status, nature, experience, held_item, in_box,
                     ev_hp, ev_attack, ev_defense, ev_speed, ev_spattack, ev_spdefense,
                     iv_hp, iv_attack, iv_defense, iv_speed, iv_spattack, iv_spdefense, sex, ability_slot)
                VALUES ($account, $slot, $species, $level, $nickname, $hp, $status, $nature, $experience, $held, $box,
                        $ev0, $ev1, $ev2, $ev3, $ev4, $ev5,
                        $iv0, $iv1, $iv2, $iv3, $iv4, $iv5, $sex, $ability)
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
            insert.Parameters.AddWithValue("$held", mon.HeldItem);
            insert.Parameters.AddWithValue("$box", where);

            for (int stat = 0; stat < EffortColumns.Length; stat++)
                insert.Parameters.AddWithValue($"$ev{stat}", stat < mon.Evs.Count ? mon.Evs[stat] : 0);

            for (int stat = 0; stat < GeneColumns.Length; stat++)
                insert.Parameters.AddWithValue($"$iv{stat}", stat < mon.Ivs.Count ? mon.Ivs[stat] : Genes.Best);

            insert.Parameters.AddWithValue("$sex", (int)mon.Sex);
            insert.Parameters.AddWithValue("$ability", mon.AbilitySlot);

            memberId = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        for (int moveSlot = 0; moveSlot < mon.Moves.Count; moveSlot++)
        {
            await using SqliteCommand move = connection.CreateCommand();
            move.Transaction = transaction;
            move.CommandText =
                "INSERT INTO party_moves (member_id, slot, move_id, pp) VALUES ($member, $slot, $move, $pp);";
            move.Parameters.AddWithValue("$member", memberId);
            move.Parameters.AddWithValue("$slot", moveSlot);
            move.Parameters.AddWithValue("$move", mon.Moves[moveSlot]);
            move.Parameters.AddWithValue("$pp", moveSlot < mon.Pp.Count ? mon.Pp[moveSlot] : -1);

            await move.ExecuteNonQueryAsync(cancellationToken);
        }

        return memberId;
    }

    private static async Task<SavedCharacter?> ReadCharacterAsync(
        SqliteConnection connection, long accountId, CancellationToken cancellationToken)
    {
        string mapId;
        int x, y, money, steps, eggAt;
        Direction facing;

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT map_id, x, y, facing, money, steps, egg_at FROM characters WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            mapId = reader.GetString(0);
            x = reader.GetInt32(1);
            y = reader.GetInt32(2);
            facing = (Direction)reader.GetInt32(3);
            money = reader.GetInt32(4);
            steps = reader.GetInt32(5);
            eggAt = reader.GetInt32(6);
        }

        var moves = new Dictionary<long, List<int>>();
        var left = new Dictionary<long, List<int>>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT m.member_id, m.move_id, m.pp
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

                if (!left.TryGetValue(memberId, out List<int>? remaining))
                    left[memberId] = remaining = [];

                remaining.Add(reader.GetInt32(2));
            }
        }

        var party = new List<SavedMon>();
        var box = new List<SavedMon>();
        var daycare = new List<SavedMon>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                $"""
                SELECT id, species, level, nickname, current_hp, status, nature, experience, held_item, in_box,
                       ev_hp, ev_attack, ev_defense, ev_speed, ev_spattack, ev_spdefense,
                       iv_hp, iv_attack, iv_defense, iv_speed, iv_spattack, iv_spdefense, sex, ability_slot
                FROM party_members
                WHERE account_id = $id AND in_box <= {LastOwnList}
                ORDER BY slot;
                """;

            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                long memberId = reader.GetInt64(0);

                var mon = new SavedMon(
                    Species: reader.GetInt32(1),
                    Level: reader.GetInt32(2),
                    Nickname: reader.IsDBNull(3) ? null : reader.GetString(3),
                    CurrentHp: reader.GetInt32(4),
                    Status: (StatusCondition)reader.GetInt32(5),
                    Nature: (Nature)reader.GetInt32(6),
                    Moves: moves.GetValueOrDefault(memberId, []),
                    Experience: reader.GetInt32(7))
                {
                    HeldItem = reader.GetInt32(8),

                    // Minus one is a row written before moves could run out. Dropped
                    // rather than stored as a number, because empty already means full
                    // and two ways of saying the same thing is one too many.
                    Pp = [.. left.GetValueOrDefault(memberId, []).Where(p => p >= 0)],

                    // Six noughts is a creature that has never won anything, and empty
                    // already says that. Storing it as six zeroes instead would make a
                    // save that has been through this table unequal to the one that went
                    // in, which is the question SavedMon exists to answer.
                    Evs = Effort.Of([.. Enumerable.Range(10, 6).Select(reader.GetInt32)]) is { IsNone: false } earned
                        ? [.. earned.Values]
                        : [],

                    // And perfect is stored as nothing, for the same reason six noughts
                    // of effort are: empty already says it, and two ways of saying one
                    // thing is one too many.
                    Ivs = Genes.Of([.. Enumerable.Range(16, 6).Select(reader.GetInt32)]) is { IsPerfect: false } born
                        ? [.. born.Values]
                        : [],

                    Sex = (Gender)reader.GetInt32(22),
                    AbilitySlot = reader.GetInt32(23),
                };

                (reader.GetInt32(9) switch { 0 => party, 1 => box, _ => daycare }).Add(mon);
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

        var owned = new List<int>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT cosmetic FROM cosmetics_owned WHERE account_id = $id ORDER BY cosmetic;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) owned.Add(reader.GetInt32(0));
        }

        var worn = new Dictionary<CosmeticSlot, int>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT slot, cosmetic FROM cosmetics_worn WHERE account_id = $id;";
            command.Parameters.AddWithValue("$id", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                worn[(CosmeticSlot)reader.GetInt32(0)] = reader.GetInt32(1);
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
            Box = box,
            Daycare = daycare,
            ItemsTaken = taken,
            RestingAt = restingAt,
            RestingX = restingX,
            RestingY = restingY,
            DefeatedTrainers = defeated,
            Items = carried,
            Money = money,
            Steps = steps,
            EggAt = eggAt,
            Flags = flags,
            Cosmetics = owned,
            Looks = new Appearance(worn),
            Variables = variables,
        };
    }

    // ---- the market ------------------------------------------------------------------
    //
    // Every one of these takes the whole character as well as what is being done, and
    // that shape is the safety rather than an awkwardness. A character's creatures are
    // rewritten wholesale from an in-memory snapshot on every save, so anything this
    // store changed about them on its own would be undone by whichever save happened
    // next. Listing is therefore not "escrow this" but "write this character down
    // without it, and escrow it, and do both or neither".

    /// <summary>The six gene columns as a listing carries them, in the six-stat order.</summary>
    private static readonly string[] ListingGeneColumns =
        ["iv_hp", "iv_attack", "iv_defense", "iv_speed", "iv_spattack", "iv_spdefense"];

    public async Task<long> ListAsync(
        long sellerId,
        SavedCharacter withoutIt,
        SavedMon offered,
        int price,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // The seller, already missing it. Written first so that if anything below throws,
        // the rollback leaves a character who still has their creature — which is the
        // failure worth having.
        await WriteCharacterAsync(connection, transaction, sellerId, withoutIt, cancellationToken);

        long memberId = await WriteMemberAsync(
            connection, transaction, sellerId, MarketSlot, offered, OnTheMarket, cancellationToken);

        long listingId;

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                $"""
                INSERT INTO market_listings
                    (seller_id, member_id, species, level, sex, {string.Join(", ", ListingGeneColumns)},
                     price, state, listed_at)
                VALUES ($seller, $member, $species, $level, $sex, $iv0, $iv1, $iv2, $iv3, $iv4, $iv5,
                        $price, {ForSale}, $now)
                RETURNING id;
                """;

            insert.Parameters.AddWithValue("$seller", sellerId);
            insert.Parameters.AddWithValue("$member", memberId);
            insert.Parameters.AddWithValue("$species", offered.Species);
            insert.Parameters.AddWithValue("$level", offered.Level);
            insert.Parameters.AddWithValue("$sex", (int)offered.Sex);
            insert.Parameters.AddWithValue("$price", price);
            insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            for (int stat = 0; stat < ListingGeneColumns.Length; stat++)
            {
                insert.Parameters.AddWithValue(
                    $"$iv{stat}", stat < offered.Ivs.Count ? offered.Ivs[stat] : Genes.Best);
            }

            listingId = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);

        return listingId;
    }

    public async Task<IReadOnlyList<Listing>> BrowseAsync(
        int most = 50, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        return await ReadListingsAsync(
            connection,
            $"WHERE l.state = {ForSale} ORDER BY l.id DESC LIMIT {Math.Clamp(most, 1, 500)}",
            [],
            cancellationToken);
    }

    public async Task<IReadOnlyList<Listing>> MineAsync(
        long sellerId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        return await ReadListingsAsync(
            connection,
            "WHERE l.seller_id = $seller ORDER BY l.state DESC, l.id DESC",
            [("$seller", sellerId)],
            cancellationToken);
    }

    public async Task<SavedMon?> CancelAsync(
        long sellerId,
        long listingId,
        SavedCharacter current,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long memberId;

        await using (SqliteCommand find = connection.CreateCommand())
        {
            find.Transaction = transaction;

            // Theirs, and still for sale. Both conditions in the query rather than in a
            // check beforehand, because "still for sale" stops being true the instant
            // somebody else buys it and a check would be reading the past.
            find.CommandText =
                $"SELECT member_id FROM market_listings " +
                $"WHERE id = $id AND seller_id = $seller AND state = {ForSale} AND member_id IS NOT NULL;";

            find.Parameters.AddWithValue("$id", listingId);
            find.Parameters.AddWithValue("$seller", sellerId);

            if (await find.ExecuteScalarAsync(cancellationToken) is not long found) return null;

            memberId = found;
        }

        SavedMon? coming = await ReadMemberAsync(connection, transaction, memberId, cancellationToken);

        if (coming is null) return null;

        // The escrowed row goes, the listing goes, and the creature comes back inside the
        // character written below. Moving the row instead would leave it holding a slot
        // number that means nothing and a state the next save disagrees with.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                "DELETE FROM party_members WHERE id = $member; DELETE FROM market_listings WHERE id = $listing;";
            clear.Parameters.AddWithValue("$member", memberId);
            clear.Parameters.AddWithValue("$listing", listingId);

            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteCharacterAsync(
            connection,
            transaction,
            sellerId,
            current with { Box = [.. current.Box, coming] },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return coming;
    }

    public async Task<(SavedMon Bought, int Price)?> BuyAsync(
        long buyerId,
        long listingId,
        SavedCharacter buyer,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long memberId;
        long sellerId;
        int price;

        await using (SqliteCommand find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText =
                $"SELECT member_id, seller_id, price FROM market_listings " +
                $"WHERE id = $id AND state = {ForSale} AND member_id IS NOT NULL;";

            find.Parameters.AddWithValue("$id", listingId);

            await using SqliteDataReader reading = await find.ExecuteReaderAsync(cancellationToken);

            if (!await reading.ReadAsync(cancellationToken)) return null;

            memberId = reading.GetInt64(0);
            sellerId = reading.GetInt64(1);
            price = reading.GetInt32(2);
        }

        // Nobody buys their own. It would work — the money would go round in a circle and
        // the creature would come home — but it is a way to launder a listing past anybody
        // watching prices, and it costs one line to refuse.
        if (sellerId == buyerId) return null;

        if (buyer.Money < price) return null;

        SavedMon? bought = await ReadMemberAsync(connection, transaction, memberId, cancellationToken);

        if (bought is null) return null;

        // The guard, and the whole of what settles two people pressing buy at once. Not a
        // lock and not a check beforehand: a check reads the past, and by the time it has
        // answered somebody else has committed. This asks the database to change the row
        // only if it is still the row that was read, and a buyer who changed nothing lost.
        await using (SqliteCommand sell = connection.CreateCommand())
        {
            sell.Transaction = transaction;
            sell.CommandText =
                $"""
                UPDATE market_listings
                SET state = {Sold}, buyer_id = $buyer, sold_at = $now, member_id = NULL
                WHERE id = $id AND state = {ForSale};
                """;

            sell.Parameters.AddWithValue("$id", listingId);
            sell.Parameters.AddWithValue("$buyer", buyerId);
            sell.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

            if (await sell.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        }

        // The escrowed row goes rather than being re-parented: the buyer's copy is written
        // below as part of their own character, where every save after this expects to
        // find it. A row left behind in the fourth state would be a creature nobody owns.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM party_members WHERE id = $member;";
            clear.Parameters.AddWithValue("$member", memberId);

            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteCharacterAsync(
            connection,
            transaction,
            buyerId,
            buyer with { Box = [.. buyer.Box, bought], Money = buyer.Money - price },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return (bought, price);
    }

    public async Task<int> CollectAsync(
        long sellerId,
        SavedCharacter current,
        int ceiling,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int owed;

        await using (SqliteCommand total = connection.CreateCommand())
        {
            total.Transaction = transaction;
            total.CommandText =
                $"SELECT COALESCE(SUM(price), 0) FROM market_listings WHERE seller_id = $seller AND state = {Sold};";

            total.Parameters.AddWithValue("$seller", sellerId);

            owed = Convert.ToInt32(await total.ExecuteScalarAsync(cancellationToken));
        }

        if (owed == 0) return 0;

        // Deleted in the same breath as being paid. A listing whose money has been
        // collected is kept for nothing, and one kept by accident is one that pays twice.
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText =
                $"DELETE FROM market_listings WHERE seller_id = $seller AND state = {Sold};";

            clear.Parameters.AddWithValue("$seller", sellerId);

            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        // The ceiling is the caller's, because how much money a character may hold is a
        // rule about the game rather than about the disk. What is paid is what fits, and
        // the difference is said out loud by whoever asked rather than lost quietly here.
        int paid = Math.Min(owed, Math.Max(0, ceiling - current.Money));

        await WriteCharacterAsync(
            connection,
            transaction,
            sellerId,
            current with { Money = current.Money + paid },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return paid;
    }

    /// <summary>Reads one creature back out of its row, moves and all.</summary>
    private static async Task<SavedMon?> ReadMemberAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long memberId,
        CancellationToken cancellationToken)
    {
        var moves = new List<int>();
        var left = new List<int>();

        await using (SqliteCommand known = connection.CreateCommand())
        {
            known.Transaction = transaction;
            known.CommandText = "SELECT move_id, pp FROM party_moves WHERE member_id = $id ORDER BY slot;";
            known.Parameters.AddWithValue("$id", memberId);

            await using SqliteDataReader reading = await known.ExecuteReaderAsync(cancellationToken);

            while (await reading.ReadAsync(cancellationToken))
            {
                moves.Add(reading.GetInt32(0));
                left.Add(reading.GetInt32(1));
            }
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT species, level, nickname, current_hp, status, nature, experience, held_item,
                   ev_hp, ev_attack, ev_defense, ev_speed, ev_spattack, ev_spdefense,
                   iv_hp, iv_attack, iv_defense, iv_speed, iv_spattack, iv_spdefense, sex, ability_slot
            FROM party_members WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$id", memberId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new SavedMon(
            Species: reader.GetInt32(0),
            Level: reader.GetInt32(1),
            Nickname: reader.IsDBNull(2) ? null : reader.GetString(2),
            CurrentHp: reader.GetInt32(3),
            Status: (StatusCondition)reader.GetInt32(4),
            Nature: (Nature)reader.GetInt32(5),
            Moves: moves,
            Experience: reader.GetInt32(6))
        {
            HeldItem = reader.GetInt32(7),
            Pp = [.. left.Where(p => p >= 0)],

            Evs = Effort.Of([.. Enumerable.Range(8, 6).Select(reader.GetInt32)]) is { IsNone: false } earned
                ? [.. earned.Values]
                : [],

            Ivs = Genes.Of([.. Enumerable.Range(14, 6).Select(reader.GetInt32)]) is { IsPerfect: false } born
                ? [.. born.Values]
                : [],

            Sex = (Gender)reader.GetInt32(20),
            AbilitySlot = reader.GetInt32(21),
        };
    }

    private static async Task<IReadOnlyList<Listing>> ReadListingsAsync(
        SqliteConnection connection,
        string where,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            $"""
            SELECT l.id, a.username, l.species, l.level, l.price, l.state, l.sex,
                   l.{string.Join(", l.", ListingGeneColumns)}
            FROM market_listings l
            JOIN accounts a ON a.id = l.seller_id
            {where};
            """;

        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);

        var listings = new List<Listing>();

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            listings.Add(new Listing(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4))
            {
                Sold = reader.GetInt32(5) != ForSale,
                Sex = (Gender)reader.GetInt32(6),
                Ivs = [.. Enumerable.Range(7, 6).Select(reader.GetInt32)],
            });
        }

        return listings;
    }

    // ---- friends ---------------------------------------------------------------------

    public async Task<bool> BefriendAsync(
        long accountId, string name, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        if (await AccountNamedAsync(connection, name, cancellationToken) is not { } friendId) return false;

        // Nobody is their own friend. Harmless if allowed, and it would put a line in
        // everybody's list saying they are online whenever they are looking at it.
        if (friendId == accountId) return false;

        await using SqliteCommand insert = connection.CreateCommand();

        // The table's own key refuses a second copy, so the duplicate case is answered by
        // the database rather than by a check this method has to remember to do first —
        // and a check first is a check two calls at once can both pass.
        insert.CommandText =
            """
            INSERT OR IGNORE INTO friends (account_id, friend_id, added_at)
            VALUES ($me, $them, $now);
            """;

        insert.Parameters.AddWithValue("$me", accountId);
        insert.Parameters.AddWithValue("$them", friendId);
        insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        return await insert.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> ForgetAsync(
        long accountId, string name, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        if (await AccountNamedAsync(connection, name, cancellationToken) is not { } friendId) return false;

        await using SqliteCommand remove = connection.CreateCommand();
        remove.CommandText = "DELETE FROM friends WHERE account_id = $me AND friend_id = $them;";
        remove.Parameters.AddWithValue("$me", accountId);
        remove.Parameters.AddWithValue("$them", friendId);

        return await remove.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<Friend>> FriendsAsync(
        long accountId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = Open();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a.id, a.username
            FROM friends f
            JOIN accounts a ON a.id = f.friend_id
            WHERE f.account_id = $me
            ORDER BY f.added_at;
            """;

        command.Parameters.AddWithValue("$me", accountId);

        var friends = new List<Friend>();

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            friends.Add(new Friend(reader.GetInt64(0), reader.GetString(1)));

        return friends;
    }

    /// <summary>
    /// The account playing under a name, folded the way logging in folds it — so adding
    /// somebody works with whatever capitals were read off the top of a head.
    /// </summary>
    private static async Task<long?> AccountNamedAsync(
        SqliteConnection connection, string name, CancellationToken cancellationToken)
    {
        await using SqliteCommand find = connection.CreateCommand();
        find.CommandText = "SELECT id FROM accounts WHERE username_folded = $folded;";
        find.Parameters.AddWithValue("$folded", UsernameRules.Fold(name));

        return await find.ExecuteScalarAsync(cancellationToken) is long id ? id : null;
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
