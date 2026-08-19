namespace PokeMmo.RomExtract.Maps;

/// <summary>
/// One record an event-list reader threw away because its square is not on the map.
/// </summary>
/// <param name="List">Which of the four lists — <c>object</c>, <c>warp</c>, <c>trigger</c>, <c>sign</c>.</param>
/// <param name="Index">Where in the table it was, and <paramref name="Count"/> is how long the table said it is.</param>
/// <param name="Count">The table's own count, so the LAST index can be told from any other.</param>
/// <param name="X">The square the record names.</param>
/// <param name="Y">The square the record names.</param>
/// <param name="Width">The map's width, which is what it was measured against.</param>
/// <param name="Height">The map's height.</param>
/// <param name="Script">The record's script pointer, or nought where the list has none.</param>
/// <param name="Variable">A trigger's condition variable, or nought.</param>
/// <param name="Value">A trigger's condition value, or nought.</param>
/// <param name="At">The byte position the record sits at, so it can be hand-dumped.</param>
/// <param name="LocalId">An object record's own id — the field that says whether it is real.</param>
/// <remarks>
/// <para>
/// <b>Four readers drop records and none of them said how many.</b> The filter is right in
/// principle — a square off the map is a square nobody can stand on — but it runs before anything
/// else sees the record, so every count this project has taken of people, warps, triggers and
/// signs is a count of what survived it. Three milestones (247, 250, 257, 258) have rested on
/// "228 triggers" without anybody printing the number underneath.
/// </para>
/// <para>
/// <b>The reading is the SAME reader.</b> This is filled in at the drop site rather than by a
/// second pass over the tables — a second copy of a record layout is how 251 lost <c>copyvar</c>
/// and how 258 lost the downward arm of a walk.
/// </para>
/// </remarks>
public sealed record DroppedEvent(
    string List,
    int Index,
    int Count,
    int X,
    int Y,
    int Width,
    int Height,
    uint Script = 0,
    int Variable = 0,
    int Value = 0,
    int At = 0,
    int LocalId = 0)
{
    /// <summary>A map's four lists, named once.</summary>
    public const string Objects = "object";

    /// <inheritdoc cref="Objects"/>
    public const string Warps = "warp";

    /// <inheritdoc cref="Objects"/>
    public const string Triggers = "trigger";

    /// <inheritdoc cref="Objects"/>
    public const string Signs = "sign";

    /// <summary>
    /// Not a person on this map at all — a CLONE of a person on a map beside it, marked by
    /// <c>0xFF</c> in the byte after the graphics id and read with a different layout.
    /// </summary>
    /// <remarks>
    /// Its square is in THIS map's coordinates and outside this map's bounds, on the side the
    /// other map lies; the byte the ordinary layout calls an elevation is the local id of the
    /// object being cloned; and the two halfwords the ordinary layout calls a trainer type and a
    /// sight range are the other map's number and bank. Its graphics id matches the object it
    /// clones on 9 of 9 against a floor of 0.21, and it has no script and no flag because a
    /// reflection has neither.
    /// </remarks>
    public const string Clones = "clone";

    /// <summary>
    /// Whether this was the LAST record the table claimed to have.
    /// </summary>
    /// <remarks>
    /// <b>The control that says whether the filter is catching garbage or throwing away work.</b>
    /// A count one too long over-reads the byte after the table, and that record is always the
    /// last one. A record dropped from the MIDDLE of a table is a record the cartridge meant.
    /// </remarks>
    public bool WasTheLastInItsTable => Index == Count - 1;

    /// <summary>
    /// Whether the record's own id is one more than where it sits — the thing bytes past the end
    /// of a table cannot do.
    /// </summary>
    /// <remarks>
    /// <b>The control that beat the other two.</b> Nought of the nine dropped objects carries a
    /// pointer into the cartridge while 1584 of 1639 kept ones do, which reads as overwhelming
    /// evidence that they are noise — and nine of nine have <c>localId == index + 1</c>, which
    /// noise cannot manage once, let alone nine times. When two readings disagree, the one
    /// following fewer edges is the one to believe: this is a byte against an arithmetic, and the
    /// other is a byte, a pointer, and a decode.
    /// </remarks>
    public bool ItsIdMatchesWhereItSits => LocalId == Index + 1;
}
