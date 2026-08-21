namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// Which side of a sign you have to be standing on to read it (279).
/// <para>
/// <b>A sign record's KIND byte takes five values on this cartridge and this project read it as
/// two.</b> One of them, <c>0x07</c>, is the buried kind 248 found — every one of its 183 records
/// holds an index where the others hold a pointer. The other four all hold pointers and were all
/// read as the same thing, and they are not: <b>kind 0x01's south neighbour is walkable on 73 of
/// 73, kind 0x03's west neighbour on 14 of 14, and kind 0x04's east on 10 of 10</b>, where the
/// commonest kind names no side at all (243, 368, 231 and 198 of 422).
/// </para>
/// <para>
/// 242 established that this project reads a sign from its own square or any of the four around
/// it. For 97 signs that is three squares too many.
/// </para>
/// </summary>
public static class WhichWayASignIsRead
{
    /// <summary>North, south, west, east — the order every side list here is in.</summary>
    public static readonly IReadOnlyList<string> Sides = ["north", "south", "west", "east"];

    /// <summary>
    /// The one side that is open on EVERY sign given, or nought when there is not exactly one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exactly one, or none.</b> A kind whose signs all have two sides open names neither: the
    /// rule would be picking whichever the loop reached first, which is a verdict about the
    /// iteration order dressed as a verdict about the cartridge. This project has had that fault
    /// in another shape — 224's shared wrong list agreed with itself everywhere — and the cheap
    /// guard is to refuse rather than choose.
    /// </para>
    /// <para>
    /// Nought for an empty population too, <b>and with no check for it</b>: a side that every one
    /// of no signs has open is every side, so all four qualify, so it is not exactly one. A guard
    /// was written for that case and a break aimed at it came back green (279) — 219's rule, that
    /// a guard nothing can fail is not a guard, so it was deleted rather than kept and decorated.
    /// <c>NoSignsNamesNoSide</c> is the test that says the behaviour still holds.
    /// </para>
    /// </remarks>
    public static int? TheSideAllOfThemHaveOpen(IReadOnlyList<bool[]> open)
    {
        List<int> always =
        [
            .. Enumerable.Range(0, Sides.Count)
                .Where(side => open.All(one => side < one.Length && one[side])),
        ];

        return always.Count == 1 ? always[0] : null;
    }

    /// <summary>
    /// How many squares carry each behaviour and how many of those hold one of the things being
    /// counted — the direction that can NAME a byte (281).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>"179 signs stand on 0x84" names nothing.</b> It reads the same whether <c>0x84</c> is a
    /// sign board or every wall in the game, and those are opposite findings. The direction that
    /// names it is the other one: <b>189 squares of it exist and 179 hold a sign</b> — 94.7%
    /// against the world's own 0.300%.
    /// </para>
    /// <para>
    /// This is 8's rule wearing a different hat: the count of hits is the numerator and the
    /// population of the BYTE is the denominator, and the one this project reaches for first is
    /// the population of the hits.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<byte, (int Squares, int Marked)> HowOften(
        IEnumerable<(byte Behaviour, bool Marked)> squares)
    {
        var tally = new Dictionary<byte, (int Squares, int Marked)>();

        foreach ((byte behaviour, bool marked) in squares)
        {
            (int was, int held) = tally.GetValueOrDefault(behaviour);

            tally[behaviour] = (was + 1, held + (marked ? 1 : 0));
        }

        return tally;
    }

    /// <summary>
    /// The share of EVERY square in the world that is marked — the floor the shares above are read
    /// against.
    /// </summary>
    /// <remarks>
    /// Over every square and not over the squares of the behaviours that scored: a floor drawn
    /// from the rows that did well is a floor the answer chose (79).
    /// </remarks>
    public static double Everywhere(IReadOnlyDictionary<byte, (int Squares, int Marked)> tally)
    {
        int all = tally.Values.Sum(one => one.Squares);

        return all == 0 ? 0 : (double)tally.Values.Sum(one => one.Marked) / all;
    }

    /// <summary>The side opposite the one given — the control on any side reading.</summary>
    /// <remarks>
    /// If the squares merely had a lot of open neighbours, the far side would be open about as
    /// often as the near one. On kinds <c>0x03</c> and <c>0x04</c> it is open NEVER, which is what
    /// turns "one side is always open" into "this side and not that one".
    /// </remarks>
    public static int Across(int side) => side switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };
}
