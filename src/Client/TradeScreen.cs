using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Your party down one side, what is on the table down the other.
/// <para>
/// Built like the box and the wardrobe, and for the same reason. What is different is that
/// a third of this screen belongs to somebody else, and none of it is this client's to
/// decide: every key press is a request, and what comes back is the server's account of a
/// table two people are looking at.
/// </para>
/// <para>
/// It replaces three console commands typed across two windows, which is not a way to drive
/// a negotiation and is why the first live run of trading ended without an explanation.
/// </para>
/// </summary>
public sealed class TradeScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Row = 44;

    private readonly GameData _data;

    private IReadOnlyList<SavedMon> _party;
    private TradeUpdated _table;
    private int _row;

    public TradeScreen(IReadOnlyList<SavedMon> party, TradeUpdated table, GameData data)
    {
        _party = party;
        _table = table;
        _data = data;
    }

    public bool IsClosed { get; private set; }

    public string? Ended { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(TradeUpdated table) => _table = table;

    public void Apply(IReadOnlyList<SavedMon> party)
    {
        _party = party;
        _row = _party.Count == 0 ? 0 : Math.Clamp(_row, 0, _party.Count - 1);
    }

    /// <summary>The trade is over; the screen says why and waits to be dismissed.</summary>
    public void Finish(string reason) => Ended = reason;

    public void Update()
    {
        if (Ended is not null)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Z) || Raylib.IsKeyPressed(KeyboardKey.X)
                || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                IsClosed = true;
            }

            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            Pending = new TradeCancel();
            return;
        }

        if (_party.Count > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
                _row = (_row + 1) % _party.Count;

            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
                _row = (_row - 1 + _party.Count) % _party.Count;
        }

        // Putting something up, and taking it back down with the same key — because the
        // thing a player wants after offering the wrong one is to stop offering it.
        if (Raylib.IsKeyPressed(KeyboardKey.Z) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            bool alreadyUp = _table.Yours is { } up && Same(up, _party.ElementAtOrDefault(_row));

            Pending = new TradeOffer(alreadyUp ? -1 : _row);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.C)) Pending = new TradeConfirm(!_table.YouAgreed);
    }

    /// <summary>
    /// Whether the thing on the table is the one under the cursor. Compared by what is on
    /// it rather than by slot, because the slot is not carried back and a party can be
    /// rearranged while a trade is open.
    /// </summary>
    private static bool Same(SavedMon a, SavedMon? b) =>
        b is not null && a.Species == b.Species && a.Level == b.Level && a.Experience == b.Experience;

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("TRADE", 40, 30, 3, Skin.Ink);
        Font.DrawRight($"with {_table.WithName}", Width - 40, 38, 2, Skin.InkFaint);

        var mine = new Rectangle(32, 76, Width / 2 - 56, Height - 168);
        var table = new Rectangle(Width / 2 + 24, 76, Width / 2 - 56, Height - 168);

        Skin.DrawPanel(mine);
        Skin.DrawPanel(table);
        Skin.DrawCutBorder(mine, Skin.Accent);

        DrawParty(mine);
        DrawTable(table);

        if (Ended is not null)
        {
            Font.Draw(Ended, 40, Height - 74, 2, Skin.HpGood);
            DrawKeys("Z close");
            return;
        }

        DrawKeys("Z offer / take back    C agree    X call it off");
    }

    private void DrawParty(Rectangle panel)
    {
        Font.Draw("YOURS", panel.X + 22, panel.Y + 14, 2, Skin.Ink);

        if (_party.Count == 0)
        {
            Font.Draw("Nobody.", panel.X + 22, panel.Y + 52, 2, Skin.InkDim);
            return;
        }

        for (int i = 0; i < _party.Count; i++)
        {
            float y = panel.Y + 52 + i * Row;

            if (i == _row && Ended is null)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 5, panel.Width - 20, Row - 6));

            bool up = _table.Yours is { } offered && Same(_party[i], offered);

            Font.Draw(Name(_party[i]), panel.X + 24, y, 2, up ? Skin.Accent : Skin.Ink);
            Font.DrawRight($"Lv{_party[i].Level}", panel.X + panel.Width - 22, y, 2, Skin.InkDim);

            if (up) Font.Draw("on the table", panel.X + 24, y + 20, 2, Skin.Accent);
        }
    }

    private void DrawTable(Rectangle panel)
    {
        Font.Draw("ON THE TABLE", panel.X + 22, panel.Y + 14, 2, Skin.Ink);

        Offered(panel, panel.Y + 60, "you", _table.Yours, _table.YouAgreed);
        Offered(panel, panel.Y + 190, _table.WithName, _table.Theirs, _table.TheyAgreed);

        // The one line that says whether anything is about to happen. Both agreed and the
        // swap has already gone through by the time this could be drawn, so what this
        // reads in practice is who everybody is waiting for.
        string waiting =
            _table.Yours is null || _table.Theirs is null ? "Nothing to agree to yet."
            : _table.YouAgreed && !_table.TheyAgreed ? $"Waiting for {_table.WithName}."
            : !_table.YouAgreed && _table.TheyAgreed ? $"{_table.WithName} has agreed."
            : _table.YouAgreed ? "Agreed."
            : "Neither has agreed.";

        Font.Draw(waiting, panel.X + 22, panel.Y + panel.Height - 48, 2, Skin.InkDim);
    }

    private void Offered(Rectangle panel, float y, string whose, SavedMon? what, bool agreed)
    {
        Font.Draw(whose.ToUpperInvariant(), panel.X + 22, y, 2, Skin.InkFaint);

        Font.Draw(
            what is null ? "nothing yet" : Name(what),
            panel.X + 22, y + 32, 2, what is null ? Skin.InkDim : Skin.Ink);

        if (what is not null)
            Font.DrawRight($"Lv{what.Level}", panel.X + panel.Width - 22, y + 32, 2, Skin.InkDim);

        Font.Draw(agreed ? "agreed" : "not agreed yet", panel.X + 22, y + 60, 2,
            agreed ? Skin.HpGood : Skin.InkDim);
    }

    private string Name(SavedMon who) =>
        GameText.ToAscii(who.Nickname ?? _data.SpeciesAt(who.Species)?.Name ?? $"species {who.Species}");

    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 40, 2, Skin.InkFaint);
}
