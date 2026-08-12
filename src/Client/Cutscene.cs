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
/// The player's own movements are read and not performed. Where the player stands is the
/// server's to say and this side only predicts it; walking them here would be predicting
/// several squares at once with no way to be refused. They are counted and reported so
/// the gap is visible rather than silent.
/// </para>
/// </summary>
public sealed class Cutscene
{
    /// <summary>How long one square of a scripted walk takes.</summary>
    private const float StepSeconds = 0.22f;

    /// <summary>
    /// What each step byte does, derived rather than known.
    /// <para>
    /// Three families — 0x08, 0x10 and 0x1C — read as one ordering at three speeds, and
    /// the ordering came from walking every list across every map and counting who ended
    /// up inside a wall. Two samples that share no scripts, no maps and no starting
    /// squares put the same ordering first: 84% of people's own paths and 95% of the
    /// player's, against 24 orderings.
    /// </para>
    /// <para>
    /// Anything outside these families is a step this project does not model — a jump, a
    /// pause, a change of face — and is treated as standing still. That is the honest
    /// reading: doing nothing is wrong in a way you can see, and guessing is wrong in a
    /// way that walks somebody through a wall.
    /// </para>
    /// </summary>
    public static Direction? DirectionOf(byte step)
    {
        foreach (byte family in Families)
        {
            if (step >= family && step <= family + 3) return Compass[step - family];
        }

        return null;
    }

    private static readonly byte[] Families = [0x08, 0x10, 0x1C];

    private static readonly Direction[] Compass =
        [Direction.Down, Direction.Up, Direction.Left, Direction.Right];

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

    /// <summary>How many of the player's own movements were skipped rather than walked.</summary>
    public int PlayerStepsSkipped =>
        _beats.OfType<SceneBeat.Walk>().Where(w => w.IsPlayer).Sum(w => w.Steps.Count);

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
                    // Not performed. See the note on this class.
                    _beat++;
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
