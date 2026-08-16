using PokeMmo.Core.Data;

namespace PokeMmo.Core.Battle;

/// <summary>
/// What a creature is carrying, in a fight.
/// <para>
/// A better position than abilities were in, and the difference is worth stating because it
/// is the whole reason this file is shorter than it looks. Every item record on this
/// cartridge carries two bytes that have been extracted since the item table was first
/// located and read by nothing: an effect number and a parameter. Seventy items of three
/// hundred and eight carry an effect, across sixty-six distinct numbers.
/// </para>
/// <para>
/// The effect number is <b>read</b>. The parameter is <b>read</b>. What the number means is
/// not in the image — it is in the game's ARM code, the same boundary abilities sit behind —
/// so the meaning is <b>modelled</b>, once per number, here.
/// </para>
/// <para>
/// The parameter turned out to be the magnitude almost every time, which was not obvious in
/// advance and is the most useful thing this file found. QUICK CLAW carries twenty and goes
/// first one time in five; KING'S ROCK carries ten and flinches one time in ten; BRIGHTPOWDER
/// carries ten and LAX INCENSE carries five, and that is exactly the difference between them;
/// SHELL BELL carries eight and heals an eighth. Seventeen type-boosting items carry ten, and
/// ten per cent is what they are worth. So the numbers below are the cartridge's rather than
/// this project's, and where one is not, it says so.
/// </para>
/// <para>
/// The file is in two halves and the cartridge drew the line between them. The first half is
/// carried and stays carried. The second is <b>used up when it works</b> — every berry and
/// the WHITE HERB, twenty-two effect numbers — and needed the one thing this engine had
/// never had: a held item that could be lost.
/// </para>
/// <para>
/// Ten effect numbers are on neither side and do nothing. Every one of them is about
/// something outside a fight — money, friendship, experience, wild encounters, evolution —
/// or, in the MENTAL HERB's case, about a condition nothing here can inflict. They are
/// carried, counted, and silent.
/// </para>
/// </summary>
public static class HeldItems
{
    // Every one of these was printed with its items and its parameter before it was written
    // down here — see RomDump's --holds.

    /// <summary>BRIGHTPOWDER and LAX INCENSE. The parameter is how much harder to hit.</summary>
    public const int Slippery = 22;

    /// <summary>MACHO BRACE. Half the speed, for twice what a fight teaches.</summary>
    public const int Heavy = 24;

    /// <summary>QUICK CLAW. The parameter is the chance, in a hundred, of going first.</summary>
    public const int Quick = 26;

    /// <summary>CHOICE BAND. Harder, and only ever the one move.</summary>
    public const int Choice = 29;

    /// <summary>KING'S ROCK. The parameter is the chance, in a hundred, of a flinch.</summary>
    public const int Startling = 30;

    /// <summary>SILVERPOWDER, which boosts Bug and sits apart from the other sixteen.</summary>
    public const int BugBoost = 31;

    /// <summary>SOUL DEW.</summary>
    public const int Dew = 34;

    /// <summary>DEEPSEATOOTH.</summary>
    public const int SeaTooth = 35;

    /// <summary>DEEPSEASCALE.</summary>
    public const int SeaScale = 36;

    /// <summary>FOCUS BAND. The parameter is the chance, in a hundred, of surviving.</summary>
    public const int Enduring = 39;

    /// <summary>SCOPE LENS.</summary>
    public const int Lens = 41;

    /// <summary>METAL COAT, which boosts Steel and also sits apart.</summary>
    public const int SteelBoost = 42;

    /// <summary>LEFTOVERS.</summary>
    public const int Scraps = 43;

    /// <summary>LIGHT BALL.</summary>
    public const int Ball = 45;

    /// <summary>SHELL BELL. The parameter is what fraction of the damage comes back.</summary>
    public const int Bell = 62;

    /// <summary>LUCKY PUNCH.</summary>
    public const int Punch = 63;

    /// <summary>METAL POWDER.</summary>
    public const int Powder = 64;

    /// <summary>THICK CLUB.</summary>
    public const int Club = 65;

    /// <summary>STICK.</summary>
    public const int Stick = 66;

    /// <summary>
    /// The sixteen effect numbers that each boost one type of move, and which type each is.
    /// <para>
    /// The pairing is the one thing in this file that could not be read at all. The effect
    /// number is on the record and the type is not; what says SOFT SAND is about Ground is
    /// the item's <em>name</em>, and the server has never seen a name. So this table was
    /// written by hand from a printout and is modelled in the strongest sense — an error in
    /// it would be a silent ten per cent on the wrong type rather than anything that throws.
    /// </para>
    /// <para>
    /// Two of the seventeen sit outside the run: SILVERPOWDER at 31 and METAL COAT at 42.
    /// They are here rather than in a special case because they behave identically; only
    /// their numbering is odd, and numbering is not a rule.
    /// </para>
    /// <para>
    /// DRAGON SCALE is deliberately absent. It carries an effect number and a parameter of
    /// ten like the rest, and in this generation it is an evolution item that does nothing in
    /// a fight — which is exactly the kind of thing a table built by pattern rather than by
    /// reading would have got wrong.
    /// </para>
    /// </summary>
    private static readonly Dictionary<int, PokemonType> Boosts = new()
    {
        [BugBoost] = PokemonType.Bug,
        [SteelBoost] = PokemonType.Steel,
        [46] = PokemonType.Ground,
        [47] = PokemonType.Rock,
        [48] = PokemonType.Grass,
        [49] = PokemonType.Dark,
        [50] = PokemonType.Fighting,
        [51] = PokemonType.Electric,
        [52] = PokemonType.Water,
        [53] = PokemonType.Flying,
        [54] = PokemonType.Poison,
        [55] = PokemonType.Ice,
        [56] = PokemonType.Ghost,
        [57] = PokemonType.Psychic,
        [58] = PokemonType.Fire,
        [59] = PokemonType.Dragon,
        [60] = PokemonType.Normal,
    };

    /// <summary>
    /// Every effect number this project has written a rule for.
    /// <para>
    /// The same list abilities keep and for the same reason: an item this project has not
    /// written a rule for is carried, named, shown, and does nothing, and the honest number
    /// is printed rather than rounded up to "held items: yes".
    /// </para>
    /// </summary>
    /// <para>
    /// Filled in a static constructor rather than where it is declared, and that is not
    /// style. A field initialiser runs in the order the fields are written, so a list built
    /// out of tables declared below it is a list built out of nulls — and the failure is a
    /// type that will not load at all rather than anything a reader would trace back to
    /// here. A static constructor runs after every initialiser, whatever the order.
    /// </para>
    public static readonly IReadOnlyList<int> Modelled;

    static HeldItems() =>
        Modelled =
        [
            Slippery, Heavy, Quick, Choice, Startling, Dew, SeaTooth, SeaScale, Enduring,
            Lens, Scraps, Ball, Bell, Punch, Powder, Club, Stick,
            .. Boosts.Keys,

            // And the half that is used up, which needed a held item that could be lost.
            .. Eaten,
        ];

    /// <summary>True when carrying this changes anything at all.</summary>
    public static bool DoesSomething(int effect) => Modelled.Contains(effect);

    /// <summary>
    /// The species each of the seven species-locked items is about.
    /// <para>
    /// Modelled the same way the type table is and for the same reason — a THICK CLUB is
    /// about CUBONE because of what it is called. The numbers are this cartridge's own
    /// species indices, printed before being written down.
    /// </para>
    /// </summary>
    private const int Pikachu = 25;

    private const int Cubone = 104;

    private const int Marowak = 105;

    private const int Farfetchd = 83;

    private const int Chansey = 113;

    private const int Ditto = 132;

    private const int Clamperl = 366;

    private static readonly int[] Eons = [380, 381];

    /// <summary>
    /// What this multiplies one of the carrier's stats by, as a percentage, or a hundred.
    /// <para>
    /// A percentage rather than a fraction because that is how the ability hook next to it
    /// works and because two of these are one-and-a-half rather than two. Applied to the
    /// stat rather than to the finished damage, which is where the games put it and also
    /// where it composes with everything else that touches a stat.
    /// </para>
    /// <para>
    /// The species check is what makes six of these seven worth having. A THICK CLUB on
    /// anything but a CUBONE or a MAROWAK is a stone, and an implementation that forgot to
    /// ask would be one that doubled everybody's attack.
    /// </para>
    /// </summary>
    public static int Multiplies(ItemData? carried, int species, Stat stat) => carried?.HoldEffect switch
    {
        Ball when species == Pikachu && stat == Stat.SpAttack => 200,
        Club when species is Cubone or Marowak && stat == Stat.Attack => 200,
        Powder when species == Ditto && stat == Stat.Defense => 200,
        SeaTooth when species == Clamperl && stat == Stat.SpAttack => 200,
        SeaScale when species == Clamperl && stat == Stat.SpDefense => 200,
        Dew when Eons.Contains(species) && stat is Stat.SpAttack or Stat.SpDefense => 150,

        // A CHOICE BAND is half again on Attack and is the only one here that asks nothing
        // about who is carrying it. What it costs is on the other side of the file.
        Choice when stat == Stat.Attack => 150,

        // And the one that costs rather than gives. Half the Speed, all the time, in
        // exchange for what a fight teaches — which is not a battle rule at all and is why
        // only the halving is here.
        Heavy when stat == Stat.Speed => 50,

        _ => 100,
    };

    /// <summary>
    /// What this adds to a move's damage, as a percentage of it, or a hundred.
    /// <para>
    /// The seventeen type boosters, and the only rule in this file that reads its magnitude
    /// straight off the record: MYSTIC WATER carries ten and SEA INCENSE carries five, both
    /// under the same effect number, and the difference between them is entirely the
    /// parameter. Hardcoding ten would have been right sixteen times out of seventeen, which
    /// is the worst kind of nearly.
    /// </para>
    /// </summary>
    public static int Boosting(ItemData? carried, PokemonType type) =>
        carried is not null
        && Boosts.TryGetValue(carried.HoldEffect, out PokemonType boosted)
        && boosted == type
            ? 100 + carried.HoldEffectParam
            : 100;

    /// <summary>
    /// How much harder this makes its carrier to hit, as a percentage taken off a move's
    /// accuracy.
    /// </summary>
    public static int Slipperiness(ItemData? carried) =>
        carried?.HoldEffect == Slippery ? carried.HoldEffectParam : 0;

    /// <summary>
    /// How many extra critical stages this is worth.
    /// <para>
    /// One for the lens that anybody may carry, two for the two that are about a particular
    /// creature — which is the games' own arrangement and the reason a CHANSEY carrying a
    /// LUCKY PUNCH crits about a quarter of the time.
    /// </para>
    /// <para>
    /// Modelled, and one of the few things here that is: the parameter on all three is
    /// nought, so the cartridge is not saying how much they are worth.
    /// </para>
    /// </summary>
    public static int Sharpens(ItemData? carried, int species) => carried?.HoldEffect switch
    {
        Lens => 1,
        Punch when species == Chansey => 2,
        Stick when species == Farfetchd => 2,
        _ => 0,
    };

    /// <summary>The chance in a hundred that this takes the turn regardless of speed.</summary>
    public static int Hurries(ItemData? carried) =>
        carried?.HoldEffect == Quick ? carried.HoldEffectParam : 0;

    /// <summary>The chance in a hundred that being hit by this carrier's move costs a turn.</summary>
    public static int Startles(ItemData? carried) =>
        carried?.HoldEffect == Startling ? carried.HoldEffectParam : 0;

    /// <summary>The chance in a hundred of surviving on one point instead of fainting.</summary>
    public static int Endures(ItemData? carried) =>
        carried?.HoldEffect == Enduring ? carried.HoldEffectParam : 0;

    /// <summary>
    /// What fraction of the damage dealt comes back as health, as a denominator, or nothing.
    /// </summary>
    public static int? Drains(ItemData? carried) =>
        carried?.HoldEffect == Bell && carried.HoldEffectParam > 0 ? carried.HoldEffectParam : null;

    /// <summary>
    /// A sixteenth of the carrier's health, at the end of every turn.
    /// <para>
    /// The one number here that contradicts its own record. LEFTOVERS carries a parameter of
    /// ten, and what it heals in these games is a sixteenth — six and a quarter per cent. The
    /// two are not the same number and no reading of the ten makes them agree, so the
    /// sixteenth is <b>modelled</b> and the ten is left alone rather than pressed into
    /// meaning something.
    /// </para>
    /// </summary>
    public const int ScrapsFraction = 16;

    public static bool Feeds(ItemData? carried) => carried?.HoldEffect == Scraps;

    /// <summary>True when carrying this means only ever using the first move chosen.</summary>
    public static bool Locks(ItemData? carried) => carried?.HoldEffect == Choice;

    // ---- the half that is used up ------------------------------------------------------

    /// <summary>
    /// BERRY JUICE, ORAN and SITRUS. The parameter is how much health, flat.
    /// <para>
    /// Three items, three different parameters — twenty, ten and thirty — under one effect
    /// number, which is the same shape as the two incenses and the same reason the amount
    /// cannot be written here.
    /// </para>
    /// </summary>
    public const int Restores = 1;

    /// <summary>LEPPA. The parameter is how many uses come back.</summary>
    public const int RestoresPp = 7;

    /// <summary>WHITE HERB. Everything that was lowered, back where it was.</summary>
    public const int Herb = 23;

    /// <summary>
    /// The six that clear one thing each, and the one that clears everything.
    /// <para>
    /// Read straight across: six consecutive effect numbers on six consecutive berries, each
    /// with a parameter of nought because there is nothing to say about how much of a
    /// paralysis to clear. Which one clears which is the item's name again, so the pairing is
    /// modelled and the numbering is read.
    /// </para>
    /// </summary>
    private static readonly Dictionary<int, Ailments> Cures = new()
    {
        [2] = Ailments.Paralysis,
        [3] = Ailments.Sleep,
        [4] = Ailments.Poison,
        [5] = Ailments.Burn,
        [6] = Ailments.Freeze,
        [8] = Ailments.Confusion,
        [9] = Ailments.Everything,
    };

    /// <summary>
    /// The five that put health back and may confuse, and which stat's dislike does it.
    /// <para>
    /// A nature that lowers a stat dislikes the flavour that stat belongs to, and eating
    /// something you dislike in a hurry is what confuses you. The stat pairing is modelled —
    /// FIGY is spicy because of what it is called — but which natures dislike what is
    /// entirely read: <see cref="Stats.EffectOf"/> gives the raised and lowered stat of every
    /// nature straight off the table the cartridge computes stats from.
    /// </para>
    /// </summary>
    private static readonly Dictionary<int, Stat> Flavours = new()
    {
        [10] = Stat.Attack,
        [11] = Stat.SpAttack,
        [12] = Stat.Speed,
        [13] = Stat.SpDefense,
        [14] = Stat.Defense,
    };

    /// <summary>
    /// The seven that answer being nearly finished, and what each is worth.
    /// <para>
    /// Five raise a stat. LANSAT sharpens instead, and STARF raises one at random by two —
    /// both are here as nulls because "which stat" is the wrong question for them, and the
    /// caller asks the two hooks below instead.
    /// </para>
    /// </summary>
    private static readonly Dictionary<int, Stat?> Pinches = new()
    {
        [15] = Stat.Attack,
        [16] = Stat.Defense,
        [17] = Stat.Speed,
        [18] = Stat.SpAttack,
        [19] = Stat.SpDefense,
        [20] = null,
        [21] = null,
    };

    /// <summary>LANSAT, which sharpens rather than strengthens.</summary>
    public const int Sharpening = 20;

    /// <summary>STARF, which raises one at random by two.</summary>
    public const int Wild = 21;

    /// <summary>
    /// The share of its health below which a berry that answers being hurt wakes up.
    /// <b>Modelled.</b>
    /// <para>
    /// Half, and it is not on any record: the parameter on ORAN is the ten it restores and
    /// the parameter on FIGY is the eighth it restores, so neither of them is a threshold.
    /// The <em>pinch</em> berries are different and say so on their own records — every one
    /// of the seven carries four, and a quarter is exactly when they go off — so that one is
    /// read and this one is not.
    /// </para>
    /// </summary>
    public const int HurtShare = 2;

    /// <summary>Every effect number that is used up when it works.</summary>
    public static readonly IReadOnlyList<int> Eaten =
    [
        Restores, RestoresPp, Herb,
        .. Cures.Keys,
        .. Flavours.Keys,
        .. Pinches.Keys,
    ];

    /// <summary>True when using this uses it up.</summary>
    public static bool IsEaten(ItemData? carried) =>
        carried is not null && Eaten.Contains(carried.HoldEffect);

    /// <summary>How much health this puts back flat, or nothing.</summary>
    public static int? Restoring(ItemData? carried) =>
        carried?.HoldEffect == Restores && carried.HoldEffectParam > 0 ? carried.HoldEffectParam : null;

    /// <summary>
    /// What share of its maximum this puts back, as a denominator, or nothing — and which
    /// stat's dislike confuses whoever ate it.
    /// </summary>
    public static (int Share, Stat Disliked)? Feeding(ItemData? carried) =>
        carried is not null
        && Flavours.TryGetValue(carried.HoldEffect, out Stat disliked)
        && carried.HoldEffectParam > 0
            ? (carried.HoldEffectParam, disliked)
            : null;

    /// <summary>What this clears, or nothing.</summary>
    public static Ailments Clearing(ItemData? carried) =>
        carried is not null && Cures.TryGetValue(carried.HoldEffect, out Ailments cleared)
            ? cleared
            : Ailments.None;

    /// <summary>How many uses of a spent move this puts back, or nothing.</summary>
    public static int? Refilling(ItemData? carried) =>
        carried?.HoldEffect == RestoresPp && carried.HoldEffectParam > 0 ? carried.HoldEffectParam : null;

    /// <summary>True when this puts back everything that was lowered.</summary>
    public static bool Restoring(ItemData? carried, bool stages) =>
        stages && carried?.HoldEffect == Herb;

    /// <summary>
    /// The share of its maximum below which this answers, as a denominator, or nothing.
    /// <para>
    /// Read: every one of the seven carries four, and a quarter is when they go off. The one
    /// threshold on this cartridge that did not have to be invented.
    /// </para>
    /// </summary>
    public static int? PinchedAt(ItemData? carried) =>
        carried is not null && Pinches.ContainsKey(carried.HoldEffect) && carried.HoldEffectParam > 0
            ? carried.HoldEffectParam
            : null;

    /// <summary>Which stat this raises when it goes off, or nothing for the odd two.</summary>
    public static Stat? Raises(ItemData? carried) =>
        carried is not null && Pinches.TryGetValue(carried.HoldEffect, out Stat? stat) ? stat : null;
}
