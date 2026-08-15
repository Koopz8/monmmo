namespace PokeMmo.Core.Save;

/// <summary>
/// Somebody on a player's list, as the store knows them.
/// <para>
/// An account and the name it plays under, and nothing else. Whether they are online, and
/// where, is not a fact about the list — it is a fact about right now, and it belongs to
/// the world rather than to the disk.
/// </para>
/// </summary>
public sealed record Friend(long AccountId, string Name);
