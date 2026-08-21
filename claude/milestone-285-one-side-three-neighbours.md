# Milestone 285: one side, three neighbours, and the walk took the first

265's longest-owed item was the lifts. Going to measure them meant asking the six floor-table runs
whether they can get back — 265's other owed item — and that column, the first time it was ever
printed, showed **six thousand squares stranded** at the boat settings where the walking runs strand
forty-eight.

The lifts were worth a hundred and eighty squares. The thing the column found was worth seven maps.

---

## `ConnectionOn` returned the first connection on a side

```csharp
public MapConnection? ConnectionOn(ConnectionSide side) =>
    Connections.FirstOrDefault(c => c.Side == side);
```

**A side can carry more than one neighbour.** Exactly one in this cartridge does:

```
    1 side(s) across 1 map(s) carry more than one neighbour — 116 join(s) in the world
      3.60  WATER PATH  Left x3
        3.59 (GREEN PATH) @0, 3.18 (SIX ISLAND) @40, 3.61 (RUIN VALLEY) @80
```

WATER PATH is a hundred rows tall with three twenty-row maps stacked along its western edge. Every
square stepping west off it was sent to GREEN PATH whatever row it stood on; `AcrossEdge` then put
the arrival at `from.Y - 0`, which for row 53 is off GREEN PATH's grid, the walkability check
refused it, and **the crossing did not happen at all.**

A fault that DELETES edges and reports nothing. It has been there since the walker was written.

## What one join was worth

The old rule is kept as a parameter so the difference is a subtraction in one process (241):

```
    + the boat (MODELLED)                              108595 square(s), 405 map(s), 179 CANNOT get back
    CONTROL: ONE NEIGHBOUR PER SIDE (the rule before)  107290 square(s), 398 map(s), 6027 CANNOT

    285's FIX: +7 map(s), +1305 square(s) stood on, and +5848 that could not get back and now can
```

**A blast radius of one edge and a cost of seven maps.** The Sevii 6/7 cluster — OUTCAST ISLAND,
GREEN PATH, WATER PATH, PATTERN BUSH, ALTERING CAVE — is entered across that join and left across
it, so getting the neighbour wrong made four and a half thousand squares a one-way trip.

And the floor table moved, which is the number this project guards hardest:

```
    --play --say-yes --boat                    388 / 300 in 7   (was 381 / 295)
    --play --say-yes --boat --in-order         388 / 301 in 7   (was 381 / 296)
    --play --say-yes --boat --surf --in-order  388 / 300 in 5   (was 381 / 295)

    --boat (MODELLED): +145 map(s) — it has been quoted at +138 since 239
```

## And it overturned 283, two milestones later

283 printed the sign scripts read at no lever setting and separated them: 36 on maps nothing
reaches, of which **five were one apiece in the DOTTED HOLE** — filed there as "a puzzle nothing in
the walk solves". The DOTTED HOLE is on RUIN VALLEY, which is `3.61`, which is the **third**
neighbour on WATER PATH's left edge.

```
    AT NO SETTING: 49 of the 519 (was 55) — 30 reach (was 36) / 18 walls / 1 the file
    the maps nothing reached: 1.96 x26, 1.62 x3, 1.102 x1   (1.116-1.120 and 3.61 are gone)
```

It was not a puzzle. It was this.

## The lifts, which is what the milestone set out to do

265: *the walk gets into `10.6`, `1.58` and `1.46` and models each as a room with no exit.*
`Warp.Dynamic` was derived so a lift cabin's runtime exit would not be reported as a hole in the
world, and understanding it that far is understanding it half way — the walker steps in and stands
there forever.

`ridingTheLifts` is the upper bound, MODELLED exactly as the boat's every-dock is: **every door that
names a sentinel room is a door out of it.**

```
    THE LIFTS: +0 map(s), +180 square(s) stood on, and +46 that could not get back and now can
```

Nought maps — every floor of SILPH CO. and the ROCKET HIDEOUT has stairs as well — and the 46 are
the three cabins themselves plus what only their doors reach. It un-strands 46 of the 48 that
265 reported; the two left are the FUCHSIA ledge pocket, which is a real one-way trip.

## The second column, at all six settings

```
      setting                                     stood   cannot get back   maps   whole
      --play                                      37179               46      3       3
      --play --say-yes                            69260               48      4       3
      --play --say-yes --in-order                 69260               48      4       3
      --play --say-yes --boat                    105105              284      7       4
      --play --say-yes --boat --in-order         105103              284      7       4
      --play --say-yes --boat --surf --in-order  105104              284      7       4
```

265 added the way back to one walk and said the floor table's six rows had not been asked. They are
asked here, out of the same six runs the rows come from, over each run's own edges — which is 265's
rule and the only reason the number means anything.

What is left stranded after the fix is ICEFALL CAVE (177 squares, entered by eight ledge hops) and
the FUCHSIA pocket. Both are ledges, and a ledge is one-way by construction.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the coverage test removed — the old first-on-the-side rule | 10 | **8** |
| `Covers` off by one at the far end | 2 | **2** |
| `Covers` drops the lower bound | 1 | **1** |
| an unknown neighbour wins immediately instead of last | 2 | **2** |
| the lift lever ignored | 1 | **1** |
| the control inverted | 3 | **3** |
| **CONTROL:** `along >= 0` written `!(along < 0)` | **0** | **0** |

**One over-prediction, and it is instructive.** The first break removed only the coverage test and
left the unknown-neighbour handling alone, so the two fixtures whose first candidate is a map the
world does not hold still got the right answer — they are guarded by the *other* half of the method.
I predicted for a broader break than I wrote, which is the same mistake as 283's green break: the
number was about my edit, not about the guards.

**And the third break is the one that earned its fixture during this milestone.** The lower bound
was unguarded when I first wrote the tests — every fixture happened to put its neighbours at
ascending offsets, so a rule that only checked the far end passed all of them. The fixture that
catches it lists the neighbours out of order on purpose.

## What is left

* **The other six sentinel rooms** — `0.0`-`0.4` and `2.11` TRAINER TOWER — are entered by a script,
  so `--through-scripted-doors` is the setting that would walk into them. Still not run.
* **The two one-way borders.** `3.50` and `3.51` both name `3.14` THREE ISLAND upward and it names
  `3.49` downward. Now that the boat rows reach them, this is measurable for the first time and was
  not measured here.
* **Every reach number quoted in this prompt from before 285 is short**, and the ones about the boat
  are short by seven maps. The floor table is re-pasted; the prose around it has not been audited.
* **ICEFALL CAVE's 177** and **FUCHSIA's 2** are ledges. Whether a player can leave ICEFALL CAVE by
  a door the walk does not model is unasked.
