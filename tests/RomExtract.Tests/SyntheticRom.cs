using PokeMmo.RomExtract;
using PokeMmo.RomExtract.Graphics;
using PokeMmo.Core.Battle;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Maps;

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

    /// <summary>
    /// A shorter palette table tagged from zero, standing in for the trainer-sprite
    /// palette table that also lives in a real image. It exists so the tests prove the
    /// selector distinguishes the shiny table by tag base rather than by "whichever
    /// palette-shaped run comes second".
    /// </summary>
    public const int DecoyPaletteTableOffset = ShinyPaletteTableOffset + SpeciesCount * 8 + 0x100;

    public const int DecoyPaletteEntryCount = 148;

    private const int TestSpriteBlobOffset = 0x010000;
    private const int FillerSpriteBlobOffset = 0x020000;
    private const int TestPaletteBlobOffset = 0x030000;
    private const int FillerPaletteBlobOffset = 0x031000;
    private const int TestBackSpriteBlobOffset = 0x032000;
    private const int ShinyPaletteBlobOffset = 0x033000;

    // --- map data ---------------------------------------------------------------
    // Placed well clear of the species tables so a stray scan cannot confuse the two.

    public const int TilesetTilesOffset = 0x040000;
    public const int TilesetPalettesOffset = 0x041000;
    public const int TilesetMetatilesOffset = 0x042000;
    public const int TilesetRecordOffset = 0x043000;
    public const int MapBlocksOffset = 0x044000;
    public const int MapLayoutOffset = 0x045000;
    public const int MapLayoutTableOffset = 0x046000;
    public const int TilesetAttributesOffset = 0x047000;

    /// <summary>
    /// Bytes per metatile in the attributes table, matching the real cartridge. The
    /// stride is the whole point of this fixture: reading it wrongly does not fail,
    /// it silently returns a neighbouring metatile's behaviour.
    /// </summary>
    public const int AttributeStride = 4;

    /// <summary>The behaviour written for a given metatile — every third one is grass.</summary>
    public static byte BehaviourOfMetatile(int metatile) =>
        metatile % 3 == 0 ? MetatileBehaviour.TallGrass : MetatileBehaviour.Normal;

    public const int MapWidth = 10;
    public const int MapHeight = 8;
    public const int MapLayoutTableLength = 64;

    /// <summary>A null slot planted mid-table, mirroring the dead entries real images carry.</summary>
    public const int DeadLayoutTableIndex = 40;

    public const int MapHeadersOffset = 0x050000;
    public const int BankArraysOffset = 0x051000;
    public const int MapGroupsOffset = 0x052000;
    public const int RegionNameTextOffset = 0x053000;
    public const int RegionMapEntriesOffset = 0x054000;
    public const int DecoyNameTextOffset = 0x055000;
    public const int DecoyNamePointersOffset = 0x056000;

    /// <summary>
    /// A decoy run of text pointers carrying names that are not places, standing in
    /// for the run a real image contains. It exists so the tests prove the locator
    /// picks the location table by its contents rather than by being the first or
    /// longest run of text pointers it happens to find.
    /// </summary>
    public const int DecoyNameCount = 52;

    public static string DecoyNameFor(int index) => $"EXIT {index:D2}";

    /// <summary>An unnamed section mid-table, as real images contain.</summary>
    public const int DeadRegionNameIndex = 47;

    public const int BankCount = 8;
    public const int MapsPerBank = 5;
    public const int RegionLocationCount = 64;

    /// <summary>
    /// The name written into the region map table for a given section id. Modelled on
    /// real place names, because "reads like a place" is what distinguishes this table
    /// from the other runs of text pointers in an image.
    /// </summary>
    public static string RegionNameFor(int index) => index switch
    {
        0 => "PALLET TOWN",
        1 => "VIRIDIAN CITY",
        2 => "VIRIDIAN FOREST",
        _ => $"ROUTE {index:D2}",
    };

    /// <summary>Which region section a given map is tagged with.</summary>
    public static int RegionSectionFor(int bank, int map) => (bank * MapsPerBank + map) % RegionLocationCount;
    public const int MetatileCount = 64;

    // --- warps and connections ---------------------------------------------------

    public const int MapEventsOffset = 0x070000;
    public const int MapWarpsOffset = 0x072000;
    public const int MapConnectionRecordOffset = 0x074000;
    public const int MapConnectionsOffset = 0x076000;

    private const int EventsStride = 32;
    private const int WarpsStride = 64;
    private const int ConnectionRecordStride = 16;
    private const int ConnectionsStride = 64;

    /// <summary>
    /// A map index with no events pointer at all, as plenty of real maps have. Reading
    /// one has to produce no warps rather than reading whatever happens to sit at
    /// address zero.
    /// </summary>
    public const int MapWithoutEvents = 3;

    /// <summary>
    /// A map carrying a warp placed outside its own bounds. Real images contain these
    /// — leftovers from editing — and they should be dropped, not stored as squares
    /// nobody can ever stand on.
    /// </summary>
    public const int MapWithAStrayWarp = 7;

    public static int MapIndex(int bank, int map) => bank * MapsPerBank + map;

    public static string MapIdAt(int index) => $"{index / MapsPerBank}.{index % MapsPerBank}";

    /// <summary>The warps written for a map, which is what extraction is checked against.</summary>
    public static List<Warp> WarpsFor(int index)
    {
        if (index == MapWithoutEvents) return [];

        int total = MapCount;

        var warps = new List<Warp>
        {
            new(1, 1, 0, MapIdAt((index + 1) % total)),
            new(2, 3, 1, MapIdAt((index + total - 1) % total)),
        };

        // The stray one is written to the cartridge but is not expected back.
        return warps;
    }

    /// <summary>The connections written for a map.</summary>
    public static List<MapConnection> ConnectionsFor(int index)
    {
        int total = MapCount;

        return
        [
            new(ConnectionSide.Down, index - 2, MapIdAt((index + 1) % total)),
            new(ConnectionSide.Left, 0, MapIdAt((index + total - 1) % total)),
        ];
    }

    public const int MapCount = BankCount * MapsPerBank;

    // --- overworld sprites -------------------------------------------------------

    public const int OverworldTableOffset = 0x080000;
    public const int OverworldRecordsOffset = 0x081000;
    public const int OverworldFrameListsOffset = 0x084000;
    public const int OverworldPixelsOffset = 0x088000;
    public const int OverworldPaletteTableOffset = 0x090000;
    public const int OverworldPaletteDataOffset = 0x091000;

    public const int OverworldCount = 80;

    /// <summary>A walking figure: three facings by three steps.</summary>
    public const int OverworldFrameCount = 9;

    public const int OverworldWidth = 16;
    public const int OverworldHeight = 32;

    /// <summary>Overworld palettes are tagged in a fixed range rather than counted from zero.</summary>
    public const int OverworldFirstPaletteTag = 0x1100;

    public const int OverworldPaletteCount = 12;

    /// <summary>
    /// One graphics id left null, as real tables contain. Keeping the hole is what
    /// stops every id after it shifting by one.
    /// </summary>
    public const int DeadOverworldIndex = 17;

    public static int OverworldPaletteTagFor(int index) =>
        OverworldFirstPaletteTag + index % OverworldPaletteCount;

    /// <summary>The colour a given sprite's given frame is filled with.</summary>
    public static byte OverworldPixelFor(int index, int frame) => (byte)((index + frame) % 16);

    // --- learnsets ---------------------------------------------------------------

    public const int LearnsetTableOffset = 0x060000;

    /// <summary>The shared terminator-only list the unused species slots point at.</summary>
    public const int EmptyLearnsetOffset = 0x061000;

    public const int LearnsetBlobsOffset = 0x062000;

    /// <summary>Room per learnset. Generous, so the lists never run into each other.</summary>
    public const int LearnsetStride = 64;

    /// <summary>
    /// The block of unused species indices this generation leaves between its two
    /// halves. Every one of them points at a learnset containing nothing but the
    /// terminator — a real shape, and one that has to be stepped over rather than
    /// treated as the end of the table, or every species after it shifts by
    /// twenty-five.
    /// </summary>
    public const int FirstUnusedSpecies = 252;

    public const int LastUnusedSpecies = 276;

    public static bool SpeciesHasLearnset(int species) =>
        species is < FirstUnusedSpecies or > LastUnusedSpecies;

    /// <summary>The learnset written for a species, which is what extraction is checked against.</summary>
    public static List<LevelUpMove> LearnsetFor(int species)
    {
        var moves = new List<LevelUpMove>();
        if (!SpeciesHasLearnset(species)) return moves;

        int count = 1 + species % 6;

        for (int i = 0; i < count; i++)
        {
            int level = 1 + i * 7 + species % 3;
            int move = 1 + (species * 3 + i * 11) % 354;

            moves.Add(new LevelUpMove(level, move));
        }

        return moves;
    }

    /// <summary>The species index whose sprite and palette are distinctive and asserted against.</summary>
    public const int TestSpecies = 1;

    /// <summary>
    /// Shiny palette entries are tagged <c>species + 500</c> rather than by species
    /// alone, so that a shiny palette and its normal counterpart can be resident in
    /// the sprite palette manager at the same time without colliding.
    /// <para>
    /// Modelled here because it is what the real cartridge does, and because assuming
    /// tags always start at zero is what made the scanner walk straight past this table.
    /// </para>
    /// </summary>
    public const int ShinyTagBase = 500;

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
        WriteMapData();
        WriteMapHeadersAndBanks();
        WriteLearnsets();
        WriteOverworldSprites();
    }

    /// <summary>Palette 0 of the synthetic tileset — what a rendered map is checked against.</summary>
    public Rgba32[] ExpectedTilesetPalette { get; } = BuildTilesetPalette();

    /// <summary>The metatile drawn at a given map square, and therefore its colour index.</summary>
    public static int MetatileAt(int blockX, int blockY) => (blockX + blockY) % 16;

    /// <summary>
    /// Builds a complete, if minimal, map: a tileset whose tiles are flat colour, a
    /// metatile per colour, a block grid, a layout record and a pointer table.
    /// <para>
    /// Flat-colour tiles are deliberate. Every map square renders as one solid 16x16
    /// block, so a rendering test can assert exact pixel colours — which catches tile
    /// ordering, palette selection, layer compositing and block indexing all at once.
    /// </para>
    /// </summary>
    private void WriteMapData()
    {
        // Tile n is filled with colour index n % 16. Tile 0 is therefore all colour 0,
        // which the top metatile layer treats as transparent.
        for (int tile = 0; tile < MetatileCount; tile++)
        {
            byte nibble = (byte)(tile % 16);
            byte packed = (byte)(nibble | (nibble << 4));

            for (int i = 0; i < 32; i++)
                _data[TilesetTilesOffset + tile * 32 + i] = packed;
        }

        byte[] palette = GbaPalette.ToBytes(ExpectedTilesetPalette);
        for (int p = 0; p < TilesetRecord.PaletteCount; p++)
            palette.CopyTo(_data, TilesetPalettesOffset + p * GbaPalette.SizeBytes);

        // Metatile m draws tile m across its whole bottom layer, and tile 0 — all
        // transparent — across its top layer.
        for (int m = 0; m < MetatileCount; m++)
        {
            for (int entry = 0; entry < 8; entry++)
            {
                ushort value = entry < 4 ? (ushort)m : (ushort)0;
                WriteU16(TilesetMetatilesOffset + (m * 8 + entry) * 2, value);
            }
        }

        for (int m = 0; m < MetatileCount; m++)
            _data[TilesetAttributesOffset + m * AttributeStride] = BehaviourOfMetatile(m);

        _data[TilesetRecordOffset] = 0;     // not compressed
        _data[TilesetRecordOffset + 1] = 0; // primary
        WriteU32(TilesetRecordOffset + 4, Rom.BaseAddress + TilesetTilesOffset);
        WriteU32(TilesetRecordOffset + 8, Rom.BaseAddress + TilesetPalettesOffset);
        WriteU32(TilesetRecordOffset + 12, Rom.BaseAddress + TilesetMetatilesOffset);

        // Attributes are a data pointer; the callback beside them is a function
        // pointer, which on this hardware carries a set low bit. That difference is
        // how the two are told apart, so the fixture has to model it.
        WriteU32(TilesetRecordOffset + 16, Rom.BaseAddress + TilesetAttributesOffset);
        WriteU32(TilesetRecordOffset + 20, Rom.BaseAddress + TilesetRecordOffset + 1);

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
                WriteU16(MapBlocksOffset + (y * MapWidth + x) * 2, (ushort)MetatileAt(x, y));
        }

        WriteU32(MapLayoutOffset, MapWidth);
        WriteU32(MapLayoutOffset + 4, MapHeight);
        WriteU32(MapLayoutOffset + 8, Rom.BaseAddress + MapBlocksOffset);   // border
        WriteU32(MapLayoutOffset + 12, Rom.BaseAddress + MapBlocksOffset);  // blocks
        WriteU32(MapLayoutOffset + 16, Rom.BaseAddress + TilesetRecordOffset);
        WriteU32(MapLayoutOffset + 20, 0);                                  // no secondary tileset
        _data[MapLayoutOffset + 24] = 2;
        _data[MapLayoutOffset + 25] = 2;

        for (int i = 0; i < MapLayoutTableLength; i++)
        {
            // A dead slot mid-table, as real images contain. Ending the run here would
            // truncate the table and shift every index after it.
            bool dead = i == DeadLayoutTableIndex;
            WriteU32(MapLayoutTableOffset + i * 4, dead ? 0u : Rom.BaseAddress + MapLayoutOffset);
        }
    }

    /// <summary>
    /// Writes map headers, the two-level bank table, and the region map table that
    /// names each section.
    /// </summary>
    private void WriteMapHeadersAndBanks()
    {
        for (int bank = 0; bank < BankCount; bank++)
        {
            for (int map = 0; map < MapsPerBank; map++)
            {
                int index = bank * MapsPerBank + map;
                int header = MapHeadersOffset + index * 28;

                WriteU32(header, Rom.BaseAddress + MapLayoutOffset);
                WriteU32(header + 4, index == MapWithoutEvents
                    ? 0
                    : Rom.BaseAddress + (uint)(MapEventsOffset + index * EventsStride));
                WriteU32(header + 8, 0);   // scripts
                WriteU32(header + 12, Rom.BaseAddress + (uint)(MapConnectionRecordOffset + index * ConnectionRecordStride));

                WriteWarpsFor(index);
                WriteConnectionsFor(index);
                WriteU16(header + 16, (ushort)(100 + index));       // music
                WriteU16(header + 18, 1);                           // layout id
                _data[header + 20] = (byte)RegionSectionFor(bank, map);
                _data[header + 21] = 0;    // cave
                _data[header + 22] = 0;    // weather
                _data[header + 23] = 1;    // map type

                WriteU32(BankArraysOffset + index * 4, Rom.BaseAddress + (uint)header);
            }

            WriteU32(
                MapGroupsOffset + bank * 4,
                Rom.BaseAddress + (uint)(BankArraysOffset + bank * MapsPerBank * 4));
        }

        for (int i = 0; i < DecoyNameCount; i++)
        {
            int decoyText = DecoyNameTextOffset + i * 16;
            EncodeTextAsCartridgeWould(DecoyNameFor(i), 16).CopyTo(_data, decoyText);
            WriteU32(DecoyNamePointersOffset + i * 4, Rom.BaseAddress + (uint)decoyText);
        }

        for (int i = 0; i < RegionLocationCount; i++)
        {
            int textAt = RegionNameTextOffset + i * 16;
            EncodeTextAsCartridgeWould(RegionNameFor(i), 16).CopyTo(_data, textAt);

            int entry = RegionMapEntriesOffset + i * 8;
            _data[entry] = (byte)(i % 20);
            _data[entry + 1] = (byte)(i % 14);
            _data[entry + 2] = 1;
            _data[entry + 3] = 1;

            // One slot left unnamed, so the scan has to step over it rather than
            // treating it as the end of the table.
            WriteU32(entry + 4, i == DeadRegionNameIndex ? 0u : Rom.BaseAddress + (uint)textAt);
        }
    }

    /// <summary>
    /// Writes the level-up table: one pointer per species, each leading to a list of
    /// packed level-and-move words ending in 0xFFFF.
    /// </summary>
    private void WriteLearnsets()
    {
        WriteU16(EmptyLearnsetOffset, LevelUpMove.Terminator);

        for (int species = 0; species < SpeciesCount; species++)
        {
            List<LevelUpMove> moves = LearnsetFor(species);

            if (moves.Count == 0)
            {
                WriteU32(LearnsetTableOffset + species * 4, Rom.BaseAddress + EmptyLearnsetOffset);
                continue;
            }

            int blob = LearnsetBlobsOffset + species * LearnsetStride;

            for (int i = 0; i < moves.Count; i++)
                WriteU16(blob + i * 2, moves[i].Encode());

            WriteU16(blob + moves.Count * 2, LevelUpMove.Terminator);
            WriteU32(LearnsetTableOffset + species * 4, Rom.BaseAddress + (uint)blob);
        }
    }

    /// <summary>
    /// Writes an events record and its warp table.
    /// <para>
    /// The record is four counts followed by four pointers, and the warps are the
    /// <em>second</em> of each — a layout worth modelling exactly, because reading the
    /// object-event count and pointer instead would produce a plausible number of
    /// plausible-looking warps from entirely the wrong table.
    /// </para>
    /// </summary>
    private void WriteWarpsFor(int index)
    {
        if (index == MapWithoutEvents) return;

        List<Warp> warps = WarpsFor(index);
        int table = MapWarpsOffset + index * WarpsStride;
        int written = 0;

        foreach (Warp warp in warps)
        {
            WriteWarp(table + written * 8, warp);
            written++;
        }

        // One map also gets a warp beyond its own edge, which extraction should drop.
        if (index == MapWithAStrayWarp)
        {
            WriteWarp(table + written * 8, new Warp(MapWidth + 4, MapHeight + 4, 0, MapIdAt(0)));
            written++;
        }

        int events = MapEventsOffset + index * EventsStride;

        _data[events] = 3;                  // object events, deliberately not the warps
        _data[events + 1] = (byte)written;
        _data[events + 2] = 1;              // coord events
        _data[events + 3] = 2;              // background events

        WriteU32(events + 4, Rom.BaseAddress + (uint)(MapWarpsOffset + 0x1000));  // objects, elsewhere
        WriteU32(events + 8, Rom.BaseAddress + (uint)table);
        WriteU32(events + 12, 0);
        WriteU32(events + 16, 0);
    }

    private void WriteWarp(int at, Warp warp)
    {
        string[] parts = warp.TargetMapId.Split('.');

        WriteU16(at, (ushort)warp.X);
        WriteU16(at + 2, (ushort)warp.Y);
        _data[at + 4] = 0;                              // elevation
        _data[at + 5] = (byte)warp.TargetWarpId;
        _data[at + 6] = byte.Parse(parts[1]);           // map number
        _data[at + 7] = byte.Parse(parts[0]);           // bank
    }

    /// <summary>
    /// Writes a connections record: a count and a pointer, leading to twelve-byte
    /// entries whose direction numbering is the cartridge's own and not this project's.
    /// </summary>
    private void WriteConnectionsFor(int index)
    {
        List<MapConnection> connections = ConnectionsFor(index);
        int table = MapConnectionsOffset + index * ConnectionsStride;

        for (int i = 0; i < connections.Count; i++)
        {
            MapConnection connection = connections[i];
            string[] parts = connection.MapId.Split('.');
            int at = table + i * 12;

            WriteU32(at, connection.Side switch
            {
                ConnectionSide.Down => 1u,
                ConnectionSide.Up => 2u,
                ConnectionSide.Left => 3u,
                _ => 4u,
            });

            WriteU32(at + 4, unchecked((uint)connection.Offset));
            _data[at + 8] = byte.Parse(parts[0]);   // bank
            _data[at + 9] = byte.Parse(parts[1]);   // map number
        }

        // A dive connection nobody can walk through, which must be read and discarded
        // rather than turning into a fifth walkable edge.
        int dive = table + connections.Count * 12;
        WriteU32(dive, 5);
        WriteU32(dive + 4, 0);

        int record = MapConnectionRecordOffset + index * ConnectionRecordStride;

        WriteU32(record, (uint)(connections.Count + 1));
        WriteU32(record + 4, Rom.BaseAddress + (uint)table);
    }

    /// <summary>
    /// Writes the overworld graphics table: pointers to 36-byte records, each naming a
    /// frame list, and a palette table tagged in the overworld range.
    /// <para>
    /// The record's <c>size</c> field is written as width times height at four bits a
    /// pixel, because that relationship is what identifies the table. A fixture that
    /// wrote an arbitrary size would let a locator that ignores the check pass.
    /// </para>
    /// </summary>
    private void WriteOverworldSprites()
    {
        const int frameBytes = OverworldWidth * OverworldHeight / 2;

        for (int index = 0; index < OverworldCount; index++)
        {
            if (index == DeadOverworldIndex)
            {
                WriteU32(OverworldTableOffset + index * 4, 0);
                continue;
            }

            int record = OverworldRecordsOffset + index * ObjectGraphicsInfo.RecordSizeBytes;
            int frameList = OverworldFrameListsOffset + index * OverworldFrameCount * 8;

            WriteU16(record, (ushort)(0xFFFF - index));                        // tile tag
            WriteU16(record + 2, (ushort)OverworldPaletteTagFor(index));       // palette tag
            WriteU16(record + 4, (ushort)OverworldPaletteTagFor(index));
            WriteU16(record + 6, frameBytes);
            WriteU16(record + 8, OverworldWidth);
            WriteU16(record + 10, OverworldHeight);
            _data[record + 12] = (byte)(index % 16);                           // packed flags
            _data[record + 13] = 1;                                            // tracks

            WriteU32(record + 16, Rom.BaseAddress + (uint)record);             // oam
            WriteU32(record + 20, 0);                                          // subsprites
            WriteU32(record + 24, Rom.BaseAddress + (uint)record);             // anims
            WriteU32(record + 28, Rom.BaseAddress + (uint)frameList);
            WriteU32(record + 32, 0);                                          // affine anims

            for (int frame = 0; frame < OverworldFrameCount; frame++)
            {
                int pixels = OverworldPixelsOffset + (index * OverworldFrameCount + frame) * frameBytes;
                byte colour = OverworldPixelFor(index, frame);

                // Flat fill, so a decoding test can assert an exact colour everywhere
                // and catch tile ordering, nibble order and stride in one assertion.
                byte packed = (byte)(colour | (colour << 4));
                for (int i = 0; i < frameBytes; i++) _data[pixels + i] = packed;

                WriteU32(frameList + frame * 8, Rom.BaseAddress + (uint)pixels);
                WriteU16(frameList + frame * 8 + 4, frameBytes);
            }

            // No terminator, and the next list starts immediately after this one. That
            // is how the cartridge lays them out, and it is why a reader cannot find
            // the end by looking for something that is not a frame — the next list is
            // frames too, of exactly the same size.

            WriteU32(OverworldTableOffset + index * 4, Rom.BaseAddress + (uint)record);
        }

        for (int i = 0; i < OverworldPaletteCount; i++)
        {
            int colours = OverworldPaletteDataOffset + i * GbaPalette.SizeBytes;
            byte[] palette = GbaPalette.ToBytes(BuildPalette(seed: 40 + i));

            palette.CopyTo(_data, colours);

            int entry = OverworldPaletteTableOffset + i * 8;
            WriteU32(entry, Rom.BaseAddress + (uint)colours);
            WriteU16(entry + 4, (ushort)(OverworldFirstPaletteTag + i));
            WriteU16(entry + 6, 0);
        }
    }

    private static byte[] EncodeTextAsCartridgeWould(string text, int fieldWidth)
    {
        var buffer = new byte[fieldWidth];

        int i = 0;
        foreach (char c in text)
        {
            if (i >= fieldWidth - 1) break;
            buffer[i++] = c == ' ' ? (byte)0x00 : EncodeCharAsCartridgeWould(c);
        }

        buffer[i] = GameText.Terminator;
        return buffer;
    }

    private static Rgba32[] BuildTilesetPalette()
    {
        var colors = new Rgba32[GbaPalette.ColorCount];

        for (int i = 0; i < colors.Length; i++)
        {
            // Map tiles are opaque, colour 0 included.
            colors[i] = new Rgba32(Expand5(i * 2), Expand5(31 - i), Expand5((i * 3) % 32), 255);
        }

        return colors;
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
            WritePaletteEntry(ShinyPaletteTableOffset + i * 8, shiny, ShinyTagBase + i);
        }

        for (int i = 0; i < DecoyPaletteEntryCount; i++)
        {
            WritePaletteEntry(
                DecoyPaletteTableOffset + i * 8,
                Rom.BaseAddress + (uint)FillerPaletteBlobOffset,
                i);
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
