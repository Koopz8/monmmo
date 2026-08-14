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

    /// <summary>Picking somebody else to send out.</summary>
    ChoosingWho,

    /// <summary>Picking which of four moves to drop for a fifth.</summary>
    ChoosingForget,

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

    /// <summary>The client's lettering. Shared, because there is one of it.</summary>
    private static PixelFont _font => Skin.Font;

    private readonly Queue<string> _pending = new();
    private string _message = "";
    private int _selectedMove;

    private BattlerView _you;
    private BattlerView _opponent;

    /// <summary>
    /// Everybody who could come out instead, and which one is out now.
    /// <para>
    /// Held rather than asked for, because a battle screen that had to reach back into
    /// the world for a party would be a battle screen that knows about the world. What it
    /// needs is a list of names, levels and health, and the slot each one sits in — the
    /// slot is what travels, because two of the same species in one party are otherwise
    /// the same request.
    /// </para>
    /// </summary>
    public IReadOnlyList<SavedMon> Party { get; set; } = [];

    /// <summary>Which of them is out, so the list can refuse to send out the one already there.</summary>
    public int Active { get; set; }

    private int _selectedMon;

    /// <summary>
    /// Moves a level-up offered and could not fit, oldest first.
    /// <para>
    /// The games ask straight away; this asks once the reading is done, because the
    /// reading is what tells a player it happened at all. More than one can be waiting —
    /// two levels in a fight is ordinary — so it is a queue rather than a flag.
    /// </para>
    /// </summary>
    private readonly Queue<int> _offered = new();

    private int _selectedForget;

    /// <summary>The move being offered right now, for whoever is drawing the question.</summary>
    public int? Offered => _offered.Count > 0 ? _offered.Peek() : null;

    /// <summary>Which of the four to drop, and for which move. Taken rather than read.</summary>
    public (int MoveId, int Forget)? Answered { get; private set; }

    public (int MoveId, int Forget)? TakeAnswer()
    {
        (int MoveId, int Forget)? answer = Answered;
        Answered = null;

        return answer;
    }

    /// <param name="challenge">
    /// What this trainer says on the way in, off their own script. Every trainer in this
    /// game used to open with "NAME wants to fight!" — a sentence this project wrote,
    /// standing in for four hundred and fifty the cartridge already had.
    /// </param>
    public BattleScreen(
        BattleStarted start, GameData data, TrainerNames? trainers = null, ItemNames? items = null,
        string? calledInstead = null, IReadOnlyList<string>? challenge = null)
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

        // Theirs first, then ours. Ours is not redundant once theirs exists: a line like
        // "I saw your feat from the grass!" never says who is talking, and the name is
        // the only thing on the screen that does.
        if (IsTrainerBattle && challenge is not null)
            foreach (string page in challenge) Say(page);

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

    /// <summary>
    /// A species' name, in characters this font can draw.
    /// <para>
    /// NIDORAN's is not just letters. The cartridge writes the male and female symbols
    /// as characters of its own, and a font with no glyph for either drew "NIDORAN?" at
    /// the top of the screen for the whole of NUGGET BRIDGE.
    /// </para>
    /// </summary>
    private string SpeciesNameOf(BattlerView battler) =>
        GameText.ToAscii(battler.Nickname ?? _data.SpeciesAt(battler.Species)?.Name ?? $"species {battler.Species}");

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

    private string MoveNamed(int moveId) => GameText.ToAscii(_data.MoveAt(moveId)?.Name ?? $"move {moveId}");

    private void RefreshMoveNames()
    {
        _moveNames.Clear();
        _selectedMove = 0;

        foreach (int moveId in _you.Moves)
            _moveNames.Add(MoveNamed(moveId));
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

    /// <summary>
    /// Queues a line to read, in characters this font can actually draw.
    /// <para>
    /// The same pass a dialogue box has always made, and this screen did without for as
    /// long as every line on it was written here. The moment a trainer's own words
    /// arrived, "I'm second!" came out as "I?m second!" — the cartridge's apostrophe is
    /// a curly one and it turns up in about every other sentence anybody says.
    /// </para>
    /// </summary>
    private void Say(string line) => _pending.Enqueue(GameText.ToAscii(line));

    /// <summary>Folds a turn's result in: what to read, and where both sides now stand.</summary>
    public void Apply(BattleUpdate update)
    {
        foreach (string line in BattleNarrator.Describe(update.Events, _names))
            _pending.Enqueue(line);

        // Queued before the reading starts, so the question is waiting when it ends.
        foreach (BattleEvent.MoveNotLearned offered in update.Events.OfType<BattleEvent.MoveNotLearned>())
            _offered.Enqueue(offered.MoveId);

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

            case BattlePhase.ChoosingWho:
                ChooseWho();
                break;

            case BattlePhase.ChoosingForget:
                ChooseForget();
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

        // A move that would not fit is asked about before anything else, including
        // before the fight is allowed to end — the offer is the last thing that happened
        // and a player who has just read about it is the one who should answer.
        if (_offered.Count > 0)
        {
            _selectedForget = 0;
            Phase = BattlePhase.ChoosingForget;

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

        // Somebody else. The last thing a battle here could not do: the next one came
        // out when somebody fainted and never by choice, so a fight was whoever happened
        // to be first in the party.
        if (Raylib.IsKeyPressed(KeyboardKey.V) && Party.Count > 1)
        {
            _selectedMon = Active;
            Phase = BattlePhase.ChoosingWho;

            return;
        }

        if (Confirmed()) Choose(new BattleAction.UseMove(_selectedMove));
    }

    /// <summary>
    /// Which of the four to drop. The fifth option is keeping them all, which the games
    /// allow and which this project used to do silently and without asking.
    /// </summary>
    private void ChooseForget()
    {
        if (_offered.Count == 0)
        {
            Phase = IsOver ? BattlePhase.Finished : BattlePhase.ChoosingMove;
            return;
        }

        int options = _moveNames.Count + 1;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _selectedForget = (_selectedForget + 1) % options;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _selectedForget = (_selectedForget - 1 + options) % options;

        if (!Confirmed()) return;

        // Anything past the four is "keep them", and it travels as such rather than as
        // silence — the server is holding the offer and has to be told either way.
        Answered = (_offered.Dequeue(), _selectedForget < _moveNames.Count ? _selectedForget : -1);

        Phase = BattlePhase.ReadingMessages;
        AdvanceMessage();
    }

    private void ChooseWho()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
            _selectedMon = (_selectedMon + 1) % Party.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
            _selectedMon = (_selectedMon - 1 + Party.Count) % Party.Count;

        if (Raylib.IsKeyPressed(KeyboardKey.X) || Raylib.IsKeyPressed(KeyboardKey.V))
        {
            Phase = BattlePhase.ChoosingMove;
            return;
        }

        if (!Confirmed()) return;

        // Refused here as well as on the server, which is the usual arrangement: this is
        // what stops a player spending a turn finding out, and the server is what makes
        // it true.
        if (_selectedMon == Active)
        {
            Say("They're already out!");
            Phase = BattlePhase.ReadingMessages;
            AdvanceMessage();

            return;
        }

        if (Party[_selectedMon].CurrentHp <= 0)
        {
            Say("They have no energy left to fight!");
            Phase = BattlePhase.ReadingMessages;
            AdvanceMessage();

            return;
        }

        Choose(new BattleAction.SwitchTo(_selectedMon));
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

    /// <summary>
    /// Health slides rather than jumps.
    /// <para>
    /// The one piece of animation on this screen, and it earns its place: a bar that
    /// snaps to its new length says a number changed, and a bar that runs down says how
    /// much. The value drawn chases the value the server sent; nothing waits for it.
    /// </para>
    /// </summary>
    private float _shownYours = -1;

    private float _shownTheirs = -1;

    private float _blink;

    /// <summary>Called once a frame, before drawing, so the meters can catch up.</summary>
    public void Animate(float delta)
    {
        _blink += delta;

        Chase(ref _shownYours, _you.CurrentHp, _you.MaxHp, delta);
        Chase(ref _shownTheirs, _opponent.CurrentHp, _opponent.MaxHp, delta);

        static void Chase(ref float shown, int target, int max, float delta)
        {
            if (shown < 0) { shown = target; return; }

            // A fixed share of the bar a second rather than of the gap, so a big hit
            // takes longer to draw than a small one — which is the whole point.
            float speed = Math.Max(1f, max * 0.7f) * delta;

            shown = shown < target
                ? Math.Min(target, shown + speed)
                : Math.Max(target, shown - speed);
        }
    }

    public void Draw()
    {
        DrawArena();

        // The plain name on the plate, not the one narration uses. "A wild PIDGEY
        // appeared!" is a sentence and wants the article; a label above a health bar is
        // not a sentence, and "the wild PIDGEY" written on one reads as a mistake.
        DrawSprite(_wildSprite, _hasWildSprite, Width - 300, 70);
        DrawSprite(_playerSprite, _hasPlayerSprite, 80, 250);

        DrawPlate(_opponent, SpeciesNameOf(_opponent), _shownTheirs, 40, 48, showHp: false);
        DrawPlate(_you, SpeciesNameOf(_you), _shownYours, Width - 400, 296, showHp: true);

        DrawMessageBox();
    }

    /// <summary>
    /// The ground the fight happens on: a wash from dusk at the top to floor at the
    /// bottom, with a band where the two battlers stand.
    /// </summary>
    private static void DrawArena()
    {
        Raylib.ClearBackground(new Color(30, 34, 50, 255));

        Raylib.DrawRectangleGradientV(0, 0, Width, 300, new Color(46, 54, 82, 255), new Color(30, 34, 50, 255));
        Raylib.DrawRectangleGradientV(0, 300, Width, 170, new Color(30, 34, 50, 255), new Color(24, 27, 40, 255));

        // Two ellipses of floor, which is all the old games draw and all that is needed
        // to stop a sprite looking as though it is falling.
        Raylib.DrawEllipse(Width - 210, 268, 150, 26, new Color(52, 60, 88, 255));
        Raylib.DrawEllipse(190, 452, 170, 30, new Color(52, 60, 88, 255));
    }

    private static void DrawSprite(Texture2D sprite, bool has, int x, int y)
    {
        if (has)
        {
            Raylib.DrawTextureEx(sprite, new System.Numerics.Vector2(x, y), 0f, SpriteScale, Color.White);
            return;
        }

        Skin.DrawCutBorder(new Rectangle(x, y, 64 * SpriteScale, 64 * SpriteScale), Skin.EdgeSoft);
    }

    /// <summary>
    /// One combatant's plate: who, what level, and how they are doing.
    /// <para>
    /// Corner-mounted the way the games have always had it, because that is where a
    /// player's eye already goes. What is new is that the level is a chip rather than a
    /// word, and that the numbers only appear on your own — knowing the opponent's exact
    /// health is not something the games give you and not something to invent.
    /// </para>
    /// </summary>
    private void DrawPlate(BattlerView battler, string name, float shownHp, int x, int y, bool showHp)
    {
        const int width = 360;
        int height = showHp ? 92 : 74;

        var box = new Rectangle(x, y, width, height);

        Skin.DrawPanel(box);

        _font.DrawShadowed(name, x + 18, y + 16, 3, Skin.Ink, new Color(0, 0, 0, 120));

        Skin.DrawChip(_font, $"Lv{battler.Level}", x + width - 78, y + 14, 2, Skin.PanelHigh);

        float fraction = battler.MaxHp <= 0 ? 0 : Math.Max(0, shownHp) / battler.MaxHp;

        _font.Draw("HP", x + 18, y + 44, 2, Skin.InkFaint);

        Skin.DrawMeter(
            new Rectangle(x + 48, y + 42, width - 66, 10),
            fraction,
            Skin.HealthColour(battler.CurrentHp, battler.MaxHp));

        if (showHp)
        {
            _font.DrawRight(
                $"{battler.CurrentHp}/{battler.MaxHp}", x + width - 18, y + 62, 2, Skin.InkDim);
        }

        if (battler.Status != StatusCondition.None)
            Skin.DrawChip(_font, ShortStatus(battler.Status), x + 18, y + height - 26, 2, Skin.HpFair);
    }

    private static string ShortStatus(StatusCondition status) => status switch
    {
        StatusCondition.Poison => "PSN",
        StatusCondition.Burn => "BRN",
        StatusCondition.Paralysis => "PAR",
        StatusCondition.Sleep => "SLP",
        StatusCondition.Freeze => "FRZ",
        _ => "",
    };

    private void DrawMessageBox()
    {
        const int boxY = 470;
        const int boxHeight = 150;

        var box = new Rectangle(24, boxY, Width - 48, boxHeight);

        Skin.DrawPanel(box);

        switch (Phase)
        {
            case BattlePhase.ChoosingForget: DrawForgetMenu(boxY); return;
            case BattlePhase.ChoosingWho: DrawPartyMenu(boxY); return;
            case BattlePhase.ChoosingMove: DrawMoveMenu(boxY); return;
        }

        // Wrapped rather than drawn as one line. "You have no more usable Pokemon! Your
        // party was healed." ran off the right edge of the box and the half that was cut
        // was the half that said what had been done about it.
        List<string> lines = _font.Wrap(_message, 3, (int)box.Width - 48);

        for (int i = 0; i < lines.Count && i < 3; i++)
            _font.Draw(lines[i], 48, boxY + 26 + i * 34, 3, Skin.Ink);

        string prompt = Phase switch
        {
            BattlePhase.Finished => "Z to return",
            BattlePhase.WaitingForServer => "",
            _ => "Z",
        };

        // The blinking marker the games put in the corner of a full box. It is the only
        // thing on screen that says the game is waiting for a person rather than for a
        // server, and without it the two look identical.
        if (prompt.Length > 0 && _blink % 1.0f < 0.6f)
        {
            _font.DrawRight(prompt, Width - 64, boxY + boxHeight - 34, 2, Skin.InkDim);

            Raylib.DrawTriangle(
                new System.Numerics.Vector2(Width - 52, boxY + boxHeight - 32),
                new System.Numerics.Vector2(Width - 40, boxY + boxHeight - 32),
                new System.Numerics.Vector2(Width - 46, boxY + boxHeight - 24),
                Skin.Accent);
        }
    }

    /// <summary>The four already known, and the option of keeping all of them.</summary>
    private void DrawForgetMenu(int boxY)
    {
        string coming = _offered.Count > 0 ? MoveNamed(_offered.Peek()) : "?";

        _font.Draw($"Forget which, to learn {coming}?", 48, boxY + 18, 2, Skin.InkDim);

        for (int i = 0; i <= _moveNames.Count; i++)
        {
            int y = boxY + 44 + i * 22;
            bool selected = i == _selectedForget || (i == _moveNames.Count && _selectedForget >= _moveNames.Count);

            if (selected) Skin.DrawSelection(new Rectangle(36, y - 5, Width - 72, 22));

            string label = i < _moveNames.Count ? _moveNames[i] : $"Keep all four - do not learn {coming}";

            _font.Draw(label, 56, y, 2, i < _moveNames.Count ? Skin.Ink : Skin.InkDim);
        }
    }

    /// <summary>
    /// The party, for choosing who comes out.
    /// <para>
    /// Names, levels and health and nothing else. There is no picture of anybody here on
    /// purpose: a battle screen that loaded six sprites to answer one question would cost
    /// a fight's worth of decompression at the moment a player is deciding something.
    /// </para>
    /// </summary>
    private void DrawPartyMenu(int boxY)
    {
        _font.Draw("Send out who?", 48, boxY + 18, 2, Skin.InkDim);
        _font.DrawRight("V or X: back", Width - 48, boxY + 18, 2, Skin.InkFaint);

        for (int i = 0; i < Party.Count; i++)
        {
            SavedMon member = Party[i];

            int y = boxY + 46 + i * 22;
            bool selected = i == _selectedMon;

            if (selected) Skin.DrawSelection(new Rectangle(36, y - 5, Width - 72, 22));

            // Out already, or unable — both drawn differently, because the difference is
            // the whole of what a player is looking at this list to find out.
            Color colour = member.CurrentHp <= 0
                ? Skin.HpPoor
                : i == Active ? Skin.InkFaint : Skin.Ink;

            string name = GameText.ToAscii(
                member.Nickname ?? _data.SpeciesAt(member.Species)?.Name ?? $"species {member.Species}");

            _font.Draw(name, 56, y, 2, colour);
            _font.Draw($"Lv{member.Level}", 260, y, 2, colour);

            if (member.CurrentHp > 0 && _data.SpeciesAt(member.Species) is not null)
            {
                Skin.DrawMeter(
                    new Rectangle(340, y + 2, 120, 8),
                    Fraction(member),
                    Skin.HealthColour(member.CurrentHp, Math.Max(1, MaxHpOf(member))));
            }

            _font.Draw(
                member.CurrentHp <= 0 ? "fainted" : $"{member.CurrentHp} HP",
                480, y, 2, colour);

            if (i == Active) Skin.DrawChip(_font, "OUT", 600, y - 3, 2, Skin.AccentDeep);
        }
    }

    /// <summary>
    /// How full a party member is, worked out the same way the server does it.
    /// <para>
    /// The client can rebuild a member's maximum health from base stats because it has
    /// the cartridge; what it must not do is decide anything with it. This is a bar to
    /// look at, not a number anybody acts on.
    /// </para>
    /// </summary>
    private float Fraction(SavedMon member)
    {
        int max = MaxHpOf(member);

        return max <= 0 ? 0 : Math.Clamp(member.CurrentHp / (float)max, 0f, 1f);
    }

    private int MaxHpOf(SavedMon member) =>
        PartyBuilder.Restore(_data, member) is { } battler ? battler.MaxHp : member.CurrentHp;

    private void DrawMoveMenu(int boxY)
    {
        // The actions along the top, as chips. A key nobody knows about is a feature
        // nobody has, and a row of chips says what the keys are without a legend.
        DrawActionChips(boxY);

        // Two by two, the way the games lay it out, in the left half.
        for (int i = 0; i < _moveNames.Count; i++)
        {
            int column = i % 2;
            int row = i / 2;

            var cell = new Rectangle(40 + column * 240, boxY + 48 + row * 38, 232, 32);

            bool selected = i == _selectedMove;

            if (selected) Skin.DrawSelection(cell);

            _font.Draw(_moveNames[i], cell.X + 14, cell.Y + 8, 3, selected ? Skin.Ink : Skin.InkDim);
        }

        // And what the highlighted one is, on the right: the modern half of this screen.
        if (_selectedMove < _you.Moves.Count && _data.MoveAt(_you.Moves[_selectedMove]) is { } move)
        {
            int right = Width - 290;

            Skin.DrawChip(_font, move.Type.ToString().ToUpperInvariant(), right, boxY + 48, 2, Skin.TypeColour(move.Type));

            _font.Draw(
                move.Power > 0 ? $"POWER {move.Power}" : "STATUS",
                right, boxY + 80, 2, Skin.InkDim);

            _font.Draw($"PP {move.Pp}", right, boxY + 104, 2, Skin.InkDim);
        }
    }

    /// <summary>
    /// The other things a turn can be spent on, as a row of chips.
    /// <para>
    /// A key nobody knows about is a feature nobody has. The old games put these in a
    /// second menu — FIGHT, BAG, POKeMON, RUN — and a second menu is a keypress before
    /// every keypress; a row that says what the keys are costs nothing and is always
    /// visible.
    /// </para>
    /// </summary>
    private void DrawActionChips(int boxY)
    {
        string ball = ChosenBall is { } b
            ? $"X {GameText.ToAscii(NameOf(b.ItemId))} x{b.Count}" + (Balls.Count > 1 ? "  < >" : "")
            : "X NO BALLS";

        string medicine = ChosenMedicine is { } potion
            ? $"C {GameText.ToAscii(NameOf(potion.ItemId))} x{potion.Count}"
            : "C NOTHING TO USE";

        int x = 44;
        int y = boxY + 14;

        Skin.DrawChip(_font, ball, x, y, 2, ChosenBall is not null ? Skin.PanelHigh : Skin.PanelDeep);
        x += Skin.ChipWidth(_font, ball, 2) + 12;

        Skin.DrawChip(_font, medicine, x, y, 2, ChosenMedicine is not null ? Skin.PanelHigh : Skin.PanelDeep);

        if (Party.Count > 1)
        {
            const string switching = "V SEND OUT SOMEBODY ELSE";

            Skin.DrawChip(
                _font, switching, Width - 68 - Skin.ChipWidth(_font, switching, 2), y, 2, Skin.PanelHigh);
        }
    }

    public void Unload()
    {
        if (_hasWildSprite) Raylib.UnloadTexture(_wildSprite);
        if (_hasPlayerSprite) Raylib.UnloadTexture(_playerSprite);
    }
}
