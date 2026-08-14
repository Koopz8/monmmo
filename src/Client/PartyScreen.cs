using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The party, looked at properly.
/// <para>
/// This game has never had one. The bag shows a column of names to use something on and
/// the box shows a column to move about, and both of those are a list beside the thing
/// they are really about — there has been nowhere to simply look at what you are
/// carrying. Health, condition, level, held item, and the four moves of whoever the
/// cursor is on.
/// </para>
/// <para>
/// And it is where the order is decided, which is the only thing anybody can decide
/// about a party. Until now the lead was whoever had been in slot nought since the day
/// they were caught, and the only way to change it was to win or to faint.
/// </para>
/// </summary>
public sealed class PartyScreen
{
    private const int Width = 960;
    private const int Height = 640;

    /// <summary>Tall enough for a name, a bar, and a line for what they are carrying.</summary>
    private const int Row = 74;

    private readonly GameData _data;
    private readonly ItemNames _items;

    private IReadOnlyList<SavedMon> _party;

    private int _row;
    private int? _holding;
    private string _message = "";

    public PartyScreen(IReadOnlyList<SavedMon> party, GameData data, ItemNames items)
    {
        _party = party;
        _data = data;
        _items = items;
    }

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(IReadOnlyList<SavedMon> party, string message = "")
    {
        _party = party;

        if (message.Length > 0) _message = message;

        _holding = null;

        _row = _party.Count == 0 ? 0 : Math.Clamp(_row, 0, _party.Count - 1);
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            // Putting somebody down is not leaving. Two escapes, the same as backing out
            // of the bag's party list.
            if (_holding is not null)
            {
                _holding = null;
                _message = "";
            }
            else
            {
                IsClosed = true;
            }

            return;
        }

        if (_party.Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _row = (_row + 1) % _party.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _row = (_row - 1 + _party.Count) % _party.Count;

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        if (_holding is not { } picked)
        {
            _holding = _row;
            _message = "";
            return;
        }

        // Picking the same one again puts it back rather than sending a swap the server
        // would refuse. Nothing has to travel for nothing to happen.
        if (picked == _row)
        {
            _holding = null;
            return;
        }

        Pending = new SwapPartyRequest(picked, _row);
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw(_holding is null ? "PARTY" : "SWAP WITH WHO?", 40, 30, 3, Skin.Ink);

        var panel = new Rectangle(32, 76, Width / 2 + 40, Height - 168);
        var detail = new Rectangle(Width / 2 + 96, 76, Width / 2 - 128, Height - 168);

        Skin.DrawPanel(panel);
        Skin.DrawPanel(detail);

        if (_party.Count == 0)
        {
            Font.Draw("Nobody yet.", panel.X + 22, panel.Y + 26, 2, Skin.InkDim);
            DrawKeys("X close");
            return;
        }

        for (int i = 0; i < _party.Count; i++)
        {
            SavedMon member = _party[i];

            float y = panel.Y + 18 + i * Row;
            bool selected = i == _row;
            bool lifted = _holding == i;

            if (selected)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 5, panel.Width - 20, Row - 8));

            string name = GameText.ToAscii(
                member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}");

            Color ink = lifted ? Skin.Accent : selected ? Skin.Ink : Skin.InkDim;

            // Who leads, said out loud. It is the whole reason this screen can rearrange
            // anything, and a list with no mark on the first row does not say it.
            if (i == 0) Font.Draw("LEADS", panel.X + 24, y + 2, 2, Skin.HpGood);

            Font.Draw(name, panel.X + 108, y, 2, ink);
            Font.DrawRight($"Lv{member.Level}", panel.X + panel.Width - 20, y, 2, ink);

            if (Ailing(member.Status) is { } ailing)
                Font.Draw(ailing, panel.X + 108 + 180, y, 2, Skin.HpPoor);

            int most = Math.Max(1, MaxHpOf(member));

            Skin.DrawMeter(
                new Rectangle(panel.X + 108, y + 24, panel.Width - 300, 6),
                member.CurrentHp / (float)most,
                Skin.HealthColour(member.CurrentHp, most));

            Font.DrawRight(
                member.CurrentHp <= 0 ? "fainted" : $"{member.CurrentHp}/{most}",
                panel.X + panel.Width - 20, y + 20, 2,
                member.CurrentHp <= 0 ? Skin.HpPoor : Skin.InkFaint);

            if (member.HeldItem != 0)
            {
                Font.Draw(
                    GameText.ToAscii(_items.Of(member.HeldItem)),
                    panel.X + 108, y + 40, 2, Skin.Accent);
            }
        }

        DrawDetail(detail, _party[_row]);

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 74, 2, Skin.HpGood);

        DrawKeys(_holding is null ? "Z pick up    X close" : "Z swap    X put down");
    }

    /// <summary>
    /// What the one under the cursor knows. The only place in the game outside a fight
    /// where a player can see their own moves.
    /// </summary>
    private void DrawDetail(Rectangle panel, SavedMon member)
    {
        Font.Draw("KNOWS", panel.X + 22, panel.Y + 14, 2, Skin.InkFaint);

        if (member.Moves.Count == 0)
        {
            Font.Draw("Nothing at all.", panel.X + 22, panel.Y + 52, 2, Skin.InkDim);
            return;
        }

        for (int i = 0; i < member.Moves.Count; i++)
        {
            float y = panel.Y + 52 + i * 46;

            MoveData? move = _data.MoveAt(member.Moves[i]);

            Font.Draw(
                GameText.ToAscii(move?.Name ?? $"move {member.Moves[i]}"),
                panel.X + 22, y, 2, Skin.InkDim);

            if (move is not { } known) continue;

            Font.Draw(
                known.Power > 1 ? $"{known.Type}   power {known.Power}" : $"{known.Type}",
                panel.X + 22, y + 20, 2, Skin.InkFaint);
        }
    }

    private static string? Ailing(StatusCondition status) => status switch
    {
        StatusCondition.Poison => "PSN",
        StatusCondition.Burn => "BRN",
        StatusCondition.Paralysis => "PAR",
        StatusCondition.Sleep => "SLP",
        StatusCondition.Freeze => "FRZ",
        _ => null,
    };

    /// <summary>Maximum health, which the save does not carry and the rules can work out.</summary>
    private int MaxHpOf(SavedMon member) =>
        PartyBuilder.Restore(_data, member) is { } battler ? battler.MaxHp : Math.Max(1, member.CurrentHp);

    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 42, 2, Skin.InkFaint);
}
