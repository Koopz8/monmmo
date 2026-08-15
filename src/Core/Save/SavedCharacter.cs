using PokeMmo.Core.Battle;
using PokeMmo.Core.World;

using PokeMmo.Core.Cosmetics;

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
    /// <summary>What this one is carrying, or zero. Kept because a stolen item is kept.</summary>
    public int HeldItem { get; init; }

    /// <summary>
    /// What is left of each move, in the same order as <see cref="Moves"/>.
    /// <para>
    /// Empty means full, which is what everything written before this existed comes back
    /// as — and what a creature just caught or just handed over should be. A list shorter
    /// than the moves is read the same way for the slots it does not reach.
    /// </para>
    /// <para>
    /// It has to be here rather than only in a battle, because PP is the one thing a fight
    /// takes that a fight cannot give back. Without it every battler was rebuilt full and
    /// running out meant nothing past the last turn of the battle it happened in.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Pp { get; init; } = [];

    /// <summary>
    /// What this one has to show for the fights it has won, in the six-stat order.
    /// <para>
    /// Empty means none, which is what everything written before this existed comes back
    /// as and what something just caught is. It belongs to the save rather than to the
    /// battle for the same reason PP does: it is earned in a fight and spent nowhere, so
    /// a creature that could only hold it for the length of one battle would be a
    /// creature that never got stronger for having fought.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Evs { get; init; } = [];

    /// <summary>The same six numbers as something that knows what they mean.</summary>
    public Effort Earned => Effort.Of(Evs);

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
        HeldItem == other.HeldItem &&
        Moves.SequenceEqual(other.Moves) &&
        Pp.SequenceEqual(other.Pp) &&
        Evs.SequenceEqual(other.Evs);

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
        foreach (int left in Pp) hash.Add(left);
        foreach (int earned in Evs) hash.Add(earned);

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
    /// What this account owns and what it has on.
    /// <para>
    /// Both are the server's, because both are what a shop sells — see the note on
    /// <see cref="Cosmetics.Appearance"/> for why nothing in that namespace is derived from
    /// a cartridge and why it is kept apart from everything that is.
    /// </para>
    /// </summary>
    public IReadOnlyList<int> Cosmetics { get; init; } = [];

    public Appearance Looks { get; init; } = Appearance.Bare;

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
    /// Everything not in the party.
    /// <para>
    /// An init property for the same reason the defeated trainers are. Empty on every
    /// account that has never filled a party, which is most of them.
    /// </para>
    /// </summary>
    public IReadOnlyList<SavedMon> Box { get; init; } = [];

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

    /// <summary>
    /// The centre this character last rested at, and where they stood to do it.
    /// <para>
    /// Persisted rather than kept for the session, because the walk back is the whole
    /// cost of losing and a character who forgets it over a disconnect has been given
    /// their money's worth back.
    /// </para>
    /// </summary>
    /// <summary>
    /// Things already picked up off the ground, as "map:person".
    /// <para>
    /// This project's own naming rather than the cartridge's. Every ball on the ground
    /// has a flag in the games and that flag is not written in the script — the same
    /// dead end the trainer flag turned out to be — while a map id and a local id are
    /// both things the world file already carries.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ItemsTaken { get; init; } = [];

    public string? RestingAt { get; init; }

    public int RestingX { get; init; }

    public int RestingY { get; init; }

    public static SavedCharacter Fresh(string mapId, int x, int y) =>
        new(mapId, x, y, Direction.Down, []);

    public bool HasParty => Party.Count > 0;
}
