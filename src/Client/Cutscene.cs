using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;

namespace PokeMmo.Client;

/// <summary>
/// Plays a scene: the lines and the walking, in the order the cartridge put them.
/// <para>
/// A conversation is a text box and nothing else, and most scripts are conversations.
/// A scene is the other kind — the professor walking over as he tells you not to go out
/// — and the difference is entirely in the timing. A player that showed all the lines
/// and then moved everybody would be showing the same content and none of the scene.
/// </para>
/// <para>
/// The player's own movements are performed too, but not decided here. This side asks —
/// with the directions, not a destination, so the server can walk them square by square
/// and refuse any that leaves the map or lands on somebody — and animates its own
/// prediction meanwhile. A destination would have to be taken on trust; a path can be
/// checked.
/// </para>
/// </summary>
public sealed class Cutscene
{
    /// <summary>How long one square of a scripted walk takes.</summary>
    private const float StepSeconds = 0.22f;

    /// <summary>
    /// What each step byte does. Derived once, beside the lists it was derived from —
    /// a client with its own copy of this table is a client that can disagree with the
    /// tool that worked it out.
    /// </summary>
    public static Direction? DirectionOf(byte step) => MovementLists.DirectionOf(step);

    private readonly List<SceneBeat> _beats;
    private readonly MapView _view;

    private int _beat;
    private int _step;
    private float _elapsed;

    /// <summary>Who this scene has moved, so they can be reported and let go afterwards.</summary>
    private readonly Dictionary<int, WalkingPerson> _walking = [];

    public Cutscene(IEnumerable<SceneBeat> beats, MapView view)
    {
        _beats = [.. beats];
        _view = view;
    }

    /// <summary>The box for whatever is being said right now, if anything is.</summary>
    public DialogueBox? Saying { get; private set; }

    public bool IsFinished => _beat >= _beats.Count && Saying is null;

    /// <summary>Everybody this scene moved, and where it left them.</summary>
    public IEnumerable<(int LocalId, GridPosition Square, Direction Facing)> Moved =>
        _walking.Select(w => (w.Key, w.Value.Square, w.Value.Facing));

    /// <summary>Who this scene will move, known before it starts so they can be held.</summary>
    public IEnumerable<int> Cast =>
        _beats.OfType<SceneBeat.Walk>().Where(w => !w.IsPlayer).Select(w => w.PersonId).Distinct();

    /// <summary>
    /// The direction the player should be stepping this instant, if a beat is walking
    /// them.
    /// <para>
    /// Handed back rather than applied, because the client's walking figure is driven by
    /// input and this is input — it just did not come from a key. Feeding it through the
    /// same path means a scripted step animates, collides and turns exactly like a
    /// walked one, instead of being a second kind of movement with its own bugs.
    /// </para>
    /// </summary>
    public Direction? PlayerInput { get; private set; }

    /// <summary>Directions this scene has asked the server to walk, not yet sent.</summary>
    private readonly List<Direction> _asked = [];

    /// <summary>What to ask the server for, once and at the start of the beat.</summary>
    public IReadOnlyList<Direction> Ask()
    {
        List<Direction> asking = [.. _asked];

        _asked.Clear();

        return asking;
    }

    public void Update(float deltaSeconds)
    {
        if (Saying is not null)
        {
            Saying.Update();

            if (!Saying.IsFinished) return;

            Saying = null;
            _beat++;
        }

        while (_beat < _beats.Count)
        {
            switch (_beats[_beat])
            {
                case SceneBeat.Say say:
                    Saying = new DialogueBox([say.Page]);
                    return;

                case SceneBeat.Walk walk when walk.IsPlayer:
                    if (!StepPlayer(walk, deltaSeconds)) return;

                    _beat++;
                    _step = 0;
                    _elapsed = 0f;
                    continue;

                case SceneBeat.Walk walk:
                    if (!Step(walk, deltaSeconds)) return;

                    _beat++;
                    _step = 0;
                    _elapsed = 0f;
                    continue;
            }
        }
    }

    /// <summary>
    /// Advances the player through one walk beat, returning true when it has finished.
    /// <para>
    /// The whole list is asked for at the start rather than a square at a time. The
    /// server checks it square by square either way, and one message per scene beats one
    /// message per footstep for something that happens while a text box is open.
    /// </para>
    /// </summary>
    private bool StepPlayer(SceneBeat.Walk walk, float deltaSeconds)
    {
        if (_step == 0 && _elapsed == 0f)
        {
            foreach (byte step in walk.Steps)
            {
                if (DirectionOf(step) is { } direction) _asked.Add(direction);
            }
        }

        _elapsed += deltaSeconds;

        PlayerInput = null;

        while (_step < walk.Steps.Count)
        {
            if (_elapsed < StepSeconds) return false;

            _elapsed -= StepSeconds;

            PlayerInput = DirectionOf(walk.Steps[_step]);
            _step++;

            return false;
        }

        return true;
    }

    /// <summary>Advances one walk beat, returning true when it has finished.</summary>
    private bool Step(SceneBeat.Walk walk, float deltaSeconds)
    {
        if (!_view.People.TryGetValue(walk.PersonId, out WalkingPerson? person)) return true;

        _walking[walk.PersonId] = person;

        _elapsed += deltaSeconds;

        while (_step < walk.Steps.Count && _elapsed >= StepSeconds)
        {
            _elapsed -= StepSeconds;

            if (DirectionOf(walk.Steps[_step]) is { } direction)
            {
                GridPosition next = person.Square.Step(direction);

                // A scene that walks somebody into a wall is a scene this project has
                // read wrongly, and the right thing then is to turn them and stop rather
                // than to put a person inside the scenery.
                if (_view.Map.Collision.IsWalkable(next)) person.GoTo(next, direction);
                else person.GoTo(person.Square, direction);
            }

            _step++;
        }

        return _step >= walk.Steps.Count;
    }
}
