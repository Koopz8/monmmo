namespace PokeMmo.Server;

/// <summary>How one flag was moved during one pass of the walk.</summary>
/// <param name="Flag">The flag.</param>
/// <param name="Pass">Which pass.</param>
/// <param name="Moves">How many times it was set or cleared during it.</param>
/// <param name="Addresses">How many distinct script addresses did the moving.</param>
public sealed record ToggledInAPass(int Flag, int Pass, int Moves, int Addresses)
{
    /// <summary>
    /// An odd number of moves in a pass leaves the flag in the opposite state to the one it
    /// started in.
    /// </summary>
    public bool Odd => Moves % 2 == 1;

    public override string ToString() =>
        $"pass {Pass}: {Moves} move(s) at {Addresses} address(es)"
        + (Odd ? " — ODD, so it ends the pass the other way round" : " — even, so it ends as it began");
}

/// <summary>
/// Why a walk over this cartridge stops on a cycle rather than on a fixed point.
/// <para>
/// <b>239 found that the run stopped settling the moment signs went into it, and 240 named the
/// two flags that make it go round.</b> Neither said why, and "the value at the end of a pass
/// depends on which map the walk reached last" has stood as the explanation since — which is a
/// description of oscillation rather than a cause of it.
/// </para>
/// <para>
/// The cause is parity. A block that reads a flag and writes the opposite is a TOGGLE, and a
/// walk that runs an odd number of toggles in a pass ends that pass with the flag the other way
/// round — every pass, forever. <c>0x026C</c> is toggled by three signs on three maps sharing one
/// block, and three is odd.
/// </para>
/// <para>
/// <b>It is a counting argument and it can come back empty.</b> A flag moved an even number of
/// times a pass settles, and one moved an odd number cannot; if every flag that moves both ways
/// were moved evenly, this would say so and the cycle would be somebody else's fault.
/// </para>
/// </summary>
public static class WhyItGoesRound
{
    /// <summary>
    /// Every flag that moves both ways, per pass, with how many moves and how many addresses.
    /// </summary>
    /// <param name="moves">The run's own record of every set and clear, in order.</param>
    public static IReadOnlyList<ToggledInAPass> In(IEnumerable<MovedAFlag> moves)
    {
        List<MovedAFlag> all = [.. moves];

        HashSet<int> bothWays =
        [
            .. all.GroupBy(m => m.Flag)
                .Where(g => g.Any(m => m.Cleared) && g.Any(m => !m.Cleared))
                .Select(g => g.Key),
        ];

        return
        [
            .. all.Where(m => bothWays.Contains(m.Flag))
                .GroupBy(m => (m.Flag, m.Pass))
                .Select(g => new ToggledInAPass(
                    g.Key.Flag,
                    g.Key.Pass,
                    g.Count(),
                    g.Select(m => m.Address).Distinct().Count()))
                .OrderBy(t => t.Flag)
                .ThenBy(t => t.Pass),
        ];
    }

    /// <summary>
    /// The flags moved an odd number of times on the run's LAST pass — the ones that cannot
    /// settle.
    /// </summary>
    /// <param name="toggled">Every both-ways flag's moves, per pass, from <see cref="In"/>.</param>
    /// <param name="lastPass">The pass the run stopped on.</param>
    /// <remarks>
    /// <para>
    /// <b>The RUN's last pass, and not the last pass each flag happened to move in.</b> The first
    /// version of this asked the second question and reported <c>0x002E</c>, which is set once on
    /// pass one and cleared once on pass two and then never moves again. Odd on the last pass it
    /// took part in, and settled by pass three — because it stopped. A flag that is not moving is
    /// not oscillating, and asking about a pass it took no part in is the only way to tell.
    /// </para>
    /// <para>
    /// The early passes of a walk are the story opening up: things happen once. What decides
    /// whether the run settles is what is still happening on the pass it stops on.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ToggledInAPass> CannotSettle(
        IEnumerable<ToggledInAPass> toggled, int lastPass) =>
        [.. toggled.Where(t => t.Pass == lastPass && t.Odd).OrderBy(t => t.Flag)];
}
