using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The party on one side, the shelf on the other, and how far off an egg is.
/// <para>
/// Built like the box screen, because it is the same act: two lists, one question, and the
/// lit column says which list a key press reaches. What is different is the third thing on
/// it — a wait, measured in steps, that is the only reason to leave anybody here.
/// </para>
/// <para>
/// Opened by talking to somebody who minds creatures, which is a person located off the
/// cartridge at export rather than a square or a key. That is why this screen has no key
/// of its own and the box does: a box is a room, and this is a person.
/// </para>
/// </summary>
public sealed class DaycareScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 6;
    private const int Row = 44;

    private readonly GameData _data;

    private IReadOnlyList<SavedMon> _party;
    private IReadOnlyList<SavedMon> _minded;
    private int _holds;
    private int _steps;
    private string _message;

    private bool _onTheShelf;
    private int _partyRow;
    private int _shelfRow;

    public DaycareScreen(DaycareUpdated opened, GameData data)
    {
        _party = opened.Party;
        _minded = opened.Minded;
        _holds = opened.Holds;
        _steps = opened.StepsToEgg;
        _message = opened.Message;
        _data = data;
    }

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(DaycareUpdated update)
    {
        _party = update.Party;
        _minded = update.Minded;
        _holds = update.Holds;
        _steps = update.StepsToEgg;
        _message = update.Message;

        Clamp();
    }

    private void Clamp()
    {
        _partyRow = _party.Count == 0 ? 0 : Math.Clamp(_partyRow, 0, _party.Count - 1);
        _shelfRow = _minded.Count == 0 ? 0 : Math.Clamp(_shelfRow, 0, _minded.Count - 1);

        // An empty shelf has nothing to point at, so the cursor goes back to the side that
        // does — the same trap the box screen closed. Sitting on an empty column is a
        // screen that appears not to respond.
        if (_onTheShelf && _minded.Count == 0) _onTheShelf = false;
    }

    public void Update()
    {
        // Not the key that opened it: this screen is made and updated inside the same
        // frame, so the press that talked to the attendant is still down when this asks.
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
        {
            _onTheShelf = false;
            _message = "";
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) && _minded.Count > 0)
        {
            _onTheShelf = true;
            _message = "";
        }

        int count = _onTheShelf ? _minded.Count : _party.Count;

        if (count == 0) return;

        ref int cursor = ref _onTheShelf ? ref _shelfRow : ref _partyRow;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            cursor = (cursor + 1) % count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            cursor = (cursor - 1 + count) % count;

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        // A slot and a direction, and nothing else. Whether it is allowed is the server's,
        // which is why this screen never refuses anything itself — it asks, and what comes
        // back is either a changed shelf or a sentence saying why not.
        Pending = _onTheShelf ? new DaycareRequest(_shelfRow, false) : new DaycareRequest(_partyRow, true);
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("DAY CARE", 40, 30, 3, Skin.Ink);
        Font.DrawRight($"{_minded.Count}/{_holds}", Width - 40, 38, 2, Skin.InkFaint);

        var party = new Rectangle(32, 76, Width / 2 - 56, Height - 208);
        var shelf = new Rectangle(Width / 2 + 24, 76, Width / 2 - 56, Height - 208);

        Skin.DrawPanel(party);
        Skin.DrawPanel(shelf);
        Skin.DrawCutBorder(_onTheShelf ? shelf : party, Skin.Accent);

        Column(party, "PARTY", _party, _partyRow, !_onTheShelf, "Nobody.");
        Column(shelf, "MINDED", _minded, _shelfRow, _onTheShelf, "Nobody here yet.");

        DrawTheWait();

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 74, 2, Skin.HpGood);

        DrawKeys(_onTheShelf ? "Z take back    < party    X leave" : "Z leave here    shelf >    X leave");
    }

    /// <summary>
    /// The one thing this screen has that the box screen does not.
    /// <para>
    /// Said three ways, because there are three states and running them together is how a
    /// player ends up leaving two creatures that will never produce anything and waiting
    /// for a message that is not coming. Nothing here is a guess: the number is the
    /// server's, and the server read it off the species record.
    /// </para>
    /// </summary>
    private void DrawTheWait()
    {
        string line =
            _steps > 0 ? $"An egg in about {_steps} steps."
            : _minded.Count < _holds ? "Two of them, and an egg may follow."
            : "Those two will not produce an egg.";

        Color ink = _steps > 0 ? Skin.Accent : Skin.InkDim;

        Font.Draw(line, 40, Height - 110, 2, ink);
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

            // Which sex, because it is the whole of why one pair works and another does
            // not, and this is the only screen in the game where it decides anything.
            Font.Draw(Sexed(member.Sex), panel.X + 24, y + 20, 2, Skin.InkFaint);
        }
    }

    private static string Sexed(Gender sex) => sex switch
    {
        Gender.Male => "male",
        Gender.Female => "female",
        _ => "neither",
    };

    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 42, 2, Skin.InkFaint);
}
