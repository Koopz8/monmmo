using System.Text;
using PokeMmo.Core.Data;
using PokeMmo.RomExtract.Items;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Reading the item table.
/// <para>
/// The easiest table in this project to be sure about, because every record contains
/// its own index. The species tables are anchored on a known name, the sprite table on
/// an arithmetic relationship, the trainer table on pointers that have to lead to
/// plausible creatures. This one simply counts — and the placeholder problem that cost
/// the trainer table a special case cannot arise, because the record that says it is
/// item zero <em>is</em> item zero.
/// </para>
/// </summary>
public class ItemTableTests
{
    private static readonly SyntheticRom Fixture = new();

    private static int Table =>
        ItemTable.Locate(Fixture.ToRom()) ?? throw new InvalidOperationException("The item table was not found.");

    [Fact]
    public void TheTableIsFoundWhereItWasWritten()
    {
        Assert.Equal(SyntheticRom.ItemTableOffset, Table);
    }

    [Fact]
    public void EveryItemComesBackAsItWasWritten()
    {
        var items = ItemTable.Read(Fixture.ToRom(), Table).ToDictionary(i => i.Id);

        for (int id = 0; id < SyntheticRom.ItemCount; id++)
        {
            if (SyntheticRom.ItemIsUnused(id)) continue;

            Assert.Equal(SyntheticRom.ItemPriceFor(id), items[id].Price);
            Assert.Equal(SyntheticRom.ItemPocketFor(id), items[id].Pocket);
            Assert.Equal(SyntheticRom.ItemNameFor(id), items[id].Name);
        }
    }

    [Fact]
    public void ABlockOfUnusedSlotsIsNotTheEndOfTheTable()
    {
        // Real tables have reserved slots that were never filled in, written as copies
        // of the "nothing" entry — so they claim to be item zero wherever they sit. A
        // reader keyed on self-indexing stops dead at the first one, which is how a
        // table of nearly four hundred items came back as fifty-two.
        List<ItemRecord> items = ItemTable.Read(Fixture.ToRom(), Table);

        Assert.Equal(SyntheticRom.ItemCount - SyntheticRom.UnusedItemCount, items.Count);
        Assert.Equal(SyntheticRom.ItemCount - 1, items[^1].Id);

        // And nothing is renumbered around the gap: an id is a position in the table.
        Assert.DoesNotContain(items, i => SyntheticRom.ItemIsUnused(i.Id));
        Assert.Contains(items, i => i.Id == SyntheticRom.FirstUnusedItem + SyntheticRom.UnusedItemCount);
    }

    [Fact]
    public void AnItemThatDisagreesAboutItsOwnIdIsNotAnItem()
    {
        // The whole discriminator. Every other field could be satisfied by chance; a
        // table that counts along with itself several hundred times could not.
        Assert.Null(ItemRecord.TryParse(Fixture.ToRom(), Table, expectedId: 1));
        Assert.NotNull(ItemRecord.TryParse(Fixture.ToRom(), Table, expectedId: 0));
    }

    [Fact]
    public void StartingOneRecordLateWouldNotBeMistakenForTheTable()
    {
        // The failure the trainer table needed a special case for cannot happen here.
        // A scan that began at the second record would find a record claiming to be
        // item one where item zero should be, and stop.
        Assert.Null(ItemRecord.TryParse(Fixture.ToRom(), Table + 44, expectedId: 0));
    }

    [Fact]
    public void RoutinesAreOptionalAndBothShapesAreRead()
    {
        // Some items do something in the field, some in a battle, some neither. A
        // reader that insisted on a pointer in both slots would find no table at all.
        List<ItemRecord> items = ItemTable.Read(Fixture.ToRom(), Table);

        Assert.True(items.Count > 3);
    }

    [Fact]
    public void ExplainSaysWhyBytesAreNotTheItemAskedFor()
    {
        string why = ItemRecord.Explain(Fixture.ToRom(), Table + 44, expectedId: 0);

        Assert.Contains("says it is item 1", why);
    }

    [Fact]
    public void APriceOfZeroIsNotForSale()
    {
        List<ItemRecord> items = ItemTable.Read(Fixture.ToRom(), Table);

        Assert.False(items[0].ToData().CanBeBought);
        Assert.False(items[SyntheticRom.KeyItem].ToData().CanBeBought);
        Assert.True(items[1].ToData().CanBeBought);
    }

    [Fact]
    public void AKeyItemIsNeverBoughtBack()
    {
        ItemData key = ItemTable.Read(Fixture.ToRom(), Table)[SyntheticRom.KeyItem].ToData();

        Assert.True(key.IsKeyItem);
        Assert.Equal(0, key.SellPrice);
    }

    [Fact]
    public void AShopPaysHalf()
    {
        ItemData ordinary = ItemTable.Read(Fixture.ToRom(), Table)[6].ToData();

        Assert.Equal(ordinary.Price / 2, ordinary.SellPrice);
    }
}

/// <summary>Items through the rules file, which is the only way the server sees one.</summary>
public class ItemRulesTests
{
    private static readonly SyntheticRom Fixture = new();

    private static GameRules Exported() => RulesExporter.Export(Fixture.ToRom());

    [Fact]
    public void EveryItemSurvivesASaveAndLoad()
    {
        GameRules exported = Exported();

        using var buffer = new MemoryStream();
        exported.Save(buffer);
        buffer.Position = 0;

        GameRules loaded = GameRules.Load(buffer);

        Assert.Equal(exported.ItemCount, loaded.ItemCount);
        Assert.Equal(SyntheticRom.ItemCount - SyntheticRom.UnusedItemCount, loaded.ItemCount);

        for (int id = 0; id < SyntheticRom.ItemCount; id++)
        {
            Assert.Equal(exported.ItemAt(id), loaded.ItemAt(id));

            // The reserved slots are not items and do not become one on the way through.
            if (SyntheticRom.ItemIsUnused(id))
            {
                Assert.Null(loaded.ItemAt(id));
                continue;
            }

            Assert.Equal(SyntheticRom.ItemPocketFor(id), loaded.ItemAt(id)!.Pocket);
        }
    }

    [Fact]
    public void NoItemNameOrDescriptionGetsIntoTheFile()
    {
        using var buffer = new MemoryStream();
        Exported().Save(buffer);

        byte[] written = buffer.ToArray();

        foreach (string text in new[] { "ITEM 06", "A THING NUMBER" })
        {
            byte[] needle = Encoding.UTF8.GetBytes(text);

            Assert.DoesNotContain(
                Enumerable.Range(0, written.Length - needle.Length),
                at => written.Skip(at).Take(needle.Length).SequenceEqual(needle));
        }
    }

    [Fact]
    public void TheOtherSectionsStillSurviveAlongsideThem()
    {
        // A format that gained a section and quietly lost one would load perfectly and
        // fail much later. Moves are not in here: this fixture has no move table, the
        // exporter treats that as an empty section, and asserting on it would be
        // asserting about the fixture rather than about the format.
        GameRules loaded = Exported();

        Assert.True(loaded.SpeciesCount > 0, $"species {loaded.SpeciesCount}");
        Assert.True(loaded.LearnsetCount > 0, $"learnsets {loaded.LearnsetCount}");
        Assert.True(loaded.TrainerCount > 0, $"trainers {loaded.TrainerCount}");
        Assert.True(loaded.ItemCount > 0, $"items {loaded.ItemCount}");
    }
}
