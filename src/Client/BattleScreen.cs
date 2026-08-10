using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>What the battle screen is waiting for.</summary>
public enum BattlePhase
{
    ReadingMessages,
    ChoosingMove,
    WaitingForServer,
    Finished,
}

/// <summary>
/// A wild battle, drawn.
/// <para>
/// This screen decides nothing. The server holds the battle, rolls every die and says
/// what happened; this shows it. Health comes down alongside the events rather than
/// being derived from them, because a client that reconstructs state by replaying a
/// narrative will eventually disagree with the server about it.
/// </para>
/// <para>
/// The one thing that happens here and nowhere else is naming. Events arrive as sides
/// and move indices, and the player's own cartridge turns them into words.
/// </para>
/// </summary>
public sealed class BattleScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int SpriteScale = 3;

    private readonly GameData _data;
    private readonly BattleNames _names;
    private readonly List<string> _moveNames = [];

    private readonly Texture2D _wildSprite;
    private readonly Texture2D _playerSprite;
    private readonly bool _hasWildSprite;
    private readonly bool _hasPlayerSprite;

    private readonly Queue<string> _pending = new();
    private string _message = "";
    private int _selectedMove;

    private BattlerView _you;
    private BattlerView _opponent;

    public BattleScreen(BattleStarted start, GameData data)
    {
        _data = data;
        _you = start.You;
        _opponent = start.Opponent;
        Balls = start.Balls;

        string yourName = start.You.Nickname ?? data.SpeciesAt(start.You.Species)?.Name ?? "Your side";
        string wildName = data.SpeciesAt(start.Opponent.Species)?.Name ?? "the wild one";

        _names = new BattleNames(yourName, $"the wild {wildName}", id => data.MoveAt(id)?.Name ?? $"move {id}");

        foreach (int moveId in start.You.Moves)
            _moveNames.Add(data.MoveAt(moveId)?.Name ?? $"move {moveId}");

        (_wildSprite, _hasWildSprite) = LoadSprite(data, start.Opponent.Species, back: false);
        (_playerSprite, _hasPlayerSprite) = LoadSprite(data, start.You.Species, back: true);

        Say($"A wild {wildName} appeared!");
    }

    public BattlePhase Phase { get; private set; } = BattlePhase.ReadingMessages;

    /// <summary>Balls remaining, as the server counts them.</summary>
    public int Balls { get; private set; }

    /// <summary>True once the battle is over and its last message has been read.</summary>
    public bool IsDismissed { get; private set; }

    /// <summary>An action the player chose, for the game loop to send. Cleared once taken.</summary>
    public BattleAction? PendingAction { get; private set; }

    public BattleAction? TakePendingAction()
    {
        BattleAction? action = PendingAction;
        PendingAction = null;
        return action;
    }

    private static (Texture2D Texture, bool Loaded) LoadSprite(GameData data, int species, bool back)
    {
        ExtractedSprite? sprite = data.Sprite(species, back);
        if (sprite is null) return (default, false);

        Image image = Raylib.LoadImageFromMemory(".png", sprite.ToPng());
        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);

        return (texture, true);
    }

    private void Say(string line) => _pending.Enqueue(line);

    /// <summary>Folds a turn's result in: what to read, and where both sides now stand.</summary>
    public void Apply(BattleUpdate update)
    {
        foreach (string line in BattleNarrator.Describe(update.Events, _names))
            _pending.Enqueue(line);

        _you = _you with { CurrentHp = update.YourHp };
        _opponent = _opponent with { CurrentHp = update.OpponentHp };
        Balls = update.Balls;

        Phase = BattlePhase.ReadingMessages;
        AdvanceMessage();
    }

    /// <summary>The battle is over; everything still queued is read before it closes.</summary>
    public void Apply(BattleFinished finished)
    {
        Balls = finished.Balls;
        IsOver = true;

        if (_pending.Count == 0 && _message.Length == 0) Phase = BattlePhase.Finished;
    }

    private bool IsOver { get; set; }

    public void Update()
    {
        switch (Phase)
        {
            case BattlePhase.ReadingMessages:
                if (Confirmed()) AdvanceMessage();
                break;

            case BattlePhase.ChoosingMove:
                ChooseMove();
                break;

            case BattlePhase.WaitingForServer:
                break;

            case BattlePhase.Finished:
                if (Confirmed()) IsDismissed = true;
                break;
        }
    }

    private static bool Confirmed() =>
        Raylib.IsKeyPressed(KeyboardKey.Z) ||
        Raylib.IsKeyPressed(KeyboardKey.Enter) ||
        Raylib.IsKeyPressed(KeyboardKey.Space);

    private void AdvanceMessage()
    {
        if (_pending.Count > 0)
        {
            _message = _pending.Dequeue();
            return;
        }

        // Messages exhausted: the battle is over, or it is the player's turn again.
        Phase = IsOver ? BattlePhase.Finished : BattlePhase.ChoosingMove;
    }

    private void ChooseMove()
    {
        if (_moveNames.Count == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _selectedMove = (_selectedMove + 1) % _moveNames.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _selectedMove = (_selectedMove - 1 + _moveNames.Count) % _moveNames.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.X))
        {
            if (Balls <= 0)
            {
                Say("You have no balls left!");
                Phase = BattlePhase.ReadingMessages;
                AdvanceMessage();
                return;
            }

            Choose(new BattleAction.ThrowBall(BallKind.Poke));
            return;
        }

        if (Confirmed()) Choose(new BattleAction.UseMove(_selectedMove));
    }

    /// <summary>
    /// Hands an action to the game loop and waits. Nothing is predicted here: a battle
    /// is turn-based, so a round trip costs nothing worth the risk of showing a player
    /// a result the server then contradicts.
    /// </summary>
    private void Choose(BattleAction action)
    {
        PendingAction = action;
        Phase = BattlePhase.WaitingForServer;
    }

    public void Draw()
    {
        Raylib.ClearBackground(new Color(248, 248, 232, 255));

        DrawCombatant(_opponent, _names.Of(Side.Opponent), _wildSprite, _hasWildSprite,
            spriteX: Width - 260, spriteY: 60, boxX: 40, boxY: 60, showHp: false);

        DrawCombatant(_you, _names.Of(Side.Player), _playerSprite, _hasPlayerSprite,
            spriteX: 90, spriteY: 250, boxX: Width - 380, boxY: 250, showHp: true);

        DrawMessageBox();
    }

    private void DrawCombatant(
        BattlerView battler, string name, Texture2D sprite, bool hasSprite,
        int spriteX, int spriteY, int boxX, int boxY, bool showHp)
    {
        if (hasSprite)
        {
            Raylib.DrawTextureEx(sprite, new System.Numerics.Vector2(spriteX, spriteY), 0f, SpriteScale, Color.White);
        }
        else
        {
            Raylib.DrawRectangleLines(spriteX, spriteY, 64 * SpriteScale, 64 * SpriteScale, Color.Gray);
        }

        const int boxWidth = 340;
        const int boxHeight = 80;

        Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, new Color(255, 255, 255, 230));
        Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, new Color(64, 64, 64, 255));

        Raylib.DrawText(name, boxX + 14, boxY + 10, 22, Color.Black);
        Raylib.DrawText($"L{battler.Level}", boxX + boxWidth - 60, boxY + 10, 22, Color.Black);

        DrawHealthBar(battler, boxX + 14, boxY + 44, boxWidth - 28);

        if (showHp)
            Raylib.DrawText($"{battler.CurrentHp}/{battler.MaxHp}", boxX + boxWidth - 110, boxY + 54, 16, Color.Black);
    }

    private static void DrawHealthBar(BattlerView battler, int x, int y, int width)
    {
        Raylib.DrawRectangle(x, y, width, 10, new Color(80, 80, 80, 255));

        int filled = battler.MaxHp <= 0 ? 0 : width * battler.CurrentHp / battler.MaxHp;

        // Green, amber, red — the same thresholds the games use, which is what makes a
        // health bar readable at a glance rather than something to calculate.
        double fraction = battler.MaxHp <= 0 ? 0 : (double)battler.CurrentHp / battler.MaxHp;

        Color colour = fraction switch
        {
            > 0.5 => new Color(88, 208, 88, 255),
            > 0.2 => new Color(248, 208, 48, 255),
            _ => new Color(232, 72, 72, 255),
        };

        Raylib.DrawRectangle(x, y, Math.Max(0, filled), 10, colour);
    }

    private void DrawMessageBox()
    {
        const int boxY = 470;
        const int boxHeight = 150;

        Raylib.DrawRectangle(30, boxY, Width - 60, boxHeight, new Color(255, 255, 255, 240));
        Raylib.DrawRectangleLines(30, boxY, Width - 60, boxHeight, new Color(64, 64, 64, 255));

        if (Phase == BattlePhase.ChoosingMove)
        {
            DrawMoveMenu(boxY);
            return;
        }

        Raylib.DrawText(_message, 52, boxY + 30, 24, Color.Black);

        string prompt = Phase switch
        {
            BattlePhase.Finished => "Press Z to return",
            BattlePhase.WaitingForServer => "...",
            _ => "Press Z",
        };

        Raylib.DrawText(prompt, Width - 220, boxY + boxHeight - 34, 18, new Color(120, 120, 120, 255));
    }

    private void DrawMoveMenu(int boxY)
    {
        Raylib.DrawText("Choose a move:", 52, boxY + 18, 20, new Color(96, 96, 96, 255));

        Raylib.DrawText(
            $"X: throw a ball ({Balls} left)",
            Width - 340, boxY + 18, 20,
            Balls > 0 ? new Color(96, 96, 96, 255) : new Color(180, 120, 120, 255));

        for (int i = 0; i < _moveNames.Count; i++)
        {
            int y = boxY + 48 + i * 26;
            bool selected = i == _selectedMove;

            if (selected) Raylib.DrawText(">", 52, y, 22, Color.Black);

            Raylib.DrawText(
                _moveNames[i],
                76, y, 22,
                selected ? Color.Black : new Color(110, 110, 110, 255));

            if (_data.MoveAt(_you.Moves[i]) is { } move)
                Raylib.DrawText($"{move.Type}", 300, y + 3, 18, new Color(140, 140, 140, 255));
        }
    }

    public void Unload()
    {
        if (_hasWildSprite) Raylib.UnloadTexture(_wildSprite);
        if (_hasPlayerSprite) Raylib.UnloadTexture(_playerSprite);
    }
}
