using PokeMmo.Core.Net;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Who else is here, and a way to go to them.
/// <para>
/// A place can have more than one copy of itself, so two people standing in the same
/// town can be in copies that cannot see each other — which, from inside, looks exactly
/// like the other person not being there. The server has known how to put them together
/// since the copies existed; the only way to ask was a console command, and the console
/// belongs to operators. This is the ordinary player's way to ask, which is the whole of
/// what it is for.
/// </para>
/// <para>
/// It lists the people this client can see, which is the people in this copy of this
/// place. A name that is not on the list is somebody in another copy — and the way to
/// reach them is to be told their name by the person themselves, so the list is a
/// convenience and not the only route: anything typed is sent as it stands and the
/// server decides.
/// </para>
/// </summary>
public sealed class PeopleScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Row = 40;

    private static PixelFont Font => Skin.Font;

    private readonly List<string> _names;

    private int _row;

    public PeopleScreen(IEnumerable<string> names) => _names = [.. names.OrderBy(n => n)];

    public bool IsClosed { get; private set; }

    /// <summary>What this screen wants the server to do, taken once.</summary>
    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? asking = Pending;
        Pending = null;

        return asking;
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.X) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsClosed = true;
            return;
        }

        if (_names.Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down)) _row = (_row + 1) % _names.Count;
        if (Raylib.IsKeyPressed(KeyboardKey.Up)) _row = (_row + _names.Count - 1) % _names.Count;

        // Stop travelling with whoever you are travelling with. Here rather than on a
        // screen of its own because this is the screen about other people, and it is the
        // one place somebody looking for that would look.
        if (Raylib.IsKeyPressed(KeyboardKey.L))
        {
            Pending = new CompanyLeaveRequest();
            IsClosed = true;

            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Z))
        {
            // Asked, not done. The server decides whether that person is still here, on
            // this map, and in a different copy — and says so if they are not.
            Pending = new GoToRequest(_names[_row]);
            IsClosed = true;
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("WHO IS HERE", 40, 30, 3, Skin.Ink);
        Font.DrawRight($"{_names.Count} nearby", Width - 40, 38, 2, Skin.InkFaint);

        var panel = new Rectangle(32, 76, Width - 64, Height - 168);

        Skin.DrawPanel(panel);
        Skin.DrawCutBorder(panel, Skin.Accent);

        if (_names.Count == 0)
        {
            Font.Draw("NOBODY ELSE IS IN THIS COPY OF THIS PLACE.", panel.X + 24, panel.Y + 30, 2, Skin.InkDim);
            Font.Draw("SOMEBODY YOU CANNOT SEE MAY BE IN ANOTHER ONE.", panel.X + 24, panel.Y + 60, 2, Skin.InkFaint);
        }

        for (int i = 0; i < _names.Count && i < 11; i++)
        {
            float y = panel.Y + 24 + i * Row;

            if (i == _row)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 6, panel.Width - 20, Row - 8));

            Font.Draw(_names[i].ToUpperInvariant(), panel.X + 24, y, 2, Skin.Ink);
        }

        Font.Draw("Z go to them    X close", 40, Height - 40, 2, Skin.InkFaint);
    }
}
