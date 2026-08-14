using System.Numerics;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The lettering, drawn here because the cartridge would not give it up.
/// <para>
/// Everything else on this client's screen comes off the player's own image, and this
/// was meant to as well — the font is the one thing left that decides whether a screen
/// looks like the game or like a debug overlay. Six hypotheses have now been ruled out
/// across two milestones: tile index as character code, 8x16 sheets, column-major tile
/// order, LZ77 compression, one-bit-per-pixel storage, and the sharpest of them — that
/// there is any run of 48 consecutive glyph-shaped 4bpp tiles anywhere in the sixteen
/// megabytes. There is not one.
/// </para>
/// <para>
/// So this is written out rather than extracted, and written out rather than bundled: a
/// font file would be the first binary asset in a repository that has never shipped one,
/// and the rule keeping cartridge data out of this project is easier to keep when there
/// is nothing in here but code. Five by seven, the smallest size a Latin alphabet reads
/// cleanly at, drawn once into a texture at startup and thereafter one textured quad a
/// letter.
/// </para>
/// </summary>
public sealed class PixelFont
{
    /// <summary>Glyph box, before spacing.</summary>
    public const int GlyphWidth = 5;

    public const int GlyphHeight = 7;

    /// <summary>One blank column between letters, so the advance is six.</summary>
    public const int Advance = GlyphWidth + 1;

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, int> _index;

    private PixelFont(Texture2D atlas, Dictionary<char, int> index)
    {
        _atlas = atlas;
        _index = index;
    }

    /// <summary>Builds the atlas once.</summary>
    public static PixelFont Build()
    {
        string[] rows = FontRows.All;
        int count = rows.Length / GlyphHeight;

        Image image = Raylib.GenImageColor(count * GlyphWidth, GlyphHeight, new Color(0, 0, 0, 0));

        for (int glyph = 0; glyph < count; glyph++)
        {
            for (int y = 0; y < GlyphHeight; y++)
            {
                string row = rows[glyph * GlyphHeight + y];

                for (int x = 0; x < GlyphWidth && x < row.Length; x++)
                {
                    if (row[x] == '#')
                        Raylib.ImageDrawPixel(ref image, glyph * GlyphWidth + x, y, Color.White);
                }
            }
        }

        Texture2D atlas = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        var index = new Dictionary<char, int>();

        for (int i = 0; i < FontRows.Characters.Length && i < count; i++)
            index[FontRows.Characters[i]] = i;

        return new PixelFont(atlas, index);
    }

    /// <summary>How wide a line comes out at a given scale, spacing included.</summary>
    public int Measure(string text, int scale) =>
        text.Length == 0 ? 0 : text.Length * Advance * scale - scale;

    public int Height(int scale) => GlyphHeight * scale;

    /// <summary>
    /// A line broken to fit a width, at spaces.
    /// <para>
    /// Every glyph here is the same width, so this is arithmetic rather than
    /// measurement. A word longer than the whole box is left on its own line and allowed
    /// to run over, because the alternative is breaking a name in half, and a name is
    /// the one thing on this screen a player has to be able to read.
    /// </para>
    /// </summary>
    public List<string> Wrap(string text, int scale, int maxWidth)
    {
        // The width of a line of n characters is n * Advance * scale - scale, since the
        // last one carries no gap after it. Turned round, that is how many fit.
        return PokeMmo.Core.Text.Lines.Wrap(text, (maxWidth + scale) / (Advance * scale));
    }

    /// <summary>
    /// Draws a line.
    /// <para>
    /// Anything with no glyph is drawn as nothing rather than as a box: a missing letter
    /// is less wrong than a wrong one, and everything that reaches this has already been
    /// through the pass that turns the cartridge's curly apostrophes into ones this can
    /// draw.
    /// </para>
    /// </summary>
    public void Draw(string text, float x, float y, int scale, Color tint)
    {
        foreach (char c in text)
        {
            if (c != ' ' && _index.TryGetValue(c, out int glyph))
            {
                Raylib.DrawTexturePro(
                    _atlas,
                    new Rectangle(glyph * GlyphWidth, 0, GlyphWidth, GlyphHeight),
                    new Rectangle(x, y, GlyphWidth * scale, GlyphHeight * scale),
                    Vector2.Zero,
                    0,
                    tint);
            }

            x += Advance * scale;
        }
    }

    /// <summary>Draws a line over its own shadow, one pixel down and right.</summary>
    public void DrawShadowed(string text, float x, float y, int scale, Color tint, Color shadow)
    {
        Draw(text, x + scale, y + scale, scale, shadow);
        Draw(text, x, y, scale, tint);
    }

    public void DrawCentred(string text, float centreX, float y, int scale, Color tint) =>
        Draw(text, centreX - Measure(text, scale) / 2f, y, scale, tint);

    public void DrawRight(string text, float rightX, float y, int scale, Color tint) =>
        Draw(text, rightX - Measure(text, scale), y, scale, tint);

    public void Unload() => Raylib.UnloadTexture(_atlas);
}
