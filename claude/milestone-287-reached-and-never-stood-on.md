# Milestone 287: reached, walkable, and never stood on

265's last owed item: *the other six sentinel rooms — `0.0`-`0.4` and `2.11` TRAINER TOWER — are
entered by a script, so `--through-scripted-doors` is the setting that would walk into them. Not
run.*

Run. It is worth nothing, none of them is entered by a script, and finding out why turned up
**4019 squares of ground this project reaches and cannot stand on**.

---

## The scripted doors are worth nought

```
    THE SCRIPTED DOORS (265's last owed item, measured at 287):
      +0 map(s), +0 square(s) stood on, and +0 square(s) that could not get back and now can
```

Fifteen extra steps are taken and every destination was already reached. `--scripted-doors` says
the same thing from the other side and always has — *15 of them, on 15 maps, naming 7 different
places; **0** of them lead somewhere no doorway and no map edge does* — so this is one fact
measured two ways rather than one fact repeated.

**And not one of the fifteen names a sentinel room.** 265's sentence was an explanation offered for
a thing nobody had measured, and it was wrong.

## Which of the nine rooms anything gets into

```
    0.0  CELADON DEPT.    NO SETTING gets in, the scripted doors included
    0.1  CELADON DEPT.    NO SETTING gets in, the scripted doors included
    0.2  CELADON DEPT.    NO SETTING gets in, the scripted doors included
    0.3  CELADON DEPT.    NO SETTING gets in, the scripted doors included
    0.4  CELADON DEPT.    NO SETTING gets in, the scripted doors included
    1.46 ROCKET HIDEOUT   7 of 7 setting(s)
    1.58 SILPH CO.        7 of 7 setting(s)
    10.6 CELADON CITY     7 of 7 setting(s)
    2.11 TRAINER TOWER    3 of 7 setting(s), from "+ the boat" on
```

**`2.11` is reached, and by the BOAT** — TRAINER TOWER is on SEVEN ISLAND. Not by a script. And it
is not stranded, because 285's lift lever is a sentinel-room lever and it lets the walk back out.

`0.0`, `0.2` and `0.3` are named by **no warp in the world at all**. `0.1` and `0.4` are named by
nineteen each — one per town, which is the cable club being nineteen doors to one room.

## And the nineteen doors are on ground nothing walks to

```
    0.1: 19 warp(s) name it, on 19 map(s); 19 of those are on a map the widest run reaches,
         19 of the square(s) can be stood on at all, and it stood on 0

      5.5 VIRIDIAN CITY (9,1): the map holds 86 walkable square(s) and the run stood on 74 of
      them, walking THROUGH people — so the door is fenced off inside its own map
```

Every one of the nineteen: reached map, walkable square, never stood on — and the walk goes
*through* people, so it is not the receptionist. **Every POKéMON CENTER in this game has the same
twelve-square pocket behind the counter, and both cable-club doors are in it.**

## Which generalises, and that is the milestone

`WhatTheWalkFencedOff` asks it of everything the widest run reaches:

```
    163 of the 405 map(s) it reaches hold walkable ground it never got to, 4019 square(s) in all

      12.0   CINNABAR ISLAND    352 of  438 walkable
      3.44   ROUTE 25           270 of  738
      1.59   SECTION 47         200 of  811
      1.5    S.S. ANNE          160 of  313
      3.10   SAFFRON CITY       152 of  925

      19 map(s) have the SAME pocket — 12 of 86 walkable   (the POKéMON CENTERS)
      17 map(s) have the SAME pocket —  3 of 89            (the shops)
      12 map(s) have the SAME pocket —  4 of 46
       4 map(s) —  4 of 84  (1.67-1.70)
       3 map(s) — 21 of 84  (22.0, 24.0, 26.0)
```

**Nineteen maps with an identical pocket is a building, not nineteen accidents**, and a count of
maps cannot say that — only the shape can. This is 282's rule one level further down again:
reaching a map is not standing on a square, and *reaching a map* is not *standing on the map*.

Only reached maps are counted. A map nothing reaches is a reach problem, and letting those two
share a number is 249's fault exactly.

## And it closed 283

283 separated the sign scripts read at no lever setting and left eighteen of them as *"it reached
the map and never got to that wall"* — ten of them on `12.0` CINNABAR ISLAND in five adjacent
pairs, filed as a shape it did not read. CINNABAR is now measured at **352 of 438 walkable squares
fenced off**, and the join is direct:

> **18 of the 18** stand in front of ground that is WALKABLE and that no run ever stands on.

The whole bucket was pockets. Not one of them is a wall the walk merely failed to visit, and
283's "five adjacent pairs" are five pairs of shopfronts on the wrong side of a fence.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| unreached maps counted as pockets | 1 | **1** |
| the denominator is the map rather than what is walkable | 2 | **2** |
| arrivals counted instead of squares | 1 | **1** |
| a pocket of nought is a pocket | 2 | **2** |
| the smallest pocket first | 1 | **1** |
| **CONTROL:** `Walkable - StoodOn` written `-(StoodOn - Walkable)` | **0** | **0** |

Five predictions, five matches.

## What is left

* **What is in the 4019.** Nineteen counters and seventeen shop fronts are a hundred and eighty
  squares of it. CINNABAR's 352 and ROUTE 25's 270 are not counters and nothing here says what
  they are.
* **How the cable club is actually entered.** It is a script this project cannot see: the fifteen
  doors `ScriptedDoors` finds come from the `0x39` warp command, and whatever puts a player in
  `0.1` is not one of them. That is a wall of the compiled-code kind unless the receptionist's
  script uses a command this project reads and drops.
* **`0.0`, `0.2` and `0.3` are named by nothing at all** — not a warp, not a connection, not a
  scripted door. Three rooms with no way in that this project can name.
* **A pocket is not proof of a fence.** The instrument says "reached the map, never stood here";
  a square unreachable because of a one-way ledge would look the same. Separating those is the
  same shape as 265's way-back and was not done.
