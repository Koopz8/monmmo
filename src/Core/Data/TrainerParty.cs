namespace PokeMmo.Core.Data;

/// <summary>
/// One creature in a trainer's party, as the server needs it.
/// <para>
/// Numbers only, like everything else the server holds. No nickname, because a
/// trainer's creatures do not have one; no name for the species, because names are
/// cartridge text and this file does not carry any.
/// </para>
/// </summary>
public sealed record TrainerMember(int Species, int Level, int HeldItem, IReadOnlyList<int> Moves)
{
    /// <summary>
    /// True when this one's moves were left to the level-up set rather than written out.
    /// <para>
    /// Most trainers in the games do exactly that. The server fills them in from the
    /// learnsets at the moment the battle is built, which is the same thing it already
    /// does for a wild creature.
    /// </para>
    /// </summary>
    public bool UsesLevelUpMoves => Moves.Count == 0;

    /// <summary>
    /// Compares move lists by their contents.
    /// <para>
    /// A record compares its members with <c>Equals</c>, and for a list that is
    /// reference equality — so a party read back out of the rules file is never equal
    /// to the one that went in, however identical it is. This project has now been
    /// caught by that twice; the first was a save that could never be recognised as
    /// unchanged.
    /// </para>
    /// </summary>
    public bool Equals(TrainerMember? other) =>
        other is not null &&
        Species == other.Species &&
        Level == other.Level &&
        HeldItem == other.HeldItem &&
        Moves.SequenceEqual(other.Moves);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Species);
        hash.Add(Level);
        hash.Add(HeldItem);

        foreach (int move in Moves) hash.Add(move);

        return hash.ToHashCode();
    }
}

/// <summary>
/// A trainer's party, keyed by the id a script names them with.
/// <para>
/// The trainer's own name and class stay on the cartridge. The server sends an id and
/// the client, which has an image, turns it into "BUG CATCHER RICK" — the same division
/// that keeps species names off this side.
/// </para>
/// </summary>
public sealed record TrainerParty(int Id, bool IsDouble, IReadOnlyList<TrainerMember> Members)
{
    public bool Equals(TrainerParty? other) =>
        other is not null &&
        Id == other.Id &&
        IsDouble == other.IsDouble &&
        Members.SequenceEqual(other.Members);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Id);
        hash.Add(IsDouble);

        foreach (TrainerMember member in Members) hash.Add(member);

        return hash.ToHashCode();
    }
}
