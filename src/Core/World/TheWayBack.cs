namespace PokeMmo.Core.World;

/// <summary>
/// Whether the places a walk got to can get back to where the walk began.
/// <para>
/// <b>Every reach number in this project is a forward number.</b> The walk leaves the starting
/// square, follows steps, doors, map edges and ledge hops, and reports what it arrived at. That
/// is reachability in a DIRECTED graph, and this project has been reading it as though it were
/// connectedness — "the run reaches 425 maps" has always been said as though reaching and
/// returning were the same fact.
/// </para>
/// <para>
/// They are not, and the cartridge is the reason: a ledge is hopped one way and climbed none, a
/// door names a warp on the far map and nothing says that warp names one back, and nineteen exits
/// in this game name a map no bank has because the room decides at runtime where you came from.
/// Each of those is an edge with a direction, and a graph with directed edges has places you can
/// get into and not out of.
/// </para>
/// <para>
/// <b>The one thing that would make this measurement worthless is deriving the edges twice.</b>
/// A "can it get back" built from a second, separately-written idea of what a step is measures the
/// difference between two authors rather than a property of the world — 241's rule, and 261 was
/// caught by it. So the edges are the walk's OWN, recorded as it takes them, and the reverse walk
/// is a traversal of that record and of nothing else.
/// </para>
/// </summary>
public static class TheWayBack
{
    /// <summary>
    /// Everywhere that can get to <paramref name="target"/> by the steps given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target reaches itself, and that is not a convention: standing where you meant to be is
    /// the whole of what "can get back" asks. Leaving it out makes the start the one square in the
    /// world that is stranded, which is a wrong answer with a tidy explanation.
    /// </para>
    /// <para>
    /// A place with no step out of it is stranded unless it IS the target — there is nothing to
    /// decide there, and it falls out of the walk rather than being special-cased.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<T> Reaching<T>(IEnumerable<(T From, T To)> steps, T target)
        where T : notnull
    {
        var back = new Dictionary<T, List<T>>();

        foreach ((T from, T to) in steps)
        {
            if (!back.TryGetValue(to, out List<T>? earlier)) back[to] = earlier = [];

            earlier.Add(from);
        }

        HashSet<T> can = [target];
        var queue = new Queue<T>();

        queue.Enqueue(target);

        while (queue.Count > 0)
        {
            T at = queue.Dequeue();

            if (!back.TryGetValue(at, out List<T>? from)) continue;

            foreach (T one in from)
            {
                if (can.Add(one)) queue.Enqueue(one);
            }
        }

        return can;
    }

    /// <summary>
    /// The places the walk stood on that cannot get back to where it began.
    /// </summary>
    /// <remarks>
    /// Asked of the places STOOD ON rather than of the edges' own endpoints, because those are two
    /// different sets and only one of them is the answer. A step recorded into a square the walk
    /// had already seen is a real edge and its endpoint is already stood on; a walk that reported
    /// on its edges' endpoints instead would be reporting on the order it happened to visit
    /// things in.
    /// </remarks>
    public static IReadOnlyList<T> Stranded<T>(
        IEnumerable<T> stood, IEnumerable<(T From, T To)> steps, T target)
        where T : notnull
    {
        IReadOnlySet<T> can = Reaching(steps, target);

        return [.. stood.Where(s => !can.Contains(s))];
    }
}
