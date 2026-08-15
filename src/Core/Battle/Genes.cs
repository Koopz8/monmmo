using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// What a creature was born with.
/// <para>
/// Six numbers, one per stat, nought to thirty-one, fixed for life. They are the other
/// half of the pair that makes two creatures of the same species different: effort is
/// what a creature has done, and this is what it was.
/// </para>
/// <para>
/// <c>Stats.Hp</c> and <c>Stats.Other</c> have taken an <c>iv</c> argument since they were
/// written and every caller in this project left it at its default of thirty-one — so
/// every creature in the game, wild, given, traded or caught, was perfect. That is not a
/// missing feature. It is the feature the whole of the rest of an MMO is about: without
/// it there is nothing to breed for, nothing to hunt for, nothing that makes one PIDGEY
/// worth more than another, and therefore nothing worth trading.
/// </para>
/// <para>
/// The range is <b>modelled</b>: nought to thirty-one is in the games' code and not in
/// any table this project can read. The roll is uniform and independent per stat, which
/// is the same code's behaviour and the only reading that needs no further numbers.
/// </para>
/// </summary>
public sealed class Genes
{
    /// <summary>The best any single one can be. <b>Modelled.</b></summary>
    public const int Best = 31;

    /// <summary>The six stats these are counted in, in the order every table uses.</summary>
    public static readonly Stat[] Order = Effort.Order;

    private readonly int[] _by;

    private Genes(int[] by) => _by = by;

    /// <summary>
    /// A creature with the best of everything, which is what every creature in this
    /// project was until this type existed.
    /// </summary>
    public static readonly Genes Perfect = new([Best, Best, Best, Best, Best, Best]);

    /// <summary>Six numbers as they came out of a save. Anything shorter is perfect in the rest.</summary>
    public static Genes Of(IReadOnlyList<int>? values)
    {
        if (values is null || values.Count == 0) return Perfect;

        var by = new int[6];

        for (int i = 0; i < 6; i++)
            by[i] = i < values.Count ? Math.Clamp(values[i], 0, Best) : Best;

        return new Genes(by);
    }

    /// <summary>
    /// A fresh roll: six independent numbers, each equally likely.
    /// <para>
    /// Every creature that is not descended from another one gets these — a wild
    /// encounter, a gift, a starter. What a trainer's party gets is a decision of its
    /// own and is made where trainers are built.
    /// </para>
    /// </summary>
    public static Genes Roll(BattleRng rng)
    {
        var by = new int[6];

        for (int i = 0; i < 6; i++) by[i] = rng.Next(Best + 1);

        return new Genes(by);
    }

    /// <summary>What this one was born with in one stat.</summary>
    public int In(Stat stat)
    {
        int index = Array.IndexOf(Order, stat);

        return index < 0 ? 0 : _by[index];
    }

    /// <summary>All six, in order, for writing down.</summary>
    public IReadOnlyList<int> Values => _by;

    /// <summary>Everything added up, which is the number a market would price.</summary>
    public int Total => _by.Sum();

    /// <summary>How many of the six are as good as they can be.</summary>
    public int Perfectly => _by.Count(v => v == Best);

    /// <summary>True when this is the best of everything, which is what an empty save means.</summary>
    public bool IsPerfect => Perfectly == 6;

    public override bool Equals(object? obj) => obj is Genes other && _by.SequenceEqual(other._by);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (int value in _by) hash.Add(value);

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join("/", _by);
}
