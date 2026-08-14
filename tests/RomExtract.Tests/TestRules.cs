using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Server;

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

    /// <summary>
    /// A fresh character with something to fight with.
    /// <para>
    /// New accounts used to be handed a party. They are not any more — the game has an
    /// opening now and the opening is where a party comes from — so every test about
    /// battles, bags, shops and travel has to say out loud that it needs one. Which is
    /// the right way round: those tests were never about registration.
    /// </para>
    /// </summary>
    public static SavedCharacter Equipped(GameWorld world, int species = 1, int level = 5)
    {
        // Built the way the server used to build it, so every test that was written
        // against the free starter is testing the same creature it always was. A
        // different species or a different level would be a silent rebalance of two
        // dozen battles.
        var factory = new BattleFactory(All);

        return factory.Wild(species, level) is { } starter
            ? world.FreshCharacter() with { Party = [BattleFactory.Save(starter)] }
            : world.FreshCharacter();
    }

    /// <summary>A trainer with three creatures, so a fight is more than one battle.</summary>
    public const int ThreeStrong = 7;

    /// <summary>A trainer with one, for the simple case.</summary>
    public const int OneAlone = 4;

    /// <summary>A trainer whose party carries something, which most of the real ones do not.</summary>
    public const int Carrying = 9;

    /// <summary>An ordinary ball, as an item id.</summary>
    public const int BallItem = 4;

    /// <summary>A better one, so tests can tell a kind from a count.</summary>
    public const int UltraBallItem = 2;

    /// <summary>A stone, so the bag has something in it that changes what a creature is.</summary>
    public const int StoneItem = 93;

    /// <summary>The two method numbers, chosen to be different so a mix-up shows.</summary>
    public const int LevelMethod = 4;

    public const int ItemMethod = 7;

    /// <summary>Species 3 becomes species 6 with a stone, and species 1 at level 8.</summary>
    public static readonly Evolution[] Evolutions =
    [
        new Evolution(1, LevelMethod, 8, 2),
        new Evolution(3, ItemMethod, StoneItem, 6),
    ];

    /// <summary>Something that is not a ball at all. Restores twenty.</summary>
    public const int PotionItem = 13;

    /// <summary>The kind that restores all of it, whatever the maximum happens to be.</summary>
    public const int FullPotionItem = 20;

    /// <summary>One that clears one thing, one that clears the lot, and one that does both.</summary>
    public const int AntidoteItem = 14;

    public const int FullHealItem = 23;

    public const int FullRestoreItem = 19;

    /// <summary>A machine that is used up, and one that is not.</summary>
    public const int DiscItem = 289;

    public const int HiddenMachineItem = 339;

    /// <summary>How many the box holds here — small, so "full" is reachable in a test.</summary>
    public const int BoxSize = 4;

    /// <summary>The one species no machine works on, so a refusal can be asked for.</summary>
    public const int LearnsNothing = 5;

    /// <summary>Something worth carrying — the one item here with a hold effect on it.</summary>
    public const int TrinketItem = 207;

    /// <summary>And something that may never leave the bag.</summary>
    public const int BicycleItem = 259;

    /// <summary>What each of them teaches. The second is the one that moves trees.</summary>
    public const int TaughtMove = 2;

    public const int FieldMove = 15;

    /// <summary>The move that gets somebody onto the water, and the only one here with a name.</summary>
    public const int SurfMove = 6;

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

            // Three more that do nothing interesting, so a test can hand somebody four
            // moves that are told apart by their ids. A move id nothing knows about is
            // dropped on the way into a battle, which turns "which slot changed" into
            // "everything was rebuilt from the learnset" — and that reads as a passing
            // test right up until it is looked at.
            new(3, string.Empty, 0, 40, PokemonType.Normal, 100, 30, 0, 0, 0),
            new(4, string.Empty, 0, 40, PokemonType.Normal, 100, 30, 0, 0, 0),
            new(5, string.Empty, 0, 40, PokemonType.Normal, 100, 30, 0, 0, 0),

            // Named, unlike every other move here, because the field moves are found by
            // the name the cartridge gives them rather than by an id remembered from
            // another game. A rules file with no move called SURF has no surfing in it,
            // which is a state worth being able to test as well.
            new(SurfMove, "SURF", 0, 95, PokemonType.Water, 100, 15, 0, 0, 0),
        };

        var learnsets = new List<Learnset>();

        for (int index = 0; index < 64; index++)
            learnsets.Add(new Learnset(index, [new LevelUpMove(1, FirstMove), new LevelUpMove(3, 2)]));

        var trainers = new List<TrainerParty>
        {
            new(OneAlone, false, [new TrainerMember(3, 5, 0, [])]),

            // Eighty-seven of the cartridge's seventeen hundred party members hold
            // something, and this is one of them.
            new(Carrying, false, [new TrainerMember(3, 5, 207, [])]),

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

            // Three that put a condition right. The first restores nothing at all, which
            // is the case that catches a use path gated on the health going up.
            new(AntidoteItem, 100, Pocket.Items, 0, 0, 0, 1, 0) { Cures = Ailments.Poison },
            new(FullHealItem, 600, Pocket.Items, 0, 0, 0, 1, 0) { Cures = Ailments.Everything },
            new(FullRestoreItem, 3000, Pocket.Items, 0, ItemData.FullRestore, 0, 1, 0)
            {
                Cures = Ailments.Everything,
            },

            // A machine of each kind, as the cartridge distinguishes them: the disc has
            // a price and no importance, the hidden machine has importance and no price.
            new(DiscItem, 3000, Pocket.Machines, 0, 0, 0, 0, 0) { Teaches = TaughtMove },
            new(HiddenMachineItem, 0, Pocket.Machines, 0, 0, 1, 0, 0) { Teaches = FieldMove },

            // A stone, which restores nothing and teaches nothing and is the only thing
            // in this bag that turns one creature into another.
            new(StoneItem, 2100, Pocket.Items, 0, 0, 0, 0, 0),

            // Something with a hold effect on it, which is what makes its pocket a
            // pocket holding is for. Nothing else in this bag carries one, and without
            // it a fixture would say — correctly, and uselessly — that this cartridge
            // has nothing anybody can be handed.
            new(TrinketItem, 1500, Pocket.Items, 5, 0, 0, 0, 0),

            // And one nobody may ever be parted from, which is refused on top of the
            // pocket rule rather than by it: it is in a pocket of its own.
            new(BicycleItem, 0, Pocket.KeyItems, 0, 0, 1, 0, 0),
        };

        // One word per species, one bit per machine, in the order the machines sit in
        // the pocket — the disc first, the hidden machine second. Everything can take
        // both except one species that can take neither, so a test that expects a
        // refusal has to name the species that gets refused rather than getting one by
        // accident.
        var machineSets = new List<ulong>();

        for (int index = 0; index < species.Count; index++)
            machineSets.Add(index == LearnsNothing ? 0UL : 0b11UL);

        return new GameRules(species, moves, learnsets, trainers, items, Evolutions, machineSets)
        {
            BoxSize = BoxSize,
            EvolveByLevel = LevelMethod,
            EvolveByItem = ItemMethod,
        };
    }
}
