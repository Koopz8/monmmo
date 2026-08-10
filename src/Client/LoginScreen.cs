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

    private void Draw()
    {
        Raylib.ClearBackground(new Color(24, 24, 32, 255));

        Raylib.DrawText("MonMMO", Width / 2 - 90, 120, 48, Color.White);

        Raylib.DrawText(
            _registering ? "Create an account" : "Sign in",
            Width / 2 - 90, 180, 20, new Color(150, 150, 170, 255));

        DrawField("Name", _username, top: 250, selected: _field == 0);
        DrawField("Password", new string('*', _password.Length), top: 330, selected: _field == 1);

        if (_message.Length > 0)
            Raylib.DrawText(_message, Width / 2 - 180, 410, 20, new Color(232, 150, 150, 255));

        string hint = _registering
            ? "Enter to create    F1 to sign in instead    Tab to switch"
            : "Enter to sign in    F1 to create an account    Tab to switch";

        Raylib.DrawText(hint, Width / 2 - 250, Height - 90, 18, new Color(110, 110, 130, 255));
    }

    private static void DrawField(string label, string value, int top, bool selected)
    {
        const int left = Width / 2 - 180;
        const int fieldWidth = 360;

        Raylib.DrawText(label, left, top - 24, 18, new Color(150, 150, 170, 255));

        Raylib.DrawRectangle(left, top, fieldWidth, 44, new Color(40, 40, 52, 255));

        Raylib.DrawRectangleLines(
            left, top, fieldWidth, 44,
            selected ? new Color(150, 200, 255, 255) : new Color(70, 70, 90, 255));

        Raylib.DrawText(value, left + 12, top + 12, 22, Color.White);

        // A caret only on the focused field, so it is obvious where typing goes.
        if (selected)
        {
            int caret = left + 14 + Raylib.MeasureText(value, 22);
            Raylib.DrawRectangle(caret, top + 10, 2, 26, new Color(150, 200, 255, 255));
        }
    }
}
