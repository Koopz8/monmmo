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
    IReadOnlyList<int> Moves,
    int Experience = 0)
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
        Experience == other.Experience &&
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
        hash.Add(Experience);

        foreach (int move in Moves) hash.Add(move);

        return hash.ToHashCode();
    }
}

/// <summary>One script variable and what it holds.</summary>
public sealed record SavedVariable(int Id, int Value);

/// <summary>
/// Everything about a player that outlives their connection.
/// </summary>
public sealed record SavedCharacter(
    string MapId,
    int X,
    int Y,
    Direction Facing,
    IReadOnlyList<SavedMon> Party)
{
    /// <summary>
    /// What a new account starts with. Enough to catch something.
    /// <para>
    /// Kept as a count of the ordinary ball rather than as a bag, because a bag needs a
    /// rules file to know what an ordinary ball <em>is</em> and a fresh character has to
    /// be makeable without one.
    /// </para>
    /// </summary>
    public const int StartingBalls = 20;

    /// <summary>What a new account starts with, in the games' own currency.</summary>
    public const int StartingMoney = 3000;

    /// <summary>
    /// Trainers this account has already beaten.
    /// <para>
    /// An init property rather than another positional member, because every existing
    /// construction of one of these is correct without it and a new position would have
    /// to be threaded through all of them to say "none yet".
    /// </para>
    /// <para>
    /// It has to be persisted rather than kept for the session. A trainer who forgets
    /// they lost challenges you again the moment you walk back past them, which is
    /// worse than having no trainers at all.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> DefeatedTrainers { get; init; } = [];

    /// <summary>
    /// Everything carried, as item ids and counts.
    /// <para>
    /// Init properties rather than positional members for the same reason the defeated
    /// trainers are: every existing construction of one of these is correct without
    /// them, and threading a new position through all of them to say "nothing yet"
    /// would be churn with no meaning in it.
    /// </para>
    /// </summary>
    public IReadOnlyList<BagEntry> Items { get; init; } = [];

    /// <summary>
    /// Script flags this character has set, and what its script variables hold.
    /// <para>
    /// The cartridge's own bookkeeping: a number per thing that can have happened, and
    /// a few hundred small integers for the things that have a count. The server has no
    /// idea what any of them mean and does not need one — it stores them and hands them
    /// back, and the machine with the cartridge is the only one that can say that flag
    /// 0x2A5 is the one about the parcel.
    /// </para>
    /// <para>
    /// Kept apart from <see cref="DefeatedTrainers"/> rather than folded into it. The
    /// two answer the same question about a trainer and disagree about everything else:
    /// a trainer id is this project's own numbering and survives a re-export, while a
    /// flag is the cartridge's and means nothing without one.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Flags { get; init; } = [];

    public IReadOnlyList<SavedVariable> Variables { get; init; } = [];

    public int Money { get; init; } = StartingMoney;

    public static SavedCharacter Fresh(string mapId, int x, int y) =>
        new(mapId, x, y, Direction.Down, []);

    public bool HasParty => Party.Count > 0;
}
