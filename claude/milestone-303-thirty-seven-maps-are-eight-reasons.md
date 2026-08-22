# Milestone 303: thirty-seven maps are eight reasons

Six milestones inside the script commands. This one goes back to the world.

The floor table has said **388 of 425** since 285 and nothing has ever asked what the other
thirty-seven are. The prompt carries *"11 of 425 maps have no way in at all"* and *"eleven maps
have no way in at all, five of them Sevii isles"* — a count with no list, entered before 285 opened
seven maps and never re-run.

---

## The count reproduces, and it is not the answer

**11 of 425.** The line is right. And it is only a third of the story:

```
      11   NO WAY IN AT ALL — no warp and no border in the file names it (a fact about the FILE)
      18   only named from maps that are themselves unreached — behind one of the roots below
       8   NAMED FROM GROUND THE RUN STANDS ON — these are the reasons
```

**A count of unreached maps is not a count of reasons.** Eighteen of the thirty-seven are behind
one another; the number worth having is the count of ROOTS — the maps that something the run
*stands on* names, and that it still cannot get to. There are eight.

The first bucket is about the FILE and not about the run, so 211's rule applies: it must not move
with a lever. Printed at all six settings it reads **11, 11, 11, 11, 11, 11** while the other two
go 218/13 at the floor and 18/8 at the widest. The check passes in the open.

```
      0.0   0.2   0.3    CELADON DEPT.
      18.1  ROUTE 6      27.0  ROUTE 19      29.0  ROUTE 23
      3.50  SEVII ISLE 6   3.51  SEVII ISLE 7   3.52  SEVII ISLE 8   3.53  SEVII ISLE 9
      31.5  SEVEN ISLAND
```

Four Sevii isles, not five — and `31.5 SEVEN ISLAND` is a seventh island map rather than an isle,
which is what the prompt's "five" was.

## A count is not a ranking either

```
      map      name             by warp   by border   behind it   named from
      2.2      TRAINER TOWER          2           0           9   2.1
      1.103    MT. EMBER              3           0           8   1.97
      1.76     SECTION 49             2           0           5   1.75
      0.1      CELADON DEPT.         19           0           1   19 maps, one per town
      0.4      CELADON DEPT.         19           0           1   19 maps, one per town
      1.62     SECTION 47             1           0           1   1.59
      2.11     TRAINER TOWER          9           0           1   2.10
      3.11     SAFFRON CITY           0           4           1   3.23, 3.24, 3.25, 3.26
```

**Three of the eight carry twenty-two of the twenty-six** (trap 3 — rank by what it costs). And
four of the eight are already known by other names:

* **`0.1` and `0.4`** are the rooms above a POKéMON CENTER, named by nineteen maps each, one per
  town. 287 found the walk stands in **none** of them — they are inside the twelve-square pocket
  behind the counter, which is the same pocket repeated on nineteen maps. **Nineteen doors into a
  room, all of them behind a shop counter.**
* **`1.103` MT. EMBER** is the `0x0089` wall this prompt has carried since 190, with the RUBY
  behind it.
* **`3.11` SAFFRON CITY** is the ONLY root named by no warp at all — four borders and nothing else.
  286 measured those crossings two milestones ago: *50 of 2646 land somewhere other than the square
  they left, and NOUGHT of the 50 is walkable*, with `3.11` SAFFRON and `3.24` ROUTE 6 as the worked
  example. **286 had the arithmetic and never asked what it cost in maps.** It costs the outdoor
  SAFFRON CITY.

What is READ: the buckets, the roots, and what sits behind each. What is NOT: why the run does not
take the seven warp-named roots' doors — that is a per-door question and this reading only says
which doors to go and look at.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the sentinel counts as a way in | 1 | **0**, then **1** |
| a border is not a way in | 2 | **1** |
| the closure leaves the root out | 2 | **2** |
| named-from-reached is not told from behind-a-root | 1 | **1** |
| **CONTROL:** the no-way-in test with its two halves swapped | **0** | **0** |

**The sentinel break came back green because the fixture pointed at the wrong map.** It put a
sentinel warp on one map and asserted about another, so the filter had nothing to do with either.
And the filter is barely reachable at all: a warp is dynamic exactly when its target is `127.127`,
so dropping those can only change the answer for a map whose own id is `127.127` — and no bank in
this cartridge holds 128 maps. It is a decoy now and carries that case (57, and 300 did the same).

The border break killed one where two were predicted: the second fixture asserts the bucket
`NamedFromReachedGround` through a WARP, so it never sees the border list at all. The prediction
was wrong about the fixtures, not the code (32).

## What is left

* **The seven warp-named roots** are seven doors the run reaches and does not take. Each is one
  `--play` question: a shut flag, an unwalkable landing square, or a script the walk does not run.
  `2.2` TRAINER TOWER is worth nine maps and is the place to start.
* **`3.11` SAFFRON CITY** needs 286's fifty crossings re-read at the square: the border arithmetic
  is measured and what the walker should do about it is a DECISION.
* **`1.62` SECTION 47 and `1.76` SECTION 49** are maps the region-name table has no name for, and
  `1.59` SECTION 47 also holds 156 of 302's 533 `0xA2` records.
* **The eleven with no way in are eleven doors nothing opens**, and three of them (`0.0`, `0.2`,
  `0.3`) are more POKéMON CENTER upper rooms — so the whole family of five is either unreachable or
  behind a counter.
