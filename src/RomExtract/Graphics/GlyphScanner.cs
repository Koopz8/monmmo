namespace PokeMmo.RomExtract.Graphics;

/// <summary>A run of tiles that reads as lettering rather than as a picture or as noise.</summary>
public sealed record GlyphRun(int Offset, int Tiles, double Score, int Colours, double Ink)
{
    public uint Address => Rom.BaseAddress + (uint)Offset;

    public int Bytes => Tiles * TileDecoder.BytesPerTile;
}

/// <summary>
/// Finds the cartridge's lettering by what it looks like.
/// <para>
/// Every screen in this client draws its text with a font that came with the graphics
/// library, on rectangles this project chose the colours of. The map and the walking
/// figures look right because they came off the player's own cartridge, and the menus
/// look wrong for exactly the same reason in reverse — so the interface should come from
/// where everything else does.
/// </para>
/// <para>
/// There is no table of contents for a font. What there is, is a shape: hundreds of
/// consecutive tiles that are mostly background, use two or three colours rather than
/// sixteen, and are never all the same tile twice over. A photograph of a Pokémon uses
/// every colour it has; a run of empty space uses one. Lettering sits in the narrow band
/// between, and it sits there for hundreds of tiles at a stretch, which nothing else in
/// an image does.
/// </para>
/// <para>
/// This finds candidates rather than the answer. What comes back is drawn as a PNG and
/// looked at, because "is this the alphabet" is a question a person answers in a second
/// and a heuristic answers badly.
/// </para>
/// </summary>
public static class GlyphScanner
{
    /// <summary>Tiles in a window. Roughly the size of an alphabet with punctuation.</summary>
    private const int WindowTiles = 64;

    /// <summary>
    /// How much of a lettering tile is background.
    /// <para>
    /// Generous at both ends on purpose. A capital M is a busy tile and a comma is
    /// nearly empty, and a band that only admits the average excludes both.
    /// </para>
    /// </summary>
    private const double MinimumInk = 0.08;

    private const double MaximumInk = 0.72;

    /// <summary>
    /// Colours a lettering tile may use, counting the background.
    /// <para>
    /// These games draw text in a fill and a shadow, so three is the usual answer and
    /// five is already a picture rather than a letter.
    /// </para>
    /// </summary>
    private const int MaximumColours = 5;

    /// <summary>
    /// The most promising runs, best first.
    /// <para>
    /// Overlapping windows are collapsed into one run so that a single font comes back
    /// as a single answer rather than as four hundred nearly identical ones.
    /// </para>
    /// </summary>
    public static List<GlyphRun> Scan(Rom rom, int wanted = 8, int step = WindowTiles / 2)
    {
        var found = new List<GlyphRun>();

        int tiles = rom.Length / TileDecoder.BytesPerTile;

        GlyphRun? open = null;

        for (int tile = 0; tile + WindowTiles <= tiles; tile += step)
        {
            (double ink, int colours, bool varied) = Measure(rom, tile);

            bool letters = varied && colours <= MaximumColours && ink is >= MinimumInk and <= MaximumInk;

            if (!letters)
            {
                if (open is { } finished) found.Add(finished);
                open = null;
                continue;
            }

            // A window that qualifies extends the run it is next to rather than starting
            // a new one, which is what keeps one font from being reported as forty.
            open = open is { } running
                ? running with
                {
                    Tiles = tile + WindowTiles - running.Offset / TileDecoder.BytesPerTile,
                    Score = running.Score + 1,
                }
                : new GlyphRun(tile * TileDecoder.BytesPerTile, WindowTiles, 1, colours, ink);
        }

        if (open is { } last) found.Add(last);

        // Longest first: a font is a long run and a coincidence is a short one.
        return [.. found.Select(r => Trim(rom, r)).OrderByDescending(r => r.Tiles).Take(wanted)];
    }

    /// <summary>
    /// Pulls the blank tiles off both ends of a run.
    /// <para>
    /// The scan steps a window at a time, so a run begins up to a window early and ends
    /// up to a window late — it picks up whatever padding sat either side. That is fine
    /// for finding something and no good at all for pointing at it, and the address is
    /// the part that outlives this diagnostic.
    /// </para>
    /// </summary>
    private static GlyphRun Trim(Rom rom, GlyphRun run)
    {
        int first = run.Offset / TileDecoder.BytesPerTile;
        int last = first + run.Tiles - 1;

        while (first < last && IsBlank(rom, first)) first++;
        while (last > first && IsBlank(rom, last)) last--;

        return run with { Offset = first * TileDecoder.BytesPerTile, Tiles = last - first + 1 };
    }

    private static bool IsBlank(Rom rom, int tile)
    {
        int at = tile * TileDecoder.BytesPerTile;

        for (int i = 0; i < TileDecoder.BytesPerTile; i++)
        {
            if (rom.ReadU8(at + i) != 0) return false;
        }

        return true;
    }

    /// <summary>How much ink a window carries, how many colours, and whether it repeats.</summary>
    private static (double Ink, int Colours, bool Varied) Measure(Rom rom, int firstTile)
    {
        var used = new bool[16];
        int inked = 0;
        int pixels = 0;

        // Two tiles being identical is ordinary; a whole window of one tile is padding,
        // and padding scores perfectly on every other test here.
        var signatures = new HashSet<int>();

        for (int tile = 0; tile < WindowTiles; tile++)
        {
            int at = (firstTile + tile) * TileDecoder.BytesPerTile;
            int signature = 17;

            for (int i = 0; i < TileDecoder.BytesPerTile; i++)
            {
                byte packed = rom.ReadU8(at + i);

                used[packed & 0x0F] = true;
                used[(packed >> 4) & 0x0F] = true;

                if ((packed & 0x0F) != 0) inked++;
                if (((packed >> 4) & 0x0F) != 0) inked++;

                pixels += 2;
                signature = signature * 31 + packed;
            }

            signatures.Add(signature);
        }

        return (inked / (double)pixels, used.Count(u => u), signatures.Count > WindowTiles / 4);
    }

    /// <summary>
    /// A run drawn as a sheet, sixteen tiles across, in greys.
    /// <para>
    /// Greys rather than a palette because which palette goes with a font is a separate
    /// question and not one that has to be answered to see whether something is an
    /// alphabet. Index zero is left white so the page reads like a page.
    /// </para>
    /// </summary>
    public static byte[] Sheet(Rom rom, GlyphRun run, int across = 16)
    {
        int rows = (run.Tiles + across - 1) / across;
        int width = across * TileDecoder.TileWidth;
        int height = rows * TileDecoder.TileHeight;

        var padded = new byte[rows * across * TileDecoder.BytesPerTile];
        rom.Slice(run.Offset, Math.Min(run.Bytes, rom.Length - run.Offset)).CopyTo(padded);

        IndexedImage image = TileDecoder.Decode4Bpp(padded, width, height);

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte index = image[x, y];

                // Zero is the page, everything else darkens with the index. A font using
                // 1 for fill and 2 for shadow comes out looking like text either way.
                byte level = index == 0 ? (byte)255 : (byte)Math.Max(0, 200 - index * 40);

                int at = (y * width + x) * 4;

                rgba[at] = level;
                rgba[at + 1] = level;
                rgba[at + 2] = level;
                rgba[at + 3] = 255;
            }
        }

        return PngWriter.ToArray(width, height, rgba);
    }
}
