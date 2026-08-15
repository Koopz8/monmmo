using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// What an ability does, which is the first thing in this project that is code all the way
/// down.
/// <para>
/// Every number modelled so far was somewhere in the cartridge waiting to be found. An
/// ability's <em>name</em> is: seventy-eight of them, thirteen bytes each, anchored on
/// STENCH. Which two a species can have is: two bytes on its own record, extracted since
/// the species table was first located and read by nothing until now. What an ability
/// <b>does</b> is not in the image as data at all — it is in the game's ARM code, the same
/// boundary the <c>special</c> routines sit behind, and no amount of dumping crosses it.
/// </para>
/// <para>
/// So every rule in this file is <b>modelled</b>, and the file says so once here rather
/// than seventy-eight times. What keeps that honest is <see cref="Modelled"/>: an ability
/// this project has not written a rule for is carried, named, shown, and does nothing —
/// and the count of those is reported rather than quietly rounded down to "abilities:
/// yes".
/// </para>
/// <para>
/// The order of business is the same as the battle engine's effect table, which is the
/// pattern this follows: the ones that change a fight, that a test can catch, and that
/// need no machinery this project does not have.
/// </para>
/// </summary>
public static class Abilities
{
    /// <summary>Nobody's ability, and the second slot of a species with only one.</summary>
    public const int None = 0;

    // Read off the cartridge's own name table rather than remembered. Every one of these
    // was printed with its index before it was written down here.
    public const int SandVeil = 8;
    public const int CloudNine = 13;
    public const int SwiftSwim = 33;
    public const int Chlorophyll = 34;
    public const int RainDish = 44;
    public const int AirLock = 77;

    public const int Sturdy = 5;
    public const int Limber = 7;
    public const int VoltAbsorb = 10;
    public const int WaterAbsorb = 11;
    public const int Insomnia = 15;
    public const int Immunity = 17;
    public const int FlashFire = 18;
    public const int OwnTempo = 20;
    public const int WonderGuard = 25;
    public const int Levitate = 26;
    public const int HugePower = 37;
    public const int MagmaArmor = 40;
    public const int WaterVeil = 41;
    public const int ThickFat = 47;
    public const int Guts = 62;
    public const int Overgrow = 65;
    public const int Blaze = 66;
    public const int Torrent = 67;
    public const int Swarm = 68;
    public const int VitalSpirit = 72;
    public const int PurePower = 74;

    /// <summary>
    /// Every ability this project has written a rule for.
    /// <para>
    /// The list exists so the honest number can be printed. Anything not in it is carried
    /// and does nothing, which is a different state from "not supported" and a different
    /// state again from "does nothing in this game" — and the report distinguishes them by
    /// counting this against the names table.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<int> Modelled =
    [
        Limber, VoltAbsorb, WaterAbsorb, Insomnia, Immunity, FlashFire, OwnTempo,
        WonderGuard, Levitate, HugePower, MagmaArmor, WaterVeil, ThickFat, Guts,
        Overgrow, Blaze, Torrent, Swarm, VitalSpirit, PurePower,

        // The ones that read the sky. Not the three that make it — DRIZZLE, DROUGHT and
        // SAND STREAM all happen when somebody arrives, and this engine has no such event
        // to hang them on. They stay silent and stay counted.
        SandVeil, CloudNine, SwiftSwim, Chlorophyll, RainDish, AirLock,
    ];

    /// <summary>
    /// True when this one ignores the weather entirely, and makes everybody else ignore it
    /// too.
    /// <para>
    /// Two abilities that do the same thing, which is the games' own doing rather than a
    /// simplification here. Either of them anywhere in the fight switches the sky off for
    /// everybody, including its owner.
    /// </para>
    /// </summary>
    public static bool Ignores(int ability) => ability is CloudNine or AirLock;

    /// <summary>
    /// What this one does to its owner's Speed under a given sky, in hundredths.
    /// <para>
    /// The two that make a fight about the weather rather than merely coloured by it: a
    /// doubled Speed changes who moves first, which changes everything else.
    /// </para>
    /// </summary>
    public static int Speed(int ability, Weather weather) => (ability, weather) switch
    {
        (SwiftSwim, Weather.Rain) => 200,
        (Chlorophyll, Weather.Sun) => 200,
        _ => 100,
    };

    /// <summary>True when this one is left alone by weather that would otherwise bite.</summary>
    public static bool ShrugsOffWeather(int ability, Weather weather) =>
        ability == SandVeil && weather == Weather.Sandstorm;

    /// <summary>True when this one is healed by the weather rather than hurt by it.</summary>
    public static bool DrinksFrom(int ability, Weather weather) =>
        ability == RainDish && weather == Weather.Rain;

    /// <summary>True when this one does something here rather than only having a name.</summary>
    public static bool DoesSomething(int ability) => Modelled.Contains(ability);

    /// <summary>
    /// Which ability a creature of this species has, given which of the two slots it was
    /// born into.
    /// <para>
    /// A species with one ability has nought in its second slot, and a creature born into
    /// that slot keeps the first one rather than having none. The alternative is a
    /// creature whose ability depends on a coin flip the cartridge never makes.
    /// </para>
    /// </summary>
    public static int Of(SpeciesData? species, int slot) => species is null
        ? None
        : slot == 1 && species.Ability2 != None ? species.Ability2 : species.Ability1;

    /// <summary>
    /// Which slot a fresh creature is born into.
    /// <para>
    /// Rolled once and stored, for the reason a creature's sex is: asking twice gives two
    /// answers, and a creature whose ability changed between questions would be a creature
    /// that could be immune to a move on the turn it mattered and not on the turn after.
    /// </para>
    /// </summary>
    public static int SlotFor(SpeciesData? species, BattleRng rng) =>
        species is null || species.Ability2 == None ? 0 : rng.OneIn(2) ? 1 : 0;

    /// <summary>
    /// What this ability does to a move aimed at its owner, as an effectiveness in
    /// hundredths — or nothing when it has no opinion.
    /// <para>
    /// Nought means the move does not land at all, which is how the four immunities work
    /// and how WONDER GUARD works. The absorbing ones heal in the games and do not here;
    /// the immunity is the part that changes a fight and the healing is the part that
    /// needs a hook this engine has not got. Written down rather than pretended.
    /// </para>
    /// </summary>
    public static int? Against(int ability, MoveData move, int effectiveness) => ability switch
    {
        Levitate when move.Type == PokemonType.Ground => 0,
        VoltAbsorb when move.Type == PokemonType.Electric => 0,
        WaterAbsorb when move.Type == PokemonType.Water => 0,
        FlashFire when move.Type == PokemonType.Fire => 0,

        // Only what is super effective gets through, which is the whole of it. A status
        // move is not damage and is not this ability's business.
        WonderGuard when move.Category != DamageCategory.Status && effectiveness <= 100 => 0,

        _ => null,
    };

    /// <summary>
    /// What the defender's ability does to damage already worked out, in hundredths.
    /// <para>
    /// A hundred is "no opinion", which is what almost every ability has.
    /// </para>
    /// </summary>
    public static int Defending(int ability, MoveData move) => ability switch
    {
        ThickFat when move.Type is PokemonType.Fire or PokemonType.Ice => 50,
        _ => 100,
    };

    /// <summary>
    /// What the attacker's ability does to its attacking stat, in hundredths.
    /// <para>
    /// The three shapes worth having: double Attack outright, half again when hurt in the
    /// right way, and half again when the fight has gone badly and the move is its own
    /// type. Between them they cover the abilities most creatures in this game actually
    /// carry.
    /// </para>
    /// </summary>
    public static int Attacking(int ability, Battler attacker, MoveData move, bool physical) => ability switch
    {
        HugePower or PurePower when physical => 200,

        // Being ill makes it hit harder, which is the joke and also the reason a burn does
        // not halve its Attack the way it halves everybody else's.
        Guts when physical && attacker.Status != StatusCondition.None => 150,

        Overgrow when Cornered(attacker) && move.Type == PokemonType.Grass => 150,
        Blaze when Cornered(attacker) && move.Type == PokemonType.Fire => 150,
        Torrent when Cornered(attacker) && move.Type == PokemonType.Water => 150,
        Swarm when Cornered(attacker) && move.Type == PokemonType.Bug => 150,

        _ => 100,
    };

    /// <summary>Down to a third, which is where the four type boosts switch on.</summary>
    private static bool Cornered(Battler battler) => battler.CurrentHp * 3 <= battler.MaxHp;

    /// <summary>
    /// True when this ability refuses that condition outright.
    /// <para>
    /// The cheapest rules in the file and among the most visible: a creature that cannot be
    /// put to sleep is a creature a whole strategy does not work on, and a player finds
    /// that out the first time they try it.
    /// </para>
    /// </summary>
    public static bool Refuses(int ability, StatusCondition condition) => (ability, condition) switch
    {
        (Limber, StatusCondition.Paralysis) => true,
        (Insomnia or VitalSpirit, StatusCondition.Sleep) => true,
        (Immunity, StatusCondition.Poison) => true,
        (WaterVeil, StatusCondition.Burn) => true,
        (MagmaArmor, StatusCondition.Freeze) => true,
        _ => false,
    };

    /// <summary>True when this ability refuses to be confused.</summary>
    public static bool RefusesConfusion(int ability) => ability == OwnTempo;
}
