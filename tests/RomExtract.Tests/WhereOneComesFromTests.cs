using PokeMmo.RomExtract.Maps;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Where an item actually comes from, asked of the image rather than of the world file.
/// <para>
/// The world file's answer for a FRESH WATER was one shop counter, on a map two hops and a
/// boat away from anywhere the story reaches. That could not be the whole truth: the world
/// file records what it can attribute to an <em>object</em>, and a vending machine that
/// offers a menu and hands the drink over inside a routine is attributable to nobody. The
/// item never appears in anybody's <c>GivesItemId</c>, so it reads as unobtainable while
/// being the place everybody actually buys one.
/// </para>
/// <para>
/// So this asks the bytes. Every command in the game that names an item, grouped by what it
/// does with it — and the one that matters is the argument slot, because that is the shape
/// nothing else can see.
/// </para>
/// </summary>
public class WhereOneComesFromTests
{
    // Two ids the fixture's own shelves and gifts do not use, so the sites this finds are
    // the ones this test wrote and not the fixture's furniture.
    private const int Drink = 0x0321;
    private const int Other = 0x0322;

    /// <summary>An image with one script on one map, written the way the cartridge writes them.</summary>
    private static (Rom Rom, MapLibrary Library) WithScript(params byte[] script)
    {
        var fixture = new SyntheticRom();

        // Over the top of the first map's first person, whose script the fixture puts at a
        // known offset. Everything else about the image stays as it was, so the library
        // opens exactly as it does for every other test.
        script.CopyTo(fixture.Bytes, SyntheticRom.ScriptFor(0, 0));

        var rom = new Rom(fixture.Bytes);

        return (rom, MapLibrary.Open(rom));
    }

    private static byte[] Word(int value) => [(byte)value, (byte)(value >> 8)];

    [Fact]
    public void SomethingHandedOverIsFoundAndSaidToBeHandedOver()
    {
        (Rom rom, MapLibrary library) = WithScript([0x46, .. Word(Drink), .. Word(2), 0x02]);

        ItemSite site = Assert.Single(
            ItemMentions.Of(rom, library, [Drink]), s => s.What == "person 1");

        Assert.Equal("handed over", site.How);
        Assert.Equal(Drink, site.ItemId);
        Assert.Equal(2, site.Count);
        Assert.Equal("0.0", site.MapId);
    }

    /// <summary>
    /// The one the world file cannot see, and the reason the instrument exists. A script that
    /// writes an item into the argument slot and calls a routine has handed it over, and
    /// which routine is a number this project cannot resolve — so nothing attributes it to
    /// anybody and it appears in no object's record anywhere.
    /// </summary>
    [Fact]
    public void SomethingLoadedIntoTheArgumentSlotIsFoundToo()
    {
        (Rom rom, MapLibrary library) = WithScript(
        [
            0x1A, .. Word(0x8000), .. Word(Drink),      // the item
            0x1A, .. Word(0x8001), .. Word(1),          // and how many
            0x09, 0x00,                                 // callstd — whichever routine that is
            0x02,
        ]);

        ItemSite site = Assert.Single(
            ItemMentions.Of(rom, library, [Drink]), s => s.What == "person 1");

        Assert.Equal("loaded for a routine", site.How);
        Assert.Equal(Drink, site.ItemId);
    }

    /// <summary>
    /// And the count beside it is not an item. The two slots are written by the same command
    /// one after the other, so reading both as ids turns "one of them" into item 1 — which
    /// would put a mention of item 1 in front of every handover in the game.
    /// </summary>
    [Fact]
    public void TheCountSlotIsNotReadAsAnItem()
    {
        (Rom rom, MapLibrary library) = WithScript(
        [
            0x1A, .. Word(0x8000), .. Word(Drink),
            0x1A, .. Word(0x8001), .. Word(1),
            0x02,
        ]);

        // The fixture's own shelves stock item 1, so the question is asked of this script
        // rather than of the world: did the count beside the item become a mention here.
        Assert.DoesNotContain(
            ItemMentions.Of(rom, library, [1]), site => site.What == "person 1");
    }

    /// <summary>
    /// Asking for one is not having one. The two are the opposite ends of this whole thread
    /// — a guard who asks for a drink is why the question is being asked at all, and putting
    /// him on the list of places to get one would send somebody in a circle.
    /// </summary>
    [Fact]
    public void AskingForOneIsToldApartFromHandingOneOver()
    {
        (Rom rom, MapLibrary library) = WithScript([0x47, .. Word(Drink), .. Word(1), 0x02]);

        Assert.Equal(
            "asked for",
            Assert.Single(ItemMentions.Of(rom, library, [Drink]), s => s.What == "person 1").How);
    }

    [Fact]
    public void SomethingTakenAwayIsToldApartAsWell()
    {
        (Rom rom, MapLibrary library) = WithScript([0x45, .. Word(Drink), .. Word(1), 0x02]);

        Assert.Equal(
            "taken away",
            Assert.Single(ItemMentions.Of(rom, library, [Drink]), s => s.What == "person 1").How);
    }

    /// <summary>Something else being named is not this being named.</summary>
    [Fact]
    public void AnotherItemAltogetherIsNotReported()
    {
        (Rom rom, MapLibrary library) = WithScript([0x46, .. Word(Other), .. Word(1), 0x02]);

        Assert.Empty(ItemMentions.Of(rom, library, [Drink]));
    }

    /// <summary>
    /// Item zero is not an item, the same rule the runner keeps. A script reaching one of
    /// these commands with nothing loaded is doing something else with it, and a sweep that
    /// reported those would answer "item 0 comes from three hundred places".
    /// </summary>
    [Fact]
    public void ItemZeroIsNotAnItemHereEither()
    {
        (Rom rom, MapLibrary library) = WithScript([0x46, .. Word(0), .. Word(1), 0x02]);

        Assert.Empty(ItemMentions.Of(rom, library, [0]));
    }

    /// <summary>
    /// Both arms, not the one that runs. This reads rather than runs on purpose: something
    /// handed over only on a branch today's save cannot take is still somewhere it comes
    /// from, and that is exactly what is being hunted.
    /// </summary>
    [Fact]
    public void AGiftBehindAConditionIsStillFound()
    {
        (Rom rom, MapLibrary library) = WithScript(
        [
            0x2B, .. Word(0x828),                                   // checkflag
            0x06, 0x00, 0x20, 0x00, 0x00, 0x08,                     // gotoif less -> 0x08000020
            0x02,
        ]);

        // The other arm, written where that jump lands.
        var fixture = new SyntheticRom();

        new byte[] { 0x2B, (byte)0x28, 0x08, 0x06, 0x00, 0x20, 0x00, 0x00, 0x08, 0x02 }
            .CopyTo(fixture.Bytes, SyntheticRom.ScriptFor(0, 0));

        new byte[] { 0x46, 0x21, 0x03, 0x01, 0x00, 0x02 }.CopyTo(fixture.Bytes, 0x20);

        var withBranch = new Rom(fixture.Bytes);

        Assert.Equal(
            "handed over",
            Assert.Single(
                ItemMentions.Of(withBranch, MapLibrary.Open(withBranch), [Drink]),
                s => s.What == "person 1").How);
    }
}
