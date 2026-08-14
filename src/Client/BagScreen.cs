using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The bag, out of a fight. Pick something, pick who it goes on.
/// <para>
/// Three things do something now — medicine restores, a machine teaches, and a stone
/// turns one creature into another — and everything else says so and is not spent. The
/// list has always shown the whole bag; what has changed is how much of it answers.
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

    /// <summary>
    /// How tall one party line is: a name, a bar, and room under it for whatever the
    /// member is carrying.
    /// </summary>
    private const int Row = 60;

    private readonly ItemNames _items;
    private readonly GameData _data;

    private IReadOnlyList<BagEntry> _bag;
    private IReadOnlyList<SavedMon> _party;

    private bool _choosingWho;
    private int _row;
    private int _member;
    private string _message = "";

    /// <summary>
    /// The move a machine has offered and which of the four it would replace.
    /// <para>
    /// Held here because this is where the machine was used. The battle screen asks the
    /// same question in the same words; what differs is only which screen the player
    /// happens to be looking at when it is asked.
    /// </para>
    /// </summary>
    private (int Slot, int MoveId)? _offered;

    private int _forget;

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

        // The sentence is put together here rather than sent, because the server has
        // never seen a name and this is the machine with the cartridge. Same words as
        // the battle screen's, arrived at from the same two numbers.
        _message = update.EvolvedInto != 0
            ? $"{SpeciesNamed(update.EvolvedFrom)} evolved into {SpeciesNamed(update.EvolvedInto)}!"
            : update.Message;

        // Back to the list. Drinking the last Potion while standing on it would
        // otherwise leave the cursor pointing at something that is no longer there.
        _choosingWho = false;
        _offered = null;

        Clamp();
    }

    /// <summary>A machine has asked which of four moves to drop.</summary>
    public void Apply(MoveOffered offer)
    {
        _offered = (offer.Slot, offer.MoveId);
        _forget = 0;
        _message = "";
    }

    private string SpeciesNamed(int species) =>
        GameText.ToAscii(_data.SpeciesAt(species)?.Name ?? $"species {species}");

    private string MoveNamed(int moveId) =>
        GameText.ToAscii(_data.MoveAt(moveId)?.Name ?? $"move {moveId}");

    private IReadOnlyList<int> OfferedTo =>
        _offered is { } offer && offer.Slot >= 0 && offer.Slot < _party.Count
            ? _party[offer.Slot].Moves
            : [];

    private List<BagEntry> Usable() => [.. _bag.Where(e => e.Count > 0)];

    private void Clamp()
    {
        List<BagEntry> lines = Usable();

        _row = lines.Count == 0 ? 0 : Math.Clamp(_row, 0, lines.Count - 1);
        _member = _party.Count == 0 ? 0 : Math.Clamp(_member, 0, _party.Count - 1);
    }

    public void Update()
    {
        // The question comes first, because it is a question. Walking away from an open
        // one would leave the server holding an offer nothing will ever answer.
        if (_offered is { } asking)
        {
            ChooseForget(asking);
            return;
        }

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

        // Handing over and taking back, which only mean anything once somebody has been
        // picked. Their own keys rather than a mode: using a Potion on somebody and
        // giving them one to carry are opposite things about the same item, and a single
        // key would have to guess which was meant.
        if (_choosingWho && lines.Count > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.G) && _data.CanBeHeld(lines[_row].ItemId))
            {
                Pending = new GiveItemRequest(lines[_row].ItemId, _member);
                return;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.T))
            {
                Pending = new TakeItemRequest(_member);
                return;
            }
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        if (!_choosingWho)
        {
            _choosingWho = true;
            _message = "";
            return;
        }

        Pending = new UseItemRequest(lines[_row].ItemId, _member);
    }

    /// <summary>
    /// Which of the four to lose, or none of them.
    /// <para>
    /// Backing out is an answer rather than an escape, and it is sent as one: the games
    /// let you keep what you have, and the server needs to be told so that the offer
    /// stops standing.
    /// </para>
    /// </summary>
    private void ChooseForget((int Slot, int MoveId) asking)
    {
        IReadOnlyList<int> moves = OfferedTo;
        int rows = moves.Count + 1;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _forget = (_forget + 1) % rows;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _forget = (_forget - 1 + rows) % rows;

        bool keep = Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X);

        if (!keep && !Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        Pending = new LearnMoveRequest(asking.MoveId, keep || _forget >= moves.Count ? -1 : _forget);

        _offered = null;
    }

    private static PixelFont Font => Skin.Font;

    /// <summary>
    /// The bag, drawn the way the battle screen is drawn.
    /// <para>
    /// Two columns, because the bag is two questions — what, and on whom — and the
    /// second is only ever asked about the first. Which one is being asked is shown by
    /// which column is lit rather than by moving anything about: a list that jumps
    /// across the screen when you press a button is a list you have to find again.
    /// </para>
    /// </summary>
    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        List<BagEntry> lines = Usable();

        if (_offered is { } asking)
        {
            DrawForgetMenu(asking);
            return;
        }

        Font.Draw(_choosingWho ? "USE ON WHO?" : "BAG", 40, 30, 3, Skin.Ink);

        var carrying = new Rectangle(32, 76, Width / 2 - 56, Height - 168);
        var party = new Rectangle(Width / 2 + 24, 76, Width / 2 - 56, Height - 168);

        Skin.DrawPanel(carrying);
        Skin.DrawPanel(party);

        // The column being asked about is the lit one. Without this the two lists look
        // equally live and the cursor is the only clue which one a key press reaches.
        Skin.DrawCutBorder(_choosingWho ? party : carrying, Skin.Accent);

        if (lines.Count == 0)
        {
            Font.Draw("You are not carrying anything.", carrying.X + 22, carrying.Y + 26, 2, Skin.InkDim);
        }

        int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, lines.Count - Rows)));

        for (int i = first; i < lines.Count && i < first + Rows; i++)
        {
            float y = carrying.Y + 18 + (i - first) * 30;
            bool selected = i == _row;

            if (selected && !_choosingWho)
                Skin.DrawSelection(new Rectangle(carrying.X + 10, y - 5, carrying.Width - 20, 26));

            Font.Draw(
                GameText.ToAscii(_items.Of(lines[i].ItemId)),
                carrying.X + 24, y, 2,
                selected && !_choosingWho ? Skin.Ink : Skin.InkDim);

            // Right-aligned inside its own panel, which is the arrangement that cannot
            // collide with the column beside it however long a name gets.
            Font.DrawRight($"x{lines[i].Count}", carrying.X + carrying.Width - 20, y, 2, Skin.InkFaint);
        }

        // More than fits, said out loud. A list that silently ends at eight is a bag
        // with things in it the player has no reason to believe are there.
        if (lines.Count > Rows)
        {
            Font.DrawRight(
                $"{_row + 1}/{lines.Count}", carrying.X + carrying.Width - 20,
                carrying.Y + carrying.Height - 24, 2, Skin.InkFaint);
        }

        for (int i = 0; i < _party.Count; i++)
        {
            SavedMon member = _party[i];

            float y = party.Y + 18 + i * Row;
            bool selected = i == _member && _choosingWho;

            if (selected)
                Skin.DrawSelection(new Rectangle(party.X + 10, y - 5, party.Width - 20, Row - 4));

            string name = GameText.ToAscii(
                member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}");

            // Whether the highlighted machine works on this one, said while the player is
            // choosing rather than after they have chosen. The server refuses it either
            // way; this is the interface declining to offer something it already knows
            // the answer to, which is the same reason a machine it cannot find is offered
            // to everybody.
            bool refused =
                _choosingWho
                && _row < lines.Count
                && _data.IsMachine(lines[_row].ItemId)
                && !_data.CanBeTaught(member.Species, lines[_row].ItemId);

            Color ink = refused
                ? Skin.InkFaint
                : _choosingWho ? selected ? Skin.Ink : Skin.InkDim : Skin.InkFaint;

            Font.Draw(name, party.X + 24, y, 2, ink);
            Font.DrawRight($"Lv{member.Level}", party.X + party.Width - 20, y, 2, ink);

            int most = Math.Max(1, MaxHpOf(member));

            // The bar stops short of the numbers rather than running under them, which
            // is what it did: a full green bar with "139/139" printed on top of it.
            Skin.DrawMeter(
                new Rectangle(party.X + 24, y + 22, party.Width - 190, 6),
                member.CurrentHp / (float)most,
                Skin.HealthColour(member.CurrentHp, most));

            Font.DrawRight(
                member.CurrentHp <= 0 ? "fainted" : $"{member.CurrentHp}/{most}",
                party.X + party.Width - 20, y + 18, 2,
                member.CurrentHp <= 0 ? Skin.HpPoor : Skin.InkFaint);

            // And what it is carrying, under the bar rather than beside the name — a
            // name can be ten letters and an item name twelve, and the first attempt put
            // BLACK BELT straight through the middle of the health bar.
            if (member.HeldItem != 0)
            {
                Font.Draw(
                    GameText.ToAscii(_items.Of(member.HeldItem)),
                    party.X + 24, y + 34, 2, Skin.Accent);
            }

            // Right-aligned on the same line, so it cannot run into a held item's name.
            if (refused)
                Font.DrawRight("can't learn it", party.X + party.Width - 20, y + 34, 2, Skin.HpPoor);
        }

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 74, 2, Skin.HpGood);

        // The give key is offered only for things that can be carried, so the line does
        // not advertise handing somebody a bicycle.
        string handing = lines.Count > 0 && _data.CanBeHeld(lines[_row].ItemId) ? "G give    " : "";

        DrawKeys(_choosingWho ? $"Z use    {handing}T take    X back" : "Z choose    X close");
    }

    /// <summary>The line along the bottom that says which keys do anything.</summary>
    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 42, 2, Skin.InkFaint);

    /// <summary>Maximum health, which the save does not carry and the rules can work out.</summary>
    private int MaxHpOf(SavedMon member) =>
        PartyBuilder.Restore(_data, member) is { } battler ? battler.MaxHp : Math.Max(1, member.CurrentHp);

    private void DrawForgetMenu((int Slot, int MoveId) asking)
    {
        IReadOnlyList<int> moves = OfferedTo;

        string who = asking.Slot >= 0 && asking.Slot < _party.Count
            ? GameText.ToAscii(
                _party[asking.Slot].Nickname
                ?? _data.SpeciesAt(_party[asking.Slot].Species)?.Name
                ?? $"species {_party[asking.Slot].Species}")
            : "It";

        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw($"{who} already knows four moves.", 40, 30, 3, Skin.Ink);
        Font.Draw($"Forget one to make room for {MoveNamed(asking.MoveId)}?", 40, 70, 2, Skin.InkDim);

        var box = new Rectangle(32, 108, Width - 64, moves.Count * 34 + 60);

        Skin.DrawPanel(box);

        for (int i = 0; i <= moves.Count; i++)
        {
            float y = box.Y + 20 + i * 34;
            bool selected = i == _forget || (i == moves.Count && _forget >= moves.Count);

            if (selected) Skin.DrawSelection(new Rectangle(box.X + 12, y - 6, box.Width - 24, 30));

            string label = i < moves.Count
                ? MoveNamed(moves[i])
                : $"Keep all four, and do not learn {MoveNamed(asking.MoveId)}";

            Font.Draw(label, box.X + 28, y, 2, i < moves.Count ? Skin.Ink : Skin.InkDim);
        }

        DrawKeys("Z choose    X keep all four");
    }
}
