using PokeMmo.Core.Net;
using PokeMmo.RomExtract;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// A guild, as something to look at.
/// <para>
/// It decides nothing, like every other screen here. Who is in a guild and who leads it come
/// off the server; every act is a request; what comes back is the whole guild again. A
/// screen that is wrong is a screen one message behind, never one that has put somebody in
/// two guilds.
/// </para>
/// <para>
/// Two states on one screen, because they are the same question from either side of having a
/// guild: the roster when there is one, and the offers to join when there is not. The second
/// is the state a new player is in and the one a screen that only knew about rosters would
/// have nothing to say to.
/// </para>
/// <para>
/// Typing happens here for two different things — a name to found under and a name to invite
/// — so the prompt says which it is asking for rather than leaving it to be inferred from
/// what was pressed a moment ago.
/// </para>
/// </summary>
public sealed class GuildScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int Rows = 10;
    private const int Row = 34;

    private GuildOpened _guild;
    private int _row;

    /// <summary>What is being typed, and what it is for. Null when nothing is.</summary>
    private GuildAsk? _typingFor;

    private string _typed = "";

    public GuildScreen(GuildOpened opened) => _guild = opened;

    public bool IsClosed { get; private set; }

    public NetMessage? Pending { get; private set; }

    public NetMessage? TakePending()
    {
        NetMessage? pending = Pending;
        Pending = null;
        return pending;
    }

    public void Apply(GuildOpened update)
    {
        _guild = update;

        // Whatever was half-typed belonged to a guild that has since changed under it.
        _typingFor = null;
        _typed = "";

        Clamp();
    }

    private int Count => _guild.Exists ? _guild.Members.Count : _guild.Invitations.Count;

    private void Clamp() => _row = Count == 0 ? 0 : Math.Clamp(_row, 0, Count - 1);

    public void Update()
    {
        if (_typingFor is not null)
        {
            TypeAName();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            IsClosed = true;
            return;
        }

        if (Count > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
                _row = (_row + 1) % Count;

            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
                _row = (_row - 1 + Count) % Count;
        }

        Clamp();

        if (!_guild.Exists)
        {
            // Nothing to lead and nothing to leave. The two things somebody without a guild
            // can do are take up an offer and start one.
            if (Raylib.IsKeyPressed(KeyboardKey.Z) && _guild.Invitations.Count > 0)
                Pending = new GuildRequest(GuildAsk.Join, _guild.Invitations[_row]);

            if (Raylib.IsKeyPressed(KeyboardKey.N)) Asking(GuildAsk.Found);

            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.L)) Pending = new GuildRequest(GuildAsk.Leave);

        // The two a leader has, and nobody else. Hidden rather than refused, because a
        // refusal that arrives a keypress after the decision is worse than never offering.
        if (!_guild.IsLeader) return;

        if (Raylib.IsKeyPressed(KeyboardKey.I)) Asking(GuildAsk.Invite);

        if (Raylib.IsKeyPressed(KeyboardKey.K)
            && _guild.Members.Count > _row
            && !_guild.Members[_row].IsLeader)
        {
            Pending = new GuildRequest(GuildAsk.Kick, _guild.Members[_row].Name);
        }
    }

    private void Asking(GuildAsk what)
    {
        _typingFor = what;
        _typed = "";
    }

    /// <summary>
    /// Letters, digits, spaces, backspace, enter, escape. Nothing else, which is what makes
    /// it safe to leave the rest of the keyboard out while this is open.
    /// </summary>
    private void TypeAName()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.X))
        {
            _typingFor = null;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _typed.Length > 0)
            _typed = _typed[..^1];

        for (int typed = Raylib.GetCharPressed(); typed != 0; typed = Raylib.GetCharPressed())
        {
            if ((char.IsLetterOrDigit((char)typed) || typed == ' ') && _typed.Length < 20)
                _typed += (char)typed;
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Enter)) return;

        if (_typed.Trim().Length > 0) Pending = new GuildRequest(_typingFor!.Value, _typed.Trim());

        _typingFor = null;
    }

    private static PixelFont Font => Skin.Font;

    public void Draw()
    {
        Raylib.ClearBackground(Skin.PanelDeep);

        Font.Draw(_guild.Exists ? GameText.ToAscii(_guild.Name) : "NO GUILD", 40, 26, 3, Skin.Ink);

        if (_guild.Exists)
        {
            Font.DrawRight(
                _guild.IsLeader ? "you lead this one" : $"{_guild.Members.Count} member(s)",
                Width - 40, 36, 2, Skin.InkFaint);
        }

        var list = new Rectangle(32, 96, Width - 64, Height - 210);

        Skin.DrawPanel(list);

        if (Count == 0)
        {
            Font.Draw(
                _guild.Exists ? "Nobody here." : "Nobody has asked you. Press N to start one.",
                list.X + 24, list.Y + 26, 2, Skin.InkDim);
        }

        int first = Math.Max(0, Math.Min(_row - Rows / 2, Math.Max(0, Count - Rows)));

        for (int i = first; i < Count && i < first + Rows; i++)
        {
            float y = list.Y + 18 + (i - first) * Row;

            if (i == _row) Skin.DrawSelection(new Rectangle(list.X + 12, y - 5, list.Width - 24, Row - 4));

            Color ink = i == _row ? Skin.Ink : Skin.InkDim;

            if (!_guild.Exists)
            {
                Font.Draw(GameText.ToAscii(_guild.Invitations[i]), list.X + 26, y, 2, ink);

                continue;
            }

            GuildFace face = _guild.Members[i];

            Font.Draw(GameText.ToAscii(face.Name), list.X + 26, y, 2, ink);

            if (face.IsLeader) Font.Draw("leader", list.X + 300, y, 2, Skin.Accent);

            // Where somebody is, or that they are not on. The whole point of a guild being a
            // list rather than a name: it tells you who is about.
            Font.DrawRight(
                face.Where.Length > 0 ? GameText.ToAscii(face.Where) : "away",
                list.X + list.Width - 26, y, 2,
                face.Where.Length > 0 ? Skin.HpGood : Skin.InkFaint);
        }

        DrawFooter();
    }

    private void DrawFooter()
    {
        if (_typingFor is { } asking)
        {
            var box = new Rectangle(32, Height - 106, Width - 64, 44);

            Skin.DrawPanel(box, raised: false);
            Skin.DrawCutBorder(box, Skin.Accent);

            Font.Draw(asking == GuildAsk.Found ? "NAME IT" : "WHO", box.X + 18, box.Y + 14, 2, Skin.InkFaint);
            Font.Draw(_typed, box.X + 190, box.Y + 12, 3, Skin.Ink);

            Font.Draw("type   Enter confirm   X back out", 40, Height - 40, 2, Skin.InkFaint);
            return;
        }

        if (_guild.Message.Length > 0) Font.Draw(_guild.Message, 40, Height - 96, 2, Skin.HpGood);

        Font.Draw(
            !_guild.Exists
                ? "up/down choose   Z join   N start one   X leave"
                : _guild.IsLeader
                    ? "up/down choose   I invite   K put out   L leave the guild   X close"
                    : "up/down choose   L leave the guild   X close",
            40, Height - 40, 2, Skin.InkFaint);
    }
}
