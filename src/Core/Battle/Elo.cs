namespace PokeMmo.Core.Battle;

/// <summary>
/// What a win is worth, by the oldest rating system there is.
/// <para>
/// Elo, and picked for one reason rather than because it is familiar: it is the only rating
/// system where somebody can be told <em>why</em> their number moved by the amount it did.
/// Two ratings go in, one probability comes out, and the change is the difference between
/// what happened and what was expected. A player who wants to check it can, with a
/// calculator.
/// </para>
/// <para>
/// Everything here is <b>modelled</b> and there is nothing on any cartridge that could
/// inform it — this is a rule about a competition rather than about a game, so it is stated
/// rather than derived. What it does share with the rest of this project is that the
/// arithmetic is published, testable, and small enough to read.
/// </para>
/// </summary>
public static class Elo
{
    /// <summary>
    /// What everybody starts on.
    /// <para>
    /// A round number in the middle of nowhere. Its only job is to be the same for everybody
    /// — a rating is a comparison and the origin cancels out of every one.
    /// </para>
    /// </summary>
    public const int Starting = 1_000;

    /// <summary>
    /// How much a single result may move a rating, at most.
    /// <para>
    /// Thirty-two, which is chess's own figure for ordinary play and is chosen for the
    /// reason it is chosen there: small enough that one lucky afternoon does not make
    /// somebody a champion, large enough that a new player reaches roughly the right number
    /// within an evening rather than a month.
    /// </para>
    /// </summary>
    public const int Swing = 32;

    /// <summary>
    /// The rating difference at which somebody is expected to win about ten times in eleven.
    /// <para>
    /// Four hundred, and it is the scale of the whole system rather than a tuning knob:
    /// every other number here is a ratio against it.
    /// </para>
    /// </summary>
    public const int Scale = 400;

    /// <summary>
    /// How often the first of these two is expected to win, from nought to one.
    /// </summary>
    public static double Expected(int rating, int opponent) =>
        1.0 / (1.0 + Math.Pow(10, (opponent - rating) / (double)Scale));

    /// <summary>
    /// What a rating becomes after a result.
    /// <para>
    /// Rounded away from nought so that a win never moves somebody by nothing. A heavily
    /// favoured player beating somebody far below them is owed a fraction of a point, and a
    /// system that rounded that to zero would be one where the top of the ladder stops
    /// moving — which looks like the ladder being broken rather than like the maths.
    /// </para>
    /// </summary>
    public static int After(int rating, int opponent, bool won, int swing = Swing)
    {
        double moved = swing * ((won ? 1.0 : 0.0) - Expected(rating, opponent));

        int change = (int)(moved > 0 ? Math.Ceiling(moved) : Math.Floor(moved));

        // Nobody goes below nought. A negative rating says nothing a rating of nought does
        // not, and it reads as a bug to whoever has one.
        return Math.Max(0, rating + change);
    }
}
