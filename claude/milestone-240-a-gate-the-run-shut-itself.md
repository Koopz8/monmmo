# Milestone 240: a gate the run shut itself

239 put the first thing in this playthrough that can take something back, wrote down why a state
has to be compared by its CONTENTS and not by its size, and left the test three lines above it
made of six counts.

```csharp
if (flags.Count == flagsWere && moves.Count == movesWere && party.Count == partyWas
    && bag.DistinctItems == carriedWas && gone.Count == goneWere
    && moved.Count == movedWere)
{
    stopped = StoppedBecause.NothingMoreOpened;
```

A pass that clears one flag and sets another matches all six. **That is trap 17 happening inside
the fix for trap 17** — the rule written into `WhereItHasBeen`'s own documentation, violated by
the line it was written next to.

---

## And the run has been taking flags back all along

The other half of the same blind spot: `flags` is a live set, so what a run REPORTS is the state
of whichever pass the loop stopped on. That was the same thing as "every flag it ever set" for as
long as nothing could clear one, and 239 ended that without anything noticing.

```
                                            EVER ON     STOPPED WITH    TOOK BACK
  --play                                      164           160             4
  --play --say-yes                            240           234             6
  --play --say-yes --in-order                 240           236             4
  --play --say-yes --boat                     305           295            10
  --play --say-yes --boat --in-order          305           296             9
  --play --say-yes --boat --surf --in-order   301           295             6
```

At the floor the four are `0x002E`, `0x003F`, `0x009E`, `0x00AE`, and **all four gate something**.

## Three of them were on before the first frame

```
  0x002E: 1 set(s), 1 clear(s), holds 1 object(s) — 3.2 p5
    last  pass 1  6.2   0x0816A5C5  set 0x002E
    last  pass 2  3.2   0x08165D8E  CLEARED 0x002E
  0x003F: 0 set(s), 1 clear(s), holds 7 object(s) — 1.47 p1, 3.10 p9, p10, p11 — ON BEFORE THE FIRST FRAME
    last  pass 3  1.57  0x08161E94  CLEARED 0x003F
  0x009E: 0 set(s), 1 clear(s), holds 2 object(s) — 10.8 p1, p2 — ON BEFORE THE FIRST FRAME
    last  pass 2  10.16 0x0816D0A0  CLEARED 0x009E
  0x00AE: 0 set(s), 1 clear(s), holds 2 object(s) — 14.6 p5, p6 — ON BEFORE THE FIRST FRAME
    last  pass 4  14.3  0x0816EE0A  CLEARED 0x00AE
```

Three are among the **49 flags a new game sets before the first frame**, no script in the run ever
sets them, and one script each turns them off. `1.57`'s is the readable one — the tail of a
trainer script:

```
  0x161EF3  16 60 40 01 00   setvar     0x4060, 1
  0x161EF8  29 3E 00         setflag    0x003E
  0x161EFB  2A 3F 00         clearflag  0x003F
  0x161EFE  6B               faceplayer
  0x161EFF  02               end
```

**A pair.** `0x003E` on, `0x003F` off, adjacent numbers, and seven objects across three maps stand
up when the second one goes. A flag in this world hides somebody, so turning one OFF is how the
cartridge puts people INTO the world — and that direction had no name in this project at all.

## Which is why the bucket was wrong

`WhyTheGatesAreShut` sorts every gate the run never set into five reasons about what the FILE can
do. All four of the floor's went into *set only where the map scan cannot see — past the code
boundary*: a claim that nothing which can actually run opens this gate, contradicted by the same
`Attempt` the bucket was computed from.

```
  the floor    12 past the boundary  ->  8, and 4 in a new bucket
  the widest   35 / 31 / 17 / 15 / 12  ->  35 / 30 / 16 / 15 / 7 / 7
```

The widest run's mislabelling was spread across **three** of the five buckets, not one. `35` and
`15` are unchanged, which is the check 211 left behind: those two are properties of the file and
must not move.

**The sixth bucket is the first one that is about the RUN rather than about the file**, and it is
ordered ahead of all five. Whatever else could or could not open a gate, a run that opened it and
shut it again settles the question.

## What 239 said about the cycle is not what a run says

239 read `9.6`'s fifteen doors off the scripts and concluded they toggle `0x0001`. Asked of an
actual run — which nothing could do until this milestone, because `--trace` watches a VARIABLE and
answers about something else entirely when it is handed a flag number:

```
  --play                          85 moves by  50 flags; 3 both ways, 2 within one pass
    0x0002  12 set / 12 clear, both in one pass — holds 8 objects
    0x4001   6 set /  6 clear, both in one pass — holds 0

  --play --say-yes --boat --in-order   174 moves by 111 flags; 5 both ways, 5 within one pass
    0x0001, 0x0002, 0x008C, 0x026C, 0x0807, 0x4001
```

**`0x0001` does not move at all in the `--say-yes` rows, and those cycle.** So it is not what makes
the state go round. What does is the class below it: `0x026C` and `0x0807`, set and cleared six and
seven times, holding nothing — scratch flags set on one map and cleared on another, whose value at
the END of a pass depends on which map the walk reached last. 239's sentence was a reading of the
bytes offered as a fact about the run, and it was wrong in the ordinary way: right about the
mechanism, wrong about the instance.

Free and not claimed: `0x4001` moves both ways in all six rows, and `0x4001` is also the number the
doors reading uses as a VARIABLE — 63 of them. The same number in two namespaces. Not resolved
here.

## The settle test moved nothing, and that is the finding

All six rows are identical after fixing it — same maps, same flags, same passes, same party. The
new test is strictly stricter, so the run can only ever go longer; it never does on this cartridge.
190 moved the map count by nought at every setting while moving flags at all six, and this is the
same shape one turn further: **a fix that changes no headline is not evidence it was not a fix.**
What it buys is that the two answers now share one definition of "the same state" instead of two.

## One direction, on purpose

The report says what stopping on the other phase would COST — maps reachable with the taken-back
flags and not without — and the first version printed the other half too, on the reasoning that a
one-directional number cannot say which way it went. That number can only ever be nought: a flag in
this walk does exactly one thing, hide somebody, and a hidden person cannot block a square, so more
flags is always a superset of the reach. **A line that cannot come back non-empty is trap 8 written
upside down.** It was deleted and the monotonicity is asserted in a test instead.

At all six settings the cost is nought. The four are free.

## The breaks

Seven, seven catches:

| break | what went red |
|---|---|
| the settle test back to counts | four tests |
| the union folded as flags move, not at the pass end | `SetAndClearedInsideOnePassIsNotAFlagItEverHad` |
| `tookBack` computed the other way round | two tests |
| the second walk uses the stopping flags | `AMapReachedOnlyWhileTheFlagWasOnIsNotInWhatItReports` |
| the new bucket ordered last instead of first | `AGateTheRunTookBackIsNotTheCodeBoundary` |
| a clear recorded as a set | two tests |
| the walk stops being monotone in flags | three tests |

The fifth is the one worth naming: three of the floor's four DO have a setter in the image the map
scan never opened, so the old answer is still available and still wrong. Ordering is the whole
guard.

3020 → 3031 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **`0x026C` and `0x0807`** — what they are for. Set on one map, cleared on another, holding
  nothing: they look like a sequence's own scratch, and the run is only the first evidence.
* **`0x4001` in two namespaces**, a flag in the run and a variable in the doors reading.
* **Which signs actually ran and what the floor's seven new flags are** (239) — still owed; this
  milestone read the four it turned OFF, not the seven it turned on.
* **`9.6`'s puzzle**, now known not to be the reason the run cycles.
* **`0x194`'s nineteen doors** (236), **`0x82`'s seven words** (238), the three numbers nothing
  computes (231), `0x406F` (229), and everything owed at 215 onwards.
