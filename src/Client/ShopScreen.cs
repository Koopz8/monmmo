using PokeMmo.RomExtract;
using PokeMmo.Core.Cosmetics;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The counter. Buy on the left, sell on the right, and nothing here decides anything.
/// <para>
/// Every price shown came from the server and every purchase is a request. The screen
/// can be wrong about what something costs and the worst that happens is a surprise —
/// it cannot make the transaction wrong, because it is not the one doing it.
/// </para>
/// </summary>
public sealed class ShopScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 8;

    private readonly ItemNames _names;

    private IReadOnlyList<ShopEntry> _stock;
    private IReadOnlyList<BagEntry> _bag;

    /// <summary>
    /// Which of the three counters is showing.
    /// <para>
    /// Three rather than a pair of flags, because a shop is one counter at a time and two
    /// booleans can say "selling and clothes at once", which is a state with no meaning
    /// and one that would have to be prevented everywhere rather than made unsayable.
    /// </para>
    /// </summary>
    private enum Counter
    {
        Buying,
        Selling,
        Clothes,
    }

    private Counter _at = Counter.Buying;

    private bool _selling => _at == Counter.Selling;

    private IReadOnlyList<ShopEntry> _clothes;
    private int _row;
    private int _quantity = 1;
    private string _message = "";

    public ShopScreen(ShopOpened opened, ItemNames names)
    {
        _names = names;
        _stock = opened.Stock;
        _clothes = opened.Clothes;
        _bag = opened.Bag;
        Money = opened.Money;
    }

    public int Money { get; private set; }

    /// <summary>True once the player has walked away from the counter.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>A request for the game loop to send. Cleared once taken.</summary>
    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(ShopUpdated update)
    {
        Money = update.Money;
        _bag = update.Bag;
        _message = update.Message;

        Clamp();
    }

    /// <summary>
    /// What is on the counter right now: their stock, or the sellable half of the bag.
    /// <para>
    /// Key items are not in the sell list at all. Showing something that will always be
    /// refused is worse than not showing it, because the refusal arrives a keypress after
    /// the decision.
    /// </para>
    /// </summary>
    private List<(int ItemId, int Price, int Held)> Lines() => _at switch
    {
        Counter.Selling => _bag.Select(b => (b.ItemId, 0, b.Count)).ToList(),

        // The id here is a cosmetic and not an item, and the two are different numbering.
        // Nothing on this screen may look one of them up in the other's table — it would
        // come back with a wrong name rather than an error, which is the kind of bug that
        // survives a review. Naming goes through NameOf, which knows which counter it is on.
        Counter.Clothes => _clothes.Select(c => (c.ItemId, c.Price, 0)).ToList(),

        _ => _stock.Select(s => (s.ItemId, s.Price, Held(s.ItemId))).ToList(),
    };

    /// <summary>
    /// What to call the thing in a row, asked of whichever table this counter belongs to.
    /// <para>
    /// The whole reason this method exists rather than a call to <c>_names</c> everywhere:
    /// a cosmetic id and an item id are two numberings that happen to be integers.
    /// </para>
    /// </summary>
    private string NameOf(int id) =>
        _at == Counter.Clothes ? Wardrobe.At(id)?.Name ?? $"cosmetic {id}" : _names.Of(id);

    private int Held(int itemId) => _bag.FirstOrDefault(b => b.ItemId == itemId)?.Count ?? 0;

    private void Clamp()
    {
        int count = Lines().Count;

        _row = count == 0 ? 0 : Math.Clamp(_row, 0, count - 1);
        _quantity = Math.Clamp(_quantity, 1, 99);
    }

    public void Update()
    {
        List<(int ItemId, int Price, int Held)> lines = Lines();

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            // Round the three, and past the clothes counter entirely when there is nothing
            // left to buy from it — a counter that can only say "nothing here" is a stop on
            // the way to somewhere and not a place.
            _at = _at switch
            {
                Counter.Buying => Counter.Selling,
                Counter.Selling when _clothes.Count > 0 => Counter.Clothes,
                _ => Counter.Buying,
            };

            _row = 0;
            _quantity = 1;
            _message = "";
            return;
        }

        if (lines.Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _row = (_row + 1) % lines.Count;
            _quantity = 1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _row = (_row - 1 + lines.Count) % lines.Count;
            _quantity = 1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) _quantity++;
        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A)) _quantity--;

        Clamp();

        if (Raylib.IsKeyPressed(KeyboardKey.Z) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            (int itemId, _, _) = lines[_row];

            // One at a time for clothes, because there is no such thing as two of one:
            // the server refuses anything already owned, and a quantity would only ever be
            // a way to ask for a refusal.
            Pending = _at switch
            {
                Counter.Selling => new SellRequest(itemId, _quantity),
                Counter.Clothes => new BuyCosmeticRequest(itemId),
                _ => new BuyRequest(itemId, _quantity),
            };
        }
    }

    private static PixelFont Font => Skin.Font;

    /// <summary>
    /// The counter, drawn the way the rest of the game is drawn.
    /// <para>
    /// The money is put where it cannot be missed, because it is the number every
    /// decision on this screen is made against, and the quantity is a panel of its own
    /// rather than a line of text: it is the only thing here that changes as you hold a
    /// key, and something that changes under your thumb should look like a control.
    /// </para>
    /// </summary>
    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw(
            _at switch { Counter.Selling => "SELLING", Counter.Clothes => "CLOTHES", _ => "BUYING" },
            40, 30, 3, Skin.Ink);

        var purse = new Rectangle(Width - 300, 22, 268, 44);

        Skin.DrawPanel(purse, raised: false);
        Font.DrawRight($"${Money}", purse.X + purse.Width - 18, purse.Y + 15, 3, Skin.HpFair);

        List<(int ItemId, int Price, int Held)> lines = Lines();

        var stock = new Rectangle(32, 84, Width - 64, Height - 220);

        Skin.DrawPanel(stock);

        if (lines.Count == 0)
        {
            Font.Draw(
                _at switch
                {
                    Counter.Selling => "You have nothing to sell.",
                    Counter.Clothes => "You own everything they have.",
                    _ => "There is nothing for sale here.",
                },
                stock.X + 24, stock.Y + 26, 2, Skin.InkDim);
        }

        int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, lines.Count - Rows)));

        for (int i = first; i < lines.Count && i < first + Rows; i++)
        {
            (int itemId, int price, int held) = lines[i];

            float y = stock.Y + 20 + (i - first) * 32;
            bool selected = i == _row;

            if (selected) Skin.DrawSelection(new Rectangle(stock.X + 12, y - 6, stock.Width - 24, 28));

            Font.Draw(GameText.ToAscii(NameOf(itemId)), stock.X + 28, y, 2, selected ? Skin.Ink : Skin.InkDim);

            // What it costs and what you already have, kept apart: they are different
            // questions and a player reading one should not have to find it inside the
            // other.
            if (!_selling) Font.DrawRight($"${price}", stock.X + stock.Width - 200, y, 2, Skin.HpFair);

            Font.DrawRight(
                held > 0 ? $"have {held}" : "", stock.X + stock.Width - 24, y, 2, Skin.InkFaint);
        }

        var many = new Rectangle(32, Height - 124, 300, 48);

        Skin.DrawPanel(many, raised: false);
        Font.Draw("HOW MANY", many.X + 18, many.Y + 8, 2, Skin.InkFaint);
        Font.DrawRight($"{_quantity}", many.X + many.Width - 18, many.Y + 16, 3, Skin.Ink);

        // What this comes to, which is the number a player is actually deciding about
        // and the one they were being asked to multiply in their head.
        if (lines.Count > 0)
        {
            (int _, int price, int _) = lines[Math.Clamp(_row, 0, lines.Count - 1)];

            Font.Draw(
                _selling ? $"for ${price * _quantity}" : $"costs ${price * _quantity}",
                many.X + many.Width + 24, many.Y + 16, 2,
                !_selling && price * _quantity > Money ? Skin.HpPoor : Skin.InkDim);
        }

        if (_message.Length > 0) Font.Draw(_message, 40, Height - 68, 2, Skin.HpGood);

        Font.Draw(
            _at == Counter.Clothes
                ? "up/down choose   Z buy   Tab counter   X leave"
                : "up/down choose   left/right how many   Z confirm   Tab counter   X leave",
            40, Height - 40, 2, Skin.InkFaint);
    }
}
