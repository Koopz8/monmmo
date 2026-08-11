using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The bag, out of a fight. Pick something, pick who it goes on.
/// <para>
/// Only what restores health is listed, because that is all anything does yet. A bag
/// that showed every ball and every key item with no way to use any of them would be a
/// longer list saying the same thing.
/// </para>
/// <para>
/// Like the counter, this screen decides nothing. It sends an id and a slot; how much
/// gets restored is worked out on the server, where maximum health is known and where a
/// Potion cannot be drunk for two hundred.
/// </para>
/// </summary>
public sealed class BagScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 8;

    private readonly ItemNames _items;
    private readonly GameData _data;

    private IReadOnlyList<BagEntry> _bag;
    private IReadOnlyList<SavedMon> _party;

    private bool _choosingWho;
    private int _row;
    private int _member;
    private string _message = "";

    public BagScreen(
        IReadOnlyList<BagEntry> bag,
        IReadOnlyList<SavedMon> party,
        ItemNames items,
        GameData data)
    {
        _bag = bag;
        _party = party;
        _items = items;
        _data = data;
    }

    /// <summary>True once the bag has been shut.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>A request for the game loop to send. Cleared once taken.</summary>
    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(BagUpdated update)
    {
        _bag = update.Bag;
        _party = update.Party;
        _message = update.Message;

        // Back to the list. Drinking the last Potion while standing on it would
        // otherwise leave the cursor pointing at something that is no longer there.
        _choosingWho = false;

        Clamp();
    }

    private List<BagEntry> Usable() => [.. _bag.Where(e => e.Count > 0)];

    private void Clamp()
    {
        List<BagEntry> lines = Usable();

        _row = lines.Count == 0 ? 0 : Math.Clamp(_row, 0, lines.Count - 1);
        _member = _party.Count == 0 ? 0 : Math.Clamp(_member, 0, _party.Count - 1);
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            // Backing out of the party list closes the list, not the bag. Two escapes
            // to leave is what every menu in these games does.
            if (_choosingWho) _choosingWho = false;
            else IsClosed = true;

            return;
        }

        List<BagEntry> lines = Usable();
        int count = _choosingWho ? _party.Count : lines.Count;

        if (count == 0) return;

        ref int cursor = ref _choosingWho ? ref _member : ref _row;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            cursor = (cursor + 1) % count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            cursor = (cursor - 1 + count) % count;

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        if (!_choosingWho)
        {
            _choosingWho = true;
            _message = "";
            return;
        }

        Pending = new UseItemRequest(lines[_row].ItemId, _member);
    }

    public void Draw()
    {
        Raylib.ClearBackground(new Color(28, 32, 44, 255));

        List<BagEntry> lines = Usable();

        Raylib.DrawText(_choosingWho ? "USE ON WHO?" : "BAG", 40, 32, 28, Color.White);

        if (lines.Count == 0)
        {
            Raylib.DrawText("You are not carrying anything.", 40, 100, 24, new Color(180, 180, 190, 255));
        }

        int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, lines.Count - Rows)));

        for (int i = first; i < lines.Count && i < first + Rows; i++)
        {
            int y = 96 + (i - first) * 34;
            bool selected = i == _row;

            if (selected && !_choosingWho) Raylib.DrawText(">", 40, y, 24, Color.White);

            Raylib.DrawText(
                _items.Of(lines[i].ItemId),
                72, y, 24,
                selected ? Color.White : new Color(190, 190, 200, 255));

            Raylib.DrawText($"x{lines[i].Count}", Width - 320, y, 24, new Color(190, 190, 200, 255));
        }

        for (int i = 0; i < _party.Count; i++)
        {
            SavedMon member = _party[i];

            int y = 96 + i * 34;
            bool selected = i == _member && _choosingWho;

            if (selected) Raylib.DrawText(">", Width / 2 + 8, y, 24, Color.White);

            Raylib.DrawText(
                $"{member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}"}  Lv{member.Level}",
                Width / 2 + 40, y, 24,
                _choosingWho
                    ? selected ? Color.White : new Color(190, 190, 200, 255)
                    : new Color(120, 120, 130, 255));

            Raylib.DrawText($"{member.CurrentHp} HP", Width - 180, y, 24, new Color(160, 200, 160, 255));
        }

        if (_message.Length > 0)
            Raylib.DrawText(_message, 40, Height - 96, 22, new Color(160, 220, 160, 255));

        Raylib.DrawText(
            _choosingWho ? "Z use    X back" : "Z choose    X close",
            40, Height - 56, 20, new Color(150, 150, 160, 255));
    }
}
