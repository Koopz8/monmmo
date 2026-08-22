# Milestone 289: a map is not one place

288 found every one of 287's 4019 fenced squares sealed. Sealed *from what* is the question
underneath, and it has an answer this project has never stated: **a map's walkable ground is not
one place.**

---

## Nine hundred and forty-five pieces

```
    the 405 reached map(s) are 945 piece(s) of walkable ground
    193 map(s) are in more than one; the most is 19 (1.86 SEAFOAM ISLANDS)

    the walk stands in 506 of them
    61 map(s) it stands in more than ONE piece of — a map it enters by two doors that do not join
```

**"The run reached this map" has always meant "it stood in at least one of its pieces."** That is
282's rule a third time: reaching a map is not standing on a square, standing on a square is not
standing on the map, and the map is not one thing.

Some of the shapes name themselves. `14.3` SAFFRON CITY is nine pieces of 54, 54, 54, 54, 54, 54,
54, 54 and 52 — and the walk stands in **all nine**, which is a room you leave by a door in the
floor rather than by walking out. Seven floors of TRAINER TOWER are `115, 1`. `12.0` is eight
pieces and the walk stands in one of 86 out of 438.

## Two thousand nine hundred and forty-eight squares nothing opens

```
    439 piece(s) the walk never stood in
      47 hold a warp
      20 run along a border a neighbour crosses in from
     372 hold NEITHER — 2948 square(s) nothing in this world file opens

      3.44  ROUTE 25          270   1.9   S.S. ANNE        112
      1.5   S.S. ANNE         103   12.0  CINNABAR ISLAND   95
      1.99  MT. EMBER          87   1.81  ROCK TUNNEL       59
```

ROUTE 25's is the biggest and it is **sea**: 270 water squares in the east of the map, in a body
that does not touch the water the walk swims. The map declares one join — `Left -> 3.43` — so
nothing crosses in from the other three sides, and no warp lands there. It is scenery you can look
at from the shore.

## And the border half was left out

The first version of this asked only about warps, and read ROUTE 25's sea as ground nothing opens
— which happened to be right, and would have been wrong for twenty other pieces. **A door is not
the only way into a place.**

The fix is a parameter, and it is REQUIRED rather than optional. A default of `null` on the map
lookup is the same fault with a default value on it: a caller who forgets it gets the wrong answer
silently, which is precisely what the first run did.

## A break's kill count is only worth what its failures are

One break here predicted six and killed **seven**. The seventh was
`OnePlayerWalkingIsVisibleToAnother`, a socket test in the server — which does not call the class
being broken, cannot see it, and passed on a clean tree three times running afterwards.

Its budget for one message was 30 seconds. The suite is about 28 seconds idle and was 55 in that
run, because a break-guard run builds first and the container was busy. **The timeout was inside
the noise**, and the test's own comment already says why that matters: *a test that fails when the
machine is busy is worse than a slow one, because it teaches everybody to re-run the suite instead
of reading it.*

Raised to 120 seconds with the measurement written beside it, and the break re-run: **six,
predicted six.**

> A break's kill count means nothing unless every failure in it was caused by the break. An
> over-prediction that matches by accident is worse than one that does not.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the border test removed | 1 | **1** |
| the border test ignores which neighbour covers the row (285's old rule) | 1 | **1** |
| a warp counted against the map rather than the piece | 1 | **1** |
| `NothingOpensIt` drops the border term | 2 | **2** |
| `StoodOn` set from the map rather than the piece | 6 | **6**, after the noise was removed |
| **CONTROL:** `NothingOpensIt` written in a different order | **0** | **0** |

And one fixture was wrong in the same way the instrument had been: `ABorderTheNeighbourDoesNot
ReachOpensNothing` set the join's offset to nought and made the neighbour one square tall, which
**covers** — so the test passed while asserting the opposite of its own name. A Down join is
measured along X and the fixture's map is one square wide, so only the offset can put a neighbour
out of reach. Rewritten, and it is the second break in the table.

## What is left

* **The 2948 are scenery or they are a wall of the compiled-code kind.** The cable club's doors
  (287) are opened by a script this project cannot see, and the same could be true of any of these
  — including `12.0`'s seven other pieces, which 283 measured ten sign scripts against.
* **`14.3`'s nine identical rooms** and **`1.86`'s nineteen pieces** are named by their shape and
  by nothing else here.
* **A piece with a warp in it is enterable from the other side** and this still does not ask from
  where — the warp names a map and an index, so it is one join away, and 288 said the same thing.
* **Pieces are counted over STEPS.** 288 measured that nought of the fenced ground needs a ledge
  hop, so folding hops in would blur a boundary already known to be clean — but that is a fact
  about the fenced ground, not about pieces the walk stands in.
