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
src/Tools/Crowd     a crowd of real clients, for measuring what the server does at scale
tests/              1564 tests, no cartridge required
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
cartridge's own tables. Moves have their own PP, run out, and stay spent across
fights and across restarts; a creature with nothing left struggles, using the
cartridge's own STRUGGLE record, and a counter puts every use back. Wild creatures
come out carrying what their own species record says they might, and beating one leaves
behind the effort its own record says it is worth — kept across fights, centres and
restarts. Trainer fights are a run of one-on-one battles with the dice carried over.
Experience, levels, learnsets, evolution and catching all work.

**Multiplayer, in three verbs.** Seeing each other, trading, and duelling. Players
walk through each other on purpose — a game where standing still is a wall is a game
where one person can shut a door for everybody.

**Cosmetics.** Twelve slots, a wardrobe with a mirror in it, and a catalogue that is
the one place in this project where invented numbers are allowed. What somebody is
wearing is drawn on them, facing the way they face, and a cape hangs behind its
wearer from the front and covers them from behind. The art is placed against the
figure's own measured outline rather than against the frame it sits in — the one read
number in the whole of it, and the reason a cap is on a head rather than above it.

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
- The move record's target byte was read by counting who is in each group: the 67 moves
  sharing one value are exactly the moves whose whole effect is on the creature using
  them, and no move outside that group is. A byte whose members are exactly one idea
  means that idea.
- The six effort yields are two bytes of a species record, and both the packing and the
  order come off a census. Of the 27 byte pairs a 28-byte record has, exactly one reads
  as six two-bit fields totalling one to three for every species this cartridge fields,
  and the 25 where it does not are 252 to 276 — one unbroken run, the block the game
  keeps and never uses. Then: a species yielding in one slice only should have the stat
  that slice means as its highest, and the diagonal is 94, 100, 97, 100, 100, 97 per cent
  with nothing off it above 20.
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

## Measuring it under a crowd

`src/Tools/Crowd` opens hundreds of real sockets, registers or logs in, and walks —
the same frames the raylib client sends, minus the drawing. It reports how long
joining took, how long a step took to be answered, and what one player costs everybody
else in messages a second.

```bash
dotnet run --project src/Tools/Crowd -c Release -- --players 100 --seconds 45
```

The first run of it moved the whole roadmap. The guess was that the database or the
JSON would be the wall. Measured on two cores, at a hundred players:

```
  joining      24560 / 42188 / 44404 ms   (median, 95th, worst)   7 never got in
  a step took      2.7 / 380.2 / 883.0 ms
```

The wall was **the door**: one password check cost 997 ms and 64 MiB, unbounded, so a
hundred people arriving were a hundred simultaneous Argon2 hashes fighting for two
cores. Bounding the door to one hash per spare core and moving the cost parameters to
OWASP's published Argon2id baseline — 19 MiB, two passes, still memory-hard, and old
hashes still verify under the parameters stored inside them — gave:

```
  joining       7782 / 13853 / 14753 ms   (median, 95th, worst)   100 of 100 got in
  a step took      2.0 /  14.7 / 589.4 ms
```

The next wall was in the same report — every step told to everybody, by a dispatch
loop that walked all connections and asked the world where each one was, taking the
server's one global lock each time. A hundred people stepping once a second on one map
was ten thousand lock acquisitions a second, contending with the world's own clock.

Fixed by an index of who is standing where, kept outside the lock and written only
where somebody joins, leaves or changes map; and by giving every connection its own
queue and pump, so one socket that has stopped reading delays nobody but itself:

```
  joining       5815 /  9889 / 10362 ms   (median, 95th, worst)
  a step took      1.5 /   5.8 /  370.4 ms
```

At 400 players on the same two cores: everybody in, 18,000 messages a second
delivered, a step answered in 3.8 ms at the median. The connections that cannot keep
up are disconnected on purpose rather than quietly missing messages.

Sight is now a circle rather than a map: the radius comes off the client's own
viewport — 960 pixels at three times life size is 20 squares across, so 11 with a
square of margin — and a step is told only to the people who can see the square it
ends on. Walking out of somebody's circle sends the same message a disconnect does,
so a client needs no new case for it.

That bounds the cost by distance instead of by map, and the measurement is honest
about what it does not do: **a crowd standing in one place still costs what a crowd
standing in one place costs.** 141 of the 425 maps are bigger than the 23-square sight
box on at least one side, and 74 on both — on those the circle bites, and on a
starting room 12 squares wide it cannot. What helps there is not putting everybody in
the same place — and that was tried, and measured, and it is not enough. Arrivals are
now placed beside whoever is already standing there rather than on top of them, a ring
at a time outward, which is right in itself: a hundred people arriving in one bedroom
were one square deep in each other. But a hundred people spread over thirteen squares
are still all inside an eleven-square circle, and the like-for-like measurement says
so — 120 messages a second per player before, 129 after.

**A heap spread thinly is still a heap.** The only thing that changes that number is a
second copy of the place — so there is one. Past forty people, the next arrivals go
into another copy, and the copies never see each other: different crowds, different
townsfolk, the same ground. A key is a place and a copy, and the first copy is spelled
exactly as every map id in every world file and every save already is.

Same measurement, same map, same hundred players:

```
one copy:     12,887 messages a second, 129 per player
three copies:  4,880 messages a second,  49 per player
```

The alternatives were capping how many people are drawn or shrinking the circle, and
both leave a crowd standing there that the player cannot see and can walk into. A copy
has no crowd in it that anybody is being lied to about. What it costs is honest too:
every copy walks its own townsfolk, and two people who want to be together have to be
put in the same one.

That last rule is written. Walking through a door keeps your copy number when the
copy on the other side has room, so two people going through together arrive together;
and `/with <name>` moves you into the copy somebody else is in, on the map you are
already standing on. It never carries anybody across the world — that is a different
feature, and one that would make every locked door optional — and a full copy does not
refuse, because somebody who asked to be with a friend would rather stand in a copy of
forty-one than be told no.

The third measurement is the disk. A save happens on anything a player does that is
not walking, at most once a second each, and it rewrites the whole character. At a
hundred players doing something every two seconds that was **21 ms a save, 458 at
worst** — and a thousand players at that rate would be five hundred saves a second,
ten times more writing than those numbers allow.

Half of them were not needed. `SavedCharacter` is a record holding lists, so two
snapshots of somebody who had not moved a muscle compared unequal — the same trap
`SavedMon` had already closed on itself, one type up. With value equality and a
last-written copy per connection, 2370 things done became 1254 saves. `synchronous =
NORMAL` under the write-ahead log took the average from 21 ms to 16. The server now
prints what it is costing every half minute:

```
= 100 online on 2 maps, door 100 in (5554 ms average wait), 1385 saves (16 ms average, 454 worst)
```

And then the save moved out of the player's way entirely. It used to happen inside
the loop that reads that player's messages, so the disk was in the path of their
input; now a character is handed to a writer that runs behind everybody, holding the
*latest state per account* rather than a list of states — two changes to one character
before either is written are one write, with the newer. A disconnect still writes by
hand and tells the writer to forget whatever it was holding, because that copy is
older.

```
before:  1254 saves (21 ms average, 458 worst),  a step 3.3 / 19.5 / 102.1 ms
after:   1258 saves ( 1 ms average, 156 worst),  a step 2.5 /  8.3 /  36.4 ms
```

A save costs a sixteenth of what it did, because it no longer fights the connection
loops for the same lock and the same core. What is left is structural: a save writes
everything about a character every time, and it should write what changed.

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

Player-versus-player is in, switching included, and the battle engine now says when
it does nothing: 220 of 354 moves have an effect it can carry out, a fight counts
what it stepped over, and the server prints that count. What is left is the rest of
that list — 119 effect groups, all of them two moves wide or fewer now — and a shop
to buy cosmetics from, now that there is something to see when they are worn.

The ferry's ticket is derived and carried, and deliberately not enforced: three of
this cartridge's 2681 map scripts mention a pass and all three are the sailor asking
for one. Nothing gives one, no shop sells one, and no script sets either flag. A gate
whose key exists is a gate; a gate whose key is nowhere in the world is a wall, so the
server enforces it only when the world can supply a key — and says which at startup.
