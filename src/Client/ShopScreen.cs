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

    private bool _selling;
    private int _row;
    private int _quantity = 1;
    private string _message = "";

    public ShopScreen(ShopOpened opened, ItemNames names)
    {
        _names = names;
        _stock = opened.Stock;
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
    private List<(int ItemId, int Price, int Held)> Lines() =>
        _selling
            ? _bag.Select(b => (b.ItemId, 0, b.Count)).ToList()
            : _stock.Select(s => (s.ItemId, s.Price, Held(s.ItemId))).ToList();

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
            _selling = !_selling;
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

            Pending = _selling
                ? new SellRequest(itemId, _quantity)
                : new BuyRequest(itemId, _quantity);
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(new Color(28, 32, 44, 255));

        Raylib.DrawText(_selling ? "SELLING" : "BUYING", 40, 32, 28, Color.White);
        Raylib.DrawText($"Money: {Money}", Width - 300, 32, 28, new Color(240, 220, 140, 255));

        List<(int ItemId, int Price, int Held)> lines = Lines();

        if (lines.Count == 0)
        {
            Raylib.DrawText(
                _selling ? "You have nothing to sell." : "There is nothing for sale here.",
                40, 100, 24, new Color(180, 180, 190, 255));
        }

        int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, lines.Count - Rows)));

        for (int i = first; i < lines.Count && i < first + Rows; i++)
        {
            (int itemId, int price, int held) = lines[i];

            int y = 96 + (i - first) * 34;
            bool selected = i == _row;

            if (selected) Raylib.DrawText(">", 40, y, 24, Color.White);

            Raylib.DrawText(
                _names.Of(itemId),
                72, y, 24,
                selected ? Color.White : new Color(190, 190, 200, 255));

            string right = _selling ? $"x{held}" : $"{price}   (have {held})";

            Raylib.DrawText(right, Width - 320, y, 24, new Color(190, 190, 200, 255));
        }

        Raylib.DrawText($"How many: {_quantity}", 40, Height - 132, 24, Color.White);

        if (_message.Length > 0)
            Raylib.DrawText(_message, 40, Height - 96, 22, new Color(160, 220, 160, 255));

        Raylib.DrawText(
            "up/down choose   left/right how many   Z confirm   Tab buy/sell   X leave",
            40, Height - 52, 18, new Color(130, 130, 145, 255));
    }
}
