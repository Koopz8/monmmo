I'm building MonMMO, a from-scratch MMO whose data is extracted from my own Pokémon FireRed
cartridge. C# / .NET 8, xUnit, Raylib-cs client, SQLite server. Repo is at
`~/OneDrive/Desktop/pokemmo`, branch `main`, everything merged. Base is the tip of
`claude-236`, 2791 tests green.

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
10. **The widest agreement can be the least evidence.** 200's `0x92` resumes on `0x00` at ALL
    NINE sites read two, three or four wide — and that is one agreement, not nine: every site is
    landing in the same run of zero bytes inside the same argument. A nop slide is what a false
    column looks like from inside the cartridge, not just inside a fixture. Count what the sites
    agree ON, not how many agree.
11. **A shape that matters somewhere does not matter everywhere.** 193 found that a scene played
   once per door wrecked the walking, because a walk ACCUMULATES. 194 predicted the same for
   every count the run keeps. Measured, it is six in five thousand — a counter accumulates
   nothing. The prediction was mine and reasonable and wrong, and only measuring said so.

## Where things are

Read `claude/milestone-200-the-money-commands-and-the-thing-behind-them.md` first, then `199`, `198`, `197`, `196`, `195`, `194`, `193`, `192`, `191`, `190`, `189`,
`188`, `187`, `186`, `185`, `184`, `183`, `182`, `181`, `180`, `179`, `178`, `177`, `176`.
**Nineteen faults closed and every one was in this project, not on the cartridge.** A walk that
stopped at a conditional call; one byte with no width; three scans that rolled their own "every
script" list; a list ranked by a count instead of by what it costs; a party that could not gain
a level; a roadmap line that called a fix a cost; a continuation that carried flags and not
variables; a trainer marked fought before the fight; a reader that was never told who had been
beaten; a walker never told it could swim; two argument widths that were wrong rather than
missing (`0x1F`, `0x6F`); a map's arrival script running after every person on that map; and
**a beaten trainer resuming inside the fight's own script instead of the bytes after it**,
which skipped a `checkflag` at all eight gyms.

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
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --entries
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --counters
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --in-the-image 0x003E,0x003F
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-writes 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --say-yes --in-order --trace 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --stops 0xC0
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --script-map 6.2
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --routines
```

`--in-the-image` scans all 16 MiB for the bytes that move a flag, says of every hit whether the
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
count, because 600 against 787 is noise and 7 against 0 is not. `--entries` counts the scenes
this cartridge writes as several doors into one room, and separates them from the shared
routines that look identical — by the number each door says, which is different per door for a
scene and the same for a crowd.

## The floor, restated

`--play` alone is not a floor: below the floor on reach, above it on anything a hanging script
hands over. **Two levers are MODELLED — `--say-yes` and `--boat`.** `--surf` is now only an
override: the walk crosses water on its own when the party knows the move, which is READ.
`--in-order` is the one lever that makes it stricter. Say which every time.

```
--play                                      183 / 150 in 6, party of 6 at 52, 11 of 103 handed twice
                                            crossing water: nobody ever knew move 57 — a wall
--play --say-yes                            243 / 225 in 6, party of 3 at 67
--play --say-yes --in-order                 243 / 227 in 5, party of FOUR at 67, 0 of 150 handed twice
--play --say-yes --boat                     381 / 287 in 6, party of 3 at 77
--play --say-yes --boat --in-order          381 / 288 in 6, party of FOUR at 77, 0 of 198
--play --say-yes --boat --surf --in-order   381 / 286 in 4  <- --surf now COSTS two flags
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
```

## The next task, precisely

1. **The money ceiling has no lever, and it is now worth a party member.** 200 read `0x92` and
   `0x91` — the commands that ask about money and take it, nine sites each. The run steps
   cleanly OVER them without answering, takes the arm where the thing is handed over, and comes
   away with a fifth party member (`#130` at 71) while its purse is nought and it is being
   refused four things at a counter. Both facts are printed and they contradict each other.
   Nine places ask and nine take; this run answers none of the eighteen. `--say-yes` and
   `--boat` are named, printed and counted. This one is not, and until it is, "5 in the party"
   means "5, one of which was not paid for". **That is the top of this list.**
2. **`0x95` at `0x0816A43E` and `0xC2` at `0x0816CDB6`.** The next two in the queue, both
   exposed by 200. The pairs keep pairing: `0x95` sits immediately after `0x91` at four of its
   nine sites and after `0x94` at the GAME CORNER; `0xC2` sits immediately after `0xB5` at all
   three of its. Remaining: `0x95`, `0x9B`, `0x73`, `0xCA`, `0x43`, `0xC4`, `0xC3`, `0xC2`.
   **`0xE6` is the wall SIX fixtures now lean on** — whoever gives it a width rebuilds all six,
   and two tests fail the moment they forget.
3. **The GAME CORNER is readable now and nobody has looked at it.** `92 <u32> <u8>` checks money
   against 10000 and 1000, `B4` hands over 500 and 50 of something, `91` takes the money, `B3`
   hands `0x4001` to the compare after it. That is a coin exchange and NONE of it is claimed —
   it is what the next read is looking at.
4. **Money, for real this time.** Three drinks at 200/300/350 and a POKé DOLL at 1000, all READ,
   all at counters the run now reaches, against a purse of nought. `--money N` is the lever and
   it is MODELLED; the payout table has never been located. 197 filed the POKé DOLL as a reach
   problem and 198's rule change showed it is a money problem after all — the reverse of 197's
   own correction, and only the fix could tell.
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
* **The drink, the vending machine, CELADON DEPT, the ferry tickets, the badge-count routine** —
  all dead, see `claude/the-drink-and-the-boat.md`.

## Open, and honestly owed

* Held items are a sixth way a thing changes hands and `Everywhere` does not know.
* The playthrough never runs signs. `3.57 sign (9,43)` asks for a LEMONADE and takes it away.
* Eleven maps have no way in at all, five of them Sevii isles with no dock in the export.
* A way in reports only the shortest chain, so an upper-bound edge can hide a real one.
* `Bag.PocketCapacity` was counted across the whole bag — fixed at 190 tests ago, but it shipped.
* Money is modelled. The payout table has never been located.
* **`MapScripts` — the fifth list — has no test coverage at all.**
* A guard nothing can fail: `SpecialContracts.ComparedAfter`. Decoy or deletion.
* Co-op step 4: a parcel still goes to one person.
* `StoryClosure` deliberately still has no bag, so `--can-it-be-finished` is the no-bag control.
* No milestone docs for `StoryClosure`, `Autoplayer` or `SpecialContracts`.
* Sound is paused: 31 unconfirmed song headers and battle music still open.
* The 5 flags that look moved and are not — `--flags` prints the count, not the list.
* The raw whole-file sweep is noise: 3762 sites against 3675 in the reversal. Only the
  jumped-into subset is above the floor. Do not quote the raw number as a finding.

Start with `--play`, `--flags` and `--who-knows`. I'll paste the output.
