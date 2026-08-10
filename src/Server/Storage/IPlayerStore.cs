using System.Text.RegularExpressions;
using PokeMmo.Core.Save;

namespace PokeMmo.Server.Storage;

/// <summary>An authenticated account.</summary>
public sealed record Account(long Id, string Username);

/// <summary>The result of trying to register or log in.</summary>
public abstract record AuthOutcome
{
    public sealed record Success(Account Account, SavedCharacter Character) : AuthOutcome;

    /// <summary>Refused, with something the player can read.</summary>
    public sealed record Failed(string Reason) : AuthOutcome;
}

/// <summary>
/// Where players are kept.
/// <para>
/// An interface rather than a class so the game tests can run against memory and
/// never touch a file, and so the day this outgrows SQLite is a new implementation
/// rather than a rewrite. Nothing above this line knows any SQL.
/// </para>
/// </summary>
public interface IPlayerStore
{
    Task<AuthOutcome> RegisterAsync(string username, string password, SavedCharacter fresh, CancellationToken cancellationToken = default);

    Task<AuthOutcome> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Writes a character back. Called on disconnect and after anything worth keeping.</summary>
    Task SaveAsync(long accountId, SavedCharacter character, CancellationToken cancellationToken = default);
}

/// <summary>What a username is allowed to be, shared by every store.</summary>
public static class UsernameRules
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 16;

    private static readonly Regex Allowed = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    /// <summary>
    /// The form uniqueness is checked against. Two accounts differing only in case
    /// would be indistinguishable to anyone reading chat, which is exactly the shape
    /// impersonation takes.
    /// </summary>
    public static string Fold(string username) => username.Trim().ToLowerInvariant();

    public static string? Problem(string username)
    {
        string trimmed = username.Trim();

        if (trimmed.Length is < MinimumLength or > MaximumLength)
            return $"Names are {MinimumLength} to {MaximumLength} characters.";

        return Allowed.IsMatch(trimmed) ? null : "Names use letters, numbers and underscores.";
    }

    public static string? PasswordProblem(string password) =>
        password.Length < PasswordHasher.MinimumPasswordLength
            ? $"Passwords are at least {PasswordHasher.MinimumPasswordLength} characters."
            : null;
}
