using PokeMmo.RomExtract.Sound;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// A move's animation is a program, and this reads it.
/// <para>
/// Load a graphic, loop a sound panned to the attacker's side, make three sprites four
/// frames apart, play a second sound, end. Every one of those is read: the opcodes, the
/// delays, the coordinates, the sound ids, the panning. An animation's whole rhythm comes
/// off the cartridge.
/// </para>
/// <para>
/// What does not is what each sprite <em>does</em> once it exists. <c>createsprite</c> names
/// a template, and a template is a pointer to a struct pointing at a callback function.
/// These tests therefore assert that the template comes back as an <em>identity</em> — and
/// in particular that two moves naming the same one are known to be naming the same one,
/// because that is the property the whole next layer is built on.
/// </para>
/// </summary>
public class WhatAMoveLooksLikeTests
{
    private static readonly SyntheticRom Synthetic = new();

    private static AnimScript ReadMove(int move) =>
        AnimScriptReader.Read(
            Synthetic.ToRom(), SyntheticRom.AnimScriptsOffset + move * SyntheticRom.AnimStride);

    /// <summary>An ordinary animation runs to its end command.</summary>
    [Fact]
    public void AnAnimationRunsToItsEnd()
    {
        AnimScript script = ReadMove(0);

        Assert.True(script.EndedProperly);
        Assert.Equal(AnimCommand.End, script.Events[^1].Command);
        Assert.Equal(0, script.Unknown);
    }

    /// <summary>
    /// And its timing is read rather than invented — the delays are the cartridge's own, and
    /// they differ from move to move.
    /// </summary>
    [Fact]
    public void AndItsTimingIsTheCartridgesOwn()
    {
        for (int move = 0; move < 6; move++)
        {
            // Three delays a script, each of the move's own length.
            Assert.Equal(SyntheticRom.AnimFramesFor(move) * 3, ReadMove(move).Frames);
        }

        // And the moves really do differ, so the loop cannot be comparing a constant.
        Assert.True(
            Enumerable.Range(0, 12).Select(m => ReadMove(m).Frames).Distinct().Count() > 1);
    }

    /// <summary>
    /// The sounds come out as sounds, with the ids the script gave. Both of a move's sounds
    /// are found — the looped one and the single one — which is what says the two sound
    /// commands with different argument lengths are both being read correctly.
    /// </summary>
    [Fact]
    public void AndItsSoundsComeOutAsSounds()
    {
        AnimScript script = ReadMove(3);

        Assert.Contains(SyntheticRom.AnimSoundFor(3), script.Sounds);
        Assert.Contains(SyntheticRom.AnimSoundFor(3) + 1, script.Sounds);
    }

    /// <summary>
    /// The sprite template comes back as an identity — the same number the script named,
    /// never dereferenced, because what is behind it is compiled code.
    /// </summary>
    [Fact]
    public void AndTheSpriteTemplateComesBackAsAnIdentity()
    {
        AnimScript script = ReadMove(2);

        Assert.Equal([SyntheticRom.AnimTemplateFor(2)], script.Templates);
    }

    /// <summary>
    /// And two moves naming the same template are known to be naming the same one.
    /// <para>
    /// This is the property the layer above depends on entirely. If templates came back as
    /// distinct values for identical scripts, every move would need its own behaviour and
    /// there would be no long tail to work down — the whole plan for animating this game
    /// rests on a few behaviours covering a great many moves.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTwoMovesNamingTheSameTemplateAreKnownToBe()
    {
        // The fixture repeats templates every five moves.
        Assert.Equal(ReadMove(1).Templates, ReadMove(6).Templates);

        Assert.NotEqual(ReadMove(1).Templates, ReadMove(2).Templates);

        // And across the whole table there are far fewer templates than moves, which is the
        // shape that makes the next step tractable at all.
        IReadOnlyList<AnimScript> all =
        [
            .. Enumerable.Range(0, SyntheticRom.AnimCount).Select(ReadMove),
        ];

        int templates = all.SelectMany(s => s.Templates).Distinct().Count();

        Assert.True(
            templates < all.Count,
            $"{templates} templates for {all.Count} moves — nothing repeats, which cannot be right");
    }

    /// <summary>A call goes somewhere else and a return comes back.</summary>
    [Fact]
    public void ACallGoesSomewhereElseAndComesBack()
    {
        AnimScript script = AnimScriptReader.Read(Synthetic.ToRom(), SyntheticRom.AnimWithACallOffset);

        Assert.True(script.EndedProperly);

        AnimEvent call = script.Events.Single(e => e.Opcode == 0x0E);

        Assert.Equal(SyntheticRom.AnimCalledSubsectionOffset, call.Target);

        // The sound is inside the subsection, so finding it proves the call was followed.
        Assert.Contains(0x2A, script.Sounds);

        // And both delays are counted, which proves the return came back rather than the
        // script ending inside the subsection.
        Assert.Equal(15, script.Frames);
    }

    /// <summary>
    /// A script that hits an opcode the format does not define stops, says it did not end,
    /// and counts the byte it could not account for.
    /// </summary>
    [Fact]
    public void AnUndefinedOpcodeStopsTheScriptAndIsCounted()
    {
        AnimScript script =
            AnimScriptReader.Read(Synthetic.ToRom(), SyntheticRom.AnimWithABadOpcodeOffset);

        Assert.False(script.EndedProperly);
        Assert.Equal(1, script.Unknown);
        Assert.Contains(script.Events, e => e.Command == AnimCommand.Unknown);
    }

    // ---- the table -----------------------------------------------------------------------

    [Fact]
    public void ItFindsTheTableIndexedByMove()
    {
        AnimTable? table = AnimTableLocator.Locate(Synthetic.ToRom());

        Assert.NotNull(table);
        Assert.Equal(SyntheticRom.AnimTableOffset, table!.Offset);
        Assert.Equal(SyntheticRom.AnimCount, table.Count);
    }

    /// <summary>And every entry in it reads to an end.</summary>
    [Fact]
    public void AndEveryAnimationInItReads()
    {
        Rom rom = Synthetic.ToRom();

        AnimTable table = AnimTableLocator.Locate(rom)!;

        var said = new List<string>();

        IReadOnlyList<AnimScript> scripts = AnimScriptReader.All(rom, table.Starts, said.Add);

        Assert.Equal(SyntheticRom.AnimCount, scripts.Count);
        Assert.All(scripts, s => Assert.True(s.EndedProperly));

        Assert.Contains(said, l => l.Contains("ran to an end"));
        Assert.Contains(said, l => l.Contains("distinct sprite templates"));
        Assert.Contains(said, l => l.Contains("does not account for"));
    }

    /// <summary>
    /// A file with nothing in it finds no table and says so, rather than throwing or
    /// returning a run of noise.
    /// </summary>
    [Fact]
    public void AndAFileWithNothingInItFindsNoTable()
    {
        var said = new List<string>();

        Assert.Null(AnimTableLocator.Locate(new Rom(new byte[0x4000]), said.Add));

        Assert.Contains(said, l => l.Contains("no animation table"));
    }
}
