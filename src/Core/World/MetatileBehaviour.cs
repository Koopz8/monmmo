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

    /// <summary>
    /// A shop counter: solid, and talked across.
    /// <para>
    /// Read two independent ways rather than taken from another game's table, because that
    /// table has already been wrong here once — three of the four ledge names it supplied did
    /// not survive contact with this cartridge.
    /// </para>
    /// <para>
    /// <b>By what it stands beside.</b> Of the 37 unwalkable squares orthogonally beside a
    /// shopkeeper, 34 carry this value — 91.9%. Of the 1923 unwalkable squares beside ANY of
    /// the file's people, 171 do — 8.9%. Ten-fold, and the control is the point: a wall stands
    /// beside everybody.
    /// </para>
    /// <para>
    /// <b>By its shape.</b> A counter is a square with somebody on one side and floor a player
    /// can stand on directly opposite. Of the 728 squares in the world carrying this value,
    /// 164 have that shape — 22.5%. Of the 92566 unwalkable squares carrying 0x00, 278 do —
    /// 0.3%. Seventy-five-fold.
    /// </para>
    /// <para>
    /// 22.5% rather than most, and that is the shape too: a counter is a RUN of squares and
    /// only the one the clerk stands behind has anybody behind it. The other three or four
    /// tiles of the same counter have wall behind them and are still counter.
    /// </para>
    /// <para>
    /// What it costs: the playthrough stood in front of at most one shop counter in the entire
    /// game before this value had a name, because it required orthogonal adjacency to talk to
    /// anybody, and every clerk in this game stands behind one of these.
    /// </para>
    /// </summary>
    public const byte Counter = 0x80;

    /// <summary>
    /// The board a sign is written on (281).
    /// <para>
    /// <b>Named from the cartridge's own two directions, and the second is the one that names
    /// it.</b> That 179 sign records stand on this byte says nothing on its own — it might be
    /// every wall in the game. What says something is the other way round: there are <b>189
    /// squares</b> of it in the world and <b>179 of them hold a sign</b>, which is 94.7% against a
    /// whole-game base rate of 0.300% — three hundred and sixteen-fold.
    /// </para>
    /// <para>
    /// And it belongs to ONE kind of sign: all 179 are kind <c>0x00</c>, the kind whose record
    /// names no side (279). Not one of the 97 that name a side stands on it, and neither does any
    /// of the 183 buried ones.
    /// </para>
    /// <para>
    /// The ten squares with nothing on them are nine on <c>3.11</c> and one on <c>10.19</c>. Nine
    /// of ten on one map is either a decoration or nine records that were removed, and there is
    /// nothing here to tell those apart.
    /// </para>
    /// <para>
    /// <b>Nothing reads this yet.</b> It is named because the evidence is in hand and because the
    /// next milestone that wants it should not have to derive it again — not because a reading
    /// depends on it.
    /// </para>
    /// </summary>
    public const byte SignBoard = 0x84;

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
    /// single square of the world; 0x3B is on 962 of the 1042 that exist.
    /// </para>
    /// <para>
    /// <b>962 and 1042, corrected at 266.</b> This said 954 of 1034 for seventy milestones, which
    /// is `--ledges`' count — and `--ledges` loops from 1 to width-1 so every square it looks at
    /// has four neighbours, so what it counts is the INTERIOR. Eight of 0x3B's squares sit on a
    /// map's outer ring. A hop from there lands off the map and <c>WorldData.HopOnto</c> refuses
    /// it, so all eight are walls to this project; whether the cartridge hops a player across a
    /// map join has never been asked. Both numbers are in `--ledges`' own output now.
    /// </para>
    /// <para>
    /// The axis is not a guess. A ledge is the edge of a step in the ground, so it runs
    /// along the direction it is <em>not</em> hopped along, and the runs are unambiguous:
    /// 950 of 0x3B's 954 interior squares sit in an east–west run and none in a north–south one,
    /// while 38 of 0x38's 39 and all 41 of 0x39's sit in north–south runs and none in
    /// east–west ones. So 0x3B is hopped north or south, and the other two east or west.
    /// </para>
    /// <para>
    /// Which of the two, on each axis, is not written in the block data anywhere this
    /// project could find — the elevation nibble is zero on every ledge square, which is
    /// the value meaning "whatever is around it", so it cannot say which side is the step
    /// up. What decides it is the world itself: a hop is one-way, and the assignment that
    /// is right is the one that opens the most of the cartridge's own geography. That is
    /// measured rather than argued, by walking the world under each assignment and
    /// counting the maps a player can reach — which is `--which-way` (266).
    /// </para>
    /// <para>
    /// <b>"Leaves the geography CONNECTED" is what this used to say, and it is not what was
    /// measured.</b> Reach and connectedness are different questions on a graph with one-way
    /// edges, which a ledge is the definition of, and nothing could ask the second until 265.
    /// Asked: 0x3B south reaches 211 maps and strands <b>35328 of the 46433 squares it stands
    /// on</b>, while 0x3B north reaches 38 and strands 247. **By connectedness the chosen answer
    /// is the worst of the four.** The criterion that decides this is REACH and the sentence says
    /// so now.
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
    /// <b>0x38 was an inference and is now MEASURED (266).</b> Each byte on its own could never
    /// have decided it: with everything else a wall the walk stands beside <b>9 of its 39
    /// squares</b>, so all five rows come out identical and read like four directions agreeing.
    /// The reason written down here — "its 39 squares are all on optional ground" — was a guess
    /// at why, and the real reason is that the walk never got there.
    /// </para>
    /// <para>
    /// Run with the other two at their measured values, which is the experiment one-byte-at-a-time
    /// cannot do, the walk stands beside 24 of the 39 and <b>west is the only direction that
    /// changes anything</b>: 46790 squares against 46568 for the wall and for all three other
    /// directions, at 212 maps either way. So the inference was right, it is not an inference any
    /// more, and it was decided by squares rather than by maps.
    /// </para>
    /// <para>
    /// The seven numbers above are `--which-way`'s, reproduced to the digit at 266 after seventy
    /// milestones in which nothing in this repository printed one of them.
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
    /// The stairs, up and down.
    /// <para>
    /// 0x6A was taken for a storage machine for thirteen milestones. It was found the way
    /// the water was — by laying the behaviour bytes against a structure that has nothing
    /// to do with them — and it passed: twenty of this cartridge's maps have somebody on
    /// them who heals a party, 0x6A is on nineteen of those twenty and on nought of the
    /// other four hundred and five, one square each and never more, in the same corner
    /// every time. Every word of that is still true.
    /// </para>
    /// <para>
    /// It is also true of the staircase, because a healing centre is the only kind of room
    /// in this game with an upstairs. The test could not tell the two apart, and the note
    /// that thought it had — <em>the stairs to the club are in the opposite corner</em> —
    /// was an assumption, checked against nothing.
    /// </para>
    /// <para>
    /// What settles it is a question the first test never asked. <b>All nineteen squares
    /// carrying 0x6A are warps, and all nineteen land on a square carrying 0x6B.</b> That
    /// is not a machine. That is a staircase and the staircase at the other end of it, and
    /// the only reason nobody noticed is that walking onto one worked perfectly while
    /// facing one opened a box.
    /// </para>
    /// </summary>
    public const byte StairsUp = 0x6A;

    /// <summary>The other end of one, and never on the same map as its partner.</summary>
    public const byte StairsDown = 0x6B;

    /// <summary>True when this square is one end of a staircase.</summary>
    public static bool IsStairs(byte behaviour) => behaviour is StairsUp or StairsDown;

    /// <summary>True when this square is a ledge of any direction.</summary>
    public static bool IsLedge(byte behaviour) => behaviour is >= HopWest and <= HopSouth;
}
