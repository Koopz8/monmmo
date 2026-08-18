I'm building MonMMO, a from-scratch MMO whose data is extracted from my own Pokémon FireRed
cartridge. C# / .NET 8, xUnit, Raylib-cs client, SQLite server. Repo is at
`~/OneDrive/Desktop/pokemmo`, branch `main`, everything merged. Base is the tip of
`claude-254`, 2867 tests green.

Standing rules — do not break these:

* Never commit cartridge images or anything derived from them. Every player supplies their own
  file; extraction happens locally. This is the project's most important rule. Do not ask me to
  upload `firered.gba` to your container.
* Don't ship anything to speed things up. Users keep needing their own ROM.
* Every number is marked read (off the cartridge) or modelled (a decision), never conflated.
* Find things by what they look like, print what was found, hardcode nothing.
* Every new guardrail gets proven by deliberately breaking the code it guards — use
  `tools/break-guard.sh`, which refuses on a dirty tree. If breaking a rule fails no test, that
  guard needs a decoy fixture or needs removing.
* **The cartridge may be staged into the session.** Two sessions running it has been, with
  permission, over the desktop bridge from `~/OneDrive/Desktop/pokemmo/firered.gba`. It stayed
  out of the repository, out of every commit and out of the bundle. It turns one measurement
  per round trip into a dozen in one turn and it is how the actual answers get found — by
  disassembling forty bytes by hand. Ask first; do not assume.
* You have no credentials and can't push. Deliver work as git bundles
  (`git bundle create <file> <base>..main`), rehearsed from a clean clone at the base, sent
  with SendUserFile. I merge with `git merge incoming` — not `--ff-only`. **Bundles sent to
  chat do not reach my disk.** Write them straight into `~/OneDrive/Desktop/pokemmo/` with the
  device bridge as well, or I can't merge them. Don't run `git` through the bridge in that
  folder — it leaves a stale `index.lock` I have to delete by hand.

## The method that works here

When two rounds of reasoning haven't converged, stop inferring and print the bytes.

Milestone 174 took thirteen measurements. **Seven killed a prediction, and every one of those
seven was an instrument written that same turn.** 190 was the same shape again: two of its own
readings disagreed about where a `setvar` lived, and forty bytes of hexdump settled it in one
turn. Knowing about the pattern in advance has never helped once. What helps is building every
instrument able to come back empty, and believing it when it does.

Traps worth carrying:

1. **The answer is often in a part of the file the scan does not open**, and the output is
   byte-identical to a scan that looked and found nothing. **Before believing any "nothing in
   the world does X", check what the scan is enumerating.** `--in-the-image` exists for this.
2. **When two of your own readings disagree, the stricter one is not automatically right.**
   Ask which reading follows fewer edges before deciding which is more rigorous. In 190
   `--trace` printed `0x081655ED` and `--who-writes` printed `0x0816569A` for the same write,
   and neither was wrong: **`--trace`'s address column is the script that ran, not the site of
   the write.** Two hexdumps and a goto chain, not an argument.
3. **A count is not a ranking.** Rank by the thing you actually care about.
4. **A break that comes back green is a claim about the break as well as about the guard.**
   175 had one: `Array.Sort(bytes, (a, b) => 0)` looks like a no-op and isn't — introsort is
   unstable. Re-broken properly, the guard caught it.
5. **A fallback that names a cause is worse than one that says nothing.** "It ran to the end,
   so the setflag is on an ordinary branch it had no reason to take" was the `else` of a
   three-case switch. There was no branch; the run had lost to GIOVANNI. Two sessions.
6. **A filter that keeps output readable must never decide which question gets asked.** 175's
   climb skipped sites the map scan had opened "because --flags already answers those". The one
   site that mattered was opened, and --flags had not answered it.
7. **A misalignment INVENTS things as readily as it hides them, and the two are indistinguishable
   from outside.** Fixing `[0x6F]` took flags moved from 259 to 258 and the playthrough's own
   count from 286 to 284 — *down*. **Do not assume a fix will make a number go up.** A number
   moving the wrong way is not a regression until you have read why.
8. **A number printed with no denominator cannot come back empty.** "Nothing was handed over
   twice" and "nothing hands anything over" read identically until 190 printed both halves.
   195 is the same trap a third time: "5051 calls to 28 routines" was the fixpoint's own passes,
   and the number about the cartridge is 319 places.
9. **Before believing "X is wrong everywhere", print how many places ask X.** 196 fixed a key
   that had been wrong for nineteen milestones and it moved nothing at any lever setting: the
   only consumer in the repository is asked about ONE setter at three settings and NONE at the
   other three. The fault was real and the blast radius was nought, and only a denominator on
   the CONSUMER could say so. A count of how wrong something is is not a count of who cares.
10. **The widest agreement is usually the WRONG width — three times running now.** 200's `0x92` resumes on `0x00` at ALL
    NINE sites read two, three or four wide — and that is one agreement, not nine: every site is
    landing in the same run of zero bytes inside the same argument. `0x95` at 202 did it across
    THREE wrong widths at seven sites each; `0x43` at 203 did it with `0x0D` five times and then
    `0x80` five times — the two halves of `0x800D` read as opcodes. Each time the right answer
    was the width whose sites DISAGREED. In this cartridge's script stream a column of identical
    resume-bytes is evidence of a misalignment, not of a boundary. Count what the sites agree
    ON, not how many agree.
11. **A shape that matters somewhere does not matter everywhere.** 193 found that a scene played
   once per door wrecked the walking, because a walk ACCUMULATES. 194 predicted the same for
   every count the run keeps. Measured, it is six in five thousand — a counter accumulates
   nothing. The prediction was mine and reasonable and wrong, and only measuring said so.
12. **A number that is only ever copied is never wrong out loud.** The floor table above was
    stale in five of its six rows for thirteen milestones and nothing anybody wrote about it was
    false: every *difference* it is quoted for — `--surf` costs two, `--in-order` adds two and
    one and a party member — was still exactly right, because each milestone re-ran the pair it
    cared about and pasted the delta onto a base nobody re-ran. **A table maintained by deltas
    drifts and stays self-consistent.** The only thing that catches it is running the whole
    block, which is why the prompt says to start with `--play` — and 207 is the first session
    that read the output against the table instead of past it.

13. **A number that cannot depend on the lever is the one that catches a wrong label** (211).
    A three-bucket sort of the run's shut gates put 134 in "nothing in the file sets it" at the
    floor and 56 with the levers on. Whether anything in the file sets a flag is a property of
    the FILE; it cannot move with a lever, so the label was wrong — the run sets sixty-five
    flags no `setflag` names, because picking a thing up sets its hide flag in compiled code.
    **When a classification has a bucket that is about the cartridge rather than about the run,
    print it at two lever settings and check it does not move.** The fixed version reads 44 at
    all three.

## Where things are

Read `claude/milestone-215-one-writer-and-nobody-listening.md` first, then
`214`, `213`, `212`, `211`, `210`, `209`, `208`, `207`, `206`, `205`, `204`, `203`, `202`, `201`, `200`, `199`, `198`, `197`, `196`, `195`, `194`, `193`, `192`, `191`, `190`, `189`,
`188`, `187`, `186`, `185`, `184`, `183`, `182`, `181`, `180`, `179`, `178`, `177`, `176`.
**Twenty faults closed and every one was in this project, not on the cartridge.** A walk that
stopped at a conditional call; one byte with no width; three scans that rolled their own "every
script" list; a list ranked by a count instead of by what it costs; a party that could not gain
a level; a roadmap line that called a fix a cost; a continuation that carried flags and not
variables; a trainer marked fought before the fight; a reader that was never told who had been
beaten; a walker never told it could swim; two argument widths that were wrong rather than
missing (`0x1F`, `0x6F`); a map's arrival script running after every person on that map; and
**a beaten trainer resuming inside the fight's own script instead of the bytes after it**,
which skipped a `checkflag` at all eight gyms; and the floor table at the top of this file,
stale in five of six rows for thirteen milestones while every sentence written about it stayed
true (207).

`175-reading-the-file-not-the-world` is the instrument set (`--in-the-image`, `--climb`, the
reversal control). `184` adds `--who-writes`. `187`/`188` are the two wrong widths and
`--stops`. `189` is `--trace` and the ordering. `190` is `--fights` and the handover count. `191` is
`--who-knows` and the sea. `192` is the walk. `193` is the one that
retired both of 192's proposed designs by reading the bytes instead, and `194` is `--entries`
and the fault 193 shipped. `195` is places against times, and a prediction of 193's that turned
out to be wrong. `173-reading-the-other-arm` still has the best table of wrong turns.

**The pattern, thirteen times over: right at every step and quietly wrong at the end.** Nothing
in this project fails when it is wrong. Assume the number in front of you is distorted until an
instrument says which direction — and note that 190 moved the map count by **zero** at every
lever setting while moving flags at all six. A fix that changes no headline is not evidence it
was not a fix.

## The instruments

```
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --say-yes --boat --surf
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --flags
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --scripts
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --fights
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-knows
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --coins
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --entries
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --counters
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --in-the-image 0x003E,0x003F
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-writes 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-reads 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --say-yes --in-order --trace 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --stops 0xC0
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --script-map 6.2
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --routines
```

`--who-reads` is `--who-writes`'s mirror and is eleven milestones late: it finds every
`compare`, `comparevars` and copy-from that looks at a variable, with the reversed-image floor
beside it. **The source of a copy is a read and the destination is a write** — counting both
would make every write a read as well. Its aggregate ("650 in the save's band are written and
never read") is BELOW its own floor of 1070 and the instrument says so; only the per-variable
answers mean anything. `--in-the-image` scans all 16 MiB for the bytes that move a flag, says of every hit whether the
map scan ever decoded that byte, and climbs to whatever names it. `--who-writes` is its mirror
for variables — **and both of them answer about the IMAGE, down every arm of every branch.** A
run takes one arm. `--trace 0xNNNN` is the same question asked of the RUN: every write and
every read, in order, with what the variable held at the moment somebody looked — but **its
address column is the script that ran, not the site of the write.** `--stops 0xNN` prints every
stopped read of one command with the run-up and **where the read started**. `--fights` reads
**both** exits of every `trainerbattle` and sorts the fall-through into four shapes; it comes
back "nothing of this kind skips a guard" for six of the eight kinds, which is the answer it
has to be able to give. `--who-knows` asks the WHOLE FILE who knows a move — the obstacle scan
asks the maps, and the maps are 0.6% of it — and prints the reversed-image floor beside the
count, because 600 against 787 is noise and 7 against 0 is not. `--coins` reads the three commands that move a count, and derives the one number none of them
holds: five places read the count, compare it against a bound, branch and hand a quantity over,
and every bound plus its own gift is 10000. Four different pairs, one sum, and the same chain
hunt on the reversed image finds NOUGHT. `--entries` counts the scenes
this cartridge writes as several doors into one room, and separates them from the shared
routines that look identical — by the number each door says, which is different per door for a
scene and the same for a crowd.

## The floor, restated

`--play` alone is not a floor: below the floor on reach, above it on anything a hanging script
hands over. **Two levers are MODELLED — `--say-yes` and `--boat`.** `--surf` is now only an
override: the walk crosses water on its own when the party knows the move, which is READ.
`--in-order` is the one lever that makes it stricter. Say which every time.

**RE-MEASURED AT 207, all six rows, and five of them had drifted.** The map counts were right;
every flag count was wrong, four party sizes were wrong and one row had the wrong number of
passes. `--play --say-yes` had been carrying **milestone 193's** reading for thirteen milestones.
Every *difference* the table is quoted for survived — `--surf` still costs two, `--in-order`
still adds two and one and a party member — which is why nobody noticed. See `milestone-207`.
If you change anything the run touches, re-run these six and rewrite this block; do not apply a
delta to it.

```
--play                                      183 / 153 in 6, party of 6 at 52, 11 of 103 handed twice
                                            crossing water: nobody ever knew move 57 — a wall
--play --say-yes                            243 / 231 in 5, party of 4 at 67, 10 of 155 handed twice
--play --say-yes --in-order                 243 / 233 in 5, party of FIVE at 67, 0 of 152 handed twice
--play --say-yes --boat                     381 / 293 in 6, party of 4 at 77, 11 of 204
--play --say-yes --boat --in-order          381 / 294 in 6, party of FIVE at 77, 0 of 200
--play --say-yes --boat --surf --in-order   381 / 292 in 4, party of five at 75, 0 of 200
                                            <- --surf still COSTS two flags
```

**381 of 425 no longer needs `--surf`** — and it was 390 until 193 stopped the run playing each
scene once per door into it. Down is the honest direction there: the extra nine were reached by
walking people repeatedly out of their own doorways. The party learns move 57 on pass 3 and swims. The
starter arrives on the floor with `--in-order`, and with `--in-order` on **nothing in the game
is handed over twice**.

Shut doors at 381, counted by reason: **41 never reached the door, 1 arrived on an island, 1
somebody standing in the way** — MT. EMBER `1.103`, behind `0x0089`, which nothing in the world
sets. CERULEAN CAVE is closed: the run now reaches it, off the SAPPHIRE thread.

## Where the reading stands

```
2915 scripts on 425 maps, reaching 3836 blocks
227 of them do nothing but hand over; 22 scenes are one scene entered several ways
3783 read to a proper end, 53 stopped
729 trainerbattle sites on 104 maps; 27 carry a second exit, 10 of those skipped a guard
7 places in the file ask who knows a move and are jumped into; 0 in the reversal; 4 offer
322 flags gate something; 258 are moved by a script somewhere; 233 are the code boundary
9 people on or beside a door behind 5 flags — the wall list
21 people never arrive at all
11 of 425 maps have no way in at all
5 places guard a coin hand-over; every bound plus its own gift is 10000; 0 chains in the reversal
2 places sell coins for money at 20 each — READ; 3 price lists, 15 rows, all READ
the floor is asked for money in ONE place and it is the coin counter; 8 at --say-yes and above
the widest run sets 212 of the 322 gating flags — 110 gates it never opens; 201 at the floor
those 110 are 35 with no opener, 31 never run, 17 never picked up, 15 obstacles, 12 past the boundary
35 and 15 are the same at every lever setting, which is how a property of the FILE has to behave
62 gates no walk opens hold 240 people; 146 of them are CUT trees and ROCK SMASH rocks
3 scripts hold 27 gating flags and 158 objects: CUT, ROCK SMASH, STRENGTH
the 12 STRENGTH boulders are SEAFOAM and VICTORY ROAD, and their flags split THREE ways
766 places call 63 routines the widest run cannot answer; 187 have an answer nothing branches on
of 1055 branching sites in the file, nought takes 212 — and 0x188's one place comes to nothing
0x4059 has one writer and NO readers anywhere; 0x4055 has 21 readers against a floor of 0
2 of those gates hold NOBODY — 0x084A and 0x084B, the ferry, with no setter anywhere
```

## The next task, precisely

1. **The other five rows of the floor table have never been chased back.** 207 re-ran all six and
   five had drifted; only `--play --say-yes` was bisected, and it turned out to be milestone
   **193's** reading, moved at 198, 199 and 200 and copied forward through all three. Where the
   floor row went 150 → 153 is the same bisect and it has not been done. **This is a small job
   and it is first on the list because the number a session reads before anything else was wrong
   for thirteen milestones and every sentence written about it stayed true.**
2. **The money ceiling is MEASURED and unlevered — decide against the number, not the worry.** 201
   counted it: **8 places** ask the run for money at five of the six lever settings and **1 of
   them hands something over** — `16.0 0x0816F75F` wants 500 and gives `#129` at level 5
   anyway, which is the `#130` at 71 the party ends with. **The floor is clean**: 1 place asks,
   nothing comes of it, so the floor's party of six is entirely earned. Whether that deserves a
   `--pay` lever or a located payout table is a DECISION and it is deliberately not made.
3. **The six mixed routines, which are what is left of the routine ceiling.** 215 read
   `0x188`'s one place — `1.93` SECTION 52, after a trainerbattle — and the arm nought takes
   writes `0x4059`, which **nothing anywhere in the file reads**. So that half comes to nothing.
   What remains is the mixed bucket: 61 places at the widest setting, **44 of their 68 branches
   taken by nought**, across six routines. **`0x194`** is the big one — 747 sites, the most of
   anything, and nought takes 1 of its 18 branches. None has been read.
   `--who-reads` is new and is the cheapest way to finish any of them: it says whether whatever
   an arm writes is ever looked at.
   Also owed and cheap: **seven boulder flags with no setter anywhere** (whatever drops a
   boulder into a hole is not script), **`0x0805`** which the STRENGTH script sets and shares
   across all twelve boulders, and **`0x0053`** holding 31 people across the SILPH CO. floors
   with no setter — the doors are open (176, 181) and the people are still held, which are two
   different facts.
4. **Money, for real this time — and the prices are READ now.** Three drinks at 200/300/350 and
   a POKé DOLL at 1000, plus 208's ¥20 a coin and fifteen coin prices, all READ, all at counters
   the run reaches, against a purse of nought. `--money N` is the lever and it is MODELLED; **the
   payout table is still unlocated**, and that is the one number that would make the lever
   unnecessary. 197 filed the POKé DOLL as a reach problem and 198's rule change showed it is a
   money problem after all — the reverse of 197's own correction, and only the fix could tell.
5. **`Attempt.Ran` is fixed (196) and it moved nothing, for a reason worth carrying.** The key
   is `(map, address)` now and five breaks caught it. But the tally 196 added says the only
   consumer in the repository is asked about **one** setter at three lever settings and **zero**
   at the other three. `--flags` never looked at it at all — it takes only the ROM. Before the
   next "X is wrong everywhere", print how many places ask X.
6. **`--entries` reads only the scripts the map scan opens**, which is 0.6% of the file. The
   same sweep asked of the whole image is `--in-the-image`'s question and has never been asked
   of this shape.
7. **The 41 doors never reached** at 381 of 425, and `1.103` MT. EMBER behind `0x0089` — nothing
   in the world sets it, so it is the code boundary with an address on it. The RUBY is behind it
   (`1.102` person 1), and `32.0` person 3 wants the RUBY and the SAPPHIRE both. The SAPPHIRE
   half is closed (190); the RUBY half is not.
8. **The blocks that still stop.** Two entries turned out to be symptoms of a wrong width
   upstream rather than commands, so **check alignment before adopting a width**: `--stops
   0xNN` prints where each read started. The remaining named stops are `0xB3`, `0xCA`, `0xC3`,
   `0xC4`, `0x43`, `0x73`, `0xE6` — 17 of 24 have something behind them at every width that
   reads on. **`--derive`'s verdict is advisory — READ THE BYTES.**
9. **The four that no width reads on from** — `0x92`, `0x9B`, `0xD3`, `0x62`. Misreads, so those
   blocks are wrong earlier; finding where is the job that found `0x1F` and `0x6F`.
10. **The five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — and the ~28
   hand-rolled map walks left in `Program.cs`.

## Fixtures lie in one direction

Guards have come back green because **the fixture was more forgiving than the cartridge**:

1. A zero-filled image is a **NOP SLIDE** — every `0x00` is a valid no-op, so a drifting read
   walks sixty bytes to the target and the test passes at the wrong width.
2. Four sites in dead space all "share their run-up".
3. A yes/no with the reward unconditionally after it never tested the answer.
4. **A stand-in fixture guards the plumbing and not the thing** (189). Four guards on the script
   ordering ran against a lambda that handed its results over ready-made.
5. **A fixture built on the shape where the two readings agree cannot tell them apart** (190).
   `AfterTheRocketsTests` put *a line and an end* after a `trainerbattle` — which `--fights`
   now counts as 17 of the 19 sites of that kind, and which **both** readings treat the same.
   It guarded the wrong answer and passed for nine milestones. The gym shape — a `checkflag`
   and a branch — is the one that discriminates.

6. **A test that reads the instrument instead of the world** (193). The break removed the
   behaviour and left the counter alone, and the test asserted the counter. Rewritten so that
   *one step* and *two steps* are different answers, it caught it. The number a milestone adds
   and the thing that milestone changed are two different claims.

7. **Every fixture in the milestone using one of the thing** (194). 193's tests all used one
   map, so none of them could see that a script attached to nineteen Pokémon Centres is
   nineteen scenes. If the rule has a key, the fixture needs two of whatever the key is made
   of — two maps, two numbers, two addresses.

8. **The ordinary case, unasserted** (195). Every fixture covered the interesting halves and
   none of them said what happens in the common one — the same script on a later pass — so the
   break that conflated it with the rare one came back green.

9. **A break run against one test says nothing about which test caught it** (207). 206's break
   edited `MoveNoiseFloor` while the test watched `NoiseFloor` and came back green; the fix is
   not a second test, it is running **each break against both tests** and writing down the 2×2.
   A guard that goes red for somebody else's break is not a guard on the thing it is named for.
   207's matrix: break the move floor → the flag test stays green, the move tests go red; break
   the flag floor → the reverse. Six break runs, one red each time, and the greens are the
   result.

10. **A fixture where the thing being looked for sits somewhere the scan never reaches** (208).
   `B3 v; B4 g; end` looks like the test for "a read with no compare after it is not a guard".
   It is not: the hand-over lands at index ONE and the fall-through scan starts at three, so the
   fixture answers correctly for a reason that has nothing to do with the compare. A break that
   removed the compare check came back green against it. **Ask where in the fixture the thing
   you are asserting about actually is.**

11. **A fixture that fails the reader before it fails the rule** (208). The replacement for the
   above put filler where the branch was, so the block stopped decoding and failed the "reads as
   a script" filter first. It passed because the block was broken, not because the branch was
   missing. **The thing you blot out has to be replaced by something the same width that the
   reader still understands.**

12. **A fixture that violates two rules at once cannot test either** (213). "Every object behind
   the gate is asked about a move" and "they agree about whether they are removed" both guard
   one function. The mixed fixture broke both, so a break that weakened the first was caught by
   the second and the first stayed untested and green. **A fixture for rule A has to satisfy
   rule B.**

Check for these shapes directly rather than waiting for a break to find them. And the same nop
that makes a slide can make a width **undiscriminable**: the `0x6F` fixture separates four from
one and cannot separate four from three. That limitation is written into the fixture rather
than left to be discovered. **A test named for a discrimination it does not make is worse than
no test.**

## Known flaky

`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother` failed once on a loaded machine
(55s suite instead of 28s) and has passed every run since. Timing-dependent, so it is a guard
that can lie in both directions. If it is red, re-run before believing it.

## A note on guards

A break that comes back green is a claim about the break as much as the guard. One rule went
green **three times** at 189, each for a different reason. At 190 a break went green because
the rule being broken was a `Where` inside `Program.cs`, which no test can reach — it moved to
`Attempt.HandedOverTwice` and was caught on the second attempt. **That is the sixth time the
same structural fault has been fixed by moving a rule about the world out of the printer.** If
a break passes, suspect the fixture — or where the rule lives — before the code.

And run the break against **every** test that could plausibly catch it, not just the one it was
written for. 206's break was aimed at one of two near-identical functions while the test watched
the other, and nothing about a single green run says which. 207 writes the 2x2 down instead.

## Things already ruled out — don't re-chase these

* **What is between a shopkeeper and the floor.** `0x80`, read twice with a control each time —
  91.9% against 8.9% by what it stands beside, 22.5% against 0.3% by its own shape. Named on
  `MetatileBehaviour.Counter` with the evidence. The walk talks across exactly one of them.
  Closed; do not re-derive it.
* **The clerks being walled in.** They are not — every one has 2 or 3 walkable squares beside
  them, on the clerk's side of the counter. Walkable is not reachable, and the collision byte
  answers a different question from the distance.
* **`--flags` using the playthrough.** It does not. `case "--flags"` reaches
  `WriteFlagGates(rom)` — one parameter, and it is the ROM. Nothing in it has ever seen an
  `Attempt`. Diffing `--flags` across a playthrough change is diffing a scan that did not look.
* **Anything else in the run keyed on an address alone.** The grep 194 asked for is done and
  `Ran` was the only one. `moved`, `gone`, `spokenTo`, `handovers`, `walkedFrom`, `refused` and
  all five of 195's counted sets already carry the map; `alreadyRun` is a per-map local, the
  same key by scope. Do not re-run this grep.
* **The blockers' own scripts.** All four contain **no conditional of any kind**. What moves
  them is on the object's record, not in the script.
* **`0x4001` is scratch, not a story counter.** 285 scripts write it. The scratch pads stop at
  `0x4010` — a cliff in the write-count distribution, measured, with the cut MODELLED.
* **The playthrough's reader.** It is `HowAScriptRuns` in RomExtract now, with a fixture. Do
  not put script-running logic back into `Program.cs`.
* **SILPH CO. and SAFFRON.** Closed in the reading (176) and now in the playthrough (181). The
  doors are open. Do not reopen it.
* **`--say-yes` "costing" party members.** It never did — the six were four duplicate gifts.
* **Levels.** The party grows now — 3 at level 75 with the sea open.
* **`0x73`.** It stops runs and it is worth nothing — the block ends two bytes later at every
  one of its four sites. Both 4 and 5 parse and adopting either opens nothing.
* **`0x009D` and the nineteen who never arrive.** Closed.
* **SILPH CO., `0x003E`, `0x003F`.** Closed. The `setflag` is not behind a branch at all.
* **`0x1F` and `0x6F`.** Both settled off columns of five sites. Do not re-litigate them; do
  look for a third.
* **The run running triggers whose condition is unmet.** Answered by `--in-order`, a lever
  rather than a decision. `1.57`'s trigger fires at `0x4060 == 0`, so its condition is met.
* **The order scripts run in on a map.** Arrival scripts, then triggers, then people — the
  cartridge's own order. Do not "tidy" those three loops.
* **The trigger north of PALLET TOWN "re-opening the story every pass".** It does that only
  without `--in-order`. Traced in order, `0x4055` is written four times, **all on pass one**,
  and `0x4050`/`0x4052`/`0x4054`/`0x4057`/`0x4060`/`0x4031` are each written once. The lever
  closed it.
* **The run's reach being the last pass rather than the union.** Measured at 190: the union of
  every pass equals the final reach at all five lever settings, even though the boat-and-surf
  runs dip from 376 to 374 between passes one and two. The dip heals. Do not re-chase it.
* **Where a beaten trainer resumes.** Settled at 190 off `--fights`: the bytes after the
  command, and the fight's own script runs once, on the pass that wins it. Kind 1 off eight of
  eight; **kind 2's two sites were read by hand at 191** — `1.114` person 6 (the SAPPHIRE) and
  `14.2` person 5 — and both have a guard in the fall-through. Closed.
* **Which move crosses water.** Move 57, read twice: the move table's own name, and the only
  block in the image that offers to cross water (`0x081A6AD6`, jumped into, on no map, saying
  *"The water is dyed a deep blue… Would you like to SURF?"*). `--surf` is now the override
  only. Do not put the lever back in front of the fact.
* **CERULEAN CAVE.** Reached. The SAPPHIRE comes off `1.114` person 6's fight and `32.0` person
  3 takes it.
* **A cutscene's displacement.** The steps travel now and the walk stops at the first square
  nobody can stand on. **Nobody is off the map at any lever setting**, and the export check says
  every person the cartridge places stands on the map it places them on. Do not put the sum back.
* **What stops a scene running twice.** Nothing does, and nothing needs to: it is not a flag,
  it is that four entry stubs are one scene, so the same `applymovement` command runs four
  times. Each command applies once **per map** — nineteen Centres share one nurse and that is
  nineteen scenes, which 193 got wrong and 194 fixed. 193 closed this and retired both designs 192
  had costed for it — including changing the settle test, which is now **sound**: the state
  stops moving when the loop stops, and the final walk agrees with the last pass exactly.
* **The run's reach being the last pass rather than the union.** Measured at 190, 191 and 192:
  the union equals the final reach at every lever setting, even where a pass dips. And since 193
  the final walk agrees with the last pass's own walk too. Closed.
* **CERULEAN CAVE is not a SAFFRON problem.** `0x005C`, set by `32.0` ONE ISLAND person 3.
* **The fifteen gating flags 0x0011-0x001F.** They hold CUT trees and ROCK SMASH rocks, one per
  map across thirty-odd maps, running two scripts between them (`0x081BDF13`, `0x081BE00C`).
  Their flags are set by the routine that removes the object. Do not file them as the boundary.
* **The twelve STRENGTH boulders** (`0x0040`-`0x0045`, `0x0048`-`0x004B`, `0x0058`, `0x0059`).
  SEAFOAM ISLANDS `1.83`-`1.86` and VICTORY ROAD `1.40`/`1.41`, one script (`0x081BE11D`), which
  removes nothing and sets the shared `0x0805`. **Their flags split three ways** — two set by
  arrival scripts on ROUTE 20 and ROUTE 23, two set out of sight, seven set by nothing. Do not
  treat them as one kind.
* **What sets a flag with no setflag anywhere in the file.** Picking the thing up. The routine
  that hands something over sets the object's own hide flag in compiled code, and only 7 of the
  575 objects carrying a hide flag have a script that sets it — it is written in `Autoplayer`
  beside `what.TakenAway` and was rediscovered the hard way at 211. Do not file those flags as
  the boundary.
* **What the coin commands count and how much fits.** Ten thousand, off five sites and four
  distinct (bound, gift) pairs, with nought chains in the reversal (208). And what a coin costs:
  ¥20, off two sites that ask, give and pay. Do not re-derive either; do look for the payout
  table, which is a different question.
* **A shuffle control on the ceiling sums.** Written at 208, proved unfalsifiable by arithmetic
  and deleted. If every bound plus its own gift is S and no two sites share a pair, a bound
  crossed with somebody else's gift can never be S. Do not write it again.
* **`special 0x0187`.** It heads all three obstacle scripts, its answer is compared against 2
  and only 2 at all 376 of its sites, and `0x081A7AE0` — the arm answer 2 takes — is two bytes,
  `release; end`. Answer 2 means "do nothing". The run answers nought and therefore behaves as
  it would for any answer but one. Closed (214); do not re-derive it.
* **Whether a routine's silence matters is about the BRANCH, not the compared value.**
  `compare 0x800D, 1 ; if LESS` is taken by nought and does not test nought — `0x084` is tested
  against 1 and 2 and nought takes nineteen of its twenty-one branches. `Profile.BranchesTakenByZero`
  evaluates the condition and is the number to use. Settled at 214; do not classify on values.
* **A plain `call` is a barrier in the answer scan.** `special ; call ; compare` reads the
  CALL's answer, not the special's — SEVEN ISLAND's `0x0028` was credited with `0x005D`'s reply
  for as long as the scan existed. Added at 214, 42 of 1097 attributions lost. Do not remove it.
* **The drink, the vending machine, CELADON DEPT, the ferry tickets, the badge-count routine** —
  all dead, see `claude/the-drink-and-the-boat.md`.

## Open, and honestly owed

* Held items are a sixth way a thing changes hands and `Everywhere` does not know.
* The playthrough never runs signs. `3.57 sign (9,43)` asks for a LEMONADE and takes it away.
* Eleven maps have no way in at all, five of them Sevii isles with no dock in the export.
* A way in reports only the shortest chain, so an upper-bound edge can hide a real one.
* `Bag.PocketCapacity` was counted across the whole bag — fixed at 190 tests ago, but it shipped.
* The purse is modelled and the payout table has never been located. The PRICES are read (208).
* `0x8009` picks which arm of the coin counter runs, 22 scripts write it and NONE on 10.14, so
  the ¥10000 arm is chosen past the code boundary. What the variable is for is not claimed.
* **`MapScripts` — the fifth list — has no test coverage at all.**
* A guard nothing can fail: `SpecialContracts.ComparedAfter`. Decoy or deletion.
* Co-op step 4: a parcel still goes to one person.
* `StoryClosure` deliberately still has no bag, so `--can-it-be-finished` is the no-bag control.
* No milestone docs for `StoryClosure`, `Autoplayer` or `SpecialContracts`.
* Sound is paused: 31 unconfirmed song headers and battle music still open.
* 201 of the floor's 396 "could not answer" places have an answer nothing branches on. They are
  reported and then explained away; they could be taken out of the ceiling line.
* Nothing in this project follows a `call` to attribute an answer. Since 214 the scan stops
  there, so `special 0x005D` inside `0x081A4EAF` is credited with nothing either — the reading
  is now honestly silent where it used to be confidently wrong. Following one level in would be
  a real instrument.
* The 5 flags that look moved and are not — `--flags` prints the count, not the list.
* The raw whole-file sweep is noise: 3762 sites against 3675 in the reversal. Only the
  jumped-into subset is above the floor. Do not quote the raw number as a finding.

Start with `--play`, `--flags` and `--who-knows`. I'll paste the output.
