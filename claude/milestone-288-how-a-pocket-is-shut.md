# Milestone 288: how a pocket is shut, and a wrong reading that gave the right answer

287 ended with a caveat rather than a finding: *a pocket is not proof of a fence. The instrument
says "reached the map, never stood here", and a square unreachable because of a one-way ledge
would look the same.* This separates them.

The separation is worth less than the thing that happened while writing it.

---

## Three ways, and two of them are nought

```
    HOW THEY ARE SHUT (288):
      0 square(s) are on the SAME GROUND the walk stood on — that number must be nought
      0 are behind a LEDGE
      4019 are SEALED, which only a door opens
```

**The first nought is the check.** A walk's steps are symmetric over walkable ground, so a square
joined to a stood-on one by ordinary steps and never visited would be a walk that stopped early or
an instrument that disagrees with the walker. It is nought, and 240's rule is that a count which
must be nought is the best thing an instrument can carry.

**The second nought is a reading about the cartridge**: no ground in this game is closed off by a
ledge alone. Every ledge in FireRed drops you somewhere you could already stand, or drops you
somewhere with a door — which is a designer's decision this project can now state as a measurement
rather than a feeling.

## And 55 doors nobody can walk to

```
    55 of the world's 1294 warp(s) sit on walkable ground inside a pocket, on 26 map(s)
```

287 found nineteen of them by chasing the cable club. Nineteen POKéMON CENTERS with two doors each
is thirty-eight; the other seventeen are on seven maps this milestone did not open. **A door in the
warp list that no walk can stand on is a door in the file and not a door in the world**, and there
is now a population for that rather than one anecdote.

## The part worth writing down

The first version of the ledge reading was wrong, and it gave **exactly the same answer**.

A hop crosses a ledge square `over` in direction `d` and lands on `over.Step(d)`. To find the
square somebody would hop FROM to land where the walk is standing, `over` is one step back from
here and the hopper is one step back from `over`. The first version asked `HopOnto(over, Back(d))`
— which can never equal the square it was testing, so the whole reverse branch was dead code.

Dead code that reports **0 behind a ledge**. Which is the right answer.

> A measurement that cannot fail on this cartridge is not a measurement of this cartridge, and
> "the number came out as expected" is the weakest evidence there is that the number was computed.

Only a fixture separates them, and the fixture is a four-square column with a ledge pointing out of
a one-square pocket — a shape this game does not contain, written for a case this game does not
have, because that is the only case that can tell the two readings apart. It is the third break in
the table below, and the wrong version dies on it.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the reverse hop read the wrong way — the first version | 1 | **1** |
| the forward hop removed | 1 | **1** |
| a square the walk could not have stood on is counted as ground | 1 | **1** |
| the ledge bucket tested before the step bucket | 1 | **1** |
| **CONTROL:** `Fenced` summed in a different order | **0** | **0** |

Four predictions, four matches.

The fourth is worth a line: the hop flood is a SUPERSET of the step flood, so asking it first puts
every square in the ledge bucket and empties the one that must be nought. An order that reads as
arbitrary was the only thing keeping the check alive.

## What is left

* **What the 4019 are.** They are sealed, which names how they are shut and not what they are.
  CINNABAR ISLAND's 352 and ROUTE 25's 270 are the two biggest and neither is a counter.
* **The seventeen other doors in pockets**, on seven maps. Thirty-eight of the fifty-five are the
  cable club; the rest are unopened.
* **A sealed pocket with a door in it is enterable from the other side**, and this does not ask
  from where. The warp names a map and an index, so it is a join away.
* **Nothing here re-derives which way a ledge goes** (266). The hops are taken undirected on
  purpose — the question a fence asks is whether anybody can be on either side without a door —
  and the directed version would be a different reading.
