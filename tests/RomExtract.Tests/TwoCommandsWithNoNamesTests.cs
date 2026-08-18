using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// <c>0x63</c> and <c>0x65</c> have had widths since 187 and no meanings. 225 found the fan
/// club's on-load chain calling both once per fan, on the arm a run's silence takes, so what they
/// take is now on the path of something the playthrough decides.
/// <para>
/// <b>Every number here has a floor beside it, and the floors are the finding.</b> "A hundred and
/// twenty-six of them name a person who is really on that map" is worth nothing on its own — a
/// map with fifteen people accepts any small number. Read the SECOND word as the person instead
/// and it is 26. And "twenty-six have the person's own square" is worth nothing without knowing
/// that chance would give <b>0.45</b>.
/// </para>
/// <para>
/// What that buys: the two words after the person in <c>0x63</c> are a square in the same
/// coordinate system as that person, fifty-eight times above chance. The byte in <c>0x65</c> is
/// drawn from the same small set as the map's own movement types and is the named person's own at
/// 54 of 105 against a floor of 22.7. <b>Neither is named here.</b> What they take is read; what
/// they do is still a guess, and saying so is the difference between this and 187.
/// </para>
/// </summary>
public sealed class TwoCommandsWithNoNamesTests
{
    private static PersonCommands.Site Three(
        int person, int x, int y, (int X, int Y)? square, int width = 20, int height = 20) =>
        new("1.97", "trigger (37,43)", 0x163960, PersonCommands.Three,
            person, x, y, square, square is null ? null : 9, width, height);

    private static PersonCommands.Site Two(int person, int byteAfter, int? movement) =>
        new("1.97", "trigger (37,43)", 0x163964, PersonCommands.Two,
            person, byteAfter, -1, movement is null ? null : (1, 1), movement, 20, 20);

    private static IReadOnlyDictionary<string, IReadOnlyList<(int LocalId, int Movement)>> Everybody(
        params (int LocalId, int Movement)[] people) =>
        new Dictionary<string, IReadOnlyList<(int LocalId, int Movement)>> { ["1.97"] = people };

    /// <summary>
    /// THE FLOOR FOR THE EXACT HITS, which is the whole of the coordinate reading. On a map of
    /// twenty by twenty a particular square comes up once in four hundred.
    /// </summary>
    [Fact]
    public void TheChanceOfLandingOnAPersonsOwnSquareIsOneInTheMap()
    {
        Assert.Equal(1.0 / 400, PersonCommands.ExactlyThereByChance([Three(2, 5, 5, (5, 5))]), 6);

        // Four sites on that map, four four-hundredths — it adds up per site, because each site
        // is its own throw and the maps are different sizes.
        Assert.Equal(
            4.0 / 400,
            PersonCommands.ExactlyThereByChance(
                [Three(2, 5, 5, (5, 5)), Three(3, 1, 1, (9, 9)), Three(4, 0, 0, (1, 2)), Three(5, 7, 7, (7, 7))]),
            6);

        // A bigger map is a smaller chance, which is why it cannot be a constant.
        Assert.True(
            PersonCommands.ExactlyThereByChance([Three(2, 5, 5, (5, 5), width: 60, height: 60)])
            < PersonCommands.ExactlyThereByChance([Three(2, 5, 5, (5, 5))]));
    }

    /// <summary>
    /// A site naming somebody who is not on that map contributes no throw — it cannot hit a
    /// square nobody stands on, and counting it would make the floor too high and the finding
    /// look weaker than it is.
    /// </summary>
    [Fact]
    public void ASiteNamingNobodyIsNotAThrow()
    {
        Assert.Equal(0, PersonCommands.ExactlyThereByChance([Three(99, 5, 5, null)]));
    }

    /// <summary>
    /// EXACTLY THERE, WITHIN THREE, AND FURTHER are three answers and the distance says which.
    /// </summary>
    [Fact]
    public void HowFarTheWordsAreFromWhereTheCartridgePutThatPerson()
    {
        Assert.Equal(0, Three(2, 5, 9, (5, 9)).Away);
        Assert.Equal(3, Three(2, 5, 12, (5, 9)).Away);
        Assert.Equal(9, Three(2, 0, 5, (5, 9)).Away);

        // And no distance at all when there is nobody to measure from.
        Assert.Null(Three(99, 5, 9, null).Away);
    }

    /// <summary>
    /// The words landing inside the map is the weak test and it is kept as one: a small pair
    /// lands inside almost any map, which is why the exact-hit floor is what the reading rests on.
    /// </summary>
    [Fact]
    public void InsideTheMapIsAboutTheMapsOwnBounds()
    {
        Assert.True(Three(2, 19, 19, (5, 5)).InsideTheMap);
        Assert.False(Three(2, 20, 19, (5, 5)).InsideTheMap);
        Assert.False(Three(2, 5, 40, (5, 5)).InsideTheMap);

        // And it is not asked of the command that has no second word.
        Assert.False(Two(2, 8, 8).InsideTheMap);
    }

    /// <summary>
    /// THE CONTROL: reading the wrong word as the person id. On the cartridge it is 126 against
    /// 26, and without it "every site names a real person" is a statement about how many people
    /// maps have.
    /// </summary>
    [Fact]
    public void ReadingTheOtherWordAsThePersonIsTheControl()
    {
        PersonCommands.Site[] sites = [Three(2, 5, 9, (5, 9)), Three(3, 40, 1, (1, 1))];

        Assert.Equal(2, PersonCommands.NamesSomebody(sites));

        // Person 5 exists, so the first site's second word would name somebody; 40 does not.
        Assert.Equal(1, PersonCommands.TheOtherWordWould(sites, Everybody((2, 9), (3, 9), (5, 8))));
    }

    /// <summary>
    /// AND THE FLOOR FOR THE MOVEMENT BYTE: how often it would be somebody else's on the same
    /// map. A room where everybody moves the same way makes "the person's own" mean nothing.
    /// </summary>
    [Fact]
    public void TheMovementFloorIsWhatSomebodyElseOnThatMapWouldGive()
    {
        // Everybody on this map moves type 8, so a byte of 8 is the named person's own AND
        // everybody else's — the floor is 1 and the finding is nothing.
        Assert.Equal(
            1.0,
            PersonCommands.SomebodyElsesMovement([Two(2, 8, 8)], Everybody((2, 8), (3, 8), (4, 8))),
            6);

        // And where the others move differently, the same match is worth something.
        Assert.Equal(
            0.0,
            PersonCommands.SomebodyElsesMovement([Two(2, 8, 8)], Everybody((2, 8), (3, 9), (4, 10))),
            6);
    }

    /// <summary>
    /// A person alone on their map has no floor to compare against and is left out rather than
    /// counted as nought — a rate over an empty set is not nought, it is no answer.
    /// </summary>
    [Fact]
    public void SomebodyAloneOnTheirMapContributesNoFloor()
    {
        Assert.Equal(0.0, PersonCommands.SomebodyElsesMovement([Two(2, 8, 8)], Everybody((2, 8))), 6);
    }
}
