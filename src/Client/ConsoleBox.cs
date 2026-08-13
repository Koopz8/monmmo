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

    private string _line = "";

    public bool IsOpen { get; private set; }

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
    public void Open()
    {
        IsOpen = true;
        _line = "";
    }

    public void Update()
    {
        if (!IsOpen) return;

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

    public void Draw(int width, int height)
    {
        // The replies stay up after the line closes, so a /help or a /where can be read
        // while walking around rather than only while the cursor is blinking.
        if (_said.Count > 0)
        {
            int top = height - 40 - (_said.Count * 18) - (IsOpen ? 34 : 0);

            Raylib.DrawRectangle(0, top - 6, width, _said.Count * 18 + 12, new Color(0, 0, 0, 170));

            for (int i = 0; i < _said.Count; i++)
            {
                Raylib.DrawText(
                    _said[i], 12, top + (i * 18), 16,
                    _said[i].StartsWith('>') ? new Color(150, 200, 255, 255) : new Color(225, 225, 235, 255));
            }
        }

        if (!IsOpen) return;

        var box = new Rectangle(0, height - 34, width, 34);

        Raylib.DrawRectangleRec(box, new Color(0, 0, 0, 220));
        Raylib.DrawText($"/{_line}|", 12, height - 27, 18, new Color(240, 240, 250, 255));
    }

    private static IEnumerable<char> Typed()
    {
        for (int code = Raylib.GetCharPressed(); code != 0; code = Raylib.GetCharPressed())
        {
            if (code is >= 32 and < 127) yield return (char)code;
        }
    }
}
