using PokeMmo.Core.World;

namespace PokeMmo.Server;

/// <summary>One ledge byte given one direction, and what the world looks like under it.</summary>
/// <param name="Behaviour">The byte.</param>
/// <param name="Way">The direction it is hopped, or null for leaving it a wall.</param>
public sealed record AnAssignment(byte Behaviour, Direction? Way)
{
    /// <summary>Maps the walk reached — the number the original derivation was decided on.</summary>
    public int Maps { get; init; }

    /// <summary>Squares it stood on.</summary>
    public int Stood { get; init; }

    /// <summary>How many of those can get back to where it began.</summary>
    public int GetsBack { get; init; }

    /// <summary>
    /// How many squares carrying this byte the walk ever stood orthogonally beside.
    /// </summary>
    /// <remarks>
    /// <b>The denominator the original derivation never printed.</b> A direction can only change
    /// an answer for ledge squares the walk gets next to; an assignment tested on a byte the walk
    /// never reaches produces the same number four times and reads like four measurements
    /// agreeing. That is how <c>0x38</c> came to be written down as an inference.
    /// </remarks>
    public int Beside { get; init; }

    public int Stranded => Stood - GetsBack;

    public string Name => Way?.ToString().ToLowerInvariant() ?? "a wall";

    public override string ToString() =>
        $"0x{Behaviour:X2} {Name,-6} {Maps,4} map(s), {Stood,6} square(s), {Stranded,6} stranded,"
        + $" {Beside,5} of its own squares stood beside";
}

/// <summary>
/// Which way each ledge byte is hopped, measured rather than quoted.
/// <para>
/// <b>The assignment in <see cref="MetatileBehaviour.Hops"/> is justified by seven numbers in a
/// doc comment that no instrument in this repository prints.</b> 231's rule is that a number
/// nothing computes cannot come back wrong, which is worse than a number that is stale — and
/// these seven are not decoration, they are the whole of the evidence for a rule the walk uses
/// on 1034 squares.
/// </para>
/// <para>
/// <b>And the criterion was never the one the comment names.</b> It says the right assignment is
/// the one that "leaves the cartridge's own geography CONNECTED", and what it measured is maps
/// REACHED. Those are different questions on a graph with one-way edges in it, which a ledge is
/// the definition of — and nothing could ask the second one until 265.
/// </para>
/// <para>
/// <b>Each byte on its own is also a trap.</b> The original tried one byte at a time with
/// everything else a wall. A ledge whose squares lie behind another ledge is then never reached,
/// so every direction scores the same and the byte reads as one the world does not care about.
/// <see cref="AnAssignment.Beside"/> is the number that tells those two apart.
/// </para>
/// </summary>
public static class WhichWayALedgeIsHopped
{
    /// <summary>Every direction, and leaving the byte a wall — which is the control.</summary>
    /// <remarks>
    /// The wall row is not a fifth candidate. It is what the world looks like with the byte doing
    /// nothing, so a direction that scores the same as it has been shown to change nothing rather
    /// than assumed to.
    /// </remarks>
    public static readonly IReadOnlyList<Direction?> Ways =
        [null, Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    /// <summary>Walk the world with one byte given one direction.</summary>
    /// <param name="alongside">
    /// What the OTHER ledge bytes are doing. Empty is the original derivation — each byte on its
    /// own — and passing the measured assignment for the others is the experiment that one could
    /// not run.
    /// </param>
    public static AnAssignment Try(
        WorldData world,
        string startMapId,
        byte behaviour,
        Direction? way,
        IReadOnlyDictionary<byte, Direction>? alongside = null)
    {
        var hops = new Dictionary<byte, Direction>();

        if (alongside is not null)
        {
            foreach ((byte other, Direction how) in alongside)
            {
                if (other != behaviour) hops[other] = how;
            }
        }

        if (way is { } one) hops[behaviour] = one;

        var steps = new List<AStepTaken>();

        // Through people, and only the ledges differ — the original derivation's own conditions,
        // said here rather than in a comment somewhere else, because the numbers are only
        // comparable with it if they were taken the same way.
        Reach reach = WorldWalker.Walk(
            world, startMapId, throughPeople: true, hops: hops, steps: steps);

        List<Somewhere> stood = [.. reach.Stood.Select(s => new Somewhere(s.MapId, s.Square))];

        int stranded = TheWayBack
            .Stranded(stood, steps.Select(s => (s.From, s.To)), reach.Start).Count;

        return new AnAssignment(behaviour, way)
        {
            Maps = reach.Maps.Count,
            Stood = stood.Count,
            GetsBack = stood.Count - stranded,
            Beside = StoodBeside(world, behaviour, stood),
        };
    }

    /// <summary>Every direction for one byte, the wall first.</summary>
    public static IReadOnlyList<AnAssignment> Sweep(
        WorldData world,
        string startMapId,
        byte behaviour,
        IReadOnlyDictionary<byte, Direction>? alongside = null) =>
        [.. Ways.Select(way => Try(world, startMapId, behaviour, way, alongside))];

    /// <summary>
    /// How many squares of one map carry a behaviour, and how many of those are on its edge.
    /// </summary>
    /// <remarks>
    /// <b>The second number exists because an instrument could not see it.</b> `--ledges` walks
    /// from 1 to width-1 so that every square it examines has neighbours on all four sides, which
    /// is right for the columns it prints and means its totals are of INTERIOR ledges. They have
    /// been quoted as totals — 954 for `0x3B`, where the world has 962.
    /// <para>
    /// It is eight squares and it is not nothing: a hop from a map's outer ring lands off the map,
    /// which <see cref="WorldData.HopOnto"/> refuses, so every one of them is a wall to this
    /// project. Whether the cartridge hops a player across a map join has never been asked.
    /// </para>
    /// </remarks>
    public static (int All, int OnTheRing) Census(
        IReadOnlyList<byte> behaviours, int width, int height, byte behaviour)
    {
        var all = 0;
        var ring = 0;

        for (var i = 0; i < behaviours.Count && i < width * height; i++)
        {
            if (behaviours[i] != behaviour) continue;

            all++;

            if (i % width == 0 || i % width == width - 1
                || i / width == 0 || i / width == height - 1)
            {
                ring++;
            }
        }

        return (all, ring);
    }

    /// <summary>
    /// How many distinct squares carrying a behaviour the walk stood orthogonally beside.
    /// </summary>
    /// <remarks>
    /// Beside rather than on, because nobody stands on a ledge — a walk that counted the ledge
    /// squares it stood ON would answer nought for every ledge in the game and every direction
    /// alike, which is a denominator that cannot discriminate anything.
    /// </remarks>
    public static int StoodBeside(
        WorldData world, byte behaviour, IEnumerable<Somewhere> stood)
    {
        Dictionary<string, MapData> maps = world.Maps.ToDictionary(m => m.Id);
        var beside = new HashSet<(string, GridPosition)>();

        foreach (Somewhere at in stood)
        {
            if (!maps.TryGetValue(at.MapId, out MapData? map)) continue;

            foreach (Direction way in Enum.GetValues<Direction>())
            {
                GridPosition next = at.Square.Step(way);

                if (next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height)
                    continue;

                int i = (next.Y * map.Width) + next.X;

                if (i < map.Behaviours.Length && map.Behaviours[i] == behaviour)
                    beside.Add((map.Id, next));
            }
        }

        return beside.Count;
    }
}
