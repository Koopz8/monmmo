using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The party on one side, the box on the other.
/// <para>
/// Built the way the bag is built, and for the same reason: two lists, one question,
/// and the lit column says which list a key press reaches. Moving between them is the
/// whole screen — there is nothing else to do here.
/// </para>
/// <para>
/// Opened by standing in front of the machine in the corner of a Pokémon Center and
/// pressing the same key that talks to people. Which squares those are is read off the
/// cartridge's own behaviour bytes, on both sides of the split independently.
/// </para>
/// </summary>
public sealed class BoxScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 8;
    private const int Row = 44;

    private readonly GameData _data;
    private readonly ItemNames _items;

    private IReadOnlyList<SavedMon> _party;
    private IReadOnlyList<SavedMon> _box;
    private int _size;

    private bool _inBox;
    private int _partyRow;
    private int _boxRow;
    private string _message = "";

    public BoxScreen(
        IReadOnlyList<SavedMon> party,
        IReadOnlyList<SavedMon> box,
        int size,
        GameData data,
        ItemNames items)
    {
        _party = party;
        _box = box;
        _size = size;
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

    public void Apply(BoxUpdated update)
    {
        _party = update.Party;
        _box = update.Box;
        _size = update.BoxSize;
        _message = update.Message;

        Clamp();
    }

    /// <summary>
    /// The party alone, from whatever else changed it.
    /// <para>
    /// A fight ends and the party comes back on a different message. Without this the
    /// box screen opens showing whoever was in the party an hour ago.
    /// </para>
    /// </summary>
    public void Apply(IReadOnlyList<SavedMon> party)
    {
        _party = party;
        Clamp();
    }

    private void Clamp()
    {
        _partyRow = _party.Count == 0 ? 0 : Math.Clamp(_partyRow, 0, _party.Count - 1);
        _boxRow = _box.Count == 0 ? 0 : Math.Clamp(_boxRow, 0, _box.Count - 1);

        // An empty box has nothing to point at, so the cursor goes back to the side that
        // does. Sitting on an empty column is a screen that appears not to respond.
        if (_inBox && _box.Count == 0) _inBox = false;
    }

    public void Update()
    {
        // Not the key that opened it. The screen is made and updated inside the same
        // frame, so the press that opened it is still down when this asks — and a box
        // that opens and shuts again in one frame looks exactly like a key that does
        // nothing at all.
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
        {
            _inBox = false;
            _message = "";
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) && _box.Count > 0)
        {
            _inBox = true;
            _message = "";
        }

        int count = _inBox ? _box.Count : _party.Count;

        if (count == 0) return;

        ref int cursor = ref _inBox ? ref _boxRow : ref _partyRow;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            cursor = (cursor + 1) % count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            cursor = (cursor - 1 + count) % count;

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        Pending = _inBox ? new WithdrawRequest(_boxRow) : new DepositRequest(_partyRow);
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("BOX", 40, 30, 3, Skin.Ink);
        Font.DrawRight($"{_box.Count}/{_size}", Width - 40, 38, 2, Skin.InkFaint);

        var party = new Rectangle(32, 76, Width / 2 - 56, Height - 168);
        var box = new Rectangle(Width / 2 + 24, 76, Width / 2 - 56, Height - 168);

        Skin.DrawPanel(party);
        Skin.DrawPanel(box);
        Skin.DrawCutBorder(_inBox ? box : party, Skin.Accent);

        Column(party, "PARTY", _party, _partyRow, !_inBox, "Nobody.");
        Column(box, "STORED", _box, _boxRow, _inBox, _size > 0 ? "Empty." : "This cartridge has no box.");

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 74, 2, Skin.HpGood);

        // Named for where the arrow goes, not for where you are. The first version said
        // "party >" while the cursor was already in the party.
        DrawKeys(_inBox ? "Z take out    < party    X close" : "Z store    box >    X close");
    }

    private void Column(
        Rectangle panel,
        string title,
        IReadOnlyList<SavedMon> members,
        int row,
        bool live,
        string empty)
    {
        Font.Draw(title, panel.X + 22, panel.Y + 14, 2, live ? Skin.Ink : Skin.InkFaint);

        if (members.Count == 0)
        {
            Font.Draw(empty, panel.X + 22, panel.Y + 52, 2, Skin.InkDim);
            return;
        }

        int first = Math.Max(0, Math.Min(row - Rows / 2, Math.Max(0, members.Count - Rows)));

        for (int i = first; i < members.Count && i < first + Rows; i++)
        {
            SavedMon member = members[i];

            float y = panel.Y + 48 + (i - first) * Row;
            bool selected = i == row && live;

            if (selected)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 5, panel.Width - 20, Row - 6));

            string name = GameText.ToAscii(
                member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}");

            Color ink = live ? selected ? Skin.Ink : Skin.InkDim : Skin.InkFaint;

            Font.Draw(name, panel.X + 24, y, 2, ink);
            Font.DrawRight($"Lv{member.Level}", panel.X + panel.Width - 20, y, 2, ink);

            // Fainted said out loud rather than shown as a number, because the one rule
            // this screen enforces is about who can still fight.
            if (member.CurrentHp <= 0)
                Font.Draw("fainted", panel.X + 24, y + 20, 2, Skin.HpPoor);
            else if (member.HeldItem != 0)
                Font.Draw(GameText.ToAscii(_items.Of(member.HeldItem)), panel.X + 24, y + 20, 2, Skin.Accent);
        }

        if (members.Count > Rows)
        {
            Font.DrawRight(
                $"{row + 1}/{members.Count}", panel.X + panel.Width - 20,
                panel.Y + panel.Height - 24, 2, Skin.InkFaint);
        }
    }

    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 42, 2, Skin.InkFaint);
}
