using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Scripts;
using Xunit;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Nine sites spread over 16 MiB and nine sites inside one kilobyte are the same number and
/// completely different findings, and they printed identically until now.
/// <para>
/// <c>--in-the-image</c> quotes a floor — "a three-byte pattern turns up by accident about 1.0
/// time(s) in an image this size" — which is a whole-image average computed as though every
/// byte were independent. <c>0x0089</c> turns up NINE times against that floor, which reads as
/// signal. Seven of the nine are inside 791 bytes of a table at 4.70 bits per byte with the
/// same record repeating and names in it; this cartridge's script regions run about 5.70.
/// </para>
/// <para>
/// So the count was nine and the finding is one, and the floor could not say so because a
/// uniform model has nothing to say about a clump.
/// </para>
/// </summary>
public class OnePlaceOrManyTests
{
    private static Rom Flat(params int[] at)
    {
        var image = new byte[0x400000];

        // Something other than zero everywhere, so entropy is not an artefact of padding.
        for (var i = 0; i < image.Length; i++) image[i] = (byte)(i * 7);

        foreach (int o in at)
        {
            image[o] = 0x29;
            image[o + 1] = 0x89;
            image[o + 2] = 0x00;
        }

        return new Rom(image);
    }

    /// <summary>Sites in a clump are one place, however many of them there are.</summary>
    [Fact]
    public void SitesInsideAKilobyteAreOnePlace()
    {
        IReadOnlyList<Clump> clumps = HowClustered.In(Flat(0x1000, 0x1100, 0x1200), [0x1000, 0x1100, 0x1200]);

        Clump only = Assert.Single(clumps);

        Assert.Equal(3, only.Sites);
        Assert.Equal(0x1000, only.From);
        Assert.Equal(0x1200, only.To);
    }

    /// <summary>
    /// And the answer that matters more: sites spread out are as many facts as there are sites.
    /// <para>
    /// The ordinary case, asserted. Without it "everything is one clump" passes the test above
    /// and the instrument says nothing it did not say before.
    /// </para>
    /// </summary>
    [Fact]
    public void SitesSpreadAcrossTheFileAreNotAClumpAtAll()
    {
        Assert.Empty(HowClustered.In(Flat(0x1000, 0x90000, 0x200000), [0x1000, 0x90000, 0x200000]));
    }

    /// <summary>
    /// Two clumps are two places, not one — the count is of runs and not of the span between
    /// the first site and the last.
    /// </summary>
    [Fact]
    public void TwoClumpsFarApartAreTwoPlaces()
    {
        int[] at = [0x1000, 0x1100, 0x90000, 0x90080];

        Assert.Equal(2, HowClustered.In(Flat(at), at).Count);
        Assert.Equal(4, HowClustered.Clumped(Flat(at), at));
    }

    /// <summary>
    /// And the FLOOR is asked the same question, which is the half a break came back green on.
    /// <para>
    /// Reversing a file preserves byte frequencies and it preserves SHAPE: a table reversed is
    /// still a table and still clumps exactly as hard. So a control that counts the reversed
    /// image's sites without asking how clumped they are is comparing a clump-aware number
    /// against a clump-blind one, and milestone 206 shipped exactly that until the break for it
    /// passed.
    /// </para>
    /// <para>
    /// The pattern goes in backwards on purpose — <c>00 89 29</c> here becomes <c>29 89 00</c>
    /// once the sweep reverses the image, which is how a fixture reaches the far side of a
    /// function that does its own reversing.
    /// </para>
    /// </summary>
    [Fact]
    public void TheReversedImageFloorIsCountedInPlacesToo()
    {
        var image = new byte[0x100000];

        for (var i = 0; i < image.Length; i++) image[i] = 0x77;

        // Six sites inside three hundred bytes, written backwards so that reversing the image
        // turns them into setflags sitting on top of each other. The `end` goes in backwards
        // too — without it the sweep's own "does this read as a script" test throws every site
        // away and the fixture produces nothing, which is how this test first failed.
        for (var n = 0; n < 6; n++)
        {
            int at = 0x40000 + (n * 48);

            image[at] = 0x02;
            image[at + 1] = 0x00;
            image[at + 2] = (byte)(0x20 + n);
            image[at + 3] = 0x29;
        }

        (int sites, int _, int places) = EverywhereInTheImage.NoiseFloor(new Rom(image));

        Assert.True(sites >= 6, $"the fixture has to produce sites at all; got {sites}");

        Assert.True(
            places < sites,
            $"the floor has to be counted in places like the thing it is a floor for;"
            + $" got {places} place(s) from {sites} site(s)");

        // AND WHICH OF THE TWO FLOORS THIS WATCHES, said out loud.
        //
        // There are TWO reversed-image floors eleven lines apart with near-identical returns —
        // this one for the flag sweep and MoveNoiseFloor for the move sweep. The first break
        // written for this test was aimed at the other one and came back green, which is the
        // fourth time in this project a break has passed because it pointed somewhere the test
        // was not watching.
        //
        // This test watches NoiseFloor and only NoiseFloor. MoveNoiseFloor has its own two
        // below, on a fixture in its own sweep's pattern, and the break for each was run
        // against both to check that neither catches the other's.
    }

    /// <summary>
    /// And the ordinary case for the flag floor: sites spread across the file are as many
    /// places as there are sites.
    /// <para>
    /// Without this, "the floor is one place, always" passes the test above and the control is
    /// a constant. The pair is the discrimination — <c>places &lt; sites</c> when they clump and
    /// <c>places == sites</c> when they do not — and neither half alone makes it.
    /// </para>
    /// </summary>
    [Fact]
    public void AndTheFlagFloorSaysAsManyPlacesAsSitesWhenTheyAreSpreadOut()
    {
        (int sites, int _, int places) =
            EverywhereInTheImage.NoiseFloor(FlagSitesBackwards(0x10000, 0x60000, 0xC0000));

        Assert.Equal(3, sites);
        Assert.Equal(3, places);
    }

    /// <summary>
    /// A <c>setflag</c> and an <c>end</c>, written backwards so that the sweep's own reversal
    /// turns them the right way round.
    /// <para>
    /// <c>02 00 xx 29</c> here is <c>29 xx 00 02</c> once reversed, which is
    /// <c>setflag &lt;xx&gt;; end</c>. The <c>end</c> is not decoration: without it the sweep's
    /// "does this read as a script" filter throws every site away and the fixture produces
    /// nothing.
    /// </para>
    /// </summary>
    private static Rom FlagSitesBackwards(params int[] at)
    {
        var image = new byte[0x100000];

        for (var i = 0; i < image.Length; i++) image[i] = 0x77;

        for (var n = 0; n < at.Length; n++)
        {
            image[at[n]] = 0x02;
            image[at[n] + 1] = 0x00;
            image[at[n] + 2] = (byte)(0x20 + n);
            image[at[n] + 3] = 0x29;
        }

        return new Rom(image);
    }

    /// <summary>
    /// THE OTHER FLOOR, which milestone 206 shipped unguarded and said so.
    /// <para>
    /// <see cref="EverywhereInTheImage.MoveNoiseFloor"/> is the move sweep's reversed-image
    /// control and it got the same clump-awareness as the flag one at 206, on no evidence: the
    /// break written for it was aimed at <see cref="EverywhereInTheImage.NoiseFloor"/> by
    /// mistake and came back green, and the fixture that caught the flag one produces no sites
    /// at all for this sweep — it matches <c>0x7C &lt;u16 move&gt;</c> and the flag fixture has
    /// no <c>0x7C</c> in it.
    /// </para>
    /// <para>
    /// So this needs its own fixture in its own pattern, written backwards for the same reason.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMoveSweepsFloorIsCountedInPlacesToo()
    {
        int[] at = [0x40000, 0x40030, 0x40060, 0x40090, 0x400C0, 0x400F0];

        (int sites, int _, int _, int places) =
            EverywhereInTheImage.MoveNoiseFloor(MoveSitesBackwards(at), MostMoves);

        Assert.Equal(6, sites);

        Assert.True(
            places < sites,
            $"the move sweep's floor has to be counted in places like the thing it is a floor"
            + $" for; got {places} place(s) from {sites} site(s)");
    }

    /// <summary>
    /// And its ordinary case, which is the half that stops "always one place" passing.
    /// </summary>
    [Fact]
    public void AndTheMoveFloorSaysAsManyPlacesAsSitesWhenTheyAreSpreadOut()
    {
        (int sites, int _, int _, int places) =
            EverywhereInTheImage.MoveNoiseFloor(
                MoveSitesBackwards([0x10000, 0x60000, 0xC0000]), MostMoves);

        Assert.Equal(3, sites);
        Assert.Equal(3, places);
    }

    /// <summary>
    /// How many moves the cartridge's own table holds, which is what the real sweep is handed.
    /// A number here rather than read, because the fixture is not the cartridge — the point is
    /// only that the id in it is inside whatever range it is asked against.
    /// </summary>
    private const int MostMoves = 355;

    /// <summary>
    /// <c>findmove &lt;move 1&gt;</c>, written backwards.
    /// <para>
    /// <c>00 01 7C</c> here is <c>7C 01 00</c> once the sweep reverses the image, and that reads
    /// as move 1 — inside <see cref="MostMoves"/>, which is the only thing the sweep checks.
    /// Unlike the flag sweep this one does not require its sites to read on to an end, so there
    /// is no backwards <c>end</c> to write; if that filter is ever added the fixture stops
    /// producing sites and says so loudly through the <c>Assert.Equal</c> on the count.
    /// </para>
    /// </summary>
    private static Rom MoveSitesBackwards(params int[] at)
    {
        var image = new byte[0x100000];

        for (var i = 0; i < image.Length; i++) image[i] = 0x77;

        foreach (int o in at)
        {
            image[o] = 0x00;
            image[o + 1] = 0x01;
            image[o + 2] = ObstacleMoves.FindMove;
        }

        return new Rom(image);
    }

    /// <summary>
    /// Entropy is the half that says WHY a clump is a clump, and it has to be able to
    /// disagree: a run of repeating table bytes and a run of varied ones are both clumps and
    /// only one of them is explained by being data.
    /// </summary>
    [Fact]
    public void ARepeatingRunReadsAsATableAndAVariedOneDoesNot()
    {
        var table = new byte[0x400000];

        for (var i = 0; i < table.Length; i++) table[i] = (byte)(i % 12);

        var varied = new byte[0x400000];

        for (var i = 0; i < varied.Length; i++) varied[i] = (byte)((i * 2654435761L) >> 13);

        Assert.True(HowClustered.EntropyOf(new Rom(table), 0x1000, 0x1400) < Clump.TableLike);
        Assert.True(HowClustered.EntropyOf(new Rom(varied), 0x1000, 0x1400) > Clump.TableLike);
    }
}
