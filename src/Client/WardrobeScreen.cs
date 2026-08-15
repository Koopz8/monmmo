using PokeMmo.Core.Cosmetics;
using PokeMmo.Core.Net;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The slots on one side, what you own for the lit one on the other.
/// <para>
/// Built the way the box is built, and for the same reason: two lists, one question, and
/// the lit column says which list a key press reaches.
/// </para>
/// <para>
/// It exists because until now the only way to put a hat on was an operator command, which
/// is the difference between machinery that works and a feature that anybody has. Nothing
/// here decides anything — every choice is a <see cref="WearRequest"/>, and the server
/// refuses the ones this account has not got.
/// </para>
/// </summary>
public sealed class WardrobeScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Row = 44;

    private static readonly CosmeticSlot[] Slots = Enum.GetValues<CosmeticSlot>();

    private IReadOnlyList<int> _owned;
    private Appearance _looks;

    private bool _inChoices;
    private int _slotRow;
    private int _choiceRow;

    public WardrobeScreen(IReadOnlyList<int> owned, Appearance looks)
    {
        _owned = owned;
        _looks = looks;
    }

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    /// <summary>
    /// What the server says is on, which is not always what was asked for — a dress takes
    /// a shirt and a pair of trousers off with it, and the screen has to show that rather
    /// than what the player pressed.
    /// </summary>
    public void Apply(Appearance looks) => _looks = looks;

    public void Apply(IReadOnlyList<int> owned)
    {
        _owned = owned;
        Clamp();
    }

    /// <summary>What this account owns in the slot the cursor is on, in catalogue order.</summary>
    private IReadOnlyList<Cosmetic> Choices =>
        [.. Wardrobe.For(Slots[_slotRow]).Where(c => _owned.Contains(c.Id))];

    private void Clamp()
    {
        _slotRow = Math.Clamp(_slotRow, 0, Slots.Length - 1);

        int count = Choices.Count;

        _choiceRow = count == 0 ? 0 : Math.Clamp(_choiceRow, 0, count - 1);

        // A slot with nothing in it has nothing to point at, so the cursor goes back to
        // the side that does. Sitting on an empty column is a screen that looks broken.
        if (_inChoices && count == 0) _inChoices = false;
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
            _inChoices = false;

        if ((Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D))
            && Choices.Count > 0)
        {
            _inChoices = true;
            _choiceRow = 0;
        }

        int rows = _inChoices ? Choices.Count : Slots.Length;

        if (rows == 0) return;

        ref int cursor = ref _inChoices ? ref _choiceRow : ref _slotRow;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            cursor = (cursor + 1) % rows;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            cursor = (cursor - 1 + rows) % rows;

        Clamp();

        // Taking a slot off, from either column, because a player who wants a hat gone
        // should not have to find the hat first.
        if (Raylib.IsKeyPressed(KeyboardKey.T))
        {
            Pending = new WearRequest(0, Slots[_slotRow]);
            return;
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Z) && !Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        if (!_inChoices)
        {
            if (Choices.Count > 0) { _inChoices = true; _choiceRow = 0; }
            return;
        }

        Cosmetic chosen = Choices[_choiceRow];

        Pending = new WearRequest(chosen.Id, chosen.Slot);
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw("WARDROBE", 40, 30, 3, Skin.Ink);
        Font.DrawRight($"{_owned.Count} owned", Width - 40, 38, 2, Skin.InkFaint);

        var slots = new Rectangle(32, 76, Width / 2 - 56, Height - 168);
        var choices = new Rectangle(Width / 2 + 24, 76, Width / 2 - 56, Height - 168);

        Skin.DrawPanel(slots);
        Skin.DrawPanel(choices);
        Skin.DrawCutBorder(_inChoices ? choices : slots, Skin.Accent);

        DrawSlots(slots);
        DrawChoices(choices);

        // What all of that adds up to, drawn at eight times life size in the corner. A
        // wardrobe that lists what you are wearing and does not show it is a wardrobe with
        // no mirror in it, and every one of these choices was made blind until now.
        DrawMirror(Width - 156, 96, 8);

        DrawKeys(_inChoices ? "Z wear    T take off    < slots    X close" : "Z open    T take off    slots >    X close");
    }

    /// <summary>
    /// The figure as it now is, from the front, drawn from the same art the map uses.
    /// <para>
    /// Not the cartridge's own walking sprite: this screen has no cartridge and does not
    /// want one. It is the outline of a person and everything worn over it, which is
    /// enough to tell a red cap from a straw one.
    /// </para>
    /// </summary>
    private void DrawMirror(float x, float y, float scale)
    {
        Raylib.DrawRectangleRec(
            new Rectangle(x - 8, y - 8, Patch.BoxWidth * scale + 16, Patch.BoxHeight * scale + 16),
            Skin.Panel);

        // The person under the clothes. Invented like the clothes, and the one drawing in
        // this project that is allowed to be a rectangle on purpose.
        Raylib.DrawRectangleRec(new Rectangle(x + 4 * scale, y + 4 * scale, 8 * scale, 8 * scale), Skin.Person);
        Raylib.DrawRectangleRec(new Rectangle(x + 4 * scale, y + 13 * scale, 8 * scale, 9 * scale), Skin.Person);
        Raylib.DrawRectangleRec(new Rectangle(x + 5 * scale, y + 22 * scale, 2 * scale, 9 * scale), Skin.Person);
        Raylib.DrawRectangleRec(new Rectangle(x + 9 * scale, y + 22 * scale, 2 * scale, 9 * scale), Skin.Person);

        foreach (bool behind in new[] { true, false })
            foreach ((CosmeticSlot _, int id) in _looks.InDrawingOrder())
            {
                if (CosmeticArt.GoesBehind(id, Aspect.Front) != behind) continue;

                foreach (Patch patch in CosmeticArt.For(id, Aspect.Front))
                    Raylib.DrawRectangleRec(
                        new Rectangle(x + patch.X * scale, y + patch.Y * scale, patch.Width * scale, patch.Height * scale),
                        new Color(patch.R, patch.G, patch.B, (byte)255));
            }
    }

    private void DrawSlots(Rectangle panel)
    {
        Font.Draw("SLOT", panel.X + 22, panel.Y + 14, 2, !_inChoices ? Skin.Ink : Skin.InkFaint);

        for (int i = 0; i < Slots.Length; i++)
        {
            float y = panel.Y + 46 + i * Row * 0.74f;

            if (i == _slotRow && !_inChoices)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 4, panel.Width - 20, Row - 16));

            int worn = _looks.In(Slots[i]);

            Font.Draw(Slots[i].ToString().ToUpperInvariant(), panel.X + 24, y, 2, Skin.Ink);

            Font.DrawRight(
                worn == 0 ? "-" : Wardrobe.At(worn)?.Name ?? $"#{worn}",
                panel.X + panel.Width - 22, y, 2, worn == 0 ? Skin.InkDim : Skin.Accent);
        }
    }

    private void DrawChoices(Rectangle panel)
    {
        IReadOnlyList<Cosmetic> choices = Choices;

        Font.Draw(
            Slots[_slotRow].ToString().ToUpperInvariant(),
            panel.X + 22, panel.Y + 14, 2, _inChoices ? Skin.Ink : Skin.InkFaint);

        if (choices.Count == 0)
        {
            Font.Draw("Nothing for this slot yet.", panel.X + 22, panel.Y + 52, 2, Skin.InkDim);
            return;
        }

        for (int i = 0; i < choices.Count; i++)
        {
            float y = panel.Y + 52 + i * Row;

            if (i == _choiceRow && _inChoices)
                Skin.DrawSelection(new Rectangle(panel.X + 10, y - 5, panel.Width - 20, Row - 6));

            bool on = _looks.In(choices[i].Slot) == choices[i].Id;

            Font.Draw(choices[i].Name, panel.X + 24, y, 2, on ? Skin.Accent : Skin.Ink);

            if (on) Font.DrawRight("worn", panel.X + panel.Width - 22, y, 2, Skin.Accent);
        }
    }

    private static void DrawKeys(string keys) => Font.Draw(keys, 40, Height - 40, 2, Skin.InkFaint);
}
