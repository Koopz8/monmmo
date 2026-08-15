using PokeMmo.Core.Save;

namespace PokeMmo.Server.Storage;

/// <summary>
/// Who somebody wants to keep track of.
/// <para>
/// One-directional on purpose, and it is worth saying why rather than defending it later.
/// A mutual list needs a request, an acceptance, a refusal and somewhere for a pending one
/// to sit — four states and three messages — and none of that buys anything the thing is
/// for. What a player wants is to know whether the two people they play with are on, and
/// which copy of which town to walk into. Adding somebody to your own list gives you that.
/// </para>
/// <para>
/// It gives them nothing about you, which is the other half of the argument: a list that
/// worked both ways the moment one person added the other would be a list that tells
/// strangers when you are online because they typed your name.
/// </para>
/// </summary>
public interface IFriendStore
{
    /// <summary>
    /// Adds somebody by the name they play under. False when there is no such account,
    /// when it is the caller, or when they are already on the list — all things a player
    /// can ask for by being slightly wrong, and none of them worth throwing over.
    /// </summary>
    Task<bool> BefriendAsync(long accountId, string name, CancellationToken cancellationToken = default);

    /// <summary>Takes somebody off it. False when they were not on it.</summary>
    Task<bool> ForgetAsync(long accountId, string name, CancellationToken cancellationToken = default);

    /// <summary>The list, in the order it was made.</summary>
    Task<IReadOnlyList<Friend>> FriendsAsync(long accountId, CancellationToken cancellationToken = default);
}
