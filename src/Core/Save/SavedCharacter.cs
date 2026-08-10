using PokeMmo.Core.Battle;
using PokeMmo.Core.World;

namespace PokeMmo.Core.Save;

/// <summary>
/// One party member as the server stores it.
/// <para>
/// Numbers only — a species index, a level, move indices. No names, no base stats, no
/// sprites. That is not laziness: the server has no cartridge and must never need
/// one, so everything it keeps has to be meaningless without the player's own image
/// to resolve it against. The client turns these numbers back into something with a
/// name and a picture; a save file on its own is a list of integers.
/// </para>
/// </summary>
public sealed record SavedMon(
    int Species,
    int Level,
    string? Nickname,
    int CurrentHp,
    StatusCondition Status,
    Nature Nature,
    IReadOnlyList<int> Moves)
{
    /// <summary>
    /// Compares move lists by their contents.
    /// <para>
    /// A record compares its members with <c>Equals</c>, and for a list that is
    /// reference equality — so without this, a save read back from the database is
    /// never equal to the one that was written, however identical it is. That is a
    /// trap worth closing on the type rather than working around at each use, because
    /// "has anything changed since the last save?" is exactly the question this type
    /// exists to answer.
    /// </para>
    /// </summary>
    public bool Equals(SavedMon? other) =>
        other is not null &&
        Species == other.Species &&
        Level == other.Level &&
        Nickname == other.Nickname &&
        CurrentHp == other.CurrentHp &&
        Status == other.Status &&
        Nature == other.Nature &&
        Moves.SequenceEqual(other.Moves);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Species);
        hash.Add(Level);
        hash.Add(Nickname);
        hash.Add(CurrentHp);
        hash.Add(Status);
        hash.Add(Nature);

        foreach (int move in Moves) hash.Add(move);

        return hash.ToHashCode();
    }
}

/// <summary>
/// Everything about a player that outlives their connection.
/// </summary>
public sealed record SavedCharacter(
    string MapId,
    int X,
    int Y,
    Direction Facing,
    int Balls,
    IReadOnlyList<SavedMon> Party)
{
    /// <summary>What a new account starts with. Enough to catch something.</summary>
    public const int StartingBalls = 20;

    public static SavedCharacter Fresh(string mapId, int x, int y) =>
        new(mapId, x, y, Direction.Down, StartingBalls, []);

    public bool HasParty => Party.Count > 0;
}
