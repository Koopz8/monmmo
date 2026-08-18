namespace PokeMmo.Server;

/// <summary>
/// The states a playthrough has already been in, so a loop that goes round in a circle can say so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The settle test compares a pass with the one before it, and that only ever finds a fixed
/// point.</b> It was enough for as long as everything the run did was one-way: flags got set,
/// things got picked up, people got talked to, and a pass that changed nothing had nothing left to
/// change.
/// </para>
/// <para>
/// Running the signs broke that. `9.6` is a fifteen-door puzzle whose doors set <c>0x8008</c> to
/// their own number and whose shared block sets and CLEARS <c>0x0001</c> depending on the answer,
/// so a run that stands in front of all fifteen every pass flips one flag on and off forever. The
/// counts go 234, 233, 234, 233 … and the pass-to-pass test never fires. Every `--say-yes` row ran
/// to the twenty-four-pass backstop.
/// </para>
/// <para>
/// A two-cycle has opened nothing new — everything it will ever reach, it has reached. This is how
/// the loop notices, and it is deliberately a THIRD answer rather than being folded into
/// <c>NothingMoreOpened</c>: a run that settles and a run that oscillates are different facts
/// about the world, and one of them is a finding.
/// </para>
/// </remarks>
public sealed class WhereItHasBeen
{
    private readonly HashSet<long> _seen = [];

    /// <summary>How many distinct states the run has been in.</summary>
    public int Count => _seen.Count;

    /// <summary>
    /// Records a state and says whether the run had already been in it.
    /// </summary>
    public bool SeenBefore(long signature) => !_seen.Add(signature);

    /// <summary>
    /// A number that stands for everything the settle test looks at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sets go in by their contents and not by their size.</b> A run that clears one flag
    /// and sets another has the same count and is not the same state, and a signature built out of
    /// counts would call that a cycle and stop a run that still had somewhere to go — which is the
    /// expensive direction to be wrong in.
    /// </para>
    /// <para>
    /// Order does not matter, because a set has none: the flags are folded in with a commutative
    /// step so that the same flags in a different order are the same state.
    /// </para>
    /// </remarks>
    public static long Signature(
        IEnumerable<int> flags,
        IEnumerable<int> moves,
        int party,
        int carried,
        int gone,
        int moved)
    {
        long signature = Fold(flags, 0x9E3779B97F4A7C15);

        signature ^= Fold(moves, 0xC2B2AE3D27D4EB4F);

        // These four are counts because that is what the pass-to-pass test has always compared
        // them as, and nothing this run does can take one of them back.
        signature = signature * 31 + party;
        signature = signature * 31 + carried;
        signature = signature * 31 + gone;

        return signature * 31 + moved;
    }

    /// <summary>
    /// The contents of a set, folded so that order cannot change the answer.
    /// </summary>
    private static long Fold(IEnumerable<int> of, ulong salt)
    {
        long folded = 0;

        foreach (int one in of)
        {
            // Scattered first, so that two flags one apart do not fold to nearly the same thing,
            // and then added — addition because it is commutative and a set has no order.
            ulong scattered = (ulong)one * salt;

            folded += (long)(scattered ^ (scattered >> 29));
        }

        return folded;
    }
}
