# Milestone 266: which way a ledge is hopped

`MetatileBehaviour.Hops` decides which way each of this cartridge's 1042 ledge squares can be
crossed. It is a rule the walk applies everywhere, and its entire justification is **seven numbers
in a doc comment that no instrument in this repository prints**.

231's rule is that a number nothing computes cannot come back wrong, which is worse than a number
that is stale. These seven are not decoration — they are the evidence.

---

## All seven reproduce

```
  EACH BYTE ON ITS OWN, everything else a wall — the original derivation
      byte  way      maps  squares  stranded  its own squares stood beside
      0x38  a wall    34    10423         0      9 of 39      (all five rows identical)
      0x39  a wall    34    10423         0     17 of 41
      0x39  right     36    10556       133     17 of 41
      0x3B  a wall    34    10423         0    411 of 962
      0x3B  up        38    11352       247    420 of 962
      0x3B  down     211    46433     35328    775 of 962
      0x3B  left      34    10423         0    411 of 962
```

**211, 38, 34, 34, 36, 34, 34 — to the digit**, seventy milestones after they were taken, across
signs entering the walk at 239, clones at 259 and the edge record at 265. That is the least
exciting way for an audit to end and it is the honest one.

## The criterion is not the one it names

The comment says:

> the assignment that is right is the one that leaves the cartridge's own geography **connected**.
> That is measured rather than argued, by walking the world under each assignment and **counting
> the maps a player can reach**.

Those are two different questions on a graph with one-way edges, which is what a ledge is, and
**nothing in this project could ask the second one until 265**. Asked now:

```
    0x3B down   211 maps   strands 35328 of the 46433 squares it stands on
    0x3B up      38 maps   strands   247 of 11352
    0x3B left    34 maps   strands     0 of 10423
```

**By connectedness the chosen answer is the worst of the four**, and by a long way. The criterion
that actually decides this is reach; the sentence says so now.

That is not a fault in the answer — reach is the right criterion here, because a ledge exists in
order to make the world one-way and the alternative is a world 34 maps large. It is a fault in
the reason, which had been unfalsifiable for seventy milestones and turns out to point the other
way.

## And `0x38` is measured now

The comment marked it as owed:

> 0x38 is not decided by this and is written down as an **inference** rather than a measurement:
> no direction changes the reach by a single map, **because its 39 squares are all on optional
> ground**.

The stated reason is wrong, and the new column says so: with everything else a wall, **the walk
stands beside 9 of `0x38`'s 39 squares**. Every direction came out identical because the walk
never got near them.

**Each byte on its own is the trap.** A ledge whose ground lies behind another ledge cannot be
decided one-at-a-time — the sweep produces four identical numbers and they read like four
measurements agreeing. Run with the other two at their measured values, which is the experiment
one-byte-at-a-time cannot do:

```
      0x38  a wall   212    46568   |   0x38  left   212    46790
      0x38  up       212    46568   |   0x38  down   212    46568
      0x38  right    212    46568   |               24 of 39 stood beside
```

**West is the only direction that changes anything: 222 squares, at the same 212 maps.** So the
inference was right, it is not an inference any more, and it was settled by squares rather than by
maps — which is why counting maps could not settle it.

`0x39 right` firms up the same way: 212 maps and 46790 squares against 211 and 46655 for every
other direction and for the wall.

## A third number, and this one was wrong

`--ledges` reports `0x3B` on **954** squares. The world has **962**.

Its loops run from 1 to `width - 1` so that every square it examines has four neighbours — right
for the columns it prints, and it means what it counts is the **interior**. Those counts have been
quoted as totals since they were taken, here and in `MetatileBehaviour`'s own comment.

**Eight ledge squares sit on a map's outer ring.** A hop from there lands off the map, which
`WorldData.HopOnto` refuses, so every one of them is a wall to this project — and whether the
cartridge hops a player across a map join has never been asked. Both numbers are in `--ledges`'
own output now, with the difference named, and the census that produces them is one function both
commands call rather than two loops that agree until one is edited.

`0x38` is 39 of 39 and `0x39` is 41 of 41, so only `0x3B` was ever affected.

## The elevation column agrees twice and contradicts once

`--ledges` also prints, for each direction, whether the square that way is higher or lower than
the one opposite. For `0x38` it points **left** and for `0x39` **right** — both the measured
answers. For `0x3B` it points **up**, and the measurement says down by 211 maps to 38.

Two agreements out of three on samples of 5 and 2 squares, against a contradiction on the byte
with 962. That is not a second reading, it is noise, and the comment already said why: the
elevation nibble is nought on every ledge square, which is the value meaning "whatever is around
it".

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the wall row is dropped from the sweep | 1 | 1 |
| the company decides the byte under test | 1 | 1 |
| the squares counted are the ones stood ON | 2 | 2 |
| only the first row and column are the ring | 1 | 1 |
| the second column is measured from the wrong end | **2** | **1** |
| **CONTROL:** the four directions are swept in another order | **0** | **0** |

The fifth prediction was wrong about the fixtures rather than about the code: one test asserts the
stranded column and the others all compare squares stood on. The guard is caught, once, which is
enough — but "two tests look at this" was a guess and it should have been a count.

## What is left

* **Whether a ledge on a map's outer ring hops across the join.** Eight squares, all `0x3B`, all
  walls to this project. `WorldWalker` already crosses borders; `HopOnto` does not know borders
  exist.
* **`0x3A` has a name and nought squares.** `--which-way` gives it no row, which is the sweep
  saying so, and nothing has ever been able to say what it would have been.
* **The sweep starts at PALLET TOWN with no moves.** A byte whose ground is behind CUT rather than
  behind another ledge is in exactly the position `0x38` was, and this milestone did not vary that
  lever.
* **`--ledges`' other quoted numbers.** The axis columns are interior counts too; 950 of 954 in an
  east–west run is a share of the interior and nobody has asked what the eight do.
