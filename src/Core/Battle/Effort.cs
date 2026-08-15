using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// What a creature has to show for every fight it has won.
/// <para>
/// Six numbers, one per stat, in the order the six stats are already in. They are not
/// invented: every species record on this cartridge says what beating one of them leaves
/// behind, in two bytes that have been extracted since the base-stat table was first
/// read and used by nothing. <see cref="Stats.Hp"/> and <see cref="Stats.Other"/> have
/// taken an <c>ev</c> argument since they were written, and until this existed every
/// caller in the project left it at its default — which is to say every creature in the
/// game had been through the same number of fights as one that had never fought.
/// </para>
/// <para>
/// The two limits are <b>modelled</b>. They are in the games' code rather than in any
/// table, so they are here, in the open, in one place, rather than spread through the
/// code that enforces them.
/// </para>
/// </summary>
public sealed class Effort
{
    /// <summary>The most one stat can hold. <b>Modelled.</b></summary>
    public const int MostInOneStat = 255;

    /// <summary>The most all six can hold together. <b>Modelled.</b></summary>
    public const int MostAltogether = 510;

    /// <summary>The six stats this is counted in, in the order every other table uses.</summary>
    public static readonly Stat[] Order =
        [Stat.Hp, Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpAttack, Stat.SpDefense];

    private readonly int[] _by;

    private Effort(int[] by) => _by = by;

    /// <summary>A creature that has never won anything.</summary>
    public static readonly Effort None = new(new int[6]);

    /// <summary>Six numbers as they came out of a save. Anything shorter is padded with nought.</summary>
    public static Effort Of(IReadOnlyList<int>? values)
    {
        if (values is null || values.Count == 0) return None;

        var by = new int[6];

        for (int i = 0; i < 6 && i < values.Count; i++)
            by[i] = Math.Clamp(values[i], 0, MostInOneStat);

        return new Effort(by);
    }

    /// <summary>What is held in one stat.</summary>
    public int In(Stat stat)
    {
        int index = Array.IndexOf(Order, stat);

        return index < 0 ? 0 : _by[index];
    }

    /// <summary>All six, in order, for writing down.</summary>
    public IReadOnlyList<int> Values => _by;

    /// <summary>Everything, added up.</summary>
    public int Total => _by.Sum();

    /// <summary>True when nothing has been earned at all.</summary>
    public bool IsNone => Total == 0;

    /// <summary>
    /// This, plus what beating one of that species leaves behind.
    /// <para>
    /// Both limits bite here rather than at the stat calculation, because they are about
    /// what a creature is allowed to have earned and not about arithmetic. The per-stat
    /// one is applied first and the total one takes whatever is left, so a creature 8
    /// short of the ceiling gains 8 of a 3 and not none of it.
    /// </para>
    /// </summary>
    public Effort Plus(SpeciesData defeated)
    {
        var by = (int[])_by.Clone();
        int room = MostAltogether - Total;

        if (room <= 0) return this;

        for (int i = 0; i < 6 && room > 0; i++)
        {
            int gained = Math.Min(defeated.EvYield(Order[i]), MostInOneStat - by[i]);
            gained = Math.Min(gained, room);

            by[i] += gained;
            room -= gained;
        }

        return new Effort(by);
    }

    public override bool Equals(object? obj) => obj is Effort other && _by.SequenceEqual(other._by);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (int value in _by) hash.Add(value);

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join("/", _by);
}
