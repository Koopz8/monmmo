using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;

namespace PokeMmo.Server;

/// <summary>
/// Turns saved numbers into battlers, using the rules file.
/// <para>
/// This is the server's half of the join the client does with a cartridge. Where
/// <c>PartyBuilder.Restore</c> reads a species index and finds a name and a sprite,
/// this reads the same index and finds base stats — everything needed to decide a
/// battle and nothing that could be called content.
/// </para>
/// </summary>
public sealed class BattleFactory(GameRules rules)
{
    /// <summary>The rules this was built from, for the few questions that are not about battles.</summary>
    public GameRules Rules => rules;

    /// <summary>Rebuilds a party member exactly as it was saved.</summary>
    /// <summary>
    /// The move a spent creature is left with, or nothing when this cartridge has none.
    /// <para>
    /// A number in the rules file, found at export off the cartridge's own move names.
    /// Handed to every battle this factory has anything to do with, because a battle
    /// cannot look one up: it has two creatures and some dice.
    /// </para>
    /// </summary>
    public MoveData? Struggle => rules.StruggleMove > 0 ? rules.MoveAt(rules.StruggleMove) : null;

    public Battler? Restore(SavedMon saved)
    {
        if (rules.SpeciesAt(saved.Species) is not { } species) return null;

        var battler = new Battler(species, saved.Level, saved.Nature, saved.Nickname);

        foreach (int moveId in saved.Moves)
            if (rules.MoveAt(moveId) is { } move) battler.Moves.Add(move);

        if (battler.Moves.Count == 0) battler.Moves.AddRange(rules.MovesKnownAt(saved.Species, saved.Level));

        // Damage is applied rather than health assigned, because maximum health is
        // computed from base stats — writing a stored number straight in would drift
        // the first time anything about that calculation changed.
        int missing = Math.Clamp(battler.MaxHp - saved.CurrentHp, 0, battler.MaxHp);
        if (missing > 0) battler.TakeDamage(missing);

        battler.Status = saved.Status;
        battler.Holding = saved.HeldItem;

        // And what is left of each move. Empty means full, which is what everything
        // written before PP existed comes back as.
        battler.RestorePp(saved.Pp);

        return battler;
    }

    /// <summary>
    /// How often a wild one is carrying its common item, and its rare one, out of a
    /// hundred.
    /// <para>
    /// <b>Modelled, not read.</b> Every species record on this cartridge names up to two
    /// items — 105 of the 386 name at least one — and says nothing anywhere about how
    /// often either turns up. The two numbers are here, in one place, rather than spread
    /// through the code that uses them.
    /// </para>
    /// <para>
    /// A species naming only its rare slot, which is most of them, therefore carries
    /// something one time in twenty. A species naming both — RATICATE, PARAS — carries
    /// the common one about half the time and the rare one rarely.
    /// </para>
    /// </summary>
    private const int OftenOutOfAHundred = 50;

    private const int RarelyOutOfAHundred = 5;

    /// <summary>
    /// What a wild one of this species walks out of the grass carrying, or nothing.
    /// <para>
    /// The rare slot is asked first, so a species naming both can still produce the rarer
    /// one — asking the common slot first would make the rare one unreachable whenever
    /// the common roll succeeded.
    /// </para>
    /// </summary>
    public int HeldBy(SpeciesData species, BattleRng rng)
    {
        if (species.Item2 != 0 && rng.Next(100) < RarelyOutOfAHundred) return species.Item2;
        if (species.Item1 != 0 && rng.Next(100) < OftenOutOfAHundred) return species.Item1;

        return 0;
    }

    /// <summary>Builds a wild encounter with the moves that species would know by then.</summary>
    public Battler? Wild(int species, int level, BattleRng? rng = null)
    {
        if (rules.SpeciesAt(species) is not { } record) return null;

        var battler = new Battler(record, level);
        battler.Moves.AddRange(rules.MovesKnownAt(species, level));

        // And whatever it happens to be carrying, which THIEF has been able to take off
        // somebody since it was written and which nothing has ever been carrying.
        if (rng is not null) battler.Holding = HeldBy(record, rng);

        // A creature with no moves at all cannot take a turn, so it gets the first
        // move in the table rather than standing there.
        if (battler.Moves.Count == 0 && rules.MoveAt(1) is { } fallback) battler.Moves.Add(fallback);

        return battler;
    }

    /// <summary>
    /// Builds a trainer's party, in the order it comes out.
    /// <para>
    /// Most trainers leave their moves to the level-up set — that is what the games do
    /// — so an empty move list means "whatever this species knows by now" rather than
    /// "no moves". Taking it literally would field a party that cannot take a turn.
    /// </para>
    /// </summary>
    public List<Battler> TrainerParty(int trainerId)
    {
        if (rules.TrainerAt(trainerId) is not { } party) return [];

        var built = new List<Battler>();

        foreach (TrainerMember member in party.Members)
        {
            if (rules.SpeciesAt(member.Species) is not { } species) continue;

            var battler = new Battler(species, member.Level);

            foreach (int moveId in member.Moves)
                if (rules.MoveAt(moveId) is { } move) battler.Moves.Add(move);

            if (battler.Moves.Count == 0)
                battler.Moves.AddRange(rules.MovesKnownAt(member.Species, member.Level));

            if (battler.Moves.Count == 0 && rules.MoveAt(1) is { } fallback) battler.Moves.Add(fallback);

            // What the cartridge says they are carrying. This number has been read off
            // every trainer's party since trainers existed and thrown away here.
            battler.Holding = member.HeldItem;

            built.Add(battler);
        }

        return built;
    }

    /// <summary>What a battler looks like to the other end: numbers, no names.</summary>
    /// <summary>
    /// What one side looks like to a client.
    /// <para>
    /// <paramref name="own"/> decides whether what is left of each move goes with it. The
    /// games show a player their own PP and never the other side's, and a view that
    /// carried both would be a client that could be asked not to display something it
    /// already has.
    /// </para>
    /// </summary>
    public static BattlerView View(Battler battler, bool own = false) => new(
        battler.Species.Index,
        battler.Level,
        battler.Nickname,
        battler.CurrentHp,
        battler.MaxHp,
        battler.Status,
        battler.Moves.Select(m => m.Id).ToList())
    {
        Pp = own ? [.. Enumerable.Range(0, battler.Moves.Count).Select(battler.PpLeft)] : [],
    };

    /// <summary>
    /// What to write down for a battler.
    /// <para>
    /// Experience is not among the things a battler carries, so what comes back here has
    /// none. That is right for something just caught, whose experience is its level and
    /// nothing more — and wrong for anything that already had a save, which is why every
    /// caller starting from one puts its experience back afterwards.
    /// </para>
    /// </summary>
    public static SavedMon Save(Battler battler) => new(
        battler.Species.Index,
        battler.Level,
        battler.Nickname,
        battler.CurrentHp,
        battler.Status,
        battler.Nature,
        battler.Moves.Select(m => m.Id).ToList())
    {
        HeldItem = battler.Holding,
        Pp = [.. Enumerable.Range(0, battler.Moves.Count).Select(battler.PpLeft)],
    };

    /// <summary>
    /// A party member restored to full health, as a visit to a centre would leave it.
    /// <para>
    /// Rebuilt rather than patched, because maximum health is computed from base stats
    /// — the stored number is not something to raise to a value worked out elsewhere.
    /// </para>
    /// <para>
    /// The experience is carried across by hand, and that line is the whole reason this
    /// method is worth reading twice. Rebuilding loses whatever the record does not
    /// hold, and a battler holds no experience — so a visit to a counter used to hand
    /// back a creature at the bottom of its level with everything since the last one
    /// gone. Nothing announced it. Levelling simply never happened twice, and the fights
    /// in between looked like they had counted.
    /// </para>
    /// </summary>
    public SavedMon Healed(SavedMon saved)
    {
        if (Restore(saved) is not { } battler) return saved;

        battler.Heal(battler.MaxHp);
        battler.Status = StatusCondition.None;
        battler.SleepTurns = 0;

        // And every use back, which is what a visit to a counter is for as much as the
        // health is. A party healed with nothing left to swing with is a party that can
        // only struggle, and there would be nowhere to fix that.
        battler.RefillPp();

        return Save(battler) with { Experience = saved.Experience };
    }

    /// <summary>
    /// A party member with health put back on, and how much actually went on.
    /// <para>
    /// The amount is worked out here rather than by whoever asked, because it depends on
    /// maximum health and maximum health is computed from base stats — a full restore is
    /// the word "all", not a number, and only this side knows what all is. It also means
    /// a client cannot drink a Potion for two hundred, which is the same rule the battle
    /// screen has followed since potions worked there.
    /// </para>
    /// </summary>
    public (SavedMon Mon, int Restored) Restored(SavedMon saved, ItemData medicine)
    {
        if (Restore(saved) is not { } battler) return (saved, 0);

        int before = battler.CurrentHp;

        battler.Heal(medicine.RestoreFor(battler.MaxHp));

        return (Save(battler) with { Experience = saved.Experience }, battler.CurrentHp - before);
    }

    /// <summary>
    /// True when this one has nothing wrong with it.
    /// <para>
    /// Not the same question as whether it can fight. Something on one health with a
    /// burn can fight and very much wants a centre, and telling a player nobody needed
    /// healing when half the party is poisoned is a lie the counter should not tell.
    /// </para>
    /// </summary>
    public bool IsWell(SavedMon saved) =>
        Restore(saved) is not { } battler
        || (battler.CurrentHp >= battler.MaxHp
            && battler.Status == StatusCondition.None
            && Enumerable.Range(0, battler.Moves.Count).All(s => battler.PpLeft(s) >= battler.Moves[s].Pp));

    /// <summary>True when this one can still fight.</summary>
    public bool CanFight(SavedMon saved) => Restore(saved) is { HasFainted: false };
}
