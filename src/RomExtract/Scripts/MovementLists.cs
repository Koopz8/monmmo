using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

namespace PokeMmo.RomExtract.Scripts;

/// <summary>One movement list, and who it is applied to on which map.</summary>
public sealed record MovementList(string MapId, int PersonId, uint Address, byte[] Steps)
{
    /// <summary>
    /// Where the person applied to was standing when the script started, if that is
    /// knowable.
    /// <para>
    /// For a person it is where the cartridge put them. For the player it is normally
    /// unknowable — where somebody is standing when they talk to you is not a fact about
    /// an image — with one exception worth having: a script that runs because a square
    /// was stepped onto knows exactly which square that was.
    /// </para>
    /// </summary>
    public GridPosition? Origin { get; init; }

    /// <summary>
    /// True when this is the first movement its script applies.
    /// <para>
    /// The one subset where a starting square can be trusted. Everywhere else an earlier
    /// part of the same scene has already walked somebody somewhere, so the square the
    /// cartridge records is where they were before the scene, not where they are when
    /// this list runs — which makes a path from it wrong through no fault of the reading.
    /// </para>
    /// </summary>
    public bool IsFirst { get; init; }

    /// <summary>The person id the games use to mean the player rather than an object.</summary>
    public const int Player = 0xFF;

    public bool IsPlayer => PersonId == Player;
}

/// <summary>
/// The movement lists a cutscene is made of.
/// <para>
/// <c>applymovement</c> takes a person and a pointer, and at the end of that pointer is a
/// list of one-byte steps terminated by 0xFE. That much is plain from looking: every list
/// found this way ends with the same byte, and the lists sit packed one after another
/// with nothing between them.
/// </para>
/// <para>
/// What each step byte <em>means</em> is not written down, and this is the part worth
/// being careful about. There is a strong oracle available and it is the map itself: a
/// person walking through a cutscene walks on squares a person can stand on. A direction
/// mapping that is wrong sends somebody through a wall, and sends them through a wall
/// repeatedly, across hundreds of lists on four hundred maps.
/// </para>
/// </summary>
public static class MovementLists
{
    /// <summary>Ends a list. Not assumed — see <see cref="Terminators"/> for the check.</summary>
    public const byte End = 0xFE;

    /// <summary>The command that applies one, and how its arguments are laid out.</summary>
    public const byte ApplyMovement = 0x4F;

    private const int LongestList = 64;

    /// <summary>
    /// Every movement list a script on any map applies, with who it is applied to.
    /// <para>
    /// Read rather than run, for the reason everything else in this project is: half the
    /// cutscenes in the game sit behind a flag a fresh save has not set, and a run walks
    /// past them.
    /// </para>
    /// </summary>
    public static List<MovementList> All(Rom rom, MapLibrary library)
    {
        var found = new List<MovementList>();
        var seen = new HashSet<(string, uint)>();

        foreach (LoadedMap map in library.All())
        {
            string mapId = WorldExporter.MapId(map.Bank, map.Number);

            List<(uint Script, GridPosition? From)> scripts =
            [
                .. map.Objects.Where(o => o.HasScript).Select(o => (o.ScriptAddress, (GridPosition?)null)),
                .. map.Triggers.Where(t => t.HasScript).Select(t => (t.ScriptAddress, (GridPosition?)t.Square)),
                .. map.Signs.Where(s => s.HasScript).Select(s => (s.ScriptAddress, (GridPosition?)null)),
            ];

            foreach ((uint script, GridPosition? from) in scripts)
            {
                var moved = new HashSet<int>();

                foreach (ScriptCommand command in ScriptReader.ReadAll(rom, script))
                {
                    if (command.Code != ApplyMovement) continue;

                    uint at = command.Pointer(2);
                    if (rom.ToOffsetOrNull(at) is null) continue;
                    if (!seen.Add((mapId, at))) continue;

                    if (Read(rom, at) is not { Length: > 0 } steps) continue;

                    int who = command.Word();

                    found.Add(new MovementList(mapId, who, at, steps)
                    {
                        // Only for the player, and only from a trigger. A person's own
                        // starting square is already known from the map.
                        Origin = who == MovementList.Player ? from : null,
                        IsFirst = moved.Add(who),
                    });
                }
            }
        }

        return found;
    }

    /// <summary>The steps at an address, up to the terminator.</summary>
    public static byte[] Read(Rom rom, uint address)
    {
        if (rom.ToOffsetOrNull(address) is not { } at) return [];

        var steps = new List<byte>();

        for (int i = 0; i < LongestList && at + i < rom.Length; i++)
        {
            byte step = rom.ReadU8(at + i);

            if (step == End) return [.. steps];

            steps.Add(step);
        }

        // No terminator within a sensible distance means this pointer is not a list.
        return [];
    }

    /// <summary>
    /// How often each byte turns up, commonest first. The evidence, before any hypothesis
    /// about what any of them mean.
    /// </summary>
    public static List<(byte Step, int Count)> Histogram(IEnumerable<MovementList> lists) =>
    [
        .. lists
            .SelectMany(l => l.Steps)
            .GroupBy(s => s)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(g => g.Item2),
    ];

    /// <summary>One reading of a family of four step bytes, and how well the maps agree.</summary>
    public sealed record Reading(
        byte First, IReadOnlyList<Direction> Directions, int Walked, int Paths, int Blocked)
    {
        public double Share => Paths == 0 ? 0 : (double)Walked / Paths;

        public override string ToString() =>
            $"0x{First:X2}..0x{First + 3:X2} = " +
            string.Join(", ", Directions.Select(d => d.ToString().ToLowerInvariant())) +
            $"   {Walked,4}/{Paths,-4} {Share,6:P0}  ({Blocked} walked into a wall)";
    }

    /// <summary>The four directions, as something to permute — not an ordering.</summary>
    private static readonly Direction[] AllFour =
        [Direction.Down, Direction.Up, Direction.Left, Direction.Right];

    /// <summary>
    /// Scores every reading of one family of four step bytes against every map.
    /// <para>
    /// The oracle is the map. A cutscene walks people over squares people can stand on,
    /// so the reading that is right leaves nobody inside a wall, and a reading that is
    /// wrong puts somebody inside one on nearly every list it touches. Twenty-four
    /// orderings, hundreds of paths, four hundred maps: a coincidence would have to hold
    /// an implausible number of times.
    /// </para>
    /// <para>
    /// Only lists made entirely of the family are scored. A list containing a byte this
    /// does not model would be judged on a path with a hole in it, which is the kind of
    /// silently-partial evidence this project has been caught by before.
    /// </para>
    /// </summary>
    public static List<Reading> Derive(
        IEnumerable<MovementList> lists, IReadOnlyDictionary<string, LoadedMap> maps, byte first)
    {
        byte[] family = [first, (byte)(first + 1), (byte)(first + 2), (byte)(first + 3)];

        List<MovementList> usable =
        [
            .. lists.Where(l =>
                !l.IsPlayer &&
                l.Steps.Length > 0 &&
                l.Steps.All(family.Contains) &&
                maps.ContainsKey(l.MapId) &&
                maps[l.MapId].Objects.Any(o => o.LocalId == l.PersonId)),
        ];

        var scored = new List<Reading>();

        foreach (Direction[] order in Orderings())
        {
            int walked = 0;
            int blocked = 0;

            foreach (MovementList list in usable)
            {
                LoadedMap map = maps[list.MapId];
                GridPosition square = map.Objects.First(o => o.LocalId == list.PersonId).Square;

                bool clear = true;

                foreach (byte step in list.Steps)
                {
                    square = square.Step(order[Array.IndexOf(family, step)]);

                    if (map.Collision.IsWalkable(square)) continue;

                    clear = false;
                    break;
                }

                if (clear) walked++;
                else blocked++;
            }

            scored.Add(new Reading(first, order, walked, usable.Count, blocked));
        }

        return [.. scored.OrderByDescending(s => s.Share)];
    }

    /// <summary>
    /// The same question asked of several families at once, which is a far bigger sample.
    /// <para>
    /// One family alone gives sixty-one usable paths and an eighty-two per cent against a
    /// seventy-nine, which is not a decision. The families almost certainly share an
    /// ordering — they read like one enumeration written out at three speeds — so scoring
    /// them together tests one hypothesis against every list in the game rather than
    /// against the few that happen to use one speed throughout.
    /// </para>
    /// <para>
    /// Anything outside the families counts as standing still. That is the assumption
    /// worth naming: if some byte this does not model is also a step, its paths acquire a
    /// hole and score as blocked, which drags every ordering down equally and blunts the
    /// contrast rather than inventing one.
    /// </para>
    /// </summary>
    public static List<Reading> DeriveJoint(
        IEnumerable<MovementList> lists, IReadOnlyDictionary<string, LoadedMap> maps, byte[] families)
    {
        List<MovementList> usable =
        [
            .. lists.Where(l =>
                !l.IsPlayer &&
                l.IsFirst &&
                l.Steps.Length > 0 &&
                maps.ContainsKey(l.MapId) &&
                maps[l.MapId].Objects.Any(o => o.LocalId == l.PersonId) &&
                l.Steps.Any(s => families.Any(f => s >= f && s <= f + 3))),
        ];

        var scored = new List<Reading>();

        foreach (Direction[] order in Orderings())
        {
            int walked = 0;
            int blocked = 0;

            foreach (MovementList list in usable)
            {
                LoadedMap map = maps[list.MapId];
                GridPosition square = map.Objects.First(o => o.LocalId == list.PersonId).Square;

                bool clear = true;

                foreach (byte step in list.Steps)
                {
                    byte family = families.FirstOrDefault(f => step >= f && step <= f + 3, (byte)0xFF);

                    if (family == 0xFF) continue;

                    square = square.Step(order[step - family]);

                    if (map.Collision.IsWalkable(square)) continue;

                    clear = false;
                    break;
                }

                if (clear) walked++;
                else blocked++;
            }

            scored.Add(new Reading(families[0], order, walked, usable.Count, blocked));
        }

        return [.. scored.OrderByDescending(s => s.Share)];
    }

    /// <summary>
    /// The same question again, on the one sample where the player's own footing is known.
    /// <para>
    /// A script that runs because a square was stepped onto knows exactly where the player
    /// is: on that square. That makes the ninety-nine lists applied to the player usable
    /// as evidence rather than merely interesting, and it is an entirely separate sample
    /// from the people — different scripts, different maps, different squares.
    /// </para>
    /// </summary>
    public static List<Reading> DeriveFromTriggers(
        IEnumerable<MovementList> lists, IReadOnlyDictionary<string, LoadedMap> maps, byte[] families)
    {
        List<MovementList> usable =
        [
            .. lists.Where(l =>
                l.Origin is not null &&
                l.IsFirst &&
                maps.ContainsKey(l.MapId) &&
                l.Steps.Any(s => families.Any(f => s >= f && s <= f + 3))),
        ];

        var scored = new List<Reading>();

        foreach (Direction[] order in Orderings())
        {
            int walked = 0;

            foreach (MovementList list in usable)
            {
                LoadedMap map = maps[list.MapId];
                GridPosition square = list.Origin!.Value;

                bool clear = true;

                foreach (byte step in list.Steps)
                {
                    byte family = families.FirstOrDefault(f => step >= f && step <= f + 3, (byte)0xFF);

                    if (family == 0xFF) continue;

                    square = square.Step(order[step - family]);

                    if (map.Collision.IsWalkable(square)) continue;

                    clear = false;
                    break;
                }

                if (clear) walked++;
            }

            scored.Add(new Reading(families[0], order, walked, usable.Count, usable.Count - walked));
        }

        return [.. scored.OrderByDescending(s => s.Share)];
    }

    /// <summary>Every ordering of the four directions. Twenty-four of them.</summary>
    private static IEnumerable<Direction[]> Orderings()
    {
        foreach (Direction a in AllFour)
        foreach (Direction b in AllFour.Where(d => d != a))
        foreach (Direction c in AllFour.Where(d => d != a && d != b))
        {
            Direction d = AllFour.Single(x => x != a && x != b && x != c);

            yield return [a, b, c, d];
        }
    }

    /// <summary>
    /// Whether 0xFE really ends these lists, asked of the bytes rather than assumed.
    /// <para>
    /// Not circular, because it does not ask where a terminator is — it asks how many
    /// <c>applymovement</c> pointers have a given byte anywhere within reach. A byte that
    /// ends every list turns up near every one of these pointers; a byte that does not,
    /// does not. Run over all 256 values, either one stands out or there is no answer.
    /// </para>
    /// </summary>
    public static List<(byte Byte, int Within)> Terminators(Rom rom, IEnumerable<uint> pointers)
    {
        var within = new int[256];
        var seen = new HashSet<uint>();

        foreach (uint pointer in pointers)
        {
            if (!seen.Add(pointer)) continue;
            if (rom.ToOffsetOrNull(pointer) is not { } at) continue;

            var found = new bool[256];

            for (int i = 0; i < LongestList && at + i < rom.Length; i++)
                found[rom.ReadU8(at + i)] = true;

            for (int value = 0; value < 256; value++)
                if (found[value]) within[value]++;
        }

        return
        [
            .. Enumerable.Range(0, 256)
                .Select(value => ((byte)value, within[value]))
                .OrderByDescending(e => e.Item2),
        ];
    }

    /// <summary>
    /// What each step byte does, derived rather than known.
    /// <para>
    /// Three families — 0x08, 0x10 and 0x1C — read as one ordering at three speeds, and
    /// the ordering came from walking every list across every map and counting who ended
    /// up inside a wall. Two samples that share no scripts, no maps and no starting
    /// squares put the same ordering first: 84% of people's own paths and 95% of the
    /// player's, against 24 orderings.
    /// </para>
    /// <para>
    /// Anything outside these families is a step this project does not model — a jump, a
    /// pause, a change of face — and is treated as standing still. That is the honest
    /// reading: doing nothing is wrong in a way you can see, and guessing is wrong in a
    /// way that walks somebody through a wall.
    /// </para>
    /// <para>
    /// Lives here rather than in the client because the client is not the only thing that
    /// walks a list. The tool that asks what an unmodelled step means has to walk one too,
    /// and two copies of this table would let the answer be derived against an ordering
    /// the game does not use.
    /// </para>
    /// </summary>
    public static Direction? DirectionOf(byte step)
    {
        foreach (byte family in Families)
        {
            if (step >= family && step <= family + 3) return Compass[step - family];
        }

        return null;
    }

    private static readonly byte[] Families = [0x08, 0x10, 0x1C];

    private static readonly Direction[] Compass =
        [Direction.Down, Direction.Up, Direction.Left, Direction.Right];

    /// <summary>One appearance of a step byte, with what the map says about where it happens.</summary>
    public sealed record Appearance(
        string MapId, int PersonId, uint Address, int Position, int Length, GridPosition? Square, bool OnWarp)
    {
        public bool IsLast => Position == Length - 1;
    }

    /// <summary>
    /// Every place one step byte turns up, and whether the walker is standing on a door
    /// when it does.
    /// <para>
    /// The question this exists to answer is what an unmodelled step does, and the map is
    /// the oracle again. A step whose meaning is "go through this door" can only ever be
    /// written where there is a door: walk the list up to it with the four directions
    /// already derived, and read what the square underneath turns out to be. One list
    /// doing that is a coincidence; every list doing it is what the byte means.
    /// </para>
    /// <para>
    /// Only lists whose starting square is trustworthy are walked — a person's own square
    /// from the map, and only when nothing in the same script has moved them yet. The
    /// rest are counted but not placed, because a path from a wrong start lands wherever
    /// it likes and would answer the question with noise.
    /// </para>
    /// </summary>
    public static List<Appearance> Where(
        Rom rom, MapLibrary library, byte step, Func<byte, Direction?> directionOf)
    {
        var found = new List<Appearance>();

        foreach (MovementList list in All(rom, library))
        {
            if (!list.Steps.Contains(step)) continue;

            LoadedMap? map = library.TryLoad(list.MapId);

            // Where this walker starts. A person's square is on the map; the player's is
            // only knowable when a trigger square started the script.
            GridPosition? at = list.IsPlayer
                ? list.Origin
                : map?.Objects.FirstOrDefault(o => o.LocalId == list.PersonId)?.Square;

            if (!list.IsFirst) at = null;

            for (int i = 0; i < list.Steps.Length; i++)
            {
                if (list.Steps[i] == step)
                {
                    found.Add(new Appearance(
                        list.MapId, list.PersonId, list.Address, i, list.Steps.Length, at,
                        at is { } here && map is not null && map.Warps.Any(w => w.Square == here)));
                }

                if (at is { } square && directionOf(list.Steps[i]) is { } direction)
                    at = square.Step(direction);
            }
        }

        return found;
    }
}
