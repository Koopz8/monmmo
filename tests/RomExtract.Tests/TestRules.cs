using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A rules file built by hand, so server tests can resolve battles without a
/// cartridge or an export.
/// <para>
/// Names are empty throughout, exactly as a real export produces them. A test whose
/// species were named would prove the server works in a world it will never be in.
/// </para>
/// </summary>
internal static class TestRules
{
    public const int FirstMove = 1;

    /// <summary>A trainer with three creatures, so a fight is more than one battle.</summary>
    public const int ThreeStrong = 7;

    /// <summary>A trainer with one, for the simple case.</summary>
    public const int OneAlone = 4;

    /// <summary>An ordinary ball, as an item id.</summary>
    public const int BallItem = 4;

    /// <summary>A better one, so tests can tell a kind from a count.</summary>
    public const int UltraBallItem = 2;

    /// <summary>Something that is not a ball at all. Restores twenty.</summary>
    public const int PotionItem = 13;

    /// <summary>The kind that restores all of it, whatever the maximum happens to be.</summary>
    public const int FullPotionItem = 20;

    /// <summary>A machine that is used up, and one that is not.</summary>
    public const int DiscItem = 289;

    public const int HiddenMachineItem = 339;

    /// <summary>What each of them teaches. The second is the one that moves trees.</summary>
    public const int TaughtMove = 2;

    public const int FieldMove = 15;

    /// <summary>The one that always works, so a kind can be told from a count.</summary>
    public const int MasterBallItem = 1;

    /// <summary>
    /// A species nothing ordinary can catch.
    /// <para>
    /// Here so that "which ball is this" has a visible consequence. With everything at
    /// a catch rate of 255 a Master Ball and a Poké Ball both catch, and a test that
    /// cannot tell them apart proves nothing about which one the server used.
    /// </para>
    /// </summary>
    public const int HardToCatch = 40;

    public static readonly GameRules All = Build();

    private static GameRules Build()
    {
        var species = new List<SpeciesData>();

        for (int index = 0; index < 64; index++)
        {
            species.Add(new SpeciesData
            {
                Index = index,
                Name = string.Empty,
                BaseHp = 45, BaseAttack = 49, BaseDefense = 49,
                BaseSpeed = 45, BaseSpAttack = 65, BaseSpDefense = 65,
                Type1 = PokemonType.Normal,
                Type2 = PokemonType.Normal,
                CatchRate = index == HardToCatch ? (byte)1 : (byte)255,
                ExpYield = 64,

                // Named rather than left to the enum's first value, so the experience
                // tests are asserting against a curve they actually chose.
                GrowthRate = GrowthRate.MediumFast,
            });
        }

        var moves = new List<MoveData>
        {
            new(0, string.Empty, 0, 0, PokemonType.Normal, 0, 0, 0, 0, 0),
            new(FirstMove, string.Empty, 0, 35, PokemonType.Normal, 95, 35, 0, 0, 0),
            new(2, string.Empty, 0, 40, PokemonType.Normal, 100, 30, 0, 0, 0),
        };

        var learnsets = new List<Learnset>();

        for (int index = 0; index < 64; index++)
            learnsets.Add(new Learnset(index, [new LevelUpMove(1, FirstMove), new LevelUpMove(3, 2)]));

        var trainers = new List<TrainerParty>
        {
            new(OneAlone, false, [new TrainerMember(3, 5, 0, [])]),

            // Moves written out on one of them and left to the learnset on the others,
            // because both are ordinary and the second is what most of the games use.
            new(ThreeStrong, false,
            [
                new TrainerMember(3, 5, 0, [FirstMove]),
                new TrainerMember(4, 6, 0, []),
                new TrainerMember(5, 7, 0, []),
            ]),
        };

        var items = new List<ItemData>
        {
            new(BallItem, 200, Pocket.Balls, 0, 0, 0, 0, 0, BallKind.Poke),
            new(UltraBallItem, 1200, Pocket.Balls, 0, 0, 0, 0, 0, BallKind.Ultra),
            new(MasterBallItem, 0, Pocket.Balls, 0, 0, 0, 0, 0, BallKind.Master),
            new(PotionItem, 300, Pocket.Items, 0, 20, 0, 1, 0),
            new(FullPotionItem, 2500, Pocket.Items, 0, ItemData.FullRestore, 0, 1, 0),

            // A machine of each kind, as the cartridge distinguishes them: the disc has
            // a price and no importance, the hidden machine has importance and no price.
            new(DiscItem, 3000, Pocket.Machines, 0, 0, 0, 0, 0) { Teaches = TaughtMove },
            new(HiddenMachineItem, 0, Pocket.Machines, 0, 0, 1, 0, 0) { Teaches = FieldMove },
        };

        return new GameRules(species, moves, learnsets, trainers, items);
    }
}
