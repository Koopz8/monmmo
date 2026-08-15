namespace PokeMmo.Server;

/// <summary>
/// More than one copy of a place.
/// <para>
/// The last wall the crowd tool found, and the only one that is a decision rather than a
/// mistake. The cost of standing in a room is quadratic in the number of people in it:
/// every step everybody takes has to be told to everybody who can see it, and if they are
/// all in one room they can all see each other. Milestone 116 tried the cheap fix —
/// spreading arrivals over the squares around the spawn — and measured no change at all,
/// because a hundred people spread over thirteen squares are still inside one screen of
/// each other.
/// </para>
/// <para>
/// So: past a number, the next arrivals go into a second copy of the place, and the two
/// copies never see each other. That is honest in a way the alternatives are not. Capping
/// how many people are drawn, or shrinking the circle, both leave a crowd standing there
/// that the player cannot see and can walk into. A copy has no crowd in it that anybody
/// is being lied to about.
/// </para>
/// <para>
/// What it costs is written here rather than discovered later. Two people who want to be
/// together have to be put in the same copy, and this file is where that rule lives. And
/// every copy simulates that map's own people separately, so a place with twenty copies
/// is walking twenty sets of the same townsfolk.
/// </para>
/// </summary>
public sealed class Instances
{
    /// <summary>
    /// How many people share one copy of a place before the next one is opened.
    /// <para>
    /// <b>Modelled</b>, and this is the number the whole feature turns on, so here is the
    /// arithmetic behind it. Everybody in a copy who can see everybody else costs
    /// <c>n × n</c> messages for each round of steps. At 40 that is 1,600; at 400 it is
    /// 160,000, which is what the crowd tool measured falling over. Forty is also about
    /// as many people as fit on one screen at once, so a full copy looks busy rather than
    /// looking like a queue.
    /// </para>
    /// </summary>
    public const int RoomFor = 40;

    /// <summary>
    /// The key that decides who hears something: a place and which copy of it.
    /// <para>
    /// The first copy keeps the bare map id. That is not tidiness — every world file,
    /// every saved character and every test in this project names maps that way, and a
    /// scheme where the ordinary case is spelled differently is a scheme that breaks all
    /// of them for nothing.
    /// </para>
    /// </summary>
    public static string Key(string mapId, int copy) => copy == 0 ? mapId : $"{mapId}#{copy}";

    /// <summary>The place a key is a copy of, whichever copy it is.</summary>
    public static string MapOf(string key) =>
        key.IndexOf('#') is var at && at >= 0 ? key[..at] : key;

    /// <summary>Which copy a key names.</summary>
    public static int CopyOf(string key) =>
        key.IndexOf('#') is var at && at >= 0 && int.TryParse(key[(at + 1)..], out int copy) ? copy : 0;

    /// <summary>
    /// Which copy of a place somebody should be put in, given how full each one is.
    /// <para>
    /// The <b>fullest</b> copy that still has room, and ties go to the lower number. That
    /// is the opposite of the obvious answer and it is the one that empties a place out
    /// again: filling the lowest copy first leaves a long tail of copies holding two or
    /// three people each as a busy evening drains away, and every one of those is a
    /// separate set of townsfolk being walked about for nobody. Filling the fullest one
    /// packs the crowd down as it shrinks, and the empty copies close by themselves.
    /// </para>
    /// <para>
    /// It also puts arrivals where the people are, which is what somebody arriving in a
    /// town actually wants — a copy with thirty-nine people in it looks like a game with
    /// other players in it, and a fresh copy with one looks like a server nobody is on.
    /// </para>
    /// </summary>
    public static int CopyWithRoom(Func<int, int> howManyIn, int mostCopies = 64)
    {
        int best = -1;
        int fullest = -1;

        for (int copy = 0; copy < mostCopies; copy++)
        {
            int howMany = howManyIn(copy);

            if (howMany >= RoomFor) continue;

            if (howMany > fullest)
            {
                fullest = howMany;
                best = copy;
            }

            // An empty copy is as empty as it gets, and every copy past the first empty
            // one is empty too — there is nothing further along worth asking about.
            if (howMany == 0) break;
        }

        if (best >= 0) return best;

        // Every copy full is a place more popular than this server can divide. Putting
        // them in the first one is worse than refusing to let them in, which is the same
        // judgement arrivals already make about standing on somebody.
        return 0;
    }
}
