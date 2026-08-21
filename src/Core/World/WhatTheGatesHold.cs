namespace PokeMmo.Core.World;

/// <summary>How many gates a set of flags is, and how many objects they hold between them.</summary>
/// <param name="Gates">How many flags.</param>
/// <param name="Objects">How many objects those flags hold, added up.</param>
/// <param name="HoldingSeveral">How many of the flags hold more than one object.</param>
/// <param name="HoldingNothing">How many hold none at all — the boat's gates do.</param>
public sealed record WhatGatesHold(int Gates, int Objects, int HoldingSeveral, int HoldingNothing)
{
    /// <summary>The objects held by the flags that hold more than one.</summary>
    public int InTheSeveral { get; init; }
}

/// <summary>
/// The object side of a flag gate, which nothing in this project ever counted.
/// </summary>
/// <remarks>
/// <para>
/// Every line <c>--play</c> prints about gates counts GATES. Four numbers about what those gates
/// HOLD — "62 gates hold 240 people", "146 trees and rocks", "158 objects" — have been quoted in
/// this project's prompt since milestone 190 and <b>no instrument printed one of them</b>. A
/// number nothing computes cannot come back wrong, which is worse than a number that is stale;
/// 231 marked the debt and this is it being paid.
/// </para>
/// <para>
/// <b>Out here rather than in the printer.</b> A total, a split and a shape summed inline in a
/// dump command is a rule no fixture can reach, which is the fault this project has fixed at 219,
/// 221, 222, 223 and 257.
/// </para>
/// </remarks>
public static class WhatTheGatesHold
{
    /// <summary>What one set of gating flags holds.</summary>
    /// <remarks>
    /// A flag that holds NOTHING is still a gate — the boat's two are — so the gate count is the
    /// flags given and not the flags with something behind them. Folding those together is how
    /// "322 gating flags" and "320 gate somebody standing there" become one number and stop
    /// being two facts.
    /// </remarks>
    public static WhatGatesHold Of(FlagGates gates, IEnumerable<int> flags)
    {
        List<int> asked = [.. flags];

        return new WhatGatesHold(
            asked.Count,
            asked.Sum(f => gates.Behind(f).Count),
            asked.Count(f => gates.Behind(f).Count > 1),
            asked.Count(f => gates.Behind(f).Count == 0))
        {
            InTheSeveral = asked.Where(f => gates.Behind(f).Count > 1).Sum(f => gates.Behind(f).Count),
        };
    }

    /// <summary>How many gates hold one object, two to four, and so on.</summary>
    /// <remarks>
    /// A total says nothing about whether one gate holds thirty-two people or thirty-two gates
    /// hold one each, and 190's "62 gates hold 240 people" is a claim about exactly that.
    /// </remarks>
    public static IReadOnlyList<(string Band, int Gates)> Shape(
        FlagGates gates, IEnumerable<int> flags) =>
        [
            .. flags
                .GroupBy(f => gates.Behind(f).Count switch
                {
                    0 => "hold nothing",
                    1 => "hold one",
                    <= 4 => "hold 2-4",
                    <= 16 => "hold 5-16",
                    _ => "hold more than 16",
                })
                .OrderByDescending(g => g.Count())
                .Select(g => (g.Key, g.Count())),
        ];
}
