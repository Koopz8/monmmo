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
    public const int Drizzle = 2;
    public const int Static = 9;
    public const int ShieldDust = 19;
    public const int SuctionCups = 21;
    public const int ShadowTag = 23;
    public const int RoughSkin = 24;
    public const int EffectSpore = 27;
    public const int ClearBody = 29;
    public const int MagnetPull = 42;
    public const int PoisonPoint = 38;
    public const int KeenEye = 51;
    public const int HyperCutter = 52;
    public const int ArenaTrap = 71;
    public const int WhiteSmoke = 73;
    public const int FlameBody = 49;
    public const int SandVeil = 8;
    public const int Intimidate = 22;
    public const int SandStream = 45;
    public const int Drought = 70;
    public const int CloudNine = 13;
    public const int SwiftSwim = 33;
    public const int Chlorophyll = 34;
    public const int RainDish = 44;
    public const int AirLock = 77;

    public const int Sturdy = 5;
    public const int Damp = 6;
    public const int BattleArmor = 4;
    public const int ShellArmor = 75;
    public const int RockHead = 69;
    public const int InnerFocus = 39;
    public const int StickyHold = 60;
    public const int Trace = 36;
    public const int ColorChange = 16;
    public const int Forecast = 59;
    public const int ShedSkin = 61;
    public const int SpeedBoost = 3;
    public const int CompoundEyes = 14;
    public const int Hustle = 55;
    public const int MarvelScale = 63;
    public const int SereneGrace = 32;
    public const int LiquidOoze = 64;
    public const int EarlyBird = 48;
    public const int Truant = 54;

    // And the ones that genuinely do nothing in a fight of one against one on this
    // cartridge. They are not silent — they are finished, which is a different answer and
    // the same distinction the move-effect table draws between "nothing to do" and "nobody
    // has written it". Counting them as unmodelled makes the number of things left to build
    // wrong for ever, because nothing will ever be built for them.
    public const int Stench = 1;
    public const int Illuminate = 35;
    public const int Pickup = 53;
    public const int Plus = 57;
    public const int Minus = 58;
    public const int LightningRod = 31;
    public const int Cacophony = 76;
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

        // And the ones that happen when somebody arrives, which needed an event this
        // engine did not have until there was a sky to hang the first three on.
        Drizzle, Drought, SandStream, Intimidate,

        // And the ones that answer being touched, which needed the flag on a move record
        // that this project had been reading past since it first read a move record.
        Static, RoughSkin, EffectSpore, PoisonPoint, FlameBody,

        // And the ones that refuse to be made worse at something, which needed no new
        // machinery at all — the engine has had a shield on stat drops since MIST, and
        // these are four more reasons to raise it.
        ShieldDust, ClearBody, KeenEye, HyperCutter, WhiteSmoke,

        // And the ones about not being allowed to leave, which turned up a gap rather than
        // needing one filled: this engine has blocked running away since WRAP, and has
        // never once blocked switching.
        SuctionCups, ShadowTag, MagnetPull, ArenaTrap,

        // And the seven that refuse something this engine already does. Not one of them
        // needed anything built: a one-hit knockout, a critical, recoil, a flinch, blowing
        // up and having an item taken were all here already and none of them had ever been
        // asked whether the creature would allow it.
        Sturdy, BattleArmor, ShellArmor, RockHead, InnerFocus, Damp, StickyHold,

        // And the five that were waiting for something. Three wanted a creature whose type
        // or ability a fight can change, which arrived with the moves that move them; two
        // wanted somewhere to happen at the end of a turn, which arrived with the berries.
        // None of them is new machinery either — all five are old hooks with a new caller.
        Trace, ColorChange, Forecast, ShedSkin, SpeedBoost,

        // And the ones that change a number somebody was already working out.
        CompoundEyes, Hustle, MarvelScale, SereneGrace, LiquidOoze, EarlyBird, Truant,

        // And the ones that are finished rather than silent.
        Stench, Illuminate, Pickup, Plus, Minus, LightningRod, Cacophony,
    ];

    /// <summary>
    /// Whether this one has nothing to do in a fight of one against one, and that is the
    /// answer rather than a gap.
    /// <para>
    /// Seven of them. Two act outside a battle entirely; two need a partner this game mode
    /// does not have; one draws a move away from somebody who is not there; and two do
    /// nothing anywhere on this cartridge at all — one of which no species even carries.
    /// </para>
    /// <para>
    /// Told apart from unmodelled on purpose, and it is the same distinction the move-effect
    /// table draws between a move that is finished and a move nobody has written. A count of
    /// what is left to build that includes things nothing will ever be built for is a count
    /// that can never reach zero.
    /// </para>
    /// </summary>
    public static bool NothingToDoHere(int ability) =>
        ability is Stench or Illuminate or Pickup or Plus or Minus or LightningRod or Cacophony;

    /// <summary>
    /// What this one does to the accuracy of its own moves, as a percentage.
    /// <para>
    /// Two of them, in opposite directions, and the second is the interesting one: it is the
    /// only ability in the game that makes its owner worse at something in exchange for
    /// making it better at something else.
    /// </para>
    /// </summary>
    public static int Aiming(int ability) => ability switch
    {
        CompoundEyes => 130,
        Hustle => 80,
        _ => 100,
    };

    /// <summary>
    /// What this one does to its own defence, as a percentage.
    /// <para>
    /// One of them, and only while its owner is suffering — the second ability on this
    /// cartridge whose whole point is that being ill helps, and the mirror of the one that
    /// raises Attack for the same reason.
    /// </para>
    /// </summary>
    public static int Guarding(int ability, Battler battler) =>
        ability == MarvelScale && battler.Status != StatusCondition.None ? 150 : 100;

    /// <summary>Whether the chances riding on this one's moves are doubled.</summary>
    public static bool SharpensChances(int ability) => ability == SereneGrace;

    /// <summary>
    /// Whether draining health from this one hurts instead.
    /// <para>
    /// Asked of the creature being drained rather than the one draining, which is what makes
    /// it a punishment rather than a defence.
    /// </para>
    /// </summary>
    public static bool PoisonsWhoDrinks(int ability) => ability == LiquidOoze;

    /// <summary>How many turns of sleep this one loses per turn. Two for one of them.</summary>
    public static int WakesInTurns(int ability) => ability == EarlyBird ? 2 : 1;

    /// <summary>Whether this one can only manage every other turn.</summary>
    public static bool ActsEveryOtherTurn(int ability) => ability == Truant;

    /// <summary>Whether this one takes on the ability of whoever it is standing opposite.</summary>
    public static bool CopiesTheirAbility(int ability) => ability == Trace;

    /// <summary>
    /// Whether this one becomes the type of whatever just hit it.
    /// <para>
    /// The only ability in the game whose owner is a different creature after every
    /// exchange, and it could not be written at all until a type was something a fight
    /// could change.
    /// </para>
    /// </summary>
    public static bool BecomesWhatHitIt(int ability) => ability == ColorChange;

    /// <summary>
    /// Whether this one's type follows the sky.
    /// <para>
    /// One species has it. Under a clear sky it is what it was born as, and under each of
    /// the four it is the type that causes that weather — the same mapping the move whose
    /// type follows the sky uses, and the same reason: it is the game's own arrangement
    /// rather than an opinion about weather.
    /// </para>
    /// </summary>
    public static bool FollowsTheSky(int ability) => ability == Forecast;

    /// <summary>
    /// How often this one sheds whatever ails it, at the end of a turn. <b>Modelled.</b>
    /// <para>
    /// Nought for every ability but one. A third is the games' figure and is nowhere in the
    /// data — which makes it the same kind of number as the three in ten for answering a
    /// touch, and it is named in the same place for the same reason.
    /// </para>
    /// </summary>
    public static int ShedsChance(int ability) => ability == ShedSkin ? 33 : 0;

    /// <summary>Whether this one gets faster at the end of every turn.</summary>
    public static bool GetsFaster(int ability) => ability == SpeedBoost;

    /// <summary>Whether this one cannot simply be ended, however much is left.</summary>
    public static bool CannotBeEndedOutright(int ability) => ability == Sturdy;

    /// <summary>
    /// Whether nothing against this one is ever a critical hit.
    /// <para>
    /// Two abilities, identical in every way, and both are in the table — which is a fact
    /// about the cartridge rather than a redundancy worth collapsing. Two species families
    /// carry one each.
    /// </para>
    /// </summary>
    public static bool NeverCritical(int ability) => ability is BattleArmor or ShellArmor;

    /// <summary>Whether this one pays nothing for the moves that cost their user.</summary>
    public static bool PaysNoRecoil(int ability) => ability == RockHead;

    /// <summary>Whether this one cannot be made to lose its turn by a flinch.</summary>
    public static bool NeverFlinches(int ability) => ability == InnerFocus;

    /// <summary>
    /// Whether this one stops anybody blowing up, on either side.
    /// <para>
    /// The second ability in this file that decides something about somebody else's options
    /// rather than about what happens to it, and the only one that reaches a move its owner
    /// is not the target of. A creature that blows up while one of these is on the field
    /// simply does not, and it is the presence on the field rather than the aim that stops
    /// it.
    /// </para>
    /// </summary>
    public static bool StopsAnybodyBlowingUp(int ability) => ability == Damp;

    /// <summary>Whether what this one is carrying cannot be taken off it.</summary>
    public static bool KeepsWhatItHolds(int ability) => ability == StickyHold;

    /// <summary>
    /// True when the creature opposite may not leave, because of what is standing there.
    /// <para>
    /// Asked of the <em>other</em> side's ability, which is what makes this different from
    /// every other rule in this file: it is the only one where a creature's ability decides
    /// something about somebody else's options rather than about what happens to it.
    /// </para>
    /// <para>
    /// Three of them, each holding a different thing. One holds anybody. One holds anybody
    /// standing on the ground, which is why it asks about Flying and about LEVITATE — the
    /// two ways of not being on it. One holds only what it can stick to.
    /// </para>
    /// </summary>
    public static bool Traps(int ability, PokemonType first, PokemonType second, int theirs) => ability switch
    {
        ShadowTag => true,

        ArenaTrap =>
            first != PokemonType.Flying && second != PokemonType.Flying && theirs != Levitate,

        MagnetPull => first == PokemonType.Steel || second == PokemonType.Steel,

        _ => false,
    };

    /// <summary>True when this one cannot be dragged off the field against its will.</summary>
    public static bool HoldsGround(int ability) => ability == SuctionCups;

    /// <summary>
    /// True when this ability refuses to let somebody else lower that stat.
    /// <para>
    /// Somebody <em>else</em>, which is the whole of the rule and the easy half to get
    /// wrong. Every one of these leaves its owner free to spend its own stats — BELLY DRUM
    /// and OVERHEAT are things you do to yourself, and an ability that stopped them would
    /// be an ability that refused a move its owner chose.
    /// </para>
    /// <para>
    /// Two of them are the same rule under two names, which is the games' doing rather than
    /// a simplification here.
    /// </para>
    /// </summary>
    public static bool Protects(int ability, Stat stat) => ability switch
    {
        ClearBody or WhiteSmoke => true,
        HyperCutter => stat == Stat.Attack,
        KeenEye => stat == Stat.Accuracy,
        _ => false,
    };

    /// <summary>
    /// True when this one is left alone by whatever a move carries as well as its damage.
    /// <para>
    /// The riders rather than the move: SHIELD DUST does not stop a FLAMETHROWER, it stops
    /// the burn that sometimes comes with one. The engine already tells those two apart —
    /// a rider is the thing that rolls against a move's secondary chance — so this is a
    /// rule with no machinery behind it.
    /// </para>
    /// </summary>
    public static bool ShrugsOffRiders(int ability) => ability == ShieldDust;

    /// <summary>
    /// How often an ability that answers a touch actually answers. <b>Modelled.</b>
    /// <para>
    /// Nothing in a move's record or a species' says. Three in ten is the games' figure for
    /// the three that hand over a condition, and it is the same number for all three, which
    /// is at least one number rather than three.
    /// </para>
    /// </summary>
    public const int TouchChance = 30;

    /// <summary>
    /// EFFECT SPORE's, which is lower because it has three things it might do.
    /// <b>Modelled</b>, and the games' figure.
    /// </summary>
    public const int SporeChance = 10;

    /// <summary>
    /// The share of an attacker's own health ROUGH SKIN costs them. <b>Modelled</b>, and
    /// the same sixteenth that everything else which bites once takes.
    /// </summary>
    public const int SkinShare = 16;

    /// <summary>
    /// What touching this one may do to whoever touched it, or nothing.
    /// <para>
    /// The dice are rolled here rather than by the caller so that every one of these
    /// answers the same question the same way, and so a reader can see all four chances in
    /// one place.
    /// </para>
    /// </summary>
    public static StatusCondition? Touched(int ability, BattleRng rng) => ability switch
    {
        Static when rng.Chance(TouchChance) => StatusCondition.Paralysis,
        PoisonPoint when rng.Chance(TouchChance) => StatusCondition.Poison,
        FlameBody when rng.Chance(TouchChance) => StatusCondition.Burn,

        // Three things, each as likely as the others and none of them likely.
        EffectSpore when rng.Chance(SporeChance) => StatusCondition.Poison,
        EffectSpore when rng.Chance(SporeChance) => StatusCondition.Paralysis,
        EffectSpore when rng.Chance(SporeChance) => StatusCondition.Sleep,

        _ => null,
    };

    /// <summary>True when touching this one simply costs the toucher health.</summary>
    public static bool Grazes(int ability) => ability == RoughSkin;

    /// <summary>
    /// What sky this one brings with it when it takes the field, or nothing.
    /// <para>
    /// These three are why an arrival had to become an event. Weather from a move is a
    /// thing somebody spent a turn on; weather from an ability is a thing that is simply
    /// true the moment a creature is standing there, and there was nowhere to say so.
    /// </para>
    /// </summary>
    public static Weather Brings(int ability) => ability switch
    {
        Drizzle => Weather.Rain,
        Drought => Weather.Sun,
        SandStream => Weather.Sandstorm,
        _ => Weather.None,
    };

    /// <summary>
    /// How many stages this one takes off the other side's Attack on arrival.
    /// <para>
    /// One, and only INTIMIDATE. A number rather than a flag because it is the shape the
    /// stage machinery already speaks, and because the next ability of this kind will want
    /// a different one.
    /// </para>
    /// </summary>
    public static int Cows(int ability) => ability == Intimidate ? -1 : 0;

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
