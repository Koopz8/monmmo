using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The market, as something to look at rather than something to type at.
/// <para>
/// Three tabs, because a market is three different questions and putting them on one
/// screen would be putting three cursors on it. What is everybody selling; what am I
/// selling; what have I got that I could sell. Each is a list with one cursor, which is
/// the only shape a keyboard can drive without a mouse.
/// </para>
/// <para>
/// It decides nothing. Every price on it came from the server, every act is a request, and
/// what comes back is the whole market again — so a screen that is wrong about something
/// is a screen that is one message behind, never a screen that has made a transaction go
/// wrong. That is the same promise the shop counter makes and it matters more here, because
/// here the money on the other side of the transaction is another player's.
/// </para>
/// <para>
/// The market has no place on the cartridge, because the cartridge has no market. So this
/// has a key of its own rather than a counter to stand at — the honest arrangement until
/// there is a reason to put it somewhere in particular.
/// </para>
/// </summary>
public sealed class MarketScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 9;
    private const int Row = 32;

    /// <summary>The most anybody may ask for one thing. The server has its own opinion.</summary>
    private const int Dearest = 9_999_999;

    private readonly GameData _data;
    private readonly ItemNames _names;

    /// <summary>
    /// Which of the three lists is showing.
    /// <para>
    /// Three states rather than two flags, for the reason the shop counter has three: a
    /// pair of booleans can say "the board and my own listings at once", which is a state
    /// with no meaning that would then have to be prevented everywhere.
    /// </para>
    /// </summary>
    private enum Tab
    {
        Board,
        Mine,
        Selling,
    }

    private Tab _at = Tab.Board;

    private IReadOnlyList<Listing> _board;
    private IReadOnlyList<Listing> _mine;
    private IReadOnlyList<SavedMon> _box;
    private IReadOnlyList<BagEntry> _bag;
    private int _money;
    private int _owed;
    private int _cut;
    private string _message;

    private int _row;

    /// <summary>
    /// The price being typed, when one is. Null the rest of the time, and that is the whole
    /// of the modality: a screen in this state answers digits and nothing else.
    /// </summary>
    private string? _asking;

    /// <summary>How many of a pile to put up, on the selling tab.</summary>
    private int _howMany = 1;

    public MarketScreen(MarketOpened opened, GameData data, ItemNames names)
    {
        _data = data;
        _names = names;
        _board = opened.Board;
        _mine = opened.Mine;
        _box = opened.Box;
        _bag = opened.Bag;
        _money = opened.Money;
        _owed = opened.Owed;
        _cut = opened.Cut;
        _message = opened.Message;
    }

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(MarketOpened update)
    {
        _board = update.Board;
        _mine = update.Mine;
        _box = update.Box;
        _bag = update.Bag;
        _money = update.Money;
        _owed = update.Owed;
        _cut = update.Cut;
        _message = update.Message;

        // Whatever was half-typed belonged to a market that has since changed under it.
        _asking = null;

        Clamp();
    }

    private int Count => _at switch
    {
        Tab.Board => _board.Count,
        Tab.Mine => _mine.Count,
        _ => _box.Count + Sellable.Count,
    };

    /// <summary>
    /// The part of the bag that is worth showing on a market screen.
    /// <para>
    /// Empty stacks are not in a bag at all, so the only filtering here is the one the
    /// shop counter does for the same reason: nothing that will certainly be refused. Key
    /// items are refused by the server and the refusal would arrive a keypress after the
    /// decision, which is worse than never offering them.
    /// </para>
    /// <para>
    /// It cannot tell which those are without the rules, so it asks the same table the rest
    /// of the client names items out of, and shows everything when there is no cartridge.
    /// </para>
    /// </summary>
    private List<BagEntry> Sellable =>
        [.. _bag.Where(e => e.Count > 0 && _data.MayBeSold(e.ItemId))];

    private void Clamp()
    {
        _row = Count == 0 ? 0 : Math.Clamp(_row, 0, Count - 1);
        _howMany = Math.Clamp(_howMany, 1, Math.Max(1, HowManyAtMost));
    }

    /// <summary>How many of the selected pile there are, or one for a creature.</summary>
    private int HowManyAtMost =>
        _at == Tab.Selling && _row >= _box.Count && _row - _box.Count < Sellable.Count
            ? Sellable[_row - _box.Count].Count
            : 1;

    public void Update()
    {
        if (_asking is not null)
        {
            TypeAPrice();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _at = _at switch
            {
                Tab.Board => Tab.Mine,
                Tab.Mine => Tab.Selling,
                _ => Tab.Board,
            };

            _row = 0;
            _howMany = 1;
            _message = "";
            return;
        }

        // Collecting is on its own key rather than on a row, because it is not a thing you
        // pick out of a list — there is only ever one answer to it and it is "all of it".
        if (Raylib.IsKeyPressed(KeyboardKey.C) && _owed > 0)
        {
            Pending = new MarketRequest(MarketAsk.Collect);
            return;
        }

        if (Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _row = (_row + 1) % Count;
            _howMany = 1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _row = (_row - 1 + Count) % Count;
            _howMany = 1;
        }

        if (_at == Tab.Selling)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) _howMany++;
            if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A)) _howMany--;
        }

        Clamp();

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        switch (_at)
        {
            case Tab.Board:
                Pending = new MarketRequest(MarketAsk.Buy) { Listing = _board[_row].Id };
                break;

            case Tab.Mine when !_mine[_row].Sold:
                Pending = new MarketRequest(MarketAsk.Cancel) { Listing = _mine[_row].Id };
                break;

            case Tab.Mine:
                _message = "That one has sold. Press C to collect.";
                break;

            // Selling is the only act here that needs a number nobody can guess, so it is
            // the only one that takes two presses.
            default:
                _asking = "";
                _message = "";
                break;
        }
    }

    /// <summary>
    /// Digits, backspace, enter, escape. Nothing else, which is what makes this safe to
    /// leave the rest of the keyboard out of.
    /// </summary>
    private void TypeAPrice()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            _asking = null;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _asking!.Length > 0)
            _asking = _asking[..^1];

        for (int typed = Raylib.GetCharPressed(); typed != 0; typed = Raylib.GetCharPressed())
        {
            if (typed is >= '0' and <= '9' && _asking!.Length < 8) _asking += (char)typed;
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Enter) && !Raylib.IsKeyPressed(KeyboardKey.Z)) return;

        if (!int.TryParse(_asking, out int price) || price <= 0 || price > Dearest)
        {
            _message = "A price is a number above nought.";
            _asking = null;
            return;
        }

        Pending = _row < _box.Count
            ? new MarketRequest(MarketAsk.SellOne) { Slot = _row, Price = price }
            : new MarketRequest(MarketAsk.SellSome)
            {
                Item = Sellable[_row - _box.Count].ItemId,
                Count = _howMany,
                Price = price,
            };

        _asking = null;
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("MARKET", 40, 26, 3, Skin.Ink);

        DrawTabs();
        DrawPurse();

        var list = new Rectangle(32, 118, Width - 64, Height - 232);

        Skin.DrawPanel(list);

        if (Count == 0)
        {
            Font.Draw(Nothing(), list.X + 24, list.Y + 26, 2, Skin.InkDim);
        }
        else
        {
            int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, Count - Rows)));

            for (int i = first; i < Count && i < first + Rows; i++)
            {
                float y = list.Y + 18 + (i - first) * Row;

                if (i == _row)
                    Skin.DrawSelection(new Rectangle(list.X + 12, y - 5, list.Width - 24, Row - 4));

                DrawRow(i, list, y);
            }
        }

        DrawFooter();
    }

    private string Nothing() => _at switch
    {
        Tab.Board => "Nothing is for sale.",
        Tab.Mine => "You have nothing on the market.",
        _ => "Nothing in the box, and nothing in the bag worth selling.",
    };

    private void DrawTabs()
    {
        string[] names = ["EVERYBODY", "MINE", "SELL"];

        for (int i = 0; i < names.Length; i++)
        {
            bool here = (int)_at == i;

            Skin.DrawChip(
                Font, names[i], 220 + i * 150, 34, 2, here ? Skin.Accent : Skin.InkFaint);
        }
    }

    /// <summary>
    /// The money, and beside it what is waiting to be collected.
    /// <para>
    /// The second number is the one worth putting on the screen rather than leaving to be
    /// discovered: money that has been earned and not fetched is money somebody has
    /// forgotten, and forgetting it is the only way to lose anything at this market.
    /// </para>
    /// </summary>
    private void DrawPurse()
    {
        var purse = new Rectangle(Width - 320, 22, 288, 44);

        Skin.DrawPanel(purse, raised: false);
        Font.DrawRight($"${_money}", purse.X + purse.Width - 18, purse.Y + 15, 3, Skin.HpFair);

        Font.DrawRight(
            _owed > 0 ? $"${_owed} waiting — press C" : $"the market keeps {_cut}%",
            Width - 32, 78, 2, _owed > 0 ? Skin.HpGood : Skin.InkFaint);
    }

    private void DrawRow(int i, Rectangle list, float y)
    {
        bool selected = i == _row;
        Color ink = selected ? Skin.Ink : Skin.InkDim;

        if (_at == Tab.Selling)
        {
            DrawSellable(i, list, y, ink);
            return;
        }

        Listing one = _at == Tab.Board ? _board[i] : _mine[i];

        Font.Draw(Named(one), list.X + 26, y, 2, ink);

        if (!one.IsItem)
        {
            Font.Draw(
                $"Lv{one.Level}  {one.Total}/{Genes.Best * 6}",
                list.X + 320, y, 2, selected ? Skin.Accent : Skin.InkFaint);
        }

        Font.DrawRight($"${one.Price}", list.X + list.Width - 200, y, 2, Skin.HpFair);

        Font.DrawRight(
            one.Sold ? "SOLD" : _at == Tab.Board ? GameText.ToAscii(one.Seller) : "",
            list.X + list.Width - 26, y, 2, one.Sold ? Skin.HpGood : Skin.InkFaint);
    }

    private void DrawSellable(int i, Rectangle list, float y, Color ink)
    {
        if (i < _box.Count)
        {
            SavedMon member = _box[i];

            Font.Draw(NameOf(member), list.X + 26, y, 2, ink);
            Font.Draw($"Lv{member.Level}", list.X + 320, y, 2, Skin.InkFaint);

            return;
        }

        BagEntry entry = Sellable[i - _box.Count];

        Font.Draw(GameText.ToAscii(_names.Of(entry.ItemId)), list.X + 26, y, 2, ink);

        // How many are going up, on the row rather than in a panel of its own, because on
        // this tab it is a property of the row and moving the cursor resets it.
        Font.Draw(
            i == _row ? $"{_howMany} of {entry.Count}" : $"{entry.Count}",
            list.X + 320, y, 2, i == _row ? Skin.Accent : Skin.InkFaint);
    }

    private string Named(Listing one) =>
        one.IsItem
            ? $"{one.Count} x {GameText.ToAscii(_names.Of(one.Item))}"
            : GameText.ToAscii(_data.SpeciesAt(one.Species)?.Name ?? $"species {one.Species}");

    private string NameOf(SavedMon member) =>
        GameText.ToAscii(
            member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}");

    private void DrawFooter()
    {
        if (_asking is not null)
        {
            var box = new Rectangle(32, Height - 106, Width - 64, 44);

            Skin.DrawPanel(box, raised: false);
            Skin.DrawCutBorder(box, Skin.Accent);

            Font.Draw("HOW MUCH", box.X + 18, box.Y + 14, 2, Skin.InkFaint);
            Font.Draw($"${_asking}", box.X + 190, box.Y + 12, 3, Skin.Ink);

            Font.Draw("digits   Enter confirm   X back out", 40, Height - 40, 2, Skin.InkFaint);
            return;
        }

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 96, 2, Skin.HpGood);

        Font.Draw(
            _at switch
            {
                Tab.Board => "up/down choose   Z buy   C collect   Tab list   X leave",
                Tab.Mine => "up/down choose   Z take it back   C collect   Tab list   X leave",
                _ => "up/down choose   left/right how many   Z name a price   Tab list   X leave",
            },
            40, Height - 40, 2, Skin.InkFaint);
    }
}
