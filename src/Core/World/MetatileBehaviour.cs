namespace PokeMmo.Core.World;

/// <summary>
/// What a map square <em>is</em>, as opposed to what it looks like.
/// <para>
/// These values were confirmed by drawing them rather than by assumption. Reading
/// Route 1's attributes at four bytes per metatile and plotting value 0x02 gives 178
/// squares in solid rectangular patches — the route's grass. At two bytes it gives 52
/// squares scattered down the map's left and right edges on alternating rows, which is
/// aliasing rather than terrain. The shape is what distinguishes them; both readings
/// produce plausible-looking counts.
/// </para>
/// <para>
/// Ledges were confirmed the same way: 0x3B appears on 61 squares of Route 1, and
/// Cycling Road — which has no grass at all — is full of 0x38 and 0x39.
/// </para>
/// <para>
/// Only the values that have been checked against a real cartridge are named here.
/// Guessing at the rest is how several earlier bugs happened, and an unnamed value is
/// honest about what is known.
/// </para>
/// </summary>
public static class MetatileBehaviour
{
    /// <summary>Ordinary ground. The overwhelming majority of any map.</summary>
    public const byte Normal = 0x00;

    /// <summary>Tall grass — where land encounters happen. Confirmed on Route 1.</summary>
    public const byte TallGrass = 0x02;

    /// <summary>Long grass. Adjacent to tall grass in the numbering and also an encounter square.</summary>
    public const byte LongGrass = 0x03;

    /// <summary>
    /// Ledges, which can be hopped in one direction only. Confirmed on Cycling Road.
    /// <para>
    /// Named for the way they are hopped, which is not the way another game's table
    /// names them: what was called LedgeSouth here for four milestones is hopped west,
    /// and the one called LedgeEast is the south one — 954 of the world's 1034 ledge
    /// squares. The evidence is on <see cref="Hops"/>. 0x3A is in the run and on no
    /// square of this cartridge, so it has a name and nothing else.
    /// </para>
    /// </summary>
    public const byte HopWest = 0x38;

    public const byte HopEast = 0x39;

    public const byte HopUnused = 0x3A;

    public const byte HopSouth = 0x3B;

    /// <summary>
    /// Which way a ledge is hopped, or nothing for a square that is not one.
    /// <para>
    /// The four names above came from another game's table and only one of the four
    /// facts in them survived contact with this cartridge. 0x3A does not appear on a
    /// single square of the world; 0x3B is on 954 of the 1034 that exist.
    /// </para>
    /// <para>
    /// The axis is not a guess. A ledge is the edge of a step in the ground, so it runs
    /// along the direction it is <em>not</em> hopped along, and the runs are unambiguous:
    /// 950 of 0x3B's 954 squares sit in an east–west run and none in a north–south one,
    /// while every one of 0x38's and 0x39's sit in north–south runs and none in
    /// east–west ones. So 0x3B is hopped north or south, and the other two east or west.
    /// </para>
    /// <para>
    /// Which of the two, on each axis, is not written in the block data anywhere this
    /// project could find — the elevation nibble is zero on every ledge square, which is
    /// the value meaning "whatever is around it", so it cannot say which side is the step
    /// up. What decides it is the world itself: a hop is one-way, and the assignment that
    /// is right is the one that leaves the cartridge's own geography connected. That is
    /// measured rather than argued, by walking the world under each assignment and
    /// counting the maps a player can reach.
    /// </para>
    /// </summary>
    public static Direction? Hop(byte behaviour) =>
        Hops.TryGetValue(behaviour, out Direction way) ? way : null;

    /// <summary>
    /// The measured assignment. Kept as a table rather than a switch so the walk can be
    /// re-run against a different one without editing the rule it is testing.
    /// <para>
    /// Each byte tried on its own, everything else left a wall, walking through people so
    /// that only the ledges differ:
    /// </para>
    /// <code>
    ///   0x3B south   211 maps        0x39 east    36 maps, CERULEAN
    ///   0x3B north    38 maps        0x39 west    34 maps
    ///   0x3B east     34 maps        0x38 any     34 maps
    ///   0x3B west     34 maps
    /// </code>
    /// <para>
    /// 0x3B south is not a close call: it is the difference between a game 34 maps large
    /// and one with most of KANTO in it. 0x39 east is smaller and just as clear — it is
    /// the only direction that changes anything at all, and what it changes is the way
    /// out of ROUTE 4, which is the road to CERULEAN.
    /// </para>
    /// <para>
    /// 0x38 is not decided by this and is written down as an inference rather than a
    /// measurement: no direction changes the reach by a single map, because its 39
    /// squares are all on optional ground. It is given west because it is the other
    /// east–west byte and 0x39 is east — which is an argument from the shape of the
    /// table, not evidence, and is worth remembering if something on CYCLING ROAD ever
    /// reads as a wall it should not be.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<byte, Direction> Hops =
        new Dictionary<byte, Direction>
        {
            [HopWest] = Direction.Left,
            [HopEast] = Direction.Right,
            [HopSouth] = Direction.Down,
        };

    /// <summary>
    /// Water. Confirmed by laying the behaviour bytes against a structure that has
    /// nothing to do with them.
    /// <para>
    /// Fifty of this cartridge's 425 maps carry a water encounter table, which is a fact
    /// held in the encounter records rather than in the block data. 0x15 is on 42 of
    /// those fifty and on 26 of the other 375; it covers 86% of ROUTE 20, 91% of the
    /// S.S. ANNE's harbour and 0% of VIRIDIAN FOREST and both floors of MT. MOON. Drawn
    /// on ROUTE 24 it is the river, three squares wide, running the length of the map.
    /// </para>
    /// <para>
    /// 0x10 is the second one, and it was found by refusing to stop at the first. Eight
    /// maps have a water encounter table and no 0x15 at all — VIRIDIAN, CELADON and
    /// FUCHSIA among them — and 0x10 is on all eight of them and on nought of the 375
    /// dry maps. Drawn on VIRIDIAN CITY it is a six-by-five rectangle exactly where the
    /// pond is. Naming one byte and leaving eight maps of water unaccounted for is the
    /// kind of tidy conclusion this project is arranged against.
    /// </para>
    /// <para>
    /// 0x21 was the next-best separator and is not water: drawn on KINDLE ROAD it is the
    /// beach, lying between the sea and the cliff.
    /// </para>
    /// </summary>
    public const byte Water = 0x15;

    /// <summary>The other one. Ponds rather than sea, on the eight maps that have no 0x15.</summary>
    public const byte PondWater = 0x10;

    /// <summary>True when this square is water — sea or pond.</summary>
    public static bool IsWater(byte behaviour) => behaviour is Water or PondWater;

    /// <summary>True when standing here can start a land encounter.</summary>
    public static bool IsEncounterGrass(byte behaviour) => behaviour is TallGrass or LongGrass;

    /// <summary>
    /// The storage machine in the corner of every Pokémon Center.
    /// <para>
    /// Found the way the water was: by laying the behaviour bytes against a structure
    /// that has nothing to do with them. Twenty of this cartridge's maps have somebody
    /// on them who heals a party, which is a fact held in a script the healer locator
    /// already found. 0x6A is on nineteen of those twenty and on <b>nought of the other
    /// four hundred and five</b>, one square each and never more.
    /// </para>
    /// <para>
    /// And it is in the same place every time. Eighteen of the nineteen rooms are
    /// fifteen by ten with the square at (1, 6) and the healer at (7, 2) — the far
    /// corner from the counter, ten steps away. That rules out the two things it might
    /// otherwise have been: the counter itself would be next to the healer, and the
    /// stairs to the club are in the opposite corner.
    /// </para>
    /// <para>
    /// The twentieth healing map is TRAINER TOWER, which has somebody who heals and no
    /// machine. That is a fact about the tower rather than a hole in the reading.
    /// </para>
    /// <para>
    /// The one in the player's bedroom is <em>not</em> this byte, and this project has
    /// not worked out which one it is — the rare bytes on that floor turn up in the
    /// ROCKET HIDEOUT and the POWER PLANT, which is what scenery does and not what a
    /// storage machine does. So the box lives in the Pokémon Centers and nowhere else,
    /// which is stated rather than quietly worked around.
    /// </para>
    /// </summary>
    public const byte Computer = 0x6A;

    /// <summary>True when there is a storage machine on this square.</summary>
    public static bool IsComputer(byte behaviour) => behaviour == Computer;

    /// <summary>True when this square is a ledge of any direction.</summary>
    public static bool IsLedge(byte behaviour) => behaviour is >= HopWest and <= HopSouth;
}
