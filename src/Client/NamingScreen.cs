using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// Where a player names something they have just been given.
/// <para>
/// This one has to be ours. The cartridge's keyboard is at 0x081A74EB and that address
/// is not script — it is ARM code, the same kind of thing a <c>special</c> is, and no
/// amount of adopting command widths will ever decode it. So the script's <c>call</c>
/// returns, the story carries on, and this stands in for the screen it would have shown.
/// </para>
/// <para>
/// It is drawn over the world rather than in place of it, because that is where the
/// question was asked and a screen that replaces everything reads as a different scene.
/// </para>
/// </summary>
public sealed class NamingScreen
{
    /// <summary>
    /// As long as a name in the cartridge's own list of them, which is ten letters.
    /// <para>
    /// Read from the image rather than chosen: the suggestions are the longest names
    /// this game ever writes into a name field, so the longest of them is the field's
    /// width, and a player can have whatever the cartridge could have.
    /// </para>
    /// </summary>
    private readonly int _limit;

    private readonly string _prompt;

    private string _name;

    public NamingScreen(int slot, string species, IReadOnlyList<string> suggestions)
    {
        Slot = slot;
        Species = species;

        _limit = Math.Max(10, suggestions.Count == 0 ? 10 : suggestions.Max(n => n.Length));
        _prompt = $"Name your {species}?";
        _name = species;
    }

    /// <summary>Which party slot is being named. The script wrote it into 0x8004.</summary>
    public int Slot { get; }

    /// <summary>
    /// What the script goes on to do once the name is settled.
    /// <para>
    /// Held rather than played, because the keyboard sits in the middle of the run: the
    /// call the cartridge makes to it is followed by the goto that leads to the rival
    /// taking his own. Playing that before the player has typed anything would have the
    /// rival walk over while the box is still open.
    /// </para>
    /// </summary>
    public PokeMmo.RomExtract.Scripts.ScriptRun? Rest { get; init; }

    public string Species { get; }

    /// <summary>The name settled on, once <see cref="IsFinished"/>.</summary>
    public string Name => _name.Trim();

    public bool IsFinished { get; private set; }

    /// <summary>
    /// True when the player left it as it was.
    /// <para>
    /// Worth telling apart from a name that happens to match. A monster with no nickname
    /// and one nicknamed after its own species look identical on every screen, but only
    /// one of them is still called whatever its species is called in a language the
    /// player has not chosen yet.
    /// </para>
    /// </summary>
    public bool Unchanged => Name.Length == 0 || Name == Species;

    public void Update()
    {
        if (IsFinished) return;

        foreach (char typed in Typed())
        {
            if (_name.Length < _limit) _name += typed;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _name.Length > 0)
            _name = _name[..^1];

        // Enter and Escape, and deliberately not Z and X. Z closes every other box in
        // this game and the temptation to keep it here is strong, but this is a field
        // somebody is typing a name into: the first run of it produced a BULBASAUR
        // called BULBASAURz, because the key that confirmed it was also a letter.
        //
        // In a text field a letter is a letter. That is not a compromise, it is the
        // only rule that does not surprise somebody named Zac.
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) IsFinished = true;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _name = Species;
            IsFinished = true;
        }
    }

    /// <summary>
    /// The naming box, in the same skin as every other box.
    /// <para>
    /// It used to be white with black lettering — the only screen in the game drawn that
    /// way, because it was written before there was a skin to draw it in. The field is
    /// still the lightest thing on the screen, which is the one part of the old version
    /// worth keeping: it is the one place a player is typing rather than choosing.
    /// </para>
    /// </summary>
    public void Draw(int width, int height)
    {
        const int boxHeight = 150;

        PixelFont font = Skin.Font;

        var box = new Rectangle(24, height - boxHeight - 24, width - 48, boxHeight);

        Skin.DrawPanel(box);

        font.Draw(_prompt, box.X + 24, box.Y + 20, 3, Skin.Ink);

        var field = new Rectangle(box.X + 24, box.Y + 62, box.Width - 48, 40);

        Skin.DrawPanel(field, raised: false, fill: Skin.PanelHigh);
        Skin.DrawCutBorder(field, Skin.Accent);

        font.Draw(_name, field.X + 14, field.Y + 13, 3, Skin.Ink);

        // A caret, so an empty field looks like somewhere to type rather than like
        // something that has gone wrong. It blinks for the same reason the login screen's
        // does: a still one reads as a letter.
        if ((float)Raylib.GetTime() % 1.0f < 0.6f)
        {
            Raylib.DrawRectangle(
                (int)field.X + 16 + font.Measure(_name, 3), (int)field.Y + 10, 3, 24, Skin.Accent);
        }

        font.Draw(
            "Enter to keep this name    Esc to leave it as it is",
            box.X + 24, box.Y + boxHeight - 28, 2, Skin.InkFaint);
    }

    /// <summary>
    /// What the player typed, in order, letters and digits only.
    /// <para>
    /// The same queue the sign-in screen reads. Filtered because this name goes into a
    /// save and onto other players' screens, and a field that accepts anything a
    /// keyboard can produce is a field somebody will put a control character in.
    /// </para>
    /// </summary>
    private static IEnumerable<char> Typed()
    {
        for (int code = Raylib.GetCharPressed(); code != 0; code = Raylib.GetCharPressed())
        {
            if (code is >= 32 and < 127 && (char.IsLetterOrDigit((char)code) || code == ' '))
                yield return (char)code;
        }
    }
}
