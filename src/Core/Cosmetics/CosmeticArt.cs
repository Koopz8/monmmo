namespace PokeMmo.Core.Cosmetics;

/// <summary>
/// Which way a figure is facing, for the art. Left and right are one drawing seen from
/// two sides, exactly as the cartridge's own walking frames are.
/// </summary>
public enum Aspect
{
    Front,
    Back,
    Side,
}

/// <summary>
/// One coloured rectangle of a garment, in the sixteen-by-thirty-two box a walking
/// figure is drawn in.
/// <para>
/// The box is the unit rather than pixels on a screen, because the figure comes off the
/// player's own cartridge and this code has never seen it. Everything here is a fraction
/// of a sprite; the client multiplies by whatever the sprite it loaded actually measures,
/// so art drawn against a sixteen-wide figure still lands on a thirty-two-wide one.
/// </para>
/// </summary>
/// <param name="X">Left edge, in sixteenths of the width.</param>
/// <param name="Y">Top edge, in thirty-secondths of the height.</param>
public readonly record struct Patch(int X, int Y, int Width, int Height, byte R, byte G, byte B)
{
    /// <summary>The width of the box these coordinates are in.</summary>
    public const int BoxWidth = 16;

    /// <summary>And its height.</summary>
    public const int BoxHeight = 32;

    /// <summary>True when this rectangle is inside the box it claims to be in.</summary>
    public bool IsInsideTheBox =>
        X >= 0 && Y >= 0 && Width > 0 && Height > 0 && X + Width <= BoxWidth && Y + Height <= BoxHeight;
}

/// <summary>
/// What each thing in the wardrobe looks like.
/// <para>
/// <b>Every number in this file is invented</b>, which is the whole reason it is in this
/// namespace and not next to the drawing code that uses it. A rectangle here is somebody
/// deciding what a hat looks like; a rectangle anywhere else in this project would be a
/// constant somebody failed to derive. Keeping the art on this side of that line is what
/// lets the rule stay checkable by grep rather than by memory — and it is the commercial
/// line too, because what can be sold is art this project owns.
/// </para>
/// <para>
/// It replaces the row of coloured marks that floated above everybody's head. Those were
/// honest about being a placeholder and they proved the hard half: that a hat gets from an
/// account, past a server that decides whether it is owned, onto a wire and into every
/// other client on the map. What they could not do is look like anything. These are still
/// simple shapes — a cap is four rectangles — but they are on the figure, they face the
/// way it faces, and a scarf drawn on somebody walking away from you is behind their neck.
/// </para>
/// </summary>
public static class CosmeticArt
{
    private static readonly Patch[] None = [];

    /// <summary>
    /// The shapes for one thing, from one side.
    /// <para>
    /// Side art is drawn as though facing left, and the client mirrors it for right in the
    /// same call it already mirrors the cartridge's own frames with — the hardware does it
    /// for nothing, and art that had to be drawn twice would be drawn differently twice.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Patch> For(int cosmeticId, Aspect aspect) => cosmeticId switch
    {
        // ---- hair, which is the layer everything else sits on ------------------------
        101 => Hair(aspect, 40, 32, 28),
        102 => Hair(aspect, 120, 72, 40),
        103 => Hair(aspect, 170, 70, 40),
        104 => Hair(aspect, 226, 196, 110),

        // ---- eyes, and only from the front ------------------------------------------
        201 => Eyes(aspect, 90, 60, 40),
        202 => Eyes(aspect, 70, 130, 210),
        203 => Eyes(aspect, 70, 160, 90),

        // ---- hats ---------------------------------------------------------------------
        301 => Cap(aspect, 210, 60, 55),
        302 => Straw(aspect),

        // ---- glasses, which are a line across the eyes -------------------------------
        401 => Glasses(aspect, 235, 235, 240),
        402 => Glasses(aspect, 40, 40, 45),

        // ---- worn on the body ---------------------------------------------------------
        501 => Scarf(aspect),
        601 => Torso(aspect, 90, 190, 120),
        602 => Striped(aspect),
        701 => Legs(aspect, 60, 80, 190),
        702 => Shorts(aspect, 220, 200, 90),
        801 => Skirt(aspect, 200, 90, 190),
        901 => Dress(aspect, 235, 130, 200),
        1001 => Shoes(aspect, 220, 220, 225),
        1002 => Shoes(aspect, 90, 60, 40),

        // ---- and the two that hang off the back --------------------------------------
        1101 => Cape(aspect, 150, 60, 200),
        1201 => Backpack(aspect, 150, 110, 60),

        _ => None,
    };

    /// <summary>
    /// True when this thing is drawn behind the figure rather than over it.
    /// <para>
    /// A cape and a backpack are the whole of it, and only from the front and the side:
    /// somebody walking away from you has their back to you, and their backpack is the
    /// nearest thing to you on the map.
    /// </para>
    /// </summary>
    public static bool GoesBehind(int cosmeticId, Aspect aspect) =>
        cosmeticId is 1101 or 1201 && aspect != Aspect.Back;

    // ---- the drawings ------------------------------------------------------------------

    private static Patch[] Hair(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Front => [new(4, 4, 8, 3, r, g, b), new(3, 6, 2, 3, r, g, b), new(11, 6, 2, 3, r, g, b)],
        Aspect.Back => [new(3, 4, 10, 6, r, g, b)],
        _ => [new(4, 4, 8, 3, r, g, b), new(4, 6, 3, 4, r, g, b)],
    };

    private static Patch[] Eyes(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Front => [new(5, 8, 2, 2, r, g, b), new(9, 8, 2, 2, r, g, b)],
        Aspect.Side => [new(6, 8, 2, 2, r, g, b)],
        _ => None,
    };

    private static Patch[] Cap(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Front => [new(3, 3, 10, 3, r, g, b), new(3, 6, 10, 1, r, g, b)],
        Aspect.Back => [new(3, 3, 10, 4, r, g, b)],
        _ => [new(3, 3, 10, 3, r, g, b), new(1, 6, 7, 1, r, g, b)],
    };

    private static Patch[] Straw(Aspect aspect) => aspect switch
    {
        Aspect.Front => [new(4, 3, 8, 3, 226, 196, 110), new(2, 6, 12, 1, 226, 196, 110)],
        Aspect.Back => [new(4, 3, 8, 3, 226, 196, 110), new(2, 6, 12, 1, 226, 196, 110)],
        _ => [new(4, 3, 8, 3, 226, 196, 110), new(1, 6, 13, 1, 226, 196, 110)],
    };

    private static Patch[] Glasses(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Front => [new(4, 8, 3, 2, r, g, b), new(9, 8, 3, 2, r, g, b), new(7, 9, 2, 1, r, g, b)],
        Aspect.Side => [new(5, 8, 4, 2, r, g, b)],
        _ => None,
    };

    private static Patch[] Scarf(Aspect aspect) => aspect switch
    {
        Aspect.Front => [new(4, 12, 8, 2, 230, 140, 60), new(6, 14, 2, 4, 230, 140, 60)],
        Aspect.Back => [new(4, 12, 8, 2, 230, 140, 60), new(7, 14, 2, 5, 230, 140, 60)],
        _ => [new(4, 12, 8, 2, 230, 140, 60), new(9, 14, 2, 4, 230, 140, 60)],
    };

    private static Patch[] Torso(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Side => [new(5, 14, 7, 7, r, g, b)],
        _ => [new(4, 14, 8, 7, r, g, b)],
    };

    private static Patch[] Striped(Aspect aspect)
    {
        List<Patch> stripes = [.. Torso(aspect, 235, 235, 240)];

        for (int band = 0; band < 3; band++)
            stripes.Add(new Patch(aspect == Aspect.Side ? 5 : 4, 15 + band * 2, aspect == Aspect.Side ? 7 : 8, 1,
                70, 110, 200));

        return [.. stripes];
    }

    private static Patch[] Legs(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Side => [new(6, 21, 5, 7, r, g, b)],
        _ => [new(5, 21, 3, 7, r, g, b), new(9, 21, 3, 7, r, g, b)],
    };

    private static Patch[] Shorts(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Side => [new(6, 21, 5, 4, r, g, b)],
        _ => [new(5, 21, 3, 4, r, g, b), new(9, 21, 3, 4, r, g, b)],
    };

    private static Patch[] Skirt(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Side => [new(5, 20, 7, 5, r, g, b)],
        _ => [new(4, 20, 9, 5, r, g, b)],
    };

    private static Patch[] Dress(Aspect aspect, byte r, byte g, byte b)
    {
        List<Patch> whole = [.. Torso(aspect, r, g, b)];

        whole.AddRange(Skirt(aspect, r, g, b));

        return [.. whole];
    }

    private static Patch[] Shoes(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Side => [new(5, 28, 7, 3, r, g, b)],
        _ => [new(4, 28, 4, 3, r, g, b), new(9, 28, 4, 3, r, g, b)],
    };

    private static Patch[] Cape(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        // From behind it is the whole of what you can see of somebody.
        Aspect.Back => [new(3, 12, 10, 14, r, g, b)],
        Aspect.Side => [new(3, 12, 4, 13, r, g, b)],
        _ => [new(2, 13, 2, 11, r, g, b), new(12, 13, 2, 11, r, g, b)],
    };

    private static Patch[] Backpack(Aspect aspect, byte r, byte g, byte b) => aspect switch
    {
        Aspect.Back => [new(4, 14, 8, 8, r, g, b), new(6, 12, 4, 2, r, g, b)],
        Aspect.Side => [new(3, 14, 3, 7, r, g, b)],
        _ => [new(3, 15, 1, 6, r, g, b), new(12, 15, 1, 6, r, g, b)],
    };
}
