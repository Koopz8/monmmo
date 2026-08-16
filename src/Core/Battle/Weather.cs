using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// What the sky is doing.
/// <para>
/// The first thing in this engine that belongs to the battle rather than to either side of
/// it, which is why it took until now: everything else that lasts turns — a trap, a
/// confusion, a disabled move — hangs off one battler, and there was nowhere for a fact
/// about the room to live.
/// </para>
/// </summary>
public enum Weather
{
    None,
    Rain,
    Sun,
    Sandstorm,
    Hail,
}

/// <summary>
/// What weather does, gathered in one place so the rules can be read rather than hunted.
/// <para>
/// What is <b>read</b>: which moves cause it. Each of the four is a group of one in the
/// cartridge's own effect table — SANDSTORM is 115, RAIN DANCE 136, SUNNY DAY 137, HAIL
/// 164 — and no other move shares any of them, so naming them costs nothing and guesses
/// nothing.
/// </para>
/// <para>
/// What is <b>modelled</b>: everything else. How long it lasts, what it does to damage,
/// what it takes off somebody at the end of a turn. None of that is in the image as data,
/// and this file says so once rather than at every rule.
/// </para>
/// </summary>
public static class Skies
{
    /// <summary>
    /// How long weather lasts when a move causes it. <b>Modelled.</b>
    /// <para>
    /// Nothing in a move's record says. Five turns is the games' own figure and the one
    /// every strategy built on weather assumes.
    /// </para>
    /// </summary>
    public const int Turns = 5;

    /// <summary>
    /// The share of its own health a sandstorm or hail takes each turn. <b>Modelled</b>,
    /// and the same sixteenth that poison and a trap take — which is the games' figure and
    /// is at least consistent with everything else here that bites once a turn.
    /// </summary>
    public const int Share = 16;

    /// <summary>What one of the four moves brings, or nothing when it is not one of them.</summary>
    public static Weather Of(int effect) => effect switch
    {
        Sandstorm => Weather.Sandstorm,
        RainDance => Weather.Rain,
        SunnyDay => Weather.Sun,
        Hail => Weather.Hail,
        _ => Weather.None,
    };

    // Read off the cartridge's own effect table, each a group of exactly one move.
    public const int Sandstorm = 115;
    public const int RainDance = 136;
    public const int SunnyDay = 137;
    public const int Hail = 164;

    /// <summary>THUNDER, which the sky decides the accuracy of.</summary>
    public const int Thunder = 152;

    /// <summary>
    /// What the sky does to a move's damage, in hundredths.
    /// <para>
    /// Rain makes water and unmakes fire; sun does the reverse. Both by half again and by
    /// half, which is the pair of numbers this whole system is built on.
    /// </para>
    /// </summary>
    public static int Damage(Weather weather, PokemonType type) => (weather, type) switch
    {
        (Weather.Rain, PokemonType.Water) => 150,
        (Weather.Rain, PokemonType.Fire) => 50,
        (Weather.Sun, PokemonType.Fire) => 150,
        (Weather.Sun, PokemonType.Water) => 50,
        _ => 100,
    };

    /// <summary>
    /// What the sky does to THUNDER's accuracy, or nothing when it has no opinion.
    /// <para>
    /// The one move whose record is overruled by the weather. It is a group of one, so this
    /// is a rule about a move rather than about a family.
    /// </para>
    /// </summary>
    public static int? Accuracy(Weather weather, int effect) => (weather, effect) switch
    {
        (Weather.Rain, Thunder) => 101,
        (Weather.Sun, Thunder) => 50,
        _ => null,
    };

    /// <summary>
    /// True when this weather takes something off whoever is standing in it.
    /// </summary>
    /// <summary>
    /// The type the sky lends to the one move that takes it.
    /// <para>
    /// Normal under a clear sky, which is the move's own record's type and therefore not a
    /// choice — the other four are, and they are the obvious ones: rain is Water, sun is
    /// Fire, a sandstorm is Rock and hail is Ice. Each is the type that <em>causes</em> that
    /// weather in this game, which is what makes the mapping a reading of the game's own
    /// arrangement rather than an opinion about meteorology.
    /// </para>
    /// </summary>
    public static PokemonType Lends(Weather weather) => weather switch
    {
        Weather.Rain => PokemonType.Water,
        Weather.Sun => PokemonType.Fire,
        Weather.Sandstorm => PokemonType.Rock,
        Weather.Hail => PokemonType.Ice,
        _ => PokemonType.Normal,
    };

    public static bool Bites(Weather weather) => weather is Weather.Sandstorm or Weather.Hail;

    /// <summary>
    /// True when these types are left alone by it.
    /// <para>
    /// Rock, Ground and Steel are at home in a sandstorm and Ice is at home in hail. Those
    /// lists are the games' and are modelled here — nothing in a type's record says what
    /// weather it minds.
    /// </para>
    /// </summary>
    public static bool Shrugs(Weather weather, PokemonType first, PokemonType second) => weather switch
    {
        Weather.Sandstorm => Is(first, second, PokemonType.Rock, PokemonType.Ground, PokemonType.Steel),
        Weather.Hail => Is(first, second, PokemonType.Ice),
        _ => true,
    };

    private static bool Is(PokemonType first, PokemonType second, params PokemonType[] any) =>
        any.Contains(first) || any.Contains(second);
}
