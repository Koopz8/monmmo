# Cartridge extractor

Reads a player-supplied Generation III cartridge and turns it into engine-native
data: base stats, species names, decoded 64x64 sprites, and rendered maps.

This is the first milestone of the client. It exists to retire the riskiest unknown
in the whole project — *can we actually read the player's file?* — before any effort
goes into netcode, battle logic, or persistence.

---

## The rule this repository is built around

**No cartridge data ever lives here.** Not in the repo, not in the installer, not on
the server. The player supplies their own file, extraction runs locally on their
machine, and the results stay on their disk. `.gitignore` blocks every ROM extension
and every extractor output directory as a first line of defence.

The project layout enforces the rest:

| Project | Role | Notes |
|---|---|---|
| `src/Core` | Shared game model | Referenced by **both** the client and, later, the server |
| `src/RomExtract` | Cartridge reading | **Client-only.** The server must never reference this |
| `src/Tools/RomDump` | CLI harness | Development tool for inspecting a cartridge |
| `src/Client` | The game client | Window, input, drawing. No engine, no editor |
| `src/Server` | Authoritative server | **Core only.** Never references `RomExtract` |
| `tests/RomExtract.Tests` | Test suite | 270 tests, no cartridge required |

The server does not read cartridges. It learns the world from a **collision-only**
file an operator exports from their own image — map ids, names, dimensions and one
byte of walkability per square. No graphics, no text, no audio.

That rule is enforced rather than remembered: a test asserts `RomExtract` does not
appear in the server assembly's referenced assemblies. A comment would not have
caught a stray `using`; this does.

---

## Running it

```bash
dotnet build
dotnet run --project src/Tools/RomDump -- /path/to/your.gba --out ./out --species 1,4,7
```

Output:

```
out/
  tables.json      where every data table was found, and how
  species.json     the full base-stat table with decoded names
  sprites/001.png  decoded 64x64 sprites, transparent background
  maps.json        every map, with its bank.map address, name and dimensions
  maps/03-00_PALLET_TOWN.png   rendered maps
```

Options: `--shiny`, `--back`, `--no-sprites`, `--diagnose`, `--tile-order row|column`,
`--list-maps`, `--map <list>`, `--map-name <text>`, `--tileset-split firered|emerald`.

To see what maps the cartridge holds, and render one:

```bash
dotnet run --project src/Tools/RomDump -- your.gba --out ./out --no-sprites --list-maps
dotnet run --project src/Tools/RomDump -- your.gba --out ./out --no-sprites --map-name pallet
```

Maps are addressed the way the game addresses them, as `bank.map`, and named from the
region map table — so `--map 3.0` and `--map-name pallet` both work, and rendered
files come out as `03-00_PALLET_TOWN.png`. `--map all` renders everything.

---

## How it finds things

The obvious implementation hardcodes addresses like `0x08254784` for the base-stat
table. That approach breaks badly: feed it a different revision or region and every
offset shifts, so it reads plausible-looking garbage and reports success.

This extractor searches for **structure** instead:

- **Species names** — anchors on the encoded name of species 1, then confirms by
  decoding 150 following records. Real names decode cleanly; misaligned data
  produces `?` almost immediately.
- **Base stats** — anchors on the ten-byte stat signature of species 1, then
  range-checks 200 following records (type ids ≤ 17, growth rate ≤ 5, egg groups ≤ 15).
- **Sprite tables** — every mon pic entry is `{pointer, 0x800, tag}`, so a real table
  is a long run of entries whose tags increase by one and whose pointers land inside
  the cartridge.
- **Palette tables** — same idea with `{pointer, tag, 0}`. The tag base is read from
  each run's own first entry rather than assumed to be zero, because the shiny table
  offsets every tag by a constant.
- **Map layouts** — a layout record is two small positive dimensions followed by
  pointers that must land in the cartridge, with block data that must fit inside it.
- **Map banks** — two levels of indirection: a table of pointers, each to an array of
  pointers, each to a map header whose own layout pointer must resolve to a valid
  layout. That shape is specific enough that nothing else in the image matches it at
  length, and it yields the game's own `(bank, map)` numbering rather than an index
  into a table whose boundaries had to be guessed.
- **Region names** — both `{x, y, width, height, name}` records and bare arrays of
  text pointers are scanned, then ranked by how many entries read like places. An
  image holds several long runs of text pointers; the location table is the one whose
  *contents* are place names, not the longest or the first.

A candidate must produce at least 100 consecutive well-formed entries to be accepted.
The result: the extractor either finds tables that genuinely satisfy the format's
invariants, or it fails loudly. New revisions tend to work without a code change, and
the located addresses are written to `tables.json` so they can be recorded.

Cartridge identification is driven by the header (game code at `0xAC`, revision at
`0xBC`) rather than a hash allowlist, because the header is self-describing. SHA-1 is
still computed and checked against the hashes published by the pret decompilation
projects; a match means you are looking at exactly the image those projects document.

---

## Testing without a cartridge

No copyrighted file is needed to test any of this. `SyntheticRom` builds a fake 2 MiB
cartridge that satisfies the same structural invariants as a real one — header, name
table, stat table, both pic tables, both palette tables, and LZ77-compressed graphics
at known offsets. The tests then assert the extractor recovers the exact bytes that
were written.

That covers the full pipeline: LZ77 round trips (including overlapping back-references
and every malformed-input rejection path), 4bpp nibble order, both tile orderings,
BGR555 expansion, PNG chunk structure and CRCs, table location, and the CLI itself
end to end.

```bash
dotnet test
```

Two negative tests matter more than the positive ones: a cartridge-sized buffer of
random noise must yield **no** tables, and a planted anchor with nothing valid after
it must be **rejected**. Those are what stop the scanner from being confidently wrong.

---

## Field notes

Run against a real FireRed (US) rev 0 image whose SHA-1 matched pret's published
hash. Three bugs surfaced that the synthetic fixture had not caught, because the
fixture encoded assumptions rather than the cartridge's actual layout:

- **Name records are zero-filled past the terminator**, not padded with more
  terminator bytes. The full-width search key therefore matched nothing and every
  species came out unnamed. The anchor is now the characters plus the terminator and
  stops there.
- **Tables sit back-to-back.** After completing a run the scanner resumed four bytes
  too far, stepping over the next table's first entry — so the shiny palette table
  was missed and the scanner latched onto unrelated data further along.
- **Shiny palette entries are tagged `species + 500`**, not by bare species index.
  Requiring tags to count from zero made the scanner walk past the table while it sat
  directly after the normal one, and latch onto the trainer palette table instead.
  Runs now read their tag base from their own first entry, and the normal and shiny
  tables are told apart by that base rather than by which one comes second.

The fixture now models all three properties — including a decoy zero-tagged palette
table standing in for the trainer one — and every failure is pinned by a regression
test. The lesson worth keeping: a fixture built from the same assumptions as the code
under test only ever proves the code agrees with itself.

**Sprite tile ordering is confirmed row-major** against the same image. The
`--tile-order` flag stays for other cartridges, but the default is correct.

## Diagnosing an unfamiliar cartridge

If the scan reports a table with an unexpected entry count, or misses one entirely:

```bash
dotnet run --project src/Tools/RomDump -- your.gba --out ./out --no-sprites --diagnose
```

That drops the run threshold to 16, lists every candidate run of both table shapes
with its address range, length and tag base, and dumps the raw 8-byte records
following each located table as `{pointer, halfword, halfword}`. All three bugs above
were identified from that output rather than guessed at.

---

## Maps

A map square is a **metatile**: a 2x2 grid of 8x8 tiles, drawn twice — once for the
bottom layer, then again for the top layer with colour 0 left transparent so terrain
shows through. Each tile reference carries its own palette index and horizontal and
vertical flip flags.

Two tilesets are in play at once, primary and secondary, and tile, metatile and
palette slots form a single shared index space split between them. Those split points
are per-game constants, so they live in `TilesetSplit` and can be swapped with
`--tileset-split` rather than edited — a wrong split produces a recognisably wrong
picture rather than an error, and being able to flip it makes that a one-word
experiment instead of a code change.

The synthetic cartridge carries a complete map: a tileset whose tiles are flat
colour, one metatile per colour, a block grid, a layout record and a pointer table.
Flat colour is deliberate — every square renders as one solid 16x16 block, so the
tests assert exact pixel colours and catch tile ordering, palette selection, layer
compositing and block indexing all at once.

---

## Next

A Godot 4 client that renders an extracted map and lets you walk around it, then the
authoritative server underneath it.
