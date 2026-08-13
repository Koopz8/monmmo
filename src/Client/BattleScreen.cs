using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
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
    private readonly string? _trainerName;
    private readonly ItemNames? _items;
    private readonly List<string> _moveNames = [];

    private BattleNames _names;

    // Not readonly any more. A trainer fight replaces whoever is out, and the sprite on
    // the graphics card has to go with them.
    private Texture2D _wildSprite;
    private Texture2D _playerSprite;
    private bool _hasWildSprite;
    private bool _hasPlayerSprite;

    private readonly Queue<string> _pending = new();
    private string _message = "";
    private int _selectedMove;

    private BattlerView _you;
    private BattlerView _opponent;

    public BattleScreen(
        BattleStarted start, GameData data, TrainerNames? trainers = null, ItemNames? items = null,
        string? calledInstead = null)
    {
        _data = data;
        _items = items;
        _you = start.You;
        _opponent = start.Opponent;
        Balls = start.Balls;
        Medicine = start.Medicine;

        // Resolved here, on the machine that has a cartridge. The server sent a number.
        //
        // Except for one boy. The trainer table calls him TERRY at all twenty-seven of
        // his entries and his own scripts call him whatever the player chose, and one of
        // those has to be a placeholder — it is the table's, and nothing else in the game
        // wears that name. Whoever opened this screen says so, because the fight and the
        // sentence that names him are the same script.
        _trainerName = start.TrainerId is { } id
            ? calledInstead ?? trainers?.Of(id) ?? "TRAINER"
            : null;

        _names = BattleNames.Unknown;
        Rename();

        RefreshMoveNames();

        (_wildSprite, _hasWildSprite) = LoadSprite(data, start.Opponent.Species, back: false);
        (_playerSprite, _hasPlayerSprite) = LoadSprite(data, start.You.Species, back: true);

        Say(IsTrainerBattle
            ? $"{_trainerName} wants to fight!"
            : $"A wild {SpeciesNameOf(start.Opponent)} appeared!");

        if (IsTrainerBattle) Say($"{_trainerName} sent out {SpeciesNameOf(start.Opponent)}!");

        // And then read the first of them, rather than waiting for something to happen.
        // Every other place that queues a line follows it with this; the constructor did
        // not, so a battle opened on an empty box with "Press Z" under it and stayed
        // that way until the first turn resolved. The lines were all there — nothing was
        // showing them.
        AdvanceMessage();
    }

    /// <summary>True when a person started this, rather than something in the grass.</summary>
    public bool IsTrainerBattle => _trainerName is not null;

    private string NameOf(int itemId) => _items?.Of(itemId) ?? $"item {itemId}";

    private string SpeciesNameOf(BattlerView battler) =>
        battler.Nickname ?? _data.SpeciesAt(battler.Species)?.Name ?? $"species {battler.Species}";

    /// <summary>
    /// Rebuilds the names narration uses, which change whenever either side does.
    /// <para>
    /// A wild creature is "the wild PIDGEY" and a trainer's is just "PIDGEY", because
    /// nothing a trainer owns is wild and reading that it is would be worse than
    /// reading nothing.
    /// </para>
    /// </summary>
    private void Rename()
    {
        string mine = SpeciesNameOf(_you);
        string theirs = SpeciesNameOf(_opponent);

        _names = new BattleNames(
            mine,
            IsTrainerBattle ? theirs : $"the wild {theirs}",
            id => _data.MoveAt(id)?.Name ?? $"move {id}");
    }

    private void RefreshMoveNames()
    {
        _moveNames.Clear();
        _selectedMove = 0;

        foreach (int moveId in _you.Moves)
            _moveNames.Add(_data.MoveAt(moveId)?.Name ?? $"move {moveId}");
    }

    /// <summary>
    /// One side has sent out somebody new.
    /// <para>
    /// Everything about that side changes at once — the sprite, the health bar, the
    /// name narration uses, and for the player's side the list of moves to choose from.
    /// Forgetting any one of them leaves a screen that is half of the last creature.
    /// </para>
    /// </summary>
    public void Apply(BattlerSentOut sent)
    {
        if (sent.Side == Side.Player)
        {
            _you = sent.Battler;

            if (_hasPlayerSprite) Raylib.UnloadTexture(_playerSprite);
            (_playerSprite, _hasPlayerSprite) = LoadSprite(_data, _you.Species, back: true);

            RefreshMoveNames();
            Rename();

            Say($"Go! {SpeciesNameOf(_you)}!");
        }
        else
        {
            _opponent = sent.Battler;

            if (_hasWildSprite) Raylib.UnloadTexture(_wildSprite);
            (_wildSprite, _hasWildSprite) = LoadSprite(_data, _opponent.Species, back: false);

            Rename();

            Say($"{_trainerName ?? "The opponent"} sent out {SpeciesNameOf(_opponent)}!");
        }

        Phase = BattlePhase.ReadingMessages;

        if (_message.Length == 0) AdvanceMessage();
    }

    public BattlePhase Phase { get; private set; } = BattlePhase.ReadingMessages;

    /// <summary>
    /// The ball pocket, as the server counts it.
    /// <para>
    /// A list rather than a number, which is what it used to be. Pressing X cycles
    /// through what is actually carried rather than assuming everybody's bag holds one
    /// kind of thing.
    /// </para>
    /// </summary>
    public IReadOnlyList<BagEntry> Balls { get; private set; } = [];

    /// <summary>What in the bag would put health back on somebody.</summary>
    public IReadOnlyList<BagEntry> Medicine { get; private set; } = [];

    private BagEntry? ChosenMedicine => Medicine.Count == 0 ? null : Medicine[0];

    /// <summary>Money, and what beating somebody just paid. Both the server's numbers.</summary>
    public int Money { get; private set; }

    private int _selectedBall;

    /// <summary>Which ball pressing X would throw, or nothing when the pocket is empty.</summary>
    private BagEntry? ChosenBall =>
        Balls.Count == 0 ? null : Balls[Math.Clamp(_selectedBall, 0, Balls.Count - 1)];

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
        Medicine = update.Medicine;

        Phase = BattlePhase.ReadingMessages;
        AdvanceMessage();
    }

    /// <summary>The battle is over; everything still queued is read before it closes.</summary>
    public void Apply(BattleFinished finished)
    {
        Balls = finished.Balls;
        Money = finished.Money;
        IsOver = true;

        if (finished.Prize > 0) Say($"You got {finished.Prize} for winning!");

        if (finished.Winner == Side.Opponent)
            Say("You have no more usable Pokémon! Your party was healed.");
        else if (IsTrainerBattle && finished.Winner == Side.Player)
            Say($"You defeated {_trainerName}!");

        // Reading, not finished: whatever is on screen still deserves a keypress. The
        // queue draining is what lands on Finished, and going straight there would
        // swallow the last line of a battle.
        Phase = BattlePhase.ReadingMessages;

        if (_message.Length == 0) AdvanceMessage();
    }

    /// <summary>
    /// The server has nothing to say about this battle.
    /// <para>
    /// Only reachable when the two sides disagree about whether a battle is running,
    /// which is a bug — but a bug that must not leave a player holding a screen that
    /// will never respond again. Closing it loses nothing: the server is authoritative
    /// and has already stopped.
    /// </para>
    /// </summary>
    public void Abandon(string reason)
    {
        IsOver = true;
        Say(reason);

        Phase = BattlePhase.ReadingMessages;

        if (_message.Length == 0) AdvanceMessage();
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

        if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            if (ChosenMedicine is not { } medicine)
            {
                Say("You have nothing to use!");
                Phase = BattlePhase.ReadingMessages;
                AdvanceMessage();
                return;
            }

            // The id only. How much it restores is the server's number — a request
            // carrying the amount would let a client drink a Potion for two hundred.
            Choose(new BattleAction.UseItem(medicine.ItemId));
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.X))
        {
            if (IsTrainerBattle)
            {
                // The server refuses this too — this is only what stops a player
                // spending a turn finding out.
                Say("You can't catch someone else's Pokémon!");
                Phase = BattlePhase.ReadingMessages;
                AdvanceMessage();
                return;
            }

            if (ChosenBall is not { } ball)
            {
                Say("You have no balls left!");
                Phase = BattlePhase.ReadingMessages;
                AdvanceMessage();
                return;
            }

            // Only the id. Which kind of ball that is, and therefore how well it works,
            // is the server's answer — a request naming a kind would let a client spend
            // the cheap one and throw the good one.
            Choose(new BattleAction.ThrowBall(ball.ItemId));
            return;
        }

        // Left and right pick which ball, which is otherwise a menu this screen has no
        // room for. Nothing happens when there is only one kind to pick from.
        if (Balls.Count > 1 && Raylib.IsKeyPressed(KeyboardKey.Right))
            _selectedBall = (_selectedBall + 1) % Balls.Count;

        if (Balls.Count > 1 && Raylib.IsKeyPressed(KeyboardKey.Left))
            _selectedBall = (_selectedBall - 1 + Balls.Count) % Balls.Count;

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

        // The plain name on the plate, not the one narration uses. "A wild PIDGEY
        // appeared!" is a sentence and wants the article; a label above a health bar is
        // not a sentence, and "the wild PIDGEY" written on one reads as a mistake.
        DrawCombatant(_opponent, SpeciesNameOf(_opponent), _wildSprite, _hasWildSprite,
            spriteX: Width - 260, spriteY: 60, boxX: 40, boxY: 60, showHp: false);

        DrawCombatant(_you, SpeciesNameOf(_you), _playerSprite, _hasPlayerSprite,
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

        string medicineLine = ChosenMedicine is { } potion
            ? $"C: use {NameOf(potion.ItemId)} ({potion.Count})"
            : "C: nothing to use";

        Raylib.DrawText(medicineLine, Width - 400, boxY + 44, 20, ChosenMedicine is not null
            ? new Color(96, 96, 96, 255)
            : new Color(180, 120, 120, 255));

        string ballLine = ChosenBall is { } ball
            ? $"X: throw {NameOf(ball.ItemId)} ({ball.Count})" + (Balls.Count > 1 ? "  < >" : "")
            : "X: no balls left";

        Raylib.DrawText(
            ballLine,
            Width - 400, boxY + 18, 20,
            ChosenBall is not null ? new Color(96, 96, 96, 255) : new Color(180, 120, 120, 255));

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
