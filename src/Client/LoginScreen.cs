using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Two fields and a button.
/// <para>
/// Deliberately plain. The interesting question here is not how it looks but what it
/// keeps: the username is remembered between sessions, the password never is, and it
/// exists in this process only long enough to be sent. Anything a client stores is
/// something that can be taken off a player's machine.
/// </para>
/// </summary>
public sealed class LoginScreen
{
    private const int Width = 960;
    private const int Height = 640;

    /// <summary>Long enough for anything sensible, short enough to bound the field.</summary>
    private const int MaxPasswordLength = 64;

    private readonly int _usernameLimit;

    private string _username;
    private string _password = "";
    private string _message = "";
    private int _field;
    private bool _registering;
    private bool _busy;

    public LoginScreen(string rememberedUsername, int usernameLimit = 16)
    {
        _usernameLimit = usernameLimit;
        _username = rememberedUsername;
        _field = _username.Length > 0 ? 1 : 0;
    }

    public string Username => _username;

    /// <summary>
    /// Runs until the player is in, or closes the window. Returns false when they
    /// gave up rather than got in.
    /// </summary>
    public bool Run(NetworkClient network)
    {
        while (!Raylib.WindowShouldClose())
        {
            if (!_busy) ReadInput(network);

            Raylib.BeginDrawing();
            Draw();
            Raylib.EndDrawing();

            if (Authenticated) return true;
        }

        return false;
    }

    private bool Authenticated { get; set; }

    private void ReadInput(NetworkClient network)
    {
        foreach (char typed in TypedCharacters())
        {
            if (_field == 0 && _username.Length < _usernameLimit) _username += typed;
            else if (_field == 1 && _password.Length < MaxPasswordLength) _password += typed;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace)) Backspace();

        if (Raylib.IsKeyPressed(KeyboardKey.Tab) ||
            Raylib.IsKeyPressed(KeyboardKey.Down) ||
            Raylib.IsKeyPressed(KeyboardKey.Up))
        {
            _field = 1 - _field;
        }

        // Ctrl is not used for anything else here, so it is free for the toggle, and a
        // player who has never registered should not have to hunt for it.
        if (Raylib.IsKeyPressed(KeyboardKey.F1) ||
            (Raylib.IsKeyDown(KeyboardKey.LeftControl) && Raylib.IsKeyPressed(KeyboardKey.N)))
        {
            _registering = !_registering;
            _message = "";
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
            Submit(network);
    }

    private static IEnumerable<char> TypedCharacters()
    {
        // Raylib hands back a queue of code points, which is how a text field gets
        // keyboard layout right without knowing anything about layouts.
        while (Raylib.GetCharPressed() is var code && code > 0)
        {
            if (code is >= 32 and < 127) yield return (char)code;
        }
    }

    private void Backspace()
    {
        if (_field == 0 && _username.Length > 0) _username = _username[..^1];
        else if (_field == 1 && _password.Length > 0) _password = _password[..^1];
    }

    private void Submit(NetworkClient network)
    {
        if (_username.Length == 0 || _password.Length == 0)
        {
            _message = "Both fields, please.";
            return;
        }

        _busy = true;
        _message = _registering ? "Creating the account..." : "Signing in...";

        string username = _username;
        string password = _password;
        bool registering = _registering;

        // Cleared before the request goes out rather than after it comes back, so a
        // slow reply cannot leave it sitting in memory longer than it has to.
        _password = "";

        Task.Run(async () =>
        {
            string? refusal = await network.AuthenticateAsync(username, password, registering).ConfigureAwait(false);

            if (refusal is null)
            {
                Authenticated = true;
                return;
            }

            _message = refusal;
            _field = 1;
            _busy = false;
        });
    }

    private static PixelFont Font => Skin.Font;

    /// <summary>
    /// The first thing anybody sees, and until now the one screen still drawn in the
    /// engine's own default face.
    /// <para>
    /// The title is drawn twice, once dark and offset, because a flat pixel title on a
    /// flat background is the one place this font looks like a debug overlay rather than
    /// a game.
    /// </para>
    /// </summary>
    private void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        // A band behind the title, so the top of the screen is not empty space with a
        // word floating in it.
        Raylib.DrawRectangleGradientV(0, 60, Width, 200, Skin.Panel, Skin.PanelDeep);

        Font.DrawCentred("MonMMO", Width / 2f, 120, 6, Skin.Ink);

        Font.DrawCentred(
            _registering ? "Create an account" : "Sign in",
            Width / 2f, 190, 2, Skin.InkDim);

        DrawField("NAME", _username, top: 250, selected: _field == 0);
        DrawField("PASSWORD", new string('*', _password.Length), top: 330, selected: _field == 1);

        if (_message.Length > 0) Font.DrawCentred(_message, Width / 2f, 410, 2, Skin.HpPoor);

        string hint = _registering
            ? "Enter to create    F1 to sign in instead    Tab to switch"
            : "Enter to sign in    F1 to create an account    Tab to switch";

        Font.DrawCentred(hint, Width / 2f, Height - 80, 2, Skin.InkFaint);
    }

    private void DrawField(string label, string value, int top, bool selected)
    {
        const int left = Width / 2 - 180;
        const int fieldWidth = 360;

        var box = new Rectangle(left, top, fieldWidth, 44);

        Font.Draw(label, left, top - 22, 2, selected ? Skin.Accent : Skin.InkFaint);

        Skin.DrawPanel(box, raised: false);

        if (selected) Skin.DrawCutBorder(box, Skin.Accent);

        Font.Draw(value, left + 14, top + 15, 2, Skin.Ink);

        // A caret only on the focused field, so it is obvious where typing goes, and it
        // blinks — a still caret on an empty field reads as a stray pixel.
        if (selected && (float)Raylib.GetTime() % 1.0f < 0.6f)
        {
            Raylib.DrawRectangle(
                left + 16 + Font.Measure(value, 2), top + 13, 2, 18, Skin.Accent);
        }
    }
}
