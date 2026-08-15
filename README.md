# MonMMO

A multiplayer game, written from scratch in C# on .NET 8, whose client reads every
asset it draws from a cartridge the player already owns.

There is an authoritative server, a raylib client, a battle engine, a world of 425
maps, and a cartridge extractor that locates every table it needs by shape rather
than by remembered address. The server has never seen a cartridge and never will.

```
src/Core            the shared model: world, battle, save, protocol, cosmetics
src/RomExtract      cartridge reading. Client-only, and enforced by a test
src/Server          the authoritative server. Core only
src/Client          the game: window, input, drawing, screens
src/Tools/RomDump   the extractor's command line, and every instrument built for it
tests/              1361 tests, no cartridge required
tools/rig/          the headless play rig — Xvfb, two clients, screenshots
```

---

## The rule this repository is built around

**No cartridge data ever lives here.** Not in the repository, not in a build, not on
the server. Every player supplies their own file, extraction runs locally on their
machine, and the results stay on their disk. `.gitignore` blocks every ROM extension
and every extractor output at any depth — `world.dat`, `rules.dat`, `players.db` and
`client.json` included.

The layout enforces the rest. `src/Server` references `Core` and nothing else, and a
test asserts `RomExtract` does not appear in the server assembly's referenced
assemblies. A comment would not have caught a stray `using`; that does.

What the server learns about the world, it learns from a file an operator exports
from their own image: map dimensions, one byte of walkability and one of behaviour
per square, the people and doors on each map, and the numbers a rule needs. No
graphics, no text, no audio, and no cartridge addresses — those stay on the
cartridge, because an address is only meaningful next to the image it came from.

---

## What works

**The world.** 425 maps. From a fresh character, 246 are reachable on foot, by water
and through every obstacle a party can shift; the boat opens 152 more, for 398. The
rest is 30 separate pieces that nothing in the cartridge sails to, and the startup
log says so in those words rather than leaving it to be discovered.

**The story, as far as it goes.** Warps, map edges, ledges, doors, triggers, arrival
scripts, people who hand things over, people who take things, obstacles that need a
move, shops, healing, the box, hidden items, and the flags that decide which line
somebody is on. The client runs the cartridge's own scripts and reports what they
did; the server checks every claim against what that person is allowed to do.

**Battles.** A one-on-one engine with the damage, accuracy, status, priority, trap,
recharge, multi-hit, drain, recoil and one-hit-knockout rules read out of the
cartridge's own tables. Trainer fights are a run of one-on-one battles with the dice
carried over. Experience, levels, learnsets, evolution and catching all work.

**Multiplayer, in three verbs.** Seeing each other, trading, and duelling. Players
walk through each other on purpose — a game where standing still is a wall is a game
where one person can shut a door for everybody.

**Cosmetics.** Twelve slots, a wardrobe screen, and a catalogue that is the one place
in this project where invented numbers are allowed.

---

## Running it

Build once, then export a world and a rules file from your own image:

```bash
dotnet build -c Release

dotnet run --project src/Tools/RomDump -- your.gba --export-world world.dat
dotnet run --project src/Tools/RomDump -- your.gba --export-rules rules.dat
```

Then a server and a client:

```bash
dotnet run --project src/Server -- --world world.dat --rules rules.dat
dotnet run --project src/Client
```

The server prints what it knows before it listens — how many maps a new character can
walk to, what stands in the way, which trainers have parties, which maps have no door
leading in. That report is the project's main instrument, and most of what is written
below was found by reading it.

An `--operator <name>` argument gives one account a console (`/` in the client) for
`/tp`, `/give`, `/flag`, `/reach`, `/hidden`, `/docks`, `/sail`, `/duel` and the rest.

---

## How the extractor finds things

The obvious implementation hardcodes `0x08254784` for the base-stat table. That
breaks badly: feed it a different revision and every offset shifts, so it reads
plausible garbage and reports success.

This one searches for **structure**, and a candidate must produce a long run of
well-formed entries before it is accepted:

- **Species names** anchor on the encoded name of species 1, then confirm by decoding
  150 following records. Real names decode cleanly; misaligned data produces `?`
  almost immediately.
- **Base stats** anchor on the ten-byte stat signature of species 1, then range-check
  200 following records.
- **Sprite and palette tables** are runs of `{pointer, size, tag}` whose tags increase
  by one and whose pointers land inside the cartridge. Each run reads its tag base
  from its own first entry, because the shiny table offsets every tag by 500.
- **Map banks** are two levels of indirection ending in a header whose layout pointer
  must itself resolve to a valid layout. That shape is specific enough that nothing
  else in the image matches it at length, and it yields the game's own `(bank, map)`
  numbering.
- **Region names** are ranked by how many entries read like places, because an image
  holds several long runs of text pointers and the location table is the one whose
  *contents* are place names.

Cartridge identification comes from the header rather than a hash allowlist, because
the header is self-describing. SHA-1 is still computed and compared against the
hashes the pret decompilation projects publish.

---

## The method

Everything in this project follows one rule: **derive, don't remember.** No constant
is written from recollection of another game. Something is located by its shape in
the image, and the evidence is printed beside it.

Worked examples, all of them in the code as comments:

- A script command's width is settled by what the read resumes on. `warp` is seven
  bytes because at seven it names a real map at a square inside that map at 19 of 19
  sites, where the next best byte manages five per cent.
- Behaviour byte `0x6A` is a staircase, not a storage machine, because every square
  carrying it is at the top or bottom of a flight and none of them is in a corner of
  a healing centre.
- The ferry's destination table was read without reading the routine that uses it:
  sixteen scripts write a number into an argument slot and then hand the screen to
  the same routine as the last thing they ever do, and no two of them write the same
  number. The numbers check themselves — 0 is VERMILION CITY and 1 through 7 are ONE
  ISLAND through SEVEN ISLAND, in order, from ten scripts that have never met.

Three categories, and they are marked in the source:

- **Read** — it came out of the cartridge.
- **Modelled** — a number that lives in the game's own code, stated in the open.
- **Invented** — allowed in `PokeMmo.Core.Cosmetics` and nowhere else.

And a fourth thing, written down rather than guessed at: **what cannot be derived
stays silent on purpose.** The cartridge's font has defeated four separate methods.
That is in the notes, not in a magic number.

---

## Testing without a cartridge

No copyrighted file is needed to run the suite. `SyntheticRom` builds a fake
cartridge that satisfies the same structural invariants as a real one, and the tests
assert the extractor recovers the exact bytes that were written. Everything else —
the world, the battle engine, the protocol, the server's rules — is tested against
synthetic maps and rules with no image involved at all.

```bash
dotnet test
```

Two negative tests matter more than the positive ones: a cartridge-sized buffer of
random noise must yield **no** tables, and a planted anchor with nothing valid after
it must be **rejected**. Those are what stop the scanner from being confidently
wrong.

A handful of tests are guardrails rather than tests of behaviour, and each exists
because something got past a review:

- every message on the wire round-trips, and every message kind is in the sample list
- every message kind is named by **both** sides of the client/server split — a
  message with a handler and no sender reads, to anybody looking at that side, as if
  the feature were there
- every battle event names at most one side, and names it first, so a duel can be
  told to both players in their own terms
- the server assembly does not reference the extractor

---

## Playing it headlessly

`tools/rig/` runs the real client against the real server with no screen: Xvfb,
software GL, `xdotool` for keys and ImageMagick for screenshots. `twoclients.sh`
brings up two signed-in players, which is how trading and duelling were checked.

Every milestone in this project ends by playing the thing that was just built and
looking at the screenshot. Three times, something that looked like a bug turned out
to be the program being right and quiet about it; twice, something that looked fine
was wrong and only the screenshot said so.

---

## Field notes

The lesson that keeps recurring, in the words it was learned in:

- **A fixture built from the same assumptions as the code under test only ever proves
  the code agrees with itself.** Three extractor bugs survived a green suite because
  the synthetic cartridge encoded the same guesses the scanner did.
- **The behaviour test beats the shape test.** Seventeen tests once rested on a
  behaviour byte that meant something else entirely, and all of them passed.
- **A refusal that does not name its cause costs more than the line it saves.** A
  blocked step that answered with silence desynchronised the client for two
  milestones and read, the whole time, as a missing person.
- **A rule enforced on one side of the split needs its counterpart on the other.**
  Learned three times before it became a heading.
- **A thing being absent from the data you have looked at is not the same as the thing
  being absent.** "179 maps have nothing leading in" was counted correctly and
  described wrongly for a dozen milestones, because the count came from map records
  and the sentence said "world".

---

## Next

Player-versus-player is in, switching included. What is left, in order: the battle
engine's silent half (127 move-effect groups the engine steps over without saying
so), and art — twelve cosmetic slots with nothing drawn in them, and a shop to buy
from.

The ferry's ticket is derived and carried, and deliberately not enforced: three of
this cartridge's 2681 map scripts mention a pass and all three are the sailor asking
for one. Nothing gives one, no shop sells one, and no script sets either flag. A gate
whose key exists is a gate; a gate whose key is nowhere in the world is a wall, so the
server enforces it only when the world can supply a key — and says which at startup.
