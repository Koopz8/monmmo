using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// A line to type commands into, and the last few replies.
/// <para>
/// It sends text and reads text, and that is the whole of it. Every command is parsed
/// and every effect decided on the server, because a console this side acted on would
/// be a cheat menu with extra steps — and the account allowed to run one is named on
/// the server's own command line, which is somewhere a player cannot reach.
/// </para>
/// <para>
/// So this opens for anybody. A server with no operators answers "There is no console
/// here" and nothing else happens, which is the right amount for somebody who went
/// looking to learn.
/// </para>
/// </summary>
public sealed class ConsoleBox
{
    /// <summary>Long enough for the longest command anybody would type.</summary>
    private const int Limit = 96;

    /// <summary>How many replies stay on screen. Enough for /help to be readable.</summary>
    private const int Kept = 12;

    private readonly List<string> _said = [];

    /// <summary>
    /// How long the replies stay up once the line is closed.
    /// <para>
    /// They used to stay forever, and the first real use of the console — reading the
    /// professor's thirty-two pages about the parcel — was done through a wall of
    /// "0x4055 holds 5". A console is for getting somewhere, and what is on the screen
    /// when you arrive should be the game.
    /// </para>
    /// </summary>
    private const float FadeAfterSeconds = 6f;

    private float _showFor;

    private string _line = "";

    public bool IsOpen { get; private set; }

    /// <summary>
    /// True when what is being typed is a thing to say rather than a command.
    /// <para>
    /// The same line at the bottom of the screen does both, because that is what it is: a
    /// place to type. Two boxes would be two sets of the same key handling, and the second
    /// one is where the paste bug lives.
    /// </para>
    /// </summary>
    public bool IsChat { get; private set; }

    /// <summary>The command to send, once there is one. Taken rather than read.</summary>
    public string? Pending { get; private set; }

    public string? TakePending()
    {
        string? sending = Pending;
        Pending = null;

        return sending;
    }

    public void Said(string line)
    {
        _said.Add(line);
        _showFor = FadeAfterSeconds;

        while (_said.Count > Kept) _said.RemoveAt(0);
    }

    /// <summary>
    /// Opens on the slash, and takes the slash with it.
    /// <para>
    /// Checked by whoever owns the keyboard rather than here, so that a text box or a
    /// name being typed keeps its own letters — a console that opens on a keystroke
    /// somebody meant for something else is worse than no console.
    /// </para>
    /// </summary>
    public void Open(bool chat = false)
    {
        IsOpen = true;
        IsChat = chat;
        _showFor = FadeAfterSeconds;
        _line = "";
    }

    public void Update(float deltaSeconds)
    {
        if (!IsOpen && _showFor > 0f)
        {
            _showFor -= deltaSeconds;

            if (_showFor <= 0f) _said.Clear();
        }

        if (!IsOpen) return;

        _showFor = FadeAfterSeconds;

        foreach (char typed in Typed())
        {
            if (_line.Length < Limit) _line += typed;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _line.Length > 0) _line = _line[..^1];

        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            if (_line.Trim().Length > 0)
            {
                Pending = _line.Trim();
                Said($"> {_line.Trim()}");
            }

            IsOpen = false;
            _line = "";
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsOpen = false;
            _line = "";
        }
    }

    /// <summary>
    /// The operator's line, and whatever it last said.
    /// <para>
    /// Drawn above the dialogue box rather than over it. It used to sit at the very
    /// bottom of the screen, which is exactly where a text box is, so a console reply
    /// and a person talking to you shared the same four inches — and the reply, being
    /// drawn second, won.
    /// </para>
    /// </summary>
    public void Draw(int width, int height)
    {
        PixelFont font = Skin.Font;

        // Clear of the text box, which owns the bottom of the screen. The console is a
        // tool and the box is the game; the tool gives way.
        const int aboveTheBox = 190;

        // The replies stay up after the line closes, so a /help or a /where can be read
        // while walking around rather than only while the cursor is blinking.
        if (_said.Count > 0 && (IsOpen || _showFor > 0f))
        {
            int top = height - aboveTheBox - (_said.Count * 20) - (IsOpen ? 36 : 0);

            Skin.DrawPanel(
                new Rectangle(8, top - 8, width - 16, _said.Count * 20 + 16),
                raised: false,
                fill: new Color(12, 14, 22, 225));

            for (int i = 0; i < _said.Count; i++)
            {
                font.Draw(
                    _said[i], 20, top + (i * 20), 2,
                    _said[i].StartsWith('>') ? Skin.Accent : Skin.Ink);
            }
        }

        if (!IsOpen) return;

        var box = new Rectangle(8, height - aboveTheBox - 36, width - 16, 32);

        Skin.DrawPanel(box, raised: false, fill: new Color(12, 14, 22, 235));

        font.Draw($"/{_line}", box.X + 12, box.Y + 8, 2, Skin.Ink);

        if ((float)Raylib.GetTime() % 1.0f < 0.6f)
        {
            Raylib.DrawRectangle(
                (int)box.X + 14 + font.Measure($"/{_line}", 2), (int)box.Y + 6, 2, 16, Skin.Accent);
        }
    }

    private static IEnumerable<char> Typed()
    {
        for (int code = Raylib.GetCharPressed(); code != 0; code = Raylib.GetCharPressed())
        {
            if (code is >= 32 and < 127) yield return (char)code;
        }
    }
}
