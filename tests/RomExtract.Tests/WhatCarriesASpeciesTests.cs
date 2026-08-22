using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// The number three things carry (301).
/// <para>
/// <c>0xB6</c> is <c>species, a byte, 00 00</c> — ten byte positions, eight species, and a byte
/// that takes 30, 34, 50 or 70, one value per species. <c>0xA1</c>'s first word is the same
/// species, and that is read off the AGREEMENT rather than off the range: of the 63 operand
/// positions occurring in the ten blocks that hold a <c>0xB6</c>, exactly two ever name the number
/// it names, and <c>0xA1 arg0</c> does it <b>10 of 10</b>.
/// </para>
/// <para>
/// The other is a <c>setvar</c>'s value, and that is the finding: six blocks put the species in an
/// argument slot and the slot is <c>0x8004</c> six times of six. The two with no <c>0xB6</c> —
/// NAVEL ROCK and BIRTH ISLAND — are the only two places in the game that call
/// <c>special 0x01BB</c>, and it is handed the species and the same byte in the slot beside it.
/// </para>
/// </summary>
public sealed class WhatCarriesASpeciesTests
{
    /// <summary>
    /// <b>A SPAN IS NOT A TABLE, and this is the fixture for the bug that made it (301).</b> The
    /// first version asked whether the value was below the COUNT of named entries. It is not a
    /// span — 386 of the 412 entries carry a name and the twenty-six that do not are in the
    /// MIDDLE, indices 252 to 276 — so <c>value &lt;= 386</c> threw away index 410, which is named,
    /// and the reading lost one of the two places it exists to explain.
    /// <para>
    /// The fixture is that shape exactly: a named index ABOVE the count, and an unnamed one BELOW
    /// it. A version testing the count passes neither.
    /// </para>
    /// </summary>
    [Fact]
    public void ANamedIndexAboveTheCountIsStillASpecies()
    {
        // Three names, and one of them at an index above the count — 264's table in miniature.
        HashSet<int> named = [1, 2, 410];

        Assert.True(WhatCarriesASpecies.CouldBeASpecies(410, named));
        Assert.False(WhatCarriesASpecies.CouldBeASpecies(3, named));

        // And nought is not one: the real table's entry 0 is `??????????`.
        Assert.False(WhatCarriesASpecies.CouldBeASpecies(0, named));
    }

    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte SetVar = 0x16;

    private static byte[] Blank()
    {
        var image = new byte[0x20000];

        Array.Fill(image, Filler);

        return image;
    }

    private static void Put(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];
    }

    private static List<ScriptCommand> Read(byte[] image) =>
        ScriptReader.ReadAll(new Rom(image), Rom.BaseAddress + 0x1000);

    /// <summary>
    /// <b>THE SECOND FIELD IS THE SLOT BESIDE IT, not the same one.</b> The species goes in
    /// <c>0x8004</c> and the byte goes in <c>0x8005</c>, which is what makes the pair the same two
    /// fields <c>0xB6</c> carries in one command.
    /// <para>
    /// The fixture writes THREE slots so that reading the same slot, the next one, or the last one
    /// written all give different answers — a fixture with two cannot tell the second from the
    /// last (119).
    /// </para>
    /// </summary>
    [Fact]
    public void TheByteIsInTheSlotBesideTheSpecies()
    {
        byte[] image = Blank();

        // NAVEL ROCK's own shape:
        //   0xA1 F9 00 02 00 ; setvar 0x8004, 249 ; setvar 0x8005, 70 ; setvar 0x8006, 0
        //   ; special 0x1BB ; end
        //
        // The 0xA1 is what SEEDS the reading — it is asked of the blocks that name a species, not
        // of every block that fills a slot, and a fixture without one finds nothing at all.
        Put(image, 0x1000, WhatCarriesASpecies.TheCry, 0xF9, 0x00, 0x02, 0x00);
        Put(image, 0x1005, SetVar, 0x04, 0x80, 0xF9, 0x00);
        Put(image, 0x100A, SetVar, 0x05, 0x80, 0x46, 0x00);
        Put(image, 0x100F, SetVar, 0x06, 0x80, 0x03, 0x00);
        Put(image, 0x1014, SpecialCalls.Special, 0xBB, 0x01);
        Put(image, 0x1017, End);

        WhereASpeciesIsNamed row = Assert.Single(
            WhatCarriesASpecies.InOneBlock(Read(image), "2.38", Named));

        Assert.Equal(249, row.Species);
        Assert.Equal(0x8004, row.Slot);

        // 70, not 249 (the same slot) and not 3 (the last one written).
        Assert.Equal(70, row.InTheSlot);

        // And the routine after it is found, which is what makes the pair belong to a call.
        Assert.Equal([0x01BB], row.Routines);
    }

    private static readonly HashSet<int> Named = [.. Enumerable.Range(1, 411)];

    /// <summary>
    /// <b>THE COMMAND'S SECOND FIELD IS A BYTE AT OFFSET TWO.</b> Read as a halfword it takes the
    /// species' high half with it, and on this cartridge every species' high byte is nought — so a
    /// halfword read gives the same answer at every one of the ten places and the fault is
    /// invisible. The fixture puts a species ABOVE 255 there, which the cartridge does at exactly
    /// one place (410, on BIRTH ISLAND) and which no <c>0xB6</c> in the game carries.
    /// </summary>
    [Fact]
    public void TheCommandsSecondFieldIsAByte()
    {
        byte[] image = Blank();

        // 0xB6 9A 01 46 00 00 — species 410, byte 70. The species' high byte is 1, so a halfword
        // read at offset two gives 0x0146 rather than 70.
        Put(image, 0x1000, WhatCarriesASpecies.TheCommand, 0x9A, 0x01, 0x46, 0x00, 0x00);
        Put(image, 0x1006, End);

        WhereASpeciesIsNamed row = Assert.Single(
            WhatCarriesASpecies.InOneBlock(Read(image), "2.56", Named));

        Assert.Equal(410, row.Species);
        Assert.Equal(70, row.ByTheCommand!.Second);
    }

    /// <summary>
    /// And a <c>setvar</c> into the save's own numbers is not the species going into a slot — read
    /// through the reading rather than through a constant, so a version that took any variable
    /// fails here.
    /// </summary>
    [Fact]
    public void OnlyASetVarIntoASlotCounts()
    {
        byte[] image = Blank();

        // setvar 0x4001, 249 ; 0xB6 F9 00 46 00 00 ; end — the species is written, but not to a slot.
        Put(image, 0x1000, SetVar, 0x01, 0x40, 0xF9, 0x00);
        Put(image, 0x1005, WhatCarriesASpecies.TheCommand, 0xF9, 0x00, 0x46, 0x00, 0x00);
        Put(image, 0x100B, End);


        WhereASpeciesIsNamed row = Assert.Single(
            WhatCarriesASpecies.InOneBlock(Read(image), "1.74", Named));

        Assert.Equal(0, row.Slot);
        Assert.NotNull(row.ByTheCommand);
    }

    /// <summary>
    /// And a <c>setvar</c> into the SAVE's own numbers is not the species going into an argument
    /// slot. Without that the reading would find a slot on every block that mentions a species at
    /// all, and answer yes before it is asked (50).
    /// </summary>
    [Fact]
    public void ASetVarIntoTheSavesOwnNumbersIsNotASlot()
    {
        Assert.True(0x4001 < SpecialCalls.FirstArgument);
        Assert.True(0x8004 is >= SpecialCalls.FirstArgument and <= SpecialCalls.LastArgument);
        Assert.False(0x8010 is >= SpecialCalls.FirstArgument and <= SpecialCalls.LastArgument);
    }

    /// <summary>
    /// The command's own bytes: a word and then a byte, so the byte is at offset TWO and reading
    /// it as a halfword would take the high half of the species with it.
    /// </summary>
    [Fact]
    public void TheCommandIsAWordThenAByte()
    {
        byte[] image = Blank();

        // 0xB6 96 00 46 00 00 — species 150, byte 70.
        Put(image, 0x1000, WhatCarriesASpecies.TheCommand, 0x96, 0x00, 0x46, 0x00, 0x00);
        Put(image, 0x1006, End);

        ScriptCommand command = Assert.Single(
            Read(image).Where(c => c.Code == WhatCarriesASpecies.TheCommand));

        Assert.Equal(150, command.Word());
        Assert.Equal(70, command.Arguments[2]);

        // And read as a halfword at offset two it is not 70, which is why the field is a byte.
        Assert.NotEqual(70, command.Word(1));
    }
}
