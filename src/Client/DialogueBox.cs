using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The text box at the bottom of the screen, and the pages waiting to go in it.
/// <para>
/// One page at a time, advanced by the same button that opened it. The cartridge has
/// already decided where the pages break — a page break is a control byte in the text
/// and it is there because somebody wrote the line to fit — so nothing here re-flows
/// anything. Guessing at line breaks would ruin lines that were written to land where
/// they land.
/// </para>
/// </summary>
public sealed class DialogueBox
{
    private const int Height = 132;
    private const int Margin = 16;
    private const int TextSize = 24;

    private readonly List<string> _pages;

    private int _page;

    public DialogueBox(IEnumerable<string> pages) =>
        _pages = pages.Select(GameText.ToAscii).ToList();

    /// <summary>True once the last page has been read and dismissed.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// True when there is nothing worth opening a box for.
    /// <para>
    /// A script with no dialogue in it is ordinary — plenty of them only set a flag or
    /// hand something over. Opening an empty box for those would be worse than doing
    /// nothing, because an empty box has to be dismissed.
    /// </para>
    /// </summary>
    public bool IsEmpty => !_pages.Any(p => !string.IsNullOrWhiteSpace(p));

    /// <summary>
    /// Advances on a button press.
    /// <para>
    /// The press, not the hold. Reading the key as held would run every page of a
    /// conversation past in three frames, which looks exactly like a text box that does
    /// not work.
    /// </para>
    /// </summary>
    public void Update()
    {
        if (IsFinished) return;
        if (!Pressed()) return;

        _page++;

        if (_page >= _pages.Count) IsFinished = true;
    }

    public static bool Pressed() =>
        Raylib.IsKeyPressed(KeyboardKey.Z) ||
        Raylib.IsKeyPressed(KeyboardKey.Enter) ||
        Raylib.IsKeyPressed(KeyboardKey.Space);

    /// <summary>Drawn over the map in screen space, after the camera has been ended.</summary>
    public void Draw(int windowWidth, int windowHeight)
    {
        if (IsFinished) return;

        int top = windowHeight - Height - Margin;
        int width = windowWidth - Margin * 2;

        Raylib.DrawRectangle(Margin, top, width, Height, new Color(248, 248, 248, 255));
        Raylib.DrawRectangleLines(Margin, top, width, Height, new Color(64, 64, 88, 255));
        Raylib.DrawRectangleLines(Margin + 3, top + 3, width - 6, Height - 6, new Color(160, 160, 184, 255));

        Raylib.DrawText(_pages[_page], Margin + 20, top + 20, TextSize, new Color(32, 32, 40, 255));

        // A quiet reminder rather than the blinking arrow the games use. The default
        // font is ASCII only, so it is words: without something here a player who has
        // read the page has no idea the box is waiting for them.
        string more = _page + 1 < _pages.Count ? $"more  ({_page + 1}/{_pages.Count})" : "Z to close";
        int hintWidth = Raylib.MeasureText(more, 16);

        Raylib.DrawText(more, Margin + width - hintWidth - 16, top + Height - 26, 16, new Color(120, 120, 140, 255));
    }
}
