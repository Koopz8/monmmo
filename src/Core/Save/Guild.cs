namespace PokeMmo.Core.Save;

/// <summary>
/// A named group of players, as everybody sees it from outside.
/// <para>
/// The count comes with it rather than being asked for separately, because the one question
/// anybody has about a guild they are not in is how many people are in it — and a list of
/// guilds that made you ask again per guild would be a list nobody would print.
/// </para>
/// </summary>
public sealed record Guild(long Id, string Name, int Members)
{
    /// <summary>The shortest and longest a name may be.</summary>
    public const int ShortestName = 3;

    public const int LongestName = 20;

    /// <summary>
    /// True when this is a name somebody may found a guild under.
    /// <para>
    /// Letters, digits and single spaces between them. Deliberately narrower than a
    /// player's own name allows: a guild name is shown beside other people's names in a
    /// chat line, and a name made of spaces or punctuation is a name that can be made to
    /// look like somebody else's.
    /// </para>
    /// </summary>
    public static bool IsAName(string? name) =>
        name is { Length: >= ShortestName and <= LongestName }
        && name.Trim() == name
        && !name.Contains("  ")
        && name.All(c => char.IsLetterOrDigit(c) || c == ' ')
        && name.Any(char.IsLetterOrDigit);
}

/// <summary>
/// One person in a guild.
/// <para>
/// Whether they are online is deliberately not here. That is a fact about this second and
/// belongs to the world; a member record is a fact about an account and belongs on the disk,
/// and putting the two in one type is how a list comes to be shown stale.
/// </para>
/// </summary>
public sealed record GuildMember(string Name, bool IsLeader);
