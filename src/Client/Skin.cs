using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Every colour and every panel on this client's screen, in one place.
/// <para>
/// Before this there was no style layer at all: eleven files drew rectangles and picked
/// their own colours inline, and the result was eleven slightly different greys. What a
/// screen looks like is not a property of the screen — it is a property of the client —
/// so it lives here and each screen asks for a panel rather than describing one.
/// </para>
/// <para>
/// The layout grammar is the old games': a message box across the bottom, the
/// combatants' plates in opposite corners, moves in a two-by-two. What is modern is the
/// drawing of it — a raised panel with a soft edge instead of a hard double border, a
/// filled selection bar instead of a caret, health that slides rather than jumps, and
/// type read as colour.
/// </para>
/// </summary>
public static class Skin
{
    /// <summary>
    /// The client's lettering, built once the window exists and shared by every screen.
    /// <para>
    /// A settable static rather than a parameter threaded through six constructors. It
    /// is the same kind of thing as the palette below it — a property of the client, not
    /// of any screen — and a texture cannot be built before there is a window to build it
    /// against, which is what stops it being a readonly field.
    /// </para>
    /// </summary>
    public static PixelFont Font { get; set; } = null!;

    // The panel stack, dark to light. Two greys and an edge is the whole chrome.
    public static readonly Color Shadow = new(8, 10, 18, 150);
    public static readonly Color PanelDeep = new(22, 25, 38, 255);
    public static readonly Color Panel = new(38, 43, 62, 255);
    public static readonly Color PanelHigh = new(52, 59, 84, 255);
    public static readonly Color Edge = new(96, 108, 148, 255);
    public static readonly Color EdgeSoft = new(70, 79, 112, 255);

    // Ink.
    public static readonly Color Ink = new(238, 241, 250, 255);
    public static readonly Color InkDim = new(150, 158, 184, 255);
    public static readonly Color InkFaint = new(104, 112, 138, 255);
    public static readonly Color InkOnLight = new(30, 34, 48, 255);

    /// <summary>The one colour that says "this is the thing you have chosen".</summary>
    /// <summary>The figure in the wardrobe's mirror, under everything worn.</summary>
    public static readonly Color Person = new(226, 186, 150, 255);

    public static readonly Color Accent = new(96, 176, 255, 255);

    public static readonly Color AccentDeep = new(38, 96, 168, 255);

    // Health, by the thresholds the games use: green, then amber under a half, then
    // red under a fifth.
    public static readonly Color HpGood = new(96, 216, 128, 255);
    public static readonly Color HpFair = new(240, 200, 88, 255);
    public static readonly Color HpPoor = new(232, 96, 96, 255);
    public static readonly Color HpTrack = new(20, 23, 34, 255);

    /// <summary>Experience, which is nothing like health and should not look like it.</summary>
    public static readonly Color Experience = new(104, 196, 232, 255);

    public static Color HealthColour(int current, int max) =>
        max <= 0 || current * 5 <= max ? HpPoor : current * 2 <= max ? HpFair : HpGood;

    /// <summary>
    /// A raised panel: a shadow, a body, a lit top edge and a drawn border.
    /// <para>
    /// Corners are cut rather than rounded — two pixels off each one — because a
    /// rounded corner at this scale is either a blur or a staircase, and a cut corner is
    /// neither and is what the old games' frames actually do.
    /// </para>
    /// </summary>
    public static void DrawPanel(Rectangle box, bool raised = true, Color? fill = null, Color? edge = null)
    {
        var body = fill ?? Panel;
        var border = edge ?? EdgeSoft;

        Raylib.DrawRectangleRec(box with { X = box.X + 4, Y = box.Y + 5 }, Shadow);

        Raylib.DrawRectangleRec(box, body);

        // The lit top, which is what makes it read as raised rather than as a hole.
        if (raised)
            Raylib.DrawRectangleRec(box with { Height = 2 }, PanelHigh);

        DrawCutBorder(box, border);
    }

    /// <summary>A border with its corners knocked off, drawn as four lines and four dots.</summary>
    public static void DrawCutBorder(Rectangle box, Color colour, int cut = 3)
    {
        int x = (int)box.X, y = (int)box.Y, w = (int)box.Width, h = (int)box.Height;

        Raylib.DrawRectangle(x + cut, y, w - cut * 2, 2, colour);
        Raylib.DrawRectangle(x + cut, y + h - 2, w - cut * 2, 2, colour);
        Raylib.DrawRectangle(x, y + cut, 2, h - cut * 2, colour);
        Raylib.DrawRectangle(x + w - 2, y + cut, 2, h - cut * 2, colour);

        // The corners themselves, one step in on each axis.
        Raylib.DrawRectangle(x + 1, y + 1, 2, 2, colour);
        Raylib.DrawRectangle(x + w - 3, y + 1, 2, 2, colour);
        Raylib.DrawRectangle(x + 1, y + h - 3, 2, 2, colour);
        Raylib.DrawRectangle(x + w - 3, y + h - 3, 2, 2, colour);
    }

    /// <summary>
    /// The bar behind whatever is selected.
    /// <para>
    /// A filled bar rather than the caret the old games use, which is the one place this
    /// deliberately parts company with them: a caret says where you are and a bar says it
    /// from across the room.
    /// </para>
    /// </summary>
    public static void DrawSelection(Rectangle box)
    {
        Raylib.DrawRectangleRec(box, new Color(Accent.R, Accent.G, Accent.B, (byte)38));
        Raylib.DrawRectangleRec(box with { Width = 3 }, Accent);
    }

    /// <summary>
    /// A meter. The track is sunk, the fill is flat, and the top row of the fill is lit.
    /// </summary>
    public static void DrawMeter(Rectangle box, float fraction, Color colour)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);

        Raylib.DrawRectangleRec(box, HpTrack);

        int width = (int)(box.Width * fraction);

        if (width > 0)
        {
            Raylib.DrawRectangle((int)box.X, (int)box.Y, width, (int)box.Height, colour);

            Raylib.DrawRectangle(
                (int)box.X, (int)box.Y, width, 1,
                new Color(
                    (byte)Math.Min(255, colour.R + 60),
                    (byte)Math.Min(255, colour.G + 60),
                    (byte)Math.Min(255, colour.B + 60),
                    (byte)255));
        }

        Raylib.DrawRectangleLinesEx(box, 1, new Color(0, 0, 0, 90));
    }

    /// <summary>
    /// A small filled chip, for a move's type or an item's count.
    /// <para>
    /// This is the modern half of the brief. The old games print the type as a word in
    /// the same ink as everything else; a coloured chip says the same thing before it is
    /// read.
    /// </para>
    /// </summary>
    /// <summary>How wide a chip comes out, so a row of them can be laid out.</summary>
    public static int ChipWidth(PixelFont font, string text, int scale) =>
        font.Measure(text, scale) + scale * 6;

    public static void DrawChip(PixelFont font, string text, float x, float y, int scale, Color colour)
    {
        int width = ChipWidth(font, text, scale);
        int height = font.Height(scale) + scale * 4;

        var box = new Rectangle(x, y, width, height);

        Raylib.DrawRectangleRec(box, colour);
        Raylib.DrawRectangleRec(box with { Height = 1 }, new Color(255, 255, 255, 60));

        font.Draw(text, x + scale * 3, y + scale * 2, scale, InkOnLight);
    }

    /// <summary>
    /// The eighteen types, as colours.
    /// <para>
    /// Assigned here rather than read off the cartridge, and said so: the games hold a
    /// type chart of effectiveness, which is arithmetic and is extracted, but the colours
    /// they draw types in are in code and in the tile data, and neither is something this
    /// project reads. These are chosen to be told apart, which is the only property that
    /// matters for a chip six pixels tall.
    /// </para>
    /// </summary>
    public static Color TypeColour(PokeMmo.Core.Data.PokemonType type) => type switch
    {
        PokeMmo.Core.Data.PokemonType.Fire => new Color(240, 128, 72, 255),
        PokeMmo.Core.Data.PokemonType.Water => new Color(104, 152, 240, 255),
        PokeMmo.Core.Data.PokemonType.Grass => new Color(120, 200, 96, 255),
        PokeMmo.Core.Data.PokemonType.Electric => new Color(248, 208, 72, 255),
        PokeMmo.Core.Data.PokemonType.Ice => new Color(152, 216, 216, 255),
        PokeMmo.Core.Data.PokemonType.Fighting => new Color(200, 96, 72, 255),
        PokeMmo.Core.Data.PokemonType.Poison => new Color(168, 104, 192, 255),
        PokeMmo.Core.Data.PokemonType.Ground => new Color(224, 192, 104, 255),
        PokeMmo.Core.Data.PokemonType.Flying => new Color(168, 144, 240, 255),
        PokeMmo.Core.Data.PokemonType.Psychic => new Color(248, 88, 136, 255),
        PokeMmo.Core.Data.PokemonType.Bug => new Color(168, 184, 32, 255),
        PokeMmo.Core.Data.PokemonType.Rock => new Color(184, 160, 56, 255),
        PokeMmo.Core.Data.PokemonType.Ghost => new Color(112, 88, 152, 255),
        PokeMmo.Core.Data.PokemonType.Dragon => new Color(112, 56, 248, 255),
        PokeMmo.Core.Data.PokemonType.Dark => new Color(112, 88, 72, 255),
        PokeMmo.Core.Data.PokemonType.Steel => new Color(184, 184, 208, 255),
        _ => new Color(168, 168, 152, 255),
    };
}
