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
/// The player is never moved. The cartridge does move them — seventy-five of the world's
/// scenes walk the player somewhere — and this deliberately does not, because a game
/// where other people are also playing cannot take somebody's character away from them
/// for four seconds at a time. The scene happens around them and they walk to it.
/// </para>
/// <para>
/// The cost of that is measured rather than assumed: of the seventy-five, six leave the
/// player standing on a door, and those six are two places — the professor's lab and a
/// house on One Island. Both are doors anybody can walk into. Everything else the
/// cartridge uses the player's feet for is a walk across a map they can take themselves,
/// which is the whole point of not taking it from them.
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

                // Skipped, not performed. See the note at the top: the player's feet are
                // theirs. The beat still costs nothing to pass over, and everything the
                // scene does around them keeps its order.
                case SceneBeat.Walk walk when walk.IsPlayer:
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
