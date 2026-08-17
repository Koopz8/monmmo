using PokeMmo.Core.Scripts;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Data;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.Server;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// What a battle cannot carry, and the one caller that did not put it back.
/// <para>
/// <c>BattleFactory.Save</c> says it in its own note: a battler holds no experience, so what
/// comes back from one has none, <em>and every caller starting from a save puts it back
/// afterwards</em>. The autoplayer wrote its party back after every fight and did not.
/// </para>
/// <para>
/// So each win reset the total to nothing and the next award started again from the bottom of
/// the level the creature was already at — a threshold a single fight never crosses. Ninety-four
/// fights won, not one level gained, six party members at twenty-five for the whole game, and a
/// floor of 179 maps out of 425 reported as a fact about the cartridge.
/// </para>
/// <para>
/// <b>Nothing failed.</b> A party that does not grow fights every battle correctly and loses
/// the ones it should lose.
/// </para>
/// </summary>
public class WinningHasToAddUpTests
{
    private static MapData Room(string id) => new(id, id, 4, 4, new byte[16]);

    private static MapObject Person(int localId, uint script) =>
        new(localId, 1, localId, 1, Direction.Down, 0, false) { ScriptAddress = script };

    private static PlayedScript Nothing => new([], [], [], [], null, null);

    /// <summary>A run that hands over one creature and then fights the trainers given.</summary>
    private static Attempt Run(params int[] trainers)
    {
        MapObject[] people =
        [
            Person(1, 0x1000),
            .. trainers.Select((_, i) => Person(i + 2, (uint)(0x2000 + (i * 0x100)))),
        ];

        Attempt played = Autoplayer.Play(
            new WorldData([Room("1.0") with { Objects = people }]),
            "1.0",
            TestRules.All,
            (address, _, _) => address == 0x1000
                ? Nothing with { Gives = (1, 50) }
                : Nothing with { Fights = trainers[(int)((address - 0x2000) / 0x100)] });

        return played;
    }

    /// <summary>
    /// <b>Two wins are worth more than one.</b> The exact statement, because everything weaker
    /// passes without the fix: after a single fight the total is right either way, and it is
    /// the second one that either adds to the first or replaces it.
    /// </summary>
    [Fact]
    public void TheSecondWinAddsToTheFirstRatherThanReplacingIt()
    {
        Attempt once = Run(TestRules.OneAlone);
        Attempt twice = Run(TestRules.OneAlone, TestRules.Carrying);

        Assert.Equal(1, once.FightsWon);
        Assert.Equal(2, twice.FightsWon);

        Assert.True(
            twice.Party[0].Experience > once.Party[0].Experience,
            $"two wins came to {twice.Party[0].Experience} and one came to {once.Party[0].Experience}");
    }

    /// <summary>
    /// And the seam it comes from: a battler carries no experience, so writing one back over a
    /// save loses it unless the save is handed in too.
    /// </summary>
    [Fact]
    public void WritingABattlerBackOverASaveKeepsWhatTheBattleCouldNotCarry()
    {
        var factory = new BattleFactory(TestRules.All);

        var before = new SavedMon(1, 50, null, 20, StatusCondition.None, Nature.Hardy, [TestRules.FirstMove])
        {
            Experience = 123456,
        };

        Battler restored = factory.Restore(before)!;

        Assert.Equal(0, BattleFactory.Save(restored).Experience);
        Assert.Equal(123456, BattleFactory.Save(restored, before).Experience);
    }
}
