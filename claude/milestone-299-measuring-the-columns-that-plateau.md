# Milestone 299: measuring the columns that plateau

298 swept the forward window and reported that it plateaus at three. Every column in that table
does. **The column it did not print does not**, and it is the number the other forward reading
exists to produce.

---

## Sweeping a window means sweeping what it decides

`SpecialContracts` has its own forward window and its own barrier rule, and 298 left it standing.
Swept:

```
      forward   across a barrier   routines   no clean compare   ONLY across   branched on
            1                  0          0                  0             0            42
            2                 17          9                 17             7            46
            3                 68         19                 68            16            48
            4                146         25                 79            17            48   <- quoted
            6                231         37                130            24            48
            8                390         38                131            24            48
           12                445         38                131            24            48
           24                446         39                132            25            48
           96                454         39                140            25            48
         none                454         39                140            25            48
```

**Only BRANCHED ON is flat.** 298 measured routines, places and selectors — all of which plateau —
and the honest does-not-know column, which is what `--routines` prints as *"N site(s) across M
routine(s) branch on the answer PAST something that may have answered instead"*, runs **148 at four
to 621 at ninety-six** under the old rule. Every number in that sentence was a property of the
constant.

> **148 / 27 / 81 / 19 is now 454 / 39 / 140 / 25.**

## What replaced the distance

**A compare belongs to the LAST answerer before it** — which is 295's rule for arguments, read in
the other direction. Past the FIRST answerer a compare is this reading's does-not-know bucket; past
the SECOND it belongs to a call two removes away and is not evidence about this one at all.

That rule alone is worth **621 -> 454**; the rest of the difference was the window. It is read off
the script and needs no number chosen here, which is what 294 asked for and 295 supplied for the
other half.

## The cross-check is the shape of what was cut

A wider window finding more compares proves nothing — it would find more in the reversed image too.
What makes it a reading is **what shape the new ones have**. A script asking
`compare 1 ; if ; compare 2 ; if ; compare 3 ; if` is six commands long, so a window of four chops
every such chain and leaves the first two values. What should appear when the window goes is
**truncated runs filling in**, and that is a prediction the change could have failed:

```
      0x078   was [1x1,2x1]           now [1x1,2x1,3x1,4x1]           <- a run from one upwards, now
      0x07A   was [1x1,2x1]           now [1x1,2x1,3x1,4x1]           <- a run from one upwards, now
      0x0B4   was [1x10,4x10,7x7]     now [1x10,4x10,5x10,7x7]
      0x0B6   was [1x2,2x4]           now [1x2,2x4,3x2]               <- a run from one upwards, now
      0x0EC   was [1x1,3x1]           now [1x1,2x1,3x1]               <- a run from one upwards, now
      0x189   was [0x1,1x1]           now [0x1,1x1,2x1,3x1,4x1,5x1,6x1]
```

Six routines gain a value; **four become a run from one upwards where NONE was**, and `0x0EC`'s
missing 2 fills its own gap. *"N routines are compared against a run from one upwards"* — a shape
this project reads as a routine that COUNTS something — goes **0 -> 4**.

Asked with `SpecialContract.LooksLikeACount` rather than a fourth private copy of it, which is what
these three milestones have been about. The first version of that block rolled its own predicate
and got 5 and 4 where the project's own gives 4 and 0.

## And the third reading had to move with it

`WhoTheCompareBelongsTo` reaches `SpecialContracts.Window` for its own forward walk, so for one
commit `--routines` printed **140** sites with no clean compare in one line and **79** in the
section that sorts them. It takes the same setting now, and the sort is **97 / 38 / 5**.

**The 38 did not move.** Those are the sites where the thing in the way is
`copyvar 0x8012, 0x8013 ; return` — 219's finding, a call that cannot have answered anything — and
widening the walk added only sites where somebody else DID answer. A wider window finding more
contamination and no more exoneration is the direction it had to move in, and it is a control the
change could have failed.

`--routines`' own line goes from *"148 sites have a compare past something, 81 with nothing else —
38 come back, 40 were somebody else's, 3 not said"* to **454 across, 140 with nothing else — 38
come back, 97 somebody else's, 5 not said**.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the forward walk takes the old distance again | 1 | **0**, then **1** (+ the known flake) |
| it stops at the FIRST answerer | 1 | **8** |
| it never stops at an answerer | 1 | **1** |
| **CONTROL:** the second-answerer test written as `>= 2` | **0** | **0** |

**Another dead default, one milestone after the last one.** `ComparedAfter`'s `forward` default was
unreachable because `WhatIsComparedAfter` always passes one, so the break edited a line that decides
nothing. That is 298's own green break in the other arm. Deleted, re-aimed at the public default,
and it kills.

**Stopping at the first answerer killed eight where one was predicted**, and the prediction was
wrong about the blast radius rather than about the fixture: the `beyond` bucket is what every
barrier test in that class is about, so removing it takes them all.

The re-aimed break also killed `ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother` — the
known flaky one. The suite ran **147 seconds** in that run against ~30 idle, which is well past the
120-second budget 289 set. 289's rule holds: read the NAMES in a break's output, not the number.

## What is left

* **`DaycareLocator` is the last forward window at four, and it is worth nought**: 936 of 936
  places agree with `SpecialCalls`, so its cruder barrier list (only a call, where `SpecialCalls`
  has eight commands) costs nothing on this cartridge.
* **`WhatStoodInTheWay` has no adjacency check**, so it can name a thing in the way that is not in
  the same run. Unmeasured.
* **`BattleMusicLocator.Window` and `Ferries.Nearby`** are the same 4 in another domain, unswept.
* **A `call` between a value and the call it is credited to** — 13 of 244 (298).
* **`All`'s threading is still unguarded** (294, 296, 297, 298, 299).
* **The nineteen routines that were branched on ONLY across a barrier are twenty-five**, and the
  list has changed. `--through-a-call` is what says whether the thing in the way answered, and it
  has not been re-run against the new list.
