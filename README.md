# Milestone 0 — cartridge extractor

Reads a player-supplied Generation III cartridge and turns it into engine-native
data: base stats, species names, and decoded 64x64 sprites as PNGs.

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
| `tests/RomExtract.Tests` | Test suite | 74 tests, no cartridge required |

When the server project lands, the one rule to hold is that its dependency graph
must not contain `RomExtract`. That way the legal posture is guaranteed by the build
rather than by remembering.

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
```

Options: `--shiny`, `--back`, `--no-sprites`, `--tile-order row|column`.

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
- **Sprite tables** — every mon pic entry is `{pointer, 0x800, species}`, so a real
  table is a long run of entries whose tags count up from zero and whose pointers
  land inside the cartridge.
- **Palette tables** — same idea with `{pointer, species, 0}`.

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
hash. Two bugs surfaced that the synthetic fixture had not caught, because the
fixture encoded assumptions rather than the cartridge's actual layout:

- **Name records are zero-filled past the terminator**, not padded with more
  terminator bytes. The full-width search key therefore matched nothing and every
  species came out unnamed. The anchor is now the characters plus the terminator and
  stops there.
- **Tables sit back-to-back.** After completing a run the scanner resumed four bytes
  too far, stepping over the next table's first entry — so the shiny palette table
  was missed and the scanner latched onto unrelated data further along.

The fixture now models both properties, and both failures are pinned by regression
tests. The lesson worth keeping: a fixture built from the same assumptions as the
code under test only ever proves the code agrees with itself.

## The one thing that needs a real cartridge to confirm

Sprite **tile ordering**. A 64x64 sprite is 64 tiles in a linear run, and both
row-major and column-major arrangements produce a correctly-sized image — so a wrong
choice yields a scrambled picture rather than an error the tests could catch. The
default is row-major.

If the first sprite you decode comes out shuffled into 8-pixel blocks, re-run with
`--tile-order column`. That is a one-word change, and once confirmed against a real
cartridge the default can be fixed in `TileDecoder`.

---

## Next

Milestone 1: render a town from the cartridge's map data and walk around it
single-player. That needs map header, tileset, and block-data extraction — the same
signature-scanning approach extends to those tables.
