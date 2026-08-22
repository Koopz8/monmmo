# Milestone 304: forty-three doors nobody walks to

303 ended with seven warp-named roots and called them *"seven doors the run reaches and does not
take"*, with three obvious guesses: a shut flag, a landing square the far map calls solid, or a
script the walk never runs.

**It is none of the three. The run never gets to the door.**

```
      what the run did at the door             into an UNREACHED map   into a REACHED one
      stood ON the square                                           0                 1165   <- known
      stood BESIDE it and not on it                                 0                    0
      never got near it                                            43                   15
      WALLED IN — nothing could ever reach it                       0                    2
```

**43 of 43.** Not one of the doors into an unreached map was ever stood on, stood beside, or walled
in — every one has one or two walkable neighbours, so something *could* reach it, and nothing did.
They are inside 287's pockets: walkable ground on a map the run **does** reach that it never stands
on. The seven roots are not seven puzzles. They are one.

## The row whose answer is known

"The run never got to any of them" is also what a broken instrument says (68, 78). So the same
question is asked of every warp from reached ground into a **reached** map, where the answer is
known in advance: **1165 of 1182 — 98.6%.** The instrument can say yes, and does, 1165 times.

The two entry points are separate on purpose: `TheKnownRow` must not be able to pick up a door into
an unreached map and `Into` must not pick up a door into a reached one, or the calibration would be
scoring itself.

**STOOD BESIDE is 0 of 1182.** An empty bucket is a fact about the population it was asked of (31):
the walker steps *onto* a door's own square — a door is walked through, never stood beside — so this
bucket can only ever fill from a door the walk got next to and refused.

## A green break that was not a fixture gap

Swapping WALLED IN and STOOD BESIDE came back **green**, predicted 1. The first read of that was
"the fixture cannot see it", and the discriminating shape was supposed to be two doors side by side
in a wall. **That fixture cannot exist.** `ToGrid` opens every warp square — a door is solid in the
block data and the games let you stand on it anyway — so **a door beside a door always has a
walkable neighbour**. The fixture failed on its first run, on that one line.

Chasing why turned up the actual fault. Walkability was being asked of the **walking** grid, which
calls water solid, and the walker is not always walking:

```
    1 door(s) of the 1225 have NO neighbour on foot and one from the water, so the walled-in count
    is asked of the surfing grid: 1.4 S.S. ANNE (33,15) -> 1.5 S.S. ANNE
```

One door on the whole cartridge, on the **S.S. ANNE**, whose harbour this repository has known
since the water work is 1446 squares of open sea. Its one open neighbour is water. The walking grid
calls it walled in — *nothing could ever reach it* — and `--surf` floats up to it.

So the break was right and the guard was wrong. Walled-in is now asked of the surfing grid, and
that also settles the order it came in about: **the two conditions cannot both hold.** The walker
only ever stands where some grid calls it walkable, and the surfing grid is the union of both, so a
neighbour that was stood on is a neighbour this count can see. Nought neighbours means nothing was
stood beside. The swap stays green, and it is green because the order is a spelling rather than a
rule — recorded as such in the code, with the two facts that make it one carried by fixtures (64).

## The breaks

| break | predicted | killed |
|---|---|---|
| the known row takes doors into unreached maps too | 1 | **1** |
| walled-in asked of the walking grid, not the surfing one | 1 | **1** |
| only-from-the-water loosened to "at most one on foot" | 1 | **1** |
| **the walled-in / stood-beside order swapped** | 1 | **0** — and 0 is the right answer |
| **CONTROL:** the four neighbours listed in another order | **0** | **0** |

## And the flaky test's budget is measured now

`OnePlayerWalkingIsVisibleToAnother` has fired twice under break-guard, both times while the suite
was taking 147–157s against ~30s idle. Its timeout was **120 seconds, chosen**. It is now
**100× the slowest connect the suite has actually seen, floored at 30s** — a number read off the
run rather than picked, which is the same rule this project applies to the cartridge.

## What is left

* **43 doors in pockets** is now one question and not seven: *why does the walk not enter a pocket
  it can see?* 287 counted the pockets; nothing has yet asked what fences one.
* **The 2 walled-in doors into reached maps** — `1.5 S.S. ANNE (3,20) -> 1.10` and `2.34 THREE ISLE
  PATH (25,5) -> 3.49 THREE ISLE PORT` — are reached from the far side and unreachable from this
  one. That is a one-way door read off the file, and nothing models it.
* **`3.11` SAFFRON CITY** is still a border question and not a door question (286, 303).
* **15 doors into reached maps were never got near** and the run reaches those maps anyway — a
  second way in each time. That number is a floor on how much of the world is doubly connected.
