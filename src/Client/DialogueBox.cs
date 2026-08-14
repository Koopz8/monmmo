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

    private float _blink;

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
    /// <summary>
    /// The box, and the question when there is one.
    /// <para>
    /// Across the bottom of the screen the way every one of these games has had it, with
    /// the answer floating above the right-hand end. What is modern is that the choice is
    /// a filled bar rather than a caret, and that the marker saying the box is waiting
    /// blinks — before, it said "Z to close" in words, because the font had no arrow to
    /// draw. It has one now.
    /// </para>
    /// </summary>
    public void Draw(int windowWidth, int windowHeight)
    {
        if (IsFinished) return;

        PixelFont font = Skin.Font;

        _blink += Raylib.GetFrameTime();

        int top = windowHeight - Height - Margin;
        int width = windowWidth - Margin * 2;

        var box = new Rectangle(Margin, top, width, Height);

        Skin.DrawPanel(box);

        // Two lines, split where the cartridge split them. Nothing is re-flowed: a page
        // break is a control byte put there by somebody who wrote the line to fit.
        string[] lines = _pages[_page].Split('\n');

        for (int i = 0; i < lines.Length; i++)
            font.Draw(lines[i], Margin + 24, top + 26 + i * 28, 3, Skin.Ink);

        if (IsQuestion && _page >= _pages.Count - 1)
        {
            DrawAnswer(font, Margin + width - 148, top - 88);
            return;
        }

        string more = _page + 1 < _pages.Count ? $"{_page + 1}/{_pages.Count}" : "";

        if (more.Length > 0)
            font.DrawRight(more, Margin + width - 44, top + Height - 26, 2, Skin.InkFaint);

        // The blinking marker, which is the only thing on screen that says the game is
        // waiting for a person.
        if (_blink % 1.0f < 0.6f)
        {
            Raylib.DrawTriangle(
                new System.Numerics.Vector2(Margin + width - 34, top + Height - 26),
                new System.Numerics.Vector2(Margin + width - 22, top + Height - 26),
                new System.Numerics.Vector2(Margin + width - 28, top + Height - 16),
                Skin.Accent);
        }
    }

    private void DrawAnswer(PixelFont font, int left, int top)
    {
        var box = new Rectangle(left, top, 132, 76);

        Skin.DrawPanel(box);

        var chosen = new Rectangle(left + 6, top + (Answer ? 10 : 40), 120, 26);

        Skin.DrawSelection(chosen);

        font.Draw("YES", left + 24, top + 18, 3, Answer ? Skin.Ink : Skin.InkDim);
        font.Draw("NO", left + 24, top + 48, 3, Answer ? Skin.InkDim : Skin.Ink);
    }

}
