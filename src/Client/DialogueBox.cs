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

    public DialogueBox(IEnumerable<string> pages, uint? resume = null, bool asks = false)
    {
        _pages = pages.Select(GameText.ToAscii).ToList();
        Resume = resume;
        Asks = asks;
    }

    /// <summary>
    /// A question with no script behind it.
    /// <para>
    /// The counter in a POKeMON CENTER is the one that needed this. She asks "Would you
    /// like me to heal your POKeMON back to perfect health?" and the yes and the no are
    /// inside a standard routine — code, not script, and this project has never followed
    /// one. So the question is real, the words are the cartridge's, and only the box is
    /// ours.
    /// </para>
    /// </summary>
    public bool Asks { get; }

    /// <summary>
    /// Where the script carries on once this has been answered, when it is a question.
    /// <para>
    /// Standard routine 5 asks; the run stops there because nothing in a save can answer
    /// it. This is what the box hands back so the rest of the script can be run with the
    /// answer in place.
    /// </para>
    /// </summary>
    public uint? Resume { get; }

    public bool IsQuestion => Resume is not null || Asks;

    /// <summary>Which way the cursor is pointing. Yes first, as the games have it.</summary>
    public bool Answer { get; private set; } = true;

    /// <summary>True once the last page has been read and dismissed.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Adds a page to the end of an open box.
    /// <para>
    /// For lines the server supplies part-way through a conversation somebody else
    /// started. Fifteen people in this game hand something over while talking, and the
    /// "Found one POTION!" arrives a round trip after their own first page is already
    /// on screen — replacing the box would throw away what they were saying, which is
    /// how the president of SILPH came to thank nobody.
    /// </para>
    /// </summary>
    public void Add(string page) => _pages.Add(GameText.ToAscii(page));

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

        // The choice only appears on the last page, because that is the page the
        // question is written on — everything before it is the run-up.
        bool choosing = IsQuestion && _page >= _pages.Count - 1;

        if (choosing)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.Down)) Answer = !Answer;

            // No is also a button, and a player who wants to decline should not have to
            // find the cursor first.
            if (Raylib.IsKeyPressed(KeyboardKey.X))
            {
                Answer = false;
                IsFinished = true;

                return;
            }
        }

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
        if (IsQuestion && _page >= _pages.Count - 1)
        {
            int box = 120;
            int left = Margin + width - box - 16;
            int top2 = top - 76;

            Raylib.DrawRectangle(left, top2, box, 72, new Color(248, 248, 248, 255));
            Raylib.DrawRectangleLines(left, top2, box, 72, new Color(64, 64, 88, 255));

            Raylib.DrawText("YES", left + 40, top2 + 8, TextSize, new Color(32, 32, 40, 255));
            Raylib.DrawText("NO", left + 40, top2 + 38, TextSize, new Color(32, 32, 40, 255));
            Raylib.DrawText(">", left + 16, top2 + (Answer ? 8 : 38), TextSize, new Color(32, 32, 40, 255));

            Raylib.DrawText("Z picks", Margin + width - 90, top + Height - 26, 16, new Color(120, 120, 140, 255));

            return;
        }

        string more = _page + 1 < _pages.Count ? $"more  ({_page + 1}/{_pages.Count})" : "Z to close";
        int hintWidth = Raylib.MeasureText(more, 16);

        Raylib.DrawText(more, Margin + width - hintWidth - 16, top + Height - 26, 16, new Color(120, 120, 140, 255));
    }
}
