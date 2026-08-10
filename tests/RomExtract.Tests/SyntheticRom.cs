using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;

namespace PokeMmo.RomExtract.Tests;

/// <summary>
/// Builds a fake cartridge that satisfies the same structural invariants the real
/// one does.
/// <para>
/// This is what makes the extractor testable without a copyrighted ROM present: the
/// tests assert that data written at known offsets, in the documented on-cartridge
/// formats, is recovered byte-for-byte. Nothing here contains cartridge content —
/// it is all generated.
/// </para>
/// </summary>
public sealed class SyntheticRom
{
    public const int RomSize = 2 * 1024 * 1024;
    public const int SpeciesCount = 412;

    // Deliberately chosen so that every table sits at a known, checkable offset.
    public const int SpeciesNamesOffset = 0x001000;
    public const int BaseStatsOffset = 0x003000;
    public const int FrontPicTableOffset = 0x008000;
    public const int BackPicTableOffset = 0x00A000;
    public const int NormalPaletteTableOffset = 0x00C000;

    /// <summary>
    /// Deliberately placed immediately after the normal palette table, with no gap.
    /// The real cartridge lays these two out back-to-back, and a scanner that skips
    /// past a completed run by the wrong amount will step over this table's first
    /// entry and never find it.
    /// </summary>
    public const int ShinyPaletteTableOffset = NormalPaletteTableOffset + SpeciesCount * 8;

    private const int TestSpriteBlobOffset = 0x010000;
    private const int FillerSpriteBlobOffset = 0x020000;
    private const int TestPaletteBlobOffset = 0x030000;
    private const int FillerPaletteBlobOffset = 0x031000;
    private const int TestBackSpriteBlobOffset = 0x032000;
    private const int ShinyPaletteBlobOffset = 0x033000;

    /// <summary>The species index whose sprite and palette are distinctive and asserted against.</summary>
    public const int TestSpecies = 1;

    private readonly byte[] _data = new byte[RomSize];

    public IndexedImage ExpectedFrontImage { get; }
    public IndexedImage ExpectedBackImage { get; }
    public Rgba32[] ExpectedPalette { get; }
    public Rgba32[] ExpectedShinyPalette { get; }

    public SyntheticRom()
    {
        ExpectedFrontImage = BuildPatternImage(seed: 7);
        ExpectedBackImage = BuildPatternImage(seed: 19);
        ExpectedPalette = BuildPalette(seed: 3);
        ExpectedShinyPalette = BuildPalette(seed: 11);

        WriteHeader();
        WriteSpeciesNames();
        WriteBaseStats();
        WriteGraphicsBlobs();
        WritePicTables();
        WritePaletteTables();
    }

    public byte[] Bytes => _data;

    public Rom ToRom() => new(_data);

    /// <summary>Names written into the synthetic name table, indexed by species.</summary>
    public static string NameFor(int index) => index == TestSpecies ? "BULBASAUR" : $"MON{index:D3}";

    // --- header -------------------------------------------------------------

    private void WriteHeader()
    {
        WriteAscii(0xA0, "POKEMON FIRE");
        WriteAscii(0xAC, "BPRE");
        WriteAscii(0xB0, "01");
        _data[0xBC] = 0;
    }

    private void WriteAscii(int offset, string text)
    {
        for (int i = 0; i < text.Length; i++) _data[offset + i] = (byte)text[i];
    }

    // --- data tables --------------------------------------------------------

    private void WriteSpeciesNames()
    {
        for (int i = 0; i < SpeciesCount; i++)
        {
            byte[] encoded = EncodeNameAsCartridgeWould(NameFor(i));
            encoded.CopyTo(_data, SpeciesNamesOffset + i * GameText.SpeciesNameLength);
        }
    }

    /// <summary>
    /// Encodes a name the way the cartridge actually stores it, rather than by calling
    /// the production encoder.
    /// <para>
    /// The name table is a fixed-width array initialised from string literals, so any
    /// space after the terminator is <em>zero</em> fill — not more terminator bytes.
    /// The fixture must model that independently; reusing the production encoder here
    /// would only ever confirm that the code agrees with itself.
    /// </para>
    /// </summary>
    private static byte[] EncodeNameAsCartridgeWould(string name)
    {
        var buffer = new byte[GameText.SpeciesNameLength]; // zero-filled

        int i = 0;
        foreach (char c in name)
        {
            if (i >= GameText.SpeciesNameLength - 1) break;
            buffer[i++] = EncodeCharAsCartridgeWould(c);
        }

        buffer[i] = GameText.Terminator;
        return buffer;
    }

    private static byte EncodeCharAsCartridgeWould(char c) => c switch
    {
        >= 'A' and <= 'Z' => (byte)(0xBB + (c - 'A')),
        >= 'a' and <= 'z' => (byte)(0xD5 + (c - 'a')),
        >= '0' and <= '9' => (byte)(0xA1 + (c - '0')),
        '.' => 0xAD,
        '-' => 0xAE,
        '’' => 0xB4,
        '♂' => 0xB5,
        '♀' => 0xB6,
        _ => 0x00,
    };

    private void WriteBaseStats()
    {
        for (int i = 0; i < SpeciesCount; i++)
        {
            byte[] record = i == TestSpecies ? BulbasaurRecord() : GenericRecord(i);
            record.CopyTo(_data, BaseStatsOffset + i * 28);
        }
    }

    /// <summary>
    /// The anchor record the locator searches for: HP 45, Atk 49, Def 49, Spe 45,
    /// SpA 65, SpD 65, Grass/Poison, catch rate 45, exp yield 64.
    /// </summary>
    private static byte[] BulbasaurRecord()
    {
        var r = new byte[28];
        r[0] = 45; r[1] = 49; r[2] = 49; r[3] = 45; r[4] = 65; r[5] = 65;
        r[6] = 12; r[7] = 3;
        r[8] = 45; r[9] = 64;
        r[10] = 0x01; r[11] = 0x00;   // 1 SpA EV... packed low bits
        r[16] = 31;                   // gender ratio
        r[17] = 20;                   // egg cycles
        r[18] = 70;                   // base friendship
        r[19] = 3;                    // medium slow
        r[20] = 1; r[21] = 7;         // monster / grass
        r[22] = 65; r[23] = 0;        // abilities
        r[24] = 0;
        r[26] = 12;                   // body colour, no-flip bit clear
        return r;
    }

    private static byte[] GenericRecord(int index)
    {
        var r = new byte[28];
        r[0] = (byte)(40 + index % 60);
        r[1] = (byte)(40 + index % 50);
        r[2] = (byte)(40 + index % 40);
        r[3] = (byte)(40 + index % 55);
        r[4] = (byte)(40 + index % 45);
        r[5] = (byte)(40 + index % 35);
        r[6] = (byte)(index % 18);
        r[7] = (byte)((index + 5) % 18);
        r[8] = (byte)(1 + index % 250);
        r[9] = (byte)(1 + index % 200);
        r[16] = 127;
        r[17] = 20;
        r[18] = 70;
        r[19] = (byte)(index % 6);
        r[20] = (byte)(1 + index % 15);
        r[21] = (byte)(1 + (index + 3) % 15);
        r[22] = (byte)(index % 70);
        r[26] = (byte)(index % 10);
        return r;
    }

    // --- graphics -----------------------------------------------------------

    private void WriteGraphicsBlobs()
    {
        WriteBlob(TestSpriteBlobOffset, Lz77.Compress(TileDecoder.Encode4Bpp(ExpectedFrontImage)));
        WriteBlob(TestBackSpriteBlobOffset, Lz77.Compress(TileDecoder.Encode4Bpp(ExpectedBackImage)));
        WriteBlob(FillerSpriteBlobOffset, Lz77.Compress(new byte[0x800]));
        WriteBlob(TestPaletteBlobOffset, Lz77.Compress(GbaPalette.ToBytes(ExpectedPalette)));
        WriteBlob(ShinyPaletteBlobOffset, Lz77.Compress(GbaPalette.ToBytes(ExpectedShinyPalette)));
        WriteBlob(FillerPaletteBlobOffset, Lz77.Compress(new byte[GbaPalette.SizeBytes]));
    }

    private void WriteBlob(int offset, byte[] blob) => blob.CopyTo(_data, offset);

    private void WritePicTables()
    {
        for (int i = 0; i < SpeciesCount; i++)
        {
            uint front = Rom.BaseAddress + (uint)(i == TestSpecies ? TestSpriteBlobOffset : FillerSpriteBlobOffset);
            WritePicEntry(FrontPicTableOffset + i * 8, front, i);

            uint back = Rom.BaseAddress + (uint)(i == TestSpecies ? TestBackSpriteBlobOffset : FillerSpriteBlobOffset);
            WritePicEntry(BackPicTableOffset + i * 8, back, i);
        }
    }

    private void WritePicEntry(int offset, uint pointer, int tag)
    {
        WriteU32(offset, pointer);
        WriteU16(offset + 4, 0x0800);
        WriteU16(offset + 6, (ushort)tag);
    }

    private void WritePaletteTables()
    {
        for (int i = 0; i < SpeciesCount; i++)
        {
            uint normal = Rom.BaseAddress + (uint)(i == TestSpecies ? TestPaletteBlobOffset : FillerPaletteBlobOffset);
            WritePaletteEntry(NormalPaletteTableOffset + i * 8, normal, i);

            uint shiny = Rom.BaseAddress + (uint)(i == TestSpecies ? ShinyPaletteBlobOffset : FillerPaletteBlobOffset);
            WritePaletteEntry(ShinyPaletteTableOffset + i * 8, shiny, i);
        }
    }

    private void WritePaletteEntry(int offset, uint pointer, int tag)
    {
        WriteU32(offset, pointer);
        WriteU16(offset + 4, (ushort)tag);
        WriteU16(offset + 6, 0);
    }

    private void WriteU16(int offset, ushort value)
    {
        _data[offset] = (byte)(value & 0xFF);
        _data[offset + 1] = (byte)(value >> 8);
    }

    private void WriteU32(int offset, uint value)
    {
        _data[offset] = (byte)(value & 0xFF);
        _data[offset + 1] = (byte)((value >> 8) & 0xFF);
        _data[offset + 2] = (byte)((value >> 16) & 0xFF);
        _data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    // --- generated content --------------------------------------------------

    private static IndexedImage BuildPatternImage(int seed)
    {
        var image = new IndexedImage(RomExtractor.MonSpriteWidth, RomExtractor.MonSpriteHeight);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // A pattern with both flat runs and structure, so LZ77 has something
                // to compress and a scrambled tile order would be obvious.
                image[x, y] = (byte)(((x / 3) ^ (y / 2) ^ seed) & 0x0F);
            }
        }

        return image;
    }

    private static Rgba32[] BuildPalette(int seed)
    {
        var colors = new Rgba32[GbaPalette.ColorCount];

        for (int i = 0; i < colors.Length; i++)
        {
            // Expressed as the 8-bit values a 5-bit channel expands to, so the
            // round trip through the cartridge format is exact.
            byte r = Expand5((i * 2 + seed) % 32);
            byte g = Expand5((i * 3 + seed) % 32);
            byte b = Expand5((i * 5 + seed) % 32);
            colors[i] = new Rgba32(r, g, b, (byte)(i == 0 ? 0 : 255));
        }

        return colors;
    }

    /// <summary>Mirrors the 5-bit to 8-bit expansion the palette decoder performs.</summary>
    private static byte Expand5(int v5) => (byte)((v5 << 3) | (v5 >> 2));
}
