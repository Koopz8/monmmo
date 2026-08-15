namespace PokeMmo.Core.World;

/// <summary>
/// How far one person can see another, and therefore who has to be told when somebody
/// moves.
/// <para>
/// Until this existed, the answer was "everybody on the map". That is right about what a
/// player should see and wrong about what it costs: four hundred people who can all see
/// each other, stepping twice a second, is a hundred and sixty thousand sightings a
/// second, and no index makes that number smaller. The arithmetic is quadratic in the
/// size of the crowd <em>within one map</em>, and the only thing that changes it is
/// telling somebody about the people near them instead of the people who share a map id.
/// </para>
/// <para>
/// The radius is not invented. It comes off the client's own viewport: the window is 960
/// by 640 at three times life size, so it shows 20 squares across and 13 down, and half
/// of the wider one is 10. What is added to that is one square of margin, so somebody
/// walking towards the edge of the screen is already known about when they arrive on it
/// rather than appearing out of nothing.
/// </para>
/// <para>
/// It is deliberately generous: seeing somebody the screen does not show costs one
/// message that changes nothing, and not seeing somebody the screen does show is a person
/// who is not there. The expensive mistake and the cheap mistake are not the same size.
/// </para>
/// </summary>
public static class Sight
{
    /// <summary>The client's window, in pixels, and how much it magnifies the world.</summary>
    private const int WindowWidth = 960;

    private const int WindowHeight = 640;

    private const int Magnification = 3;

    /// <summary>One square of margin, so nobody arrives on screen out of nothing.</summary>
    private const int Margin = 1;

    /// <summary>How many squares across the client shows.</summary>
    public static int SquaresAcross => WindowWidth / Magnification / WalkingCharacter.SquarePixels;

    /// <summary>And how many down.</summary>
    public static int SquaresDown => WindowHeight / Magnification / WalkingCharacter.SquarePixels;

    /// <summary>
    /// How far away somebody can be and still matter, in squares.
    /// <para>
    /// Half the wider side of the screen, plus the margin — one number rather than one
    /// per axis, because a circle drawn to the wider side is never too small, and a rule
    /// with two numbers in it is a rule somebody applies to the wrong axis.
    /// </para>
    /// </summary>
    public static int Squares => Math.Max(SquaresAcross, SquaresDown) / 2 + Margin;

    /// <summary>
    /// True when somebody standing at one square can see the other.
    /// <para>
    /// Chebyshev distance — the larger of the two gaps — because that is the shape of a
    /// screen. Straight-line distance would draw a circle inside a rectangular window and
    /// hide people standing in its corners.
    /// </para>
    /// </summary>
    public static bool CanSee(GridPosition from, GridPosition to) =>
        Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y)) <= Squares;
}
