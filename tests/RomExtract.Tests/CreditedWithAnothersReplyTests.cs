using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Scanning forward for "the compare that reads this routine's answer" has to stop at anything
/// that could have answered in the meantime, and getting it wrong credits one routine with
/// another's reply.
/// <para>
/// It has done that twice. The first time was <c>0xA0</c>, in BILL's house, and the barrier list
/// was written for it. The second was an ordinary <c>call</c>: SEVEN ISLAND's
/// <c>special 0x0028 ; call 0x081A4EAF ; compare 0x800D 0</c> credited the compare to
/// <c>0x0028</c>, and <c>0x081A4EAF</c> is three commands long and the first of them is
/// <c>special 0x005D</c>. The answer being read belongs to a routine two levels away.
/// </para>
/// <para>
/// Adding the barrier lost 42 of 1097 attributions across the cartridge and dropped three
/// routines out of "asked a question" entirely. <b>Losing attributions is the only direction
/// this can safely be wrong in</b>: a missed reading is a reading nobody makes, a false one goes
/// into a doc as a fact.
/// </para>
/// </summary>
public sealed class CreditedWithAnothersReplyTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte Special = 0x25;
    private const byte Call = 0x04;
    private const byte Compare = 0x21;
    private const byte GotoIf = 0x06;
    private const byte Lock = 0x6A;

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

    private static void Address(byte[] image, int at, int offset)
    {
        uint address = Rom.BaseAddress + (uint)offset;

        for (var i = 0; i < 4; i++) image[at + i] = (byte)(address >> (i * 8));
    }

    /// <summary>The compares attributed to the special at the head of a block.</summary>
    private static IReadOnlyList<(int Value, byte Condition)> Attributed(byte[] image)
    {
        List<ScriptCommand> commands = ScriptReader.Read(new Rom(image), Rom.BaseAddress + 0x1000);

        return SpecialCalls.WhatIsComparedAfter(commands, 0);
    }

    /// <summary>
    /// The ordinary case: the compare right after a special is that special's answer.
    /// <para>
    /// Asserted first, because without it every test below passes on an instrument that
    /// attributes nothing to anybody.
    /// </para>
    /// </summary>
    [Fact]
    public void ACompareRightAfterASpecialReadsThatSpecialsAnswer()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x88, 0x01);
        Put(image, 0x1003, Compare, 0x0D, 0x80, 0x07, 0x00);
        Put(image, 0x1008, GotoIf, 0x01);
        Address(image, 0x100A, 0x1000);
        Put(image, 0x100E, End);

        (int value, byte condition) = Assert.Single(Attributed(image));

        Assert.Equal(7, value);
        Assert.Equal(1, condition);
    }

    /// <summary>
    /// AND THE ONE THAT WAS WRONG: a call in between, and the answer is no longer this
    /// routine's.
    /// </summary>
    [Fact]
    public void ACallInBetweenMeansTheAnswerBelongsToSomebodyElse()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x88, 0x01);
        Put(image, 0x1003, Call);
        Address(image, 0x1004, 0x1800);
        Put(image, 0x1008, Compare, 0x0D, 0x80, 0x07, 0x00);
        Put(image, 0x100D, GotoIf, 0x01);
        Address(image, 0x100F, 0x1000);
        Put(image, 0x1013, End);

        // Something to be called, so the block is a block rather than a broken read.
        Put(image, 0x1800, Special, 0x5D, 0x00, 0x03);

        Assert.Empty(Attributed(image));
    }

    /// <summary>
    /// And the barrier is specific rather than "anything in between": a command that cannot
    /// have answered does not stop the reading.
    /// <para>
    /// Without this, "stop at the first command of any kind" passes the test above and the
    /// instrument attributes nothing anywhere — which is the same output as being careful and
    /// a completely different rule.
    /// </para>
    /// </summary>
    [Fact]
    public void ACommandThatCannotHaveAnsweredDoesNotStopTheReading()
    {
        byte[] image = Blank();

        Put(image, 0x1000, Special, 0x88, 0x01);
        Put(image, 0x1003, Lock);
        Put(image, 0x1004, Compare, 0x0D, 0x80, 0x07, 0x00);
        Put(image, 0x1009, GotoIf, 0x01);
        Address(image, 0x100B, 0x1000);
        Put(image, 0x100F, End);

        Assert.Equal(7, Assert.Single(Attributed(image)).Value);
    }
}
