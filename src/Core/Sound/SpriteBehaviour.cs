namespace PokeMmo.Core.Sound;

/// <summary>
/// What a sprite in a move's animation actually does over time.
/// <para>
/// <b>Modelled, every one of them.</b> The cartridge's animation script says which template
/// to make and where; the template is a pointer to a struct pointing at a callback function,
/// and a callback function is compiled code. So these are behaviours this project writes,
/// chosen to cover what the callbacks visibly do rather than to reproduce them.
/// </para>
/// <para>
/// The list is short on purpose. An enormous number of moves in this game are one of a
/// handful of things — something travels from the attacker to the target, something appears
/// on the target and fades, the screen flashes a colour — and the long tail after that is
/// filled in over time. What matters is that the tail is <em>counted</em>.
/// </para>
/// </summary>
public enum SpriteBehaviour
{
    /// <summary>
    /// Nothing modelled for this template yet. A plain hit is drawn instead, with the
    /// script's own timing and the script's own sounds.
    /// </summary>
    NotYetModelled,

    /// <summary>Straight from the one using the move to the one being hit.</summary>
    Travels,

    /// <summary>Thrown in an arc rather than a line.</summary>
    Arcs,

    /// <summary>Appears where the target is and fades out.</summary>
    Lands,

    /// <summary>Sits on the user rather than the target — a wind-up, a glow, a shield.</summary>
    OnTheUser,

    /// <summary>The whole screen, rather than anybody in particular.</summary>
    Screen,

    /// <summary>The target shakes.</summary>
    Shakes,
}

/// <summary>
/// Which behaviour a sprite template gets, and how much of the game that comes to.
/// <para>
/// This is deliberately the same shape as <c>MoveEffects</c> and its silent groups, for the
/// same reason: a move whose animation is not modelled yet still animates — with the correct
/// timing and the correct sounds, because both of those are read — and it is <em>counted</em>.
/// A generic flash for every move is a day's work that can never improve, because nothing is
/// measuring it. A registry with a count gets better every time somebody spends an evening
/// on it, and anybody can see by how much.
/// </para>
/// <para>
/// Templates are identified by the address the script names. That address is a cartridge
/// address, so this table is a set of numbers a particular cartridge uses — which makes it
/// the one part of the sound and animation work that is <em>not</em> portable to another
/// game. Said plainly here rather than discovered later.
/// </para>
/// </summary>
public sealed class SpriteBehaviours
{
    private readonly Dictionary<uint, SpriteBehaviour> _known = [];
    private readonly HashSet<uint> _steppedOver = [];

    /// <summary>Teaches this registry what one template does.</summary>
    public void Learn(uint template, SpriteBehaviour behaviour) => _known[template] = behaviour;

    /// <summary>
    /// What this template does, or that nothing is modelled for it yet — and remembers the
    /// asking either way, which is what makes the count possible.
    /// </summary>
    public SpriteBehaviour Of(uint template)
    {
        if (_known.TryGetValue(template, out SpriteBehaviour behaviour)) return behaviour;

        _steppedOver.Add(template);

        return SpriteBehaviour.NotYetModelled;
    }

    /// <summary>How many distinct templates are modelled.</summary>
    public int Modelled => _known.Count;

    /// <summary>
    /// Templates that have been asked about and have no behaviour. The number this project
    /// is trying to drive down, and the number that would otherwise be invisible.
    /// </summary>
    public IReadOnlyCollection<uint> SteppedOver => _steppedOver;

    /// <summary>
    /// What proportion of a set of moves this registry can animate properly.
    /// <para>
    /// Counted over moves rather than over templates, because a template used by forty moves
    /// and one used by a single move are not worth the same. This is the number that says
    /// whether an evening's work mattered.
    /// </para>
    /// </summary>
    public Coverage Over(IEnumerable<IReadOnlyList<uint>> movesAndTheirTemplates)
    {
        int moves = 0;
        int animated = 0;

        foreach (IReadOnlyList<uint> templates in movesAndTheirTemplates)
        {
            moves++;

            // A move counts as animated when every template it names is modelled. Every
            // rather than any: a move that draws three things and knows what one of them
            // does is a move that looks wrong, not a move that looks two-thirds right.
            if (templates.Count > 0 && templates.All(t => Of(t) != SpriteBehaviour.NotYetModelled))
                animated++;
        }

        return new Coverage(moves, animated, _known.Count, _steppedOver.Count);
    }
}

/// <summary>How much of the game animates properly, said as numbers rather than impressions.</summary>
public sealed record Coverage(int Moves, int Animated, int TemplatesModelled, int TemplatesNot)
{
    public int NotAnimated => Moves - Animated;

    public override string ToString() =>
        $"{Animated} of {Moves} moves animate; " +
        $"{TemplatesModelled} templates modelled, {TemplatesNot} not yet";
}
