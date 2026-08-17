I'm building MonMMO, a from-scratch MMO whose data is extracted from my own Pokémon FireRed
cartridge. C# / .NET 8, xUnit, Raylib-cs client, SQLite server. Repo is at
`~/OneDrive/Desktop/pokemmo`, branch `main`, everything merged. Base is the tip of
`claude-226`, 2744 tests green.

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

## Where things are

Read `claude/milestone-190-the-guard-on-the-other-side-of-the-fight.md` first, then `189`,
`188`, `187`, `186`, `185`, `184`, `183`, `182`, `181`, `180`, `179`, `178`, `177`, `176`.
**Thirteen faults closed and every one was in this project, not on the cartridge.** A walk that
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
`--stops`. `189` is `--trace` and the ordering. `190` is `--fights` and the handover count.
`173-reading-the-other-arm` still has the best table of wrong turns.

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
**both** exits of every `trainerbattle` and sorts the fall-through into four shapes; it is the
instrument that settled 190 and it comes back "nothing of this kind skips a guard" for six of
the eight kinds, which is the answer it has to be able to give.

## The floor, restated

`--play` alone is not a floor: below the floor on reach, above it on anything a hanging script
hands over. Three levers are MODELLED — `--say-yes`, `--boat`, `--surf` — so anything with them
on is a ceiling. `--in-order` is the one lever that makes it stricter. Say which every time.

```
--play                                      183 / 150, party of 6 at 52, 11 of 103 handed twice
--play --say-yes                            215 / 195, party of 3 at 59, 10 of 128 handed twice
--play --say-yes --in-order                 215 / 196, party of FOUR at 60, 0 of 125
--play --say-yes --boat                     306 / 223
--play --say-yes --boat --surf              390 / 285, party of 3 at 75, 11 of 202
--play --say-yes --boat --surf --in-order   390 / 286, party of FOUR at 75, 0 of 198
```

**The starter arrives on the FLOOR and not on the ceiling** (`#3`, with `--in-order`). And
**with `--in-order` on, nothing in the game is handed over twice** — 0 of 125 places, 0 of 198
with the sea open. Without it, ten or eleven places hand the same thing over on every pass:
the SILPH CO. gift and nine arrival scripts and triggers. That is the lever's cost, and it has
a number on it for the first time.

Shut doors at 390, counted by reason: **39 never reached the door, 1 arrived on an island, zero
somebody standing in the way.**

## Where the reading stands

```
2915 scripts on 425 maps, reaching 3836 blocks
3783 read to a proper end, 53 stopped
729 trainerbattle sites on 104 maps; 27 carry a second exit, 10 of those skipped a guard
322 flags gate something; 258 are moved by a script somewhere; 233 are the code boundary
9 people on or beside a door behind 5 flags — the wall list
21 people never arrive at all
11 of 425 maps have no way in at all
```

## The next task, precisely

1. **The two kind-2 sites `--fights` names** — `1.114 person 6` at `0x08163F93` and
   `14.2 person 5` at `0x0816EDA2`. Both skip a guard and both were changed by 190's edit, and
   **nobody has read their bytes by hand.** Kind 1 was settled off a column of eight; kind 2 is
   riding on a column of two out of nineteen. `--script-map 1.114` and forty bytes of hexdump.
   This is the smallest well-shaped job on the list.
2. **Which move crosses water, READ rather than assumed.** Still the largest single number:
   `--surf` is a lever standing in for a fact, and reading it turns 390 of 425 from a ceiling
   into a floor. The sea it stands in is 1245 squares across 35 maps.
3. **The 39 doors never reached** at 390 of 425. `--play` counts shut doors by reason.
   And `--trace` the other counters — `0x4050`, `0x4052`, `0x4057`, `0x4060` were all traced in
   order at 190 and each is written exactly once, so **the re-run problem is closed for the
   story counters.** What has not been looked at is anything below `0x4010`.
4. **The 53 blocks that still stop.** Two entries on this list turned out to be symptoms of a
   wrong width upstream rather than commands, so **check alignment before adopting a width**:
   `--stops 0xNN` prints where each read started. The remaining named stops are `0xB3`, `0xCA`,
   `0xC3`, `0xC4`, `0x43`, `0x73`, and `0xE6` — 17 of 24 have something behind them at every
   width that reads on.

   **`--derive`'s verdict is advisory — READ THE BYTES.** It is wrong about `0xD0` and it threw
   out both plausible widths for `0x3F`; neither has been tuned away, and tuning a scorer until
   it agrees with a reading is decoration rather than evidence.
5. **The four that no width reads on from** — `0x92`, `0x9B`, `0xD3`, `0x62`. Misreads, so those
   blocks are wrong earlier; finding where is the job that found `0x1F` and `0x6F`.
6. **The five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — and the ~28
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
  command, and the fight's own script runs once, on the pass that wins it. Kind 1 is settled
  off eight of eight. Kind 2 is the one still owed a reading — see task 1.
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

Start with `--play`, `--flags` and `--fights`. I'll paste the output.
