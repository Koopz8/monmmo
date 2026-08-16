namespace PokeMmo.Core.Sound;

/// <summary>Which sort of fight is starting, as far as choosing music needs to know.</summary>
public enum BattleKind
{
    /// <summary>Something in the grass, the water or a cave.</summary>
    Wild,

    /// <summary>A person, of no particular standing.</summary>
    Trainer,

    /// <summary>A person the story stops for: a gym, the four, the champion, the rival.</summary>
    Important,

    /// <summary>A fight a script set up, which is the only kind that can name its own song.</summary>
    Scripted,
}

/// <summary>
/// One kind of fight and the song it plays, with where the number came from.
/// </summary>
/// <param name="Kind">Which sort of fight.</param>
/// <param name="Song">The song number, or <see cref="Jukebox.Nothing"/> for no answer.</param>
/// <param name="Read">
/// True when the cartridge said so and false when this project decided. Never inferred from
/// the number itself, and never dropped: a battle theme somebody supplied and a battle theme
/// found in a script are the same integer and different facts.
/// </param>
/// <param name="Where">Where it came from — a script's address, or what the decision was.</param>
public sealed record BattleTheme(BattleKind Kind, int Song, bool Read, string Where);

/// <summary>
/// Which song a fight plays.
/// <para>
/// <b>This is the part of the sound work with the least cartridge in it, and it is worth
/// being exact about why.</b> A map's music is a number in the map header — two bytes, at a
/// fixed place in a record this project has read since the first milestone. A fight's music
/// is not anywhere like that. FireRed chooses it in the sound driver's caller: a handful of
/// constants in a switch on what sort of opponent it is. That is compiled code, and this
/// project does not read code.
/// </para>
/// <para>
/// So the honest split, and it is not an even one:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Read.</b> A fight a script sets up can name its own song, because a script is data and
/// <c>playbgm</c> is one of its commands. The legendaries and the fights the story stops for
/// mostly do. Those numbers come off the file with an address to point at.
/// </description></item>
/// <item><description>
/// <b>Modelled.</b> An ordinary wild encounter and an ordinary trainer have no script, so
/// there is nothing to read and nothing that will ever be readable without reading code. A
/// number in one of those slots is a decision, and it is labelled as one for as long as it
/// sits there.
/// </description></item>
/// </list>
/// <para>
/// <b>What this deliberately does not do is invent the missing ones.</b> An empty slot leaves
/// the map's music playing and is counted, which is the same discipline the battle engine
/// used for silent moves and the animation registry used for sprite behaviours. Filling four
/// slots with four plausible integers would make this look finished and make the count that
/// says otherwise report zero — and a number nobody is watching is how the sample locator
/// missed every cry on the cartridge.
/// </para>
/// <para>
/// The slots are filled from outside rather than from a table in here, which is the same
/// arrangement the sound research asked for: one place a song id is looked up, so a player
/// pointing it at something of their own costs nothing.
/// </para>
/// </summary>
public sealed class BattleMusic
{
    private readonly Dictionary<BattleKind, BattleTheme> _themes = [];

    /// <summary>Nothing known about any fight, which is where this starts.</summary>
    public BattleMusic()
    {
    }

    public BattleMusic(IEnumerable<BattleTheme> themes)
    {
        foreach (BattleTheme theme in themes) Set(theme);
    }

    /// <summary>
    /// Names the song for one kind of fight. A second answer for the same kind replaces the
    /// first, except that something read is never replaced by something decided.
    /// </summary>
    public void Set(BattleTheme theme)
    {
        if (_themes.TryGetValue(theme.Kind, out BattleTheme? had) && had.Read && !theme.Read) return;

        _themes[theme.Kind] = theme;
    }

    /// <summary>
    /// The song for a fight, or <see cref="Jukebox.Nothing"/> when there is no answer.
    /// <para>
    /// Nothing rather than a stand-in. The caller leaves the map's music playing, which is
    /// what happens today and is wrong in a way a player can hear — and that is better than
    /// right-sounding music nobody can trace to a byte.
    /// </para>
    /// </summary>
    public int For(BattleKind kind) =>
        _themes.TryGetValue(kind, out BattleTheme? theme) ? theme.Song : Jukebox.Nothing;

    /// <summary>What is known about one kind of fight, including where the number came from.</summary>
    public BattleTheme? Of(BattleKind kind) =>
        _themes.TryGetValue(kind, out BattleTheme? theme) ? theme : null;

    /// <summary>How many kinds have a song whose number came off the cartridge.</summary>
    public int ReadKinds => _themes.Values.Count(t => t.Read);

    /// <summary>How many have one this project decided.</summary>
    public int ModelledKinds => _themes.Values.Count(t => !t.Read);

    /// <summary>
    /// The kinds of fight with no song at all, which keep playing whatever the map was
    /// playing. The number that has to be watched.
    /// </summary>
    public IReadOnlyList<BattleKind> Silent =>
        [.. Enum.GetValues<BattleKind>().Where(k => !_themes.ContainsKey(k))];

    /// <summary>Everything known, for anybody printing it.</summary>
    public IReadOnlyList<BattleTheme> All => [.. _themes.Values.OrderBy(t => t.Kind)];
}
