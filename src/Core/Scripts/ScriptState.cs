namespace PokeMmo.Core.Scripts;

/// <summary>
/// How one script's condition came out: less, equal or greater.
/// <para>
/// Gen III scripts have no expressions. A command puts a comparison somewhere and the
/// jump that follows names which outcomes it wants — so the condition and the branch
/// are two separate instructions with a single register between them, and reading
/// either one alone tells you nothing.
/// </para>
/// </summary>
public enum Comparison
{
    Less,
    Equal,
    Greater,
}

/// <summary>
/// Everything a script has already done to a save: which flags are set and what the
/// variables hold.
/// <para>
/// This is in <c>Core</c> and not beside the reader on purpose. The bytes live on the
/// cartridge and only the client has one; the answers live in the save and only the
/// server has that. The two have to meet somewhere, and it is cheaper for the state to
/// be portable than for either side to grow the other's half.
/// </para>
/// <para>
/// A flag that was never set and a variable that was never written are both simply
/// absent. The games start every save with the whole space zeroed, so absent and zero
/// have to mean the same thing or a fresh character reads as having done everything.
/// </para>
/// </summary>
public sealed class ScriptState
{
    private readonly HashSet<int> _flags;
    private readonly Dictionary<int, int> _variables;
    private readonly HashSet<int> _beaten;

    public ScriptState(
        IEnumerable<int>? flags = null,
        IEnumerable<KeyValuePair<int, int>>? variables = null,
        IEnumerable<int>? beaten = null)
    {
        _flags = flags is null ? [] : [.. flags];
        _variables = variables is null ? [] : new Dictionary<int, int>(variables);
        _beaten = beaten is null ? [] : [.. beaten];
    }

    /// <summary>
    /// Trainers this save has already beaten, by id.
    /// <para>
    /// Beside the flags rather than among them, because it is not one. A
    /// <c>trainerbattle</c> command names a trainer and then a word that is not a flag
    /// number — on a real FireRed image that word is zero for every trainer on Route 8,
    /// and this project spent a commit believing otherwise. Whatever the games use to
    /// remember a beaten trainer is not written in the script, so there is nothing to
    /// read and no number to guess at.
    /// </para>
    /// <para>
    /// The id is used instead. It is this project's own numbering, the server has
    /// persisted it since trainers existed, and it survives a re-export — which the
    /// cartridge's own numbering, whatever it is, would not need to.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<int> Beaten => _beaten;

    public bool HasBeaten(int trainerId) => _beaten.Contains(trainerId);

    public bool MarkBeaten(int trainerId) => _beaten.Add(trainerId);

    public IReadOnlyCollection<int> Flags => _flags;

    public IReadOnlyDictionary<int, int> Variables => _variables;

    public bool Has(int flag) => _flags.Contains(flag);

    /// <summary>Sets a flag, and says whether that changed anything.</summary>
    public bool Set(int flag) => _flags.Add(flag);

    public bool Clear(int flag) => _flags.Remove(flag);

    public int Read(int variable) => _variables.GetValueOrDefault(variable);

    public void Write(int variable, int value)
    {
        // Zero is the absence of a value rather than a value, so that a save which has
        // written a variable back down to nothing is identical to one that never wrote
        // it — which is what the games' zeroed save space means.
        if (value == 0) _variables.Remove(variable);
        else _variables[variable] = value;
    }

    public ScriptState Copy() => new(_flags, _variables, _beaten);

    /// <summary>How two numbers compare, in the only three answers a script has.</summary>
    public static Comparison Compare(int left, int right) =>
        left < right ? Comparison.Less : left > right ? Comparison.Greater : Comparison.Equal;

    /// <summary>
    /// Whether a jump's condition byte accepts a comparison.
    /// <para>
    /// Six conditions over three outcomes, which is every useful combination but "never"
    /// and "always": below, equal, above, and the three negations of those. Written as a
    /// membership test rather than as six cases so that the one thing worth getting right
    /// — which numbers mean which — is a single readable line.
    /// </para>
    /// </summary>
    public static bool Accepts(byte condition, Comparison result) => condition switch
    {
        0 => result == Comparison.Less,
        1 => result == Comparison.Equal,
        2 => result == Comparison.Greater,
        3 => result != Comparison.Greater,
        4 => result != Comparison.Less,
        5 => result != Comparison.Equal,
        _ => false,
    };
}
