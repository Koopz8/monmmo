# Milestone 190: the guard on the other side of the fight

A `trainerbattle` has two exits. This project has only ever read one of them, and the one it
read is the one that belongs to winning.

---

## What was wrong

The runner, meeting a `trainerbattle` for a trainer it had already beaten, jumped to the last
pointer inside the command that reads like a script. That was adopted at milestone 181 off the
ROCKET HIDEOUT, where the `clearflag` that puts the LIFT KEY on the floor is inside one of
those pointers and sixty-six maps sat behind it. It was right about the LIFT KEY.

It was wrong about what it costs. That pointer is the **battle's** continuation — the badge,
the flags, the thing the victory was for. The cartridge runs it when the fight is won, once.
Jumping there whenever the trainer is beaten ran the whole victory again on every pass, and
skipped the bytes immediately after the command for ever.

Those bytes are where all eight gym leaders keep this:

```
0816A5A0  5C 01 9E 01 00 00 [08190CD4] [08190E4F] [0816A5C5]   trainerbattle 1, BROCK
0816A5B2  2B 54 02                                             checkflag 0x0254
0816A5B5  06 00 [0816A5F3]                                     if clear -> give the TM
0816A5BB  0F 00 [0819110F] 09 04 6C 02                         otherwise, just talk
```

`0x0254` is set by the arm that hands the TM over, eight commands later. It is the cartridge's
own "have you already taken it". Under the jump reading nothing ever arrived at
`0x0816A5B2`, so the question was never asked, and BROCK handed TM39 over again every pass
for as long as the run went on. So did the other seven.

---

## The instrument: `--fights`

```
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --fights
```

Every `trainerbattle` the map scan opens, with **both** exits read: the byte after the command,
and the last pointer inside it that reads like a script. It sorts the fall-through into four
shapes — a guard, just a line, nothing at all, not commands — and says how many places in the
whole image name that address.

```
729 trainerbattle(s) the map scan opens, across 104 map(s)

kind 0 — 385 site(s), 0 of them with a script pointer to jump to
kind 1 —   8 site(s), 8 with a jump — the after reads as A GUARD at 8 of 8, named by nothing else
           8 of them SKIP A GUARD
kind 2 —  19 site(s), 19 with a jump — 17 just a line, 2 A GUARD
           2 of them SKIP A GUARD
kind 3 —  49 site(s), 0 with a jump      kind 4 — 28, 0      kind 5 — 208, 0
kind 7 —  26 site(s), 0 with a jump      kind 9 —  6, 0
```

Only kinds 1 and 2 carry a second exit at all, so for 683 of the 729 sites the question does
not arise — a beaten trainer already fell through. Of the 27 that do, **10 skip a guard**: the
fall-through is a conditional, the jump never arrives at it, and nothing else in the file holds
that address as a pointer. Falling through is the only reading under which those bytes mean
anything, and a cartridge does not write a guard where nothing can reach it.

The eight are the eight gyms — `5.1`, `6.2`, `7.5`, `9.6`, `10.16`, `11.3`, `12.0`, `14.3`.

---

## The change

* A beaten trainer **carries on with the bytes after the command**.
* The fight's own script is **handed back** — `ScriptRun.AfterTheFight`, `PlayedScript.AfterTheFight`
  — because only whoever resolves the battle knows whether it was won.
* The walk runs it **on the pass that wins it**, once, through the same queue as everything else.
  A second copy of the folding code is how the two would drift apart, so `Reachable(...)` now
  seeds a `Queue<Runnable>` and the victory is enqueued into it.

The LIFT KEY still gets cleared. It is behind *winning* rather than behind *having won at some
point in the past*, which is what the cartridge means and one pass earlier than before.

---

## What moved

| run | maps | flags | passes | party | handed over twice |
|---|---|---|---|---|---|
| `--play` | 183 (was 183) | **150** (was 149) | 6 | 6 at 52 | 11 of 103 |
| `--play --say-yes` | 215 | **195** (was 193) | 7 | 3 at 59 | 10 of 128 |
| `--play --say-yes --in-order` | 215 | **196** (was 193) | **5** (was 6) | **4 at 60** (was 59) | **0 of 125** |
| `--play --say-yes --boat` | 306 | 223 | 5 | 3 at 65 | 11 of 144 |
| `--play --say-yes --boat --surf` | 390 | **285** (was 284) | **4** (was 5) | 3 at 75 | 11 of 202 |
| `--play --say-yes --boat --surf --in-order` | 390 | **286** (was 284) | **4** (was 5) | 4 at 75 | **0 of 198** |

The map count did not move at any lever setting. That was expected and it is not evidence the
fix was not a fix — three in a row did the same before 178 moved one.

What did move: flags up by one to three everywhere, the fixpoint settling one to two passes
sooner because the victory's flags land on the pass that won rather than the pass after, and
`--say-yes --in-order` gaining a level.

---

## And a number the run has never printed

```
    125 place(s) handed something over; 0 of them did it on more than one pass
```

The party has said this for a while — *a second copy of something already in it* — and the bag
never has. An item off the floor is kept from refilling by the flag on the object's own record,
which milestone 138 read; an item somebody hands over is kept from refilling by a guard inside
their own script, which nothing read until now. So the eight TMs were arriving once per pass
underneath a run whose output said nothing unusual at all.

With the levers on it is **0 of 125**, and **0 of 198** with the sea open. Without `--in-order`
it is 10 or 11 — the SILPH CO. gift and nine arrival scripts and triggers the fixpoint takes
out of order, which is the lever's known cost and now has a number on it for the first time.

The denominator is printed on purpose. *None of them twice* and *nothing hands anything over*
printed the same as each other before this.

---

## The fixture that agreed with the wrong answer

`AfterTheRocketsTests` guarded the old reading and passed the whole time. Its bytes after the
command are **a line and an end** — which `--fights` now counts as 17 of the 19 sites of that
kind, and which *both readings agree about*. It was built on the harmless shape and the
conclusion was carried to the guarded one.

This is the fourth shape of forgiving fixture this project has found, after the NOP slide, the
shared run-up in dead space, and the unconditional reward behind a yes/no. **A fixture built on
the shape where the two readings agree cannot tell them apart, and it will pass under either.**

`TheGuardAfterTheFightTests` is the same question asked on the gym shape, and the test named
for the discrimination makes it: beaten with the flag clear hands the reward over; beaten with
the flag set does not. `AfterTheRocketsTests` keeps the half it does settle — where the fight's
own script is reported, and that it is not run before the fight.

## Guards broken on purpose

Ten breaks, all caught, `tools/break-guard.sh` each time:

| break | caught by |
|---|---|
| put the jump back | `ABeatenTrainerReachesTheGuardAndHandsTheRewardOver`, `TheVictoryItselfIsNotRunAgain…` |
| fall through, but `checkflag` always reports clear | `AndDoesNotHandItOverASecondTime` |
| winning no longer runs the victory | `WinningRunsWhatTheVictoryWasFor` |
| the victory runs whether or not the fight was won | `LosingRunsNothing` |
| the fight's script is not reported | `TheScriptTheFightLeadsToIsHandedBack`, `BeforeTheFightNeitherHappens` |
| a guard is sorted as a line | `AConditionalReadsAsAGuard`, `BothExitsAreReported` |
| a continuation that rejoins is not noticed | `AContinuationThatComesBackIsNotSkippingAnything` |
| bytes with no width sorted as an end | `BytesWithNoWidthReadAsNotCommands` |
| only the first pass a thing changes hands is recorded | `SomethingHandedOverOnEveryPassIsSaidSo` |
| a place that handed something over once counts as a repeat | `SomethingHandedOverOnceIsCountedAndNotRepeated` |

The last one came back **green** first time round, and it was green for the reason this project
keeps finding: the rule was a `Where` inside `Program.cs`, which no test can reach. It moved to
`Attempt.HandedOverTwice` and was caught on the second attempt. That is the sixth time the same
structural fault has been fixed by moving a rule about the world out of the printer.

---

## What this does not settle

* **The two kind-2 sites** — `1.114 person 6` and `14.2 person 5` — also skip a guard and were
  changed by the same edit. Nobody has read their bytes by hand. They are the obvious next
  forty bytes to disassemble.
* **Whether kind 2 should fall through at all.** The eight gym leaders settle kind 1 off a
  column of eight. Kind 2 is settled by the same edit and a column of two out of nineteen, and
  the other seventeen are the shape that cannot discriminate. If a future reading says kind 2
  behaves differently, this is where it is written down.
* **The 683 sites with no second exit** were never affected and still are not.
* The run still re-runs everything it can reach on every pass. What it no longer does is *take*
  anything twice with `--in-order` on. The fixpoint is still a fixpoint.
