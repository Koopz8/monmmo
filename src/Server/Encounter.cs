using PokeMmo.Core.Battle;

namespace PokeMmo.Server;

/// <summary>
/// A fight, from the first creature sent out to the last one standing.
/// <para>
/// The engine underneath is one-on-one and stays that way. A trainer fight is modelled
/// as a run of one-on-one battles rather than as a single battle holding two parties:
/// when somebody faints, the next one comes out and a new <see cref="Battle"/> begins
/// with the dice carried over. That keeps every rule about damage, accuracy and status
/// exactly where it already is, and exactly as tested.
/// </para>
/// <para>
/// The price is that anything meant to last across a switch would be reset by one —
/// stat changes, most obviously. There are none yet. When there are, this is the place
/// that has to know about it, and this paragraph is the note saying so.
/// </para>
/// </summary>
public sealed class Encounter
{
    private readonly List<Battler> _opponents;

    private int _opponentSlot;

    public Encounter(int playerSlot, Battler player, IReadOnlyList<Battler> opponents, uint seed, int? trainerId = null)
    {
        if (opponents.Count == 0) throw new ArgumentException("An encounter needs somebody to fight.", nameof(opponents));

        PlayerSlot = playerSlot;
        TrainerId = trainerId;
        _opponents = [.. opponents];

        Current = new Battle(player, _opponents[0], seed) { IsWild = trainerId is null };
    }

    /// <summary>Which trainer started this, or null when something walked out of the grass.</summary>
    public int? TrainerId { get; }

    public bool IsTrainerBattle => TrainerId is not null;

    /// <summary>Which of the player's party is out. The index, not the species — two of
    /// the same species in one party would otherwise be indistinguishable.</summary>
    public int PlayerSlot { get; private set; }

    /// <summary>The one-on-one battle currently being fought.</summary>
    public Battle Current { get; private set; }

    public Battler Player => Current.Player;

    public Battler Opponent => Current.Opponent;

    /// <summary>Everything the other side brought, in the order it comes out.</summary>
    public IReadOnlyList<Battler> Opponents => _opponents;

    /// <summary>True when the other side still has somebody who can fight.</summary>
    public bool OpponentHasAnotherOne =>
        _opponents.Skip(_opponentSlot + 1).Any(o => !o.HasFainted);

    /// <summary>
    /// True when the other side has nobody left at all.
    /// <para>
    /// Whether the <em>player</em> has anybody left is not decided here. That needs the
    /// party, which lives on the player and not in the fight — and putting a copy of it
    /// here would mean two answers to "who has fainted".
    /// </para>
    /// </summary>
    public bool OpponentIsBeaten => Current.Opponent.HasFainted && !OpponentHasAnotherOne;

    /// <summary>Sends out the other side's next one, and returns it.</summary>
    public Battler SendNextOpponent()
    {
        do
        {
            _opponentSlot++;
        }
        while (_opponentSlot < _opponents.Count - 1 && _opponents[_opponentSlot].HasFainted);

        // Carried on from where the dice had got to, not restarted from the seed.
        // Restarting would replay the same rolls against everyone they send out.
        Current = new Battle(Current.Player, _opponents[_opponentSlot], Current.State) { IsWild = !IsTrainerBattle };

        return Current.Opponent;
    }

    /// <summary>Sends out one of the player's party, replacing whoever fainted.</summary>
    public void SendPlayer(int slot, Battler battler)
    {
        PlayerSlot = slot;
        Current = new Battle(battler, Current.Opponent, Current.State) { IsWild = !IsTrainerBattle };
    }
}
