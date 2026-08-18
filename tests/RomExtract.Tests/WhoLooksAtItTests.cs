using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// This project has had <c>--who-writes</c> since milestone 184 and nothing on the other side.
/// <para>
/// "Nothing sets this" has been askable for eleven milestones and "nothing reads this" has not,
/// so a variable written once and never looked at has read exactly like a variable that gates
/// something. 214's last piece of routine ceiling turned out to be one of the first kind —
/// <c>0x4059</c>, one writer, nothing anywhere that looks at it — and saying so took a
/// hand-grep of fifty-nine byte pairs.
/// </para>
/// <para>
/// The rule that makes this a different question from <c>--who-writes</c> rather than the same
/// one twice is <b>which operand</b>: the source of a copy is a read and the destination is a
/// write, and counting both would make every write a read as well.
/// </para>
/// </summary>
public sealed class WhoLooksAtItTests
{
    private const byte Filler = 0x77;
    private const byte End = 0x02;
    private const byte SetVar = 0x16;
    private const byte Compare = 0x21;
    private const byte CompareVars = 0x22;
    private const byte CopyVar = 0x19;

    private const int Watched = 0x4059;
    private const int Other = 0x4060;

    private static byte[] Blank()
    {
        var image = new byte[0x40000];

        Array.Fill(image, Filler);

        return image;
    }

    private static void Block(byte[] image, int at, params int[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++) image[at + i] = (byte)bytes[i];

        image[at + bytes.Length] = End;
    }

    private static int Lo(int v) => v & 0xFF;

    private static int Hi(int v) => v >> 8;

    private static IReadOnlyList<VariableSite> Reads(byte[] image, int variable = Watched) =>
        EverywhereInTheImage.Reads(new Rom(image), variable);

    /// <summary>A compare against a number is the plainest way to look at a variable.</summary>
    [Fact]
    public void AComparelooksAtTheVariableItNames()
    {
        byte[] image = Blank();

        Block(image, 0x1000, Compare, Lo(Watched), Hi(Watched), 1, 0);

        Assert.Equal(0x1000, Assert.Single(Reads(image)).Offset);
    }

    /// <summary>
    /// AND <c>comparevars</c> LOOKS AT BOTH, which is why it is in the table twice.
    /// <para>
    /// Two of whatever the key is made of: one fixture with the watched variable first and one
    /// with it second. A rule that read only the first operand passes the first and misses the
    /// second, and the miss is silent.
    /// </para>
    /// </summary>
    [Fact]
    public void ComparevarsLooksAtBothOfThem()
    {
        byte[] first = Blank();

        Block(first, 0x1000, CompareVars, Lo(Watched), Hi(Watched), Lo(Other), Hi(Other));

        byte[] second = Blank();

        Block(second, 0x1000, CompareVars, Lo(Other), Hi(Other), Lo(Watched), Hi(Watched));

        Assert.Equal(0x1000, Assert.Single(Reads(first)).Offset);
        Assert.Equal(0x1000, Assert.Single(Reads(second)).Offset);
    }

    /// <summary>
    /// THE RULE THAT MAKES THIS A DIFFERENT QUESTION: a copy reads its source and writes its
    /// destination.
    /// <para>
    /// Both fixtures are the same command with the operands swapped. Counting both would make
    /// every write a read as well, and "nothing reads this" — the whole point of the instrument
    /// — could then never be true of anything anybody had written to.
    /// </para>
    /// </summary>
    [Fact]
    public void ACopyLooksAtItsSourceAndNotItsDestination()
    {
        byte[] read = Blank();

        Block(read, 0x1000, CopyVar, Lo(Other), Hi(Other), Lo(Watched), Hi(Watched));

        byte[] written = Blank();

        Block(written, 0x1000, CopyVar, Lo(Watched), Hi(Watched), Lo(Other), Hi(Other));

        Assert.Equal(0x1000, Assert.Single(Reads(read)).Offset);
        Assert.Empty(Reads(written));
    }

    /// <summary>A setvar puts something in and looks at nothing.</summary>
    [Fact]
    public void PuttingSomethingInIsNotLookingAtIt()
    {
        byte[] image = Blank();

        Block(image, 0x1000, SetVar, Lo(Watched), Hi(Watched), 1, 0);

        Assert.Empty(Reads(image));
    }

    /// <summary>
    /// AND IT COMES BACK EMPTY AND MEANS IT, which is the whole reason this exists.
    /// <para>
    /// A file that writes a variable and never looks at it has to answer nought here, or the
    /// finding <c>0x4059</c> produced — one writer, no readers — is unreachable.
    /// </para>
    /// </summary>
    [Fact]
    public void AVariableWrittenAndNeverLookedAtHasNoReaders()
    {
        byte[] image = Blank();

        Block(image, 0x1000, SetVar, Lo(Watched), Hi(Watched), 1, 0);
        Block(image, 0x2000, Compare, Lo(Other), Hi(Other), 1, 0);

        Assert.Empty(Reads(image));
        Assert.Equal(0x2000, Assert.Single(Reads(image, Other)).Offset);
    }

    /// <summary>
    /// The floor is counted in places like the thing it is a floor for (206), and the fixture is
    /// written backwards because the sweep reverses the image before it looks.
    /// </summary>
    [Fact]
    public void TheReversedImageFloorIsCountedInPlaces()
    {
        (int sites, int reads, int places) = EverywhereInTheImage.ReadNoiseFloor(
            new Rom(Backwards(0x8000, 0x8030, 0x8060, 0x8090, 0x80C0, 0x80F0)), Watched);

        Assert.Equal(6, sites);
        Assert.Equal(6, reads);

        Assert.True(
            places < reads,
            $"the floor has to be counted in places like the thing it is a floor for;"
            + $" got {places} place(s) from {reads} site(s)");
    }

    /// <summary>And the ordinary case, which is what stops "one place, always" passing.</summary>
    [Fact]
    public void AndTheFloorSaysAsManyPlacesAsSitesWhenTheyAreSpreadOut()
    {
        (int sites, int reads, int places) = EverywhereInTheImage.ReadNoiseFloor(
            new Rom(Backwards(0x2000, 0x14000, 0x30000)), Watched);

        Assert.Equal(3, sites);
        Assert.Equal(3, reads);
        Assert.Equal(3, places);
    }

    /// <summary>
    /// <c>compare &lt;watched&gt;, 1 ; end</c> written back to front, so that reversing the image
    /// turns it the right way round.
    /// </summary>
    private static byte[] Backwards(params int[] at)
    {
        byte[] image = Blank();

        int[] forwards = [Compare, Lo(Watched), Hi(Watched), 1, 0, End];

        foreach (int o in at)
        {
            for (var i = 0; i < forwards.Length; i++)
            {
                image[o + i] = (byte)forwards[forwards.Length - 1 - i];
            }
        }

        return image;
    }
}
