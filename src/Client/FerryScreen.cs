using PokeMmo.Core.Net;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Where the boat goes, and one of them chosen.
/// <para>
/// Built like the shop and the wardrobe: the list arrives from the server, the choosing
/// happens here, and nothing on this side decides anything — the crossing is a request and
/// is checked again on the other side.
/// </para>
/// <para>
/// It is the first screen in this game that opens onto a routine nobody can read. What the
/// cartridge does when a sailor is asked is ARM code; what the scripts around it say is a
/// table of ten places with a number each, and that table is the whole of this screen.
/// </para>
/// </summary>
public sealed class FerryScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Row = 44;

    private readonly FerryOpened _boat;

    private int _row;

    public FerryScreen(FerryOpened boat)
    {
        _boat = boat;

        // Never opening on the place already being stood on, because the first thing a
        // player does on a list is press the button.
        _row = Math.Max(0, Elsewhere.FindIndex(p => p.Number != boat.From));
    }

    /// <summary>Everywhere but here. A boat to where you are is not a journey.</summary>
    private List<FerryPort> Elsewhere => [.. _boat.Ports.Where(p => p.Number != _boat.From)];

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        List<FerryPort> ports = Elsewhere;

        if (ports.Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _row = (_row + 1) % ports.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _row = (_row - 1 + ports.Count) % ports.Count;

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        Pending = new SailRequest(ports[_row].Number);
        IsClosed = true;
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("WHERE TO?", 40, 30, 3, Skin.Ink);

        string here = _boat.Ports.FirstOrDefault(p => p.Number == _boat.From)?.Name ?? "here";

        Font.DrawRight($"sailing from {here}", Width - 40, 38, 2, Skin.InkFaint);

        var panel = new Rectangle(32, 76, Width - 64, Height - 168);

        Skin.DrawPanel(panel);
        Skin.DrawCutBorder(panel, Skin.Accent);

        List<FerryPort> ports = Elsewhere;

        if (ports.Count == 0)
        {
            Font.Draw("This boat goes nowhere else.", panel.X + 24, panel.Y + 52, 2, Skin.InkDim);
        }
        else
        {
            for (int i = 0; i < ports.Count; i++)
            {
                float y = panel.Y + 34 + i * Row;

                if (i == _row)
                    Skin.DrawSelection(new Rectangle(panel.X + 10, y - 5, panel.Width - 20, Row - 6));

                Font.Draw(ports[i].Name, panel.X + 24, y, 2, Skin.Ink);
                Font.DrawRight(ports[i].MapId, panel.X + panel.Width - 22, y, 2, Skin.InkDim);
            }
        }

        Font.Draw("Z sail    X stay", 40, Height - 40, 2, Skin.InkFaint);
    }
}
