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
                CatchRate = 255,
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

        return new GameRules(species, moves, learnsets, trainers);
    }
}
