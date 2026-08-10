namespace PokeMmo.Core.Battle;

/// <summary>
/// What a player is carrying.
/// <para>
/// Six is the limit the series has always used, and it is worth enforcing here rather
/// than in the client: the server will need the same rule, and a party that can
/// silently grow is the sort of thing that becomes a duplication exploit once trading
/// exists.
/// </para>
/// </summary>
public sealed class Party
{
    public const int MaxSize = 6;

    private readonly List<Battler> _members = [];

    public IReadOnlyList<Battler> Members => _members;

    public int Count => _members.Count;

    public bool IsFull => _members.Count >= MaxSize;

    public bool IsEmpty => _members.Count == 0;

    /// <summary>True when anything in the party can still fight.</summary>
    public bool HasHealthyMember => _members.Any(m => !m.HasFainted);

    /// <summary>The first member still standing, which is who leads a battle.</summary>
    public Battler? Lead => _members.FirstOrDefault(m => !m.HasFainted) ?? _members.FirstOrDefault();

    /// <summary>Adds a member. Returns false when the party is already full.</summary>
    public bool TryAdd(Battler member)
    {
        if (IsFull) return false;

        _members.Add(member);
        return true;
    }

    public Battler? At(int index) => index >= 0 && index < _members.Count ? _members[index] : null;

    /// <summary>Restores everything, as a visit to a centre would.</summary>
    public void HealAll()
    {
        foreach (Battler member in _members)
        {
            member.Heal(member.MaxHp);
            member.Status = StatusCondition.None;
            member.SleepTurns = 0;
            member.ResetStages();
        }
    }
}
