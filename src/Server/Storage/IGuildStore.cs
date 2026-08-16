using PokeMmo.Core.Save;

namespace PokeMmo.Server.Storage;

/// <summary>
/// Named groups of players, and who is in them.
/// <para>
/// The one rule everything here is built on: <b>somebody is in at most one guild</b>, and
/// that is enforced by the membership table's own key rather than by a check. A check is
/// something two calls at once can both pass, and two calls at once is exactly what happens
/// when somebody accepts two invitations in the same breath.
/// </para>
/// <para>
/// Joining is by invitation and acceptance, which is the opposite of how the friends list
/// works and for a stated reason. A friends list is private and one-directional — adding
/// somebody tells them nothing. A guild is public and shows your name beside other people's,
/// so being put in one without agreeing would be somebody else deciding what you are called.
/// </para>
/// </summary>
public interface IGuildStore
{
    /// <summary>
    /// Founds one, with the founder as its leader. Nothing when the name is taken or not a
    /// name, or when the founder is already in a guild.
    /// </summary>
    Task<Guild?> FoundAsync(long accountId, string name, CancellationToken cancellationToken = default);

    /// <summary>Which guild this account is in, or nothing.</summary>
    Task<Guild?> OfAsync(long accountId, CancellationToken cancellationToken = default);

    /// <summary>Who is in one, leader first and then in the order they joined.</summary>
    Task<IReadOnlyList<GuildMember>> MembersAsync(long guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account ids of everybody in one, for saying something to all of them at once.
    /// <para>
    /// Ids rather than names, because what a chat line needs is to find the people who are
    /// online — and the world knows a player by their account, not by their spelling.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<long>> MemberIdsAsync(long guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks somebody to join. Only the leader may, and only somebody who is not already in
    /// a guild. False for anything else, all of which a player can ask for by being slightly
    /// out of date.
    /// </summary>
    Task<bool> InviteAsync(long leaderId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes up an invitation, and returns what was joined. Nothing when there is no
    /// invitation from that guild, or when the joiner has since joined another.
    /// </summary>
    Task<Guild?> AcceptAsync(long accountId, string guildName, CancellationToken cancellationToken = default);

    /// <summary>Which guilds have asked this account, so somebody can see what to accept.</summary>
    Task<IReadOnlyList<Guild>> InvitationsAsync(long accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves. The last one out takes the guild with them; a leader who leaves with others
    /// still in it hands over to whoever has been in it longest.
    /// <para>
    /// Handing over rather than refusing, because a guild whose leader has stopped playing
    /// would otherwise be a guild nobody can ever invite anybody to — and the longest-serving
    /// member is the only choice that needs no opinion.
    /// </para>
    /// </summary>
    Task<bool> LeaveAsync(long accountId, CancellationToken cancellationToken = default);

    /// <summary>Puts somebody out. Only the leader may, and never themselves.</summary>
    Task<bool> KickAsync(long leaderId, string name, CancellationToken cancellationToken = default);

    /// <summary>Every guild, biggest first.</summary>
    Task<IReadOnlyList<Guild>> AllAsync(int most = 50, CancellationToken cancellationToken = default);
}
