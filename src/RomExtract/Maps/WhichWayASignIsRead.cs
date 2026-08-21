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
    /// Nought for an empty population too. A side that every one of no signs has open is every
    /// side, which is the same ambiguity with a smaller denominator.
    /// </para>
    /// </remarks>
    public static int? TheSideAllOfThemHaveOpen(IReadOnlyList<bool[]> open)
    {
        if (open.Count == 0) return null;

        List<int> always =
        [
            .. Enumerable.Range(0, Sides.Count)
                .Where(side => open.All(one => side < one.Length && one[side])),
        ];

        return always.Count == 1 ? always[0] : null;
    }

    /// <summary>The side opposite the one given — the control on any side reading.</summary>
    /// <remarks>
    /// If the squares merely had a lot of open neighbours, the far side would be open about as
    /// often as the near one. On kinds <c>0x03</c> and <c>0x04</c> it is open NEVER, which is what
    /// turns "one side is always open" into "this side and not that one".
    /// </remarks>
    public static int Across(int side) => side switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };
}
