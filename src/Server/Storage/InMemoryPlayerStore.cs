using PokeMmo.Core.Save;

namespace PokeMmo.Server.Storage;

/// <summary>
/// The same store, in a dictionary.
/// <para>
/// It exists so tests about joining, moving and saving do not become tests about
/// SQLite. The SQLite implementation has its own tests; everything above the store
/// should be provable without a file on disk.
/// </para>
/// </summary>
public sealed class InMemoryPlayerStore : IPlayerStore
{
    private sealed record Row(long Id, string Username, string Hash)
    {
        public SavedCharacter Character { get; set; } = SavedCharacter.Fresh("", 0, 0);
    }

    private readonly Dictionary<string, Row> _byFoldedName = [];
    private readonly Dictionary<long, Row> _byId = [];
    private readonly object _gate = new();

    private long _nextId = 1;

    /// <summary>
    /// Hashing is skipped here — Argon2id takes tens of milliseconds by design, which
    /// is the point in production and a waste in a test that creates fifty accounts.
    /// The real hashing is tested directly.
    /// </summary>
    private static string Obscure(string password) => $"plain:{password}";

    public Task<AuthOutcome> RegisterAsync(
        string username, string password, SavedCharacter fresh, CancellationToken cancellationToken = default)
    {
        if (UsernameRules.Problem(username) is { } nameProblem)
            return Task.FromResult<AuthOutcome>(new AuthOutcome.Failed(nameProblem));

        if (UsernameRules.PasswordProblem(password) is { } wordProblem)
            return Task.FromResult<AuthOutcome>(new AuthOutcome.Failed(wordProblem));

        lock (_gate)
        {
            string folded = UsernameRules.Fold(username);

            if (_byFoldedName.ContainsKey(folded))
                return Task.FromResult<AuthOutcome>(new AuthOutcome.Failed("That name is taken."));

            var row = new Row(_nextId++, username.Trim(), Obscure(password)) { Character = fresh };

            _byFoldedName[folded] = row;
            _byId[row.Id] = row;

            return Task.FromResult<AuthOutcome>(
                new AuthOutcome.Success(new Account(row.Id, row.Username), fresh));
        }
    }

    public Task<AuthOutcome> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_byFoldedName.TryGetValue(UsernameRules.Fold(username), out Row? row) || row.Hash != Obscure(password))
                return Task.FromResult<AuthOutcome>(new AuthOutcome.Failed("Wrong name or password."));

            return Task.FromResult<AuthOutcome>(
                new AuthOutcome.Success(new Account(row.Id, row.Username), row.Character));
        }
    }

    public Task SaveAsync(long accountId, SavedCharacter character, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_byId.TryGetValue(accountId, out Row? row)) row.Character = character;
        }

        return Task.CompletedTask;
    }

    public Task<int> ForgetStoryAsync(string username, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_byFoldedName.TryGetValue(UsernameRules.Fold(username), out Row? row))
                return Task.FromResult(-1);

            int forgotten = row.Character.Flags.Count + row.Character.Variables.Count;

            row.Character = row.Character with { Flags = [], Variables = [] };

            return Task.FromResult(forgotten);
        }
    }
}
