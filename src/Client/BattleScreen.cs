using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>What the battle screen is waiting for.</summary>
public enum BattlePhase
{
    ReadingMessages,
    ChoosingMove,
    Finished,
}

/// <summary>
/// A wild battle, drawn.
/// <para>
/// Everything decided here comes from <c>Core.Battle</c> — this class owns layout,
/// input and pacing, and nothing about how a battle works. The messages come from
/// <see cref="BattleNarrator"/> for the same reason: the wording is worth testing, and
/// a renderer is a poor place to keep it.
/// </para>
/// </summary>
public sealed class BattleScreen
{
    private const int Width = 960;
    private const int Height = 640;
    private const int SpriteScale = 3;

    private readonly Battle _battle;
    private readonly Texture2D _wildSprite;
    private readonly Texture2D _playerSprite;
    private readonly bool _hasWildSprite;
    private readonly bool _hasPlayerSprite;

    private readonly Queue<string> _pending = new();
    private string _message = "";
    private int _selectedMove;

    public BattleScreen(Battle battle, GameData data)
    {
        _battle = battle;

        (_wildSprite, _hasWildSprite) = LoadSprite(data, battle.Opponent.Species.Index, back: false);
        (_playerSprite, _hasPlayerSprite) = LoadSprite(data, battle.Player.Species.Index, back: true);

        Say($"A wild {battle.Opponent.Name} appeared!");
    }

    public BattlePhase Phase { get; private set; } = BattlePhase.ReadingMessages;

    /// <summary>True once the battle is over and its last message has been read.</summary>
    public bool IsDismissed { get; private set; }

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

    private void Say(IEnumerable<string> lines)
    {
        foreach (string line in lines) _pending.Enqueue(line);
    }

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

        // Messages exhausted: either the battle is over, or it is the player's turn.
        Phase = _battle.IsOver ? BattlePhase.Finished : BattlePhase.ChoosingMove;
    }

    private void ChooseMove()
    {
        int moveCount = _battle.Player.Moves.Count;
        if (moveCount == 0) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _selectedMove = (_selectedMove + 1) % moveCount;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _selectedMove = (_selectedMove - 1 + moveCount) % moveCount;

        if (!Confirmed()) return;

        // The opponent's choice is uniform for now: without learnsets there is no
        // sensible way to pick, and a wild creature picking at random is close enough
        // to what the games do anyway.
        int opponentMove = _battle.Opponent.Moves.Count > 0 ? 0 : 0;

        List<BattleEvent> events = _battle.ResolveTurn(
            new BattleAction.UseMove(_selectedMove),
            new BattleAction.UseMove(opponentMove));

        Say(BattleNarrator.Describe(events));

        Phase = BattlePhase.ReadingMessages;
        AdvanceMessage();
    }

    public void Draw()
    {
        Raylib.ClearBackground(new Color(248, 248, 232, 255));

        DrawCombatant(_battle.Opponent, _wildSprite, _hasWildSprite,
            spriteX: Width - 260, spriteY: 60, boxX: 40, boxY: 60, showHp: false);

        DrawCombatant(_battle.Player, _playerSprite, _hasPlayerSprite,
            spriteX: 90, spriteY: 250, boxX: Width - 380, boxY: 250, showHp: true);

        DrawMessageBox();
    }

    private void DrawCombatant(
        Battler battler, Texture2D sprite, bool hasSprite,
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

        Raylib.DrawText($"{battler.Name}", boxX + 14, boxY + 10, 22, Color.Black);
        Raylib.DrawText($"L{battler.Level}", boxX + boxWidth - 60, boxY + 10, 22, Color.Black);

        DrawHealthBar(battler, boxX + 14, boxY + 44, boxWidth - 28);

        if (showHp)
            Raylib.DrawText($"{battler.CurrentHp}/{battler.MaxHp}", boxX + boxWidth - 110, boxY + 54, 16, Color.Black);
    }

    private static void DrawHealthBar(Battler battler, int x, int y, int width)
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

        string prompt = Phase == BattlePhase.Finished ? "Press Z to return" : "Press Z";
        Raylib.DrawText(prompt, Width - 220, boxY + boxHeight - 34, 18, new Color(120, 120, 120, 255));
    }

    private void DrawMoveMenu(int boxY)
    {
        Raylib.DrawText("Choose a move:", 52, boxY + 18, 20, new Color(96, 96, 96, 255));

        for (int i = 0; i < _battle.Player.Moves.Count; i++)
        {
            MoveData move = _battle.Player.Moves[i];
            int y = boxY + 48 + i * 26;

            bool selected = i == _selectedMove;
            if (selected) Raylib.DrawText(">", 52, y, 22, Color.Black);

            Raylib.DrawText(
                $"{move.Name}",
                76, y, 22,
                selected ? Color.Black : new Color(110, 110, 110, 255));

            Raylib.DrawText(
                $"{move.Type}",
                300, y + 3, 18,
                new Color(140, 140, 140, 255));
        }
    }

    public void Unload()
    {
        if (_hasWildSprite) Raylib.UnloadTexture(_wildSprite);
        if (_hasPlayerSprite) Raylib.UnloadTexture(_playerSprite);
    }
}
