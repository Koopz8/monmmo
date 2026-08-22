# Milestone 286: a border asked at the square, and what 285 moved

285 closed the last of 265's list except one: *the two one-way borders — `3.50` and `3.51` both
name `3.14` THREE ISLAND upward and it names `3.49` downward — neither is reached without the boat,
so what a walker crossing north from either comes back onto is unmeasured.* The fix put them in
reach, and going to measure them turned out to be a question 265 had asked of the wrong thing.

And 285 moved seven maps, so several numbers in this prompt were short. Both halves are here.

---

## 265 asked the borders at the map. A walker crosses at a square.

265's border test is *does the far map declare a join back on the opposite side* — 116 joins, 114
declared, 2 not. That is a question about two map records, and it is the LOOSE half of the pair
265 itself established for the doors: *does this door name THIS door back* scored **920**, where
*does it come back to this map at all* scored 237 against a control of 233, which is to say nothing.

The tight half for a border is: step off, step straight back, and land on the square you left.

```
    2646 crossing(s), 2596 land back on the square they left, 50 DO NOT

      3.11  SAFFRON CITY  Down -> 3.24 (ROUTE 6)     x24  back to 3.11 SAFFRON CITY
      3.24  ROUTE 6       Up   -> 3.11 (SAFFRON)     x12  back to NOTHING
      3.24  ROUTE 6       Up   -> 3.11 (SAFFRON)     x12  back to 3.24 ROUTE 6
      3.50  SEVII ISLE 6  Up   -> 3.14 (THREE ISLAND) x1  back to 3.49 THREE ISLE PORT
      3.51  SEVII ISLE 7  Up   -> 3.14 (THREE ISLAND) x1  back to 3.49 THREE ISLE PORT
```

**Forty-eight of the fifty are a join 265 scored as declared back**, and they are one of the 114.

## And the arithmetic is printed rather than asserted

```
    3.11  SAFFRON CITY   48x40  Up->3.23@0, Down->3.24@12, Left->3.25@10, Right->3.26@10
    3.24  ROUTE 6        24x40  Up->3.11@0,  Down->3.5@-12
    3.14  THREE ISLAND   24x40  Down->3.49@0, Left->3.48@0
    3.50  SEVII ISLE 6    1x1   Up->3.14@0
```

A pair round-trips exactly when the offsets are negatives of one another: `AcrossEdge` subtracts
the offset going and the other one coming back. **SAFFRON declares `Down->ROUTE 6 @12` and ROUTE 6
declares `Up->SAFFRON @0`**, so a player walking north out of ROUTE 6 arrives twelve squares west of
where they walked south. ROUTE 6's OTHER side is `Down->3.5 @-12`, which is the consistent
convention — so the anomaly is one connection record, not a convention this project has misread.

`3.50` and `3.51` are **one square each**. Two 1×1 maps whose single square declares a join north.

## Nought of the fifty is walkable

That is where the milestone would have stopped, and it is the wrong place to stop: an asymmetry
nobody can reach is a different finding from one somebody walks into.

> Of all 2646 crossings, **976 are walkable at both ends**. Of the 50 that do not round-trip,
> **NOUGHT** is.

Thirty-seven per cent of crossings can be stood on at both ends; the fifty score nought. If the
misalignments fell anywhere, eighteen of them would be crossable. **Every broken join in this
cartridge is behind a wall** — which is why nobody ever noticed and why it costs the walk nothing.

So 265's question has an answer: *a walker crossing north from SEVII ISLE 6 comes back onto THREE
ISLE PORT* — and no walker can, because the square is not walkable at any lever setting, water
open.

## And what 285 moved

285's fix reached seven more maps, so every "at the widest" number in this prompt was short. Re-run
rather than adjusted:

| line | was | is |
|---|---|---|
| gating flags the widest run sets | 213 of 322, 109 never | **216 of 322, 106 never** |
| sign scripts run at the widest | 463 (327 addresses, 134 maps) | **469 (333, 140)** |
| unread at the widest | 56 — 36 reach / 19 walls / 1 | **50 — 30 / 19 / 1** |
| buried, at the widest | BESIDE 177, UNDERFOOT 137 of 142 | **BESIDE 178, UNDERFOOT 138 of 142** |
| routines it cannot answer | 860 places, 75 routines; 276 across 58 | **869, 76; 279 across 59** |
| the ceiling in byte positions | 17 of 359 | **17 of 359 — unchanged** |
| the floor's way back | 35142 over 174 maps, 24029 cannot | **unchanged — the floor has no boat** |

Six moved and two did not, and the two that did not are the check: the byte-position ceiling is a
property of the file and the floor row has no boat in it, so neither *could* have moved. A re-run
where everything moves is a re-run of the wrong thing.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `RoundTrips` compares the map and not the square | 1 | **1** |
| the step back uses the same side rather than the opposite | 5 | **5** |
| the step back starts from the square left rather than the square reached | 2 | **2** |
| the bottom edge is read as the top | 3 | **3** |
| **CONTROL:** `RoundTrips` written with the operands swapped | **0** | **0** |

Four predictions, four matches.

## What is left

* **Whether ROUTE 6's `Up@0` is the cartridge's or this project's.** The offset is read from the
  connection record and the sibling connection on the same map uses the opposite sign, which is
  evidence but not proof — a second cartridge would settle it and this project has one.
* **`3.50` and `3.51` are 1×1 maps.** Nothing here asked what a one-square map is for; both are
  named SEVII ISLE and both declare a join north to THREE ISLAND that nothing can walk.
* **The other six sentinel rooms** — `0.0`-`0.4` and `2.11` — are entered by a script, so
  `--through-scripted-doors` is the setting that would walk into them. Still not run, and now the
  oldest thing on the list.
* **976 of 2646 crossings are walkable**, which is a number nothing has ever used. It is the
  denominator for any future question about borders.
