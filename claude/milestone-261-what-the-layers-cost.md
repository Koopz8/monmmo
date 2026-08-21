# Milestone 261: what the layers cost, and the sea the water reading missed

260 ended on a worry: every event record carries the elevation of its own square, **423 of 425
maps carry more than one elevation**, and this project's walk is two-dimensional. A worry is not a
number.

`--layers` is the number. It changes nothing — whether the walk should enforce layers is a
decision, and 249 did the same for the buried items it found the run standing on.

---

## One fill, one predicate

The flat answer and the layered answer come out of **one** `Fill` with one predicate swapped: the
flat run passes a rule that always says yes. A before-and-after built from two separately-written
fills is a measurement with no instrument, which is 241's rule and a fault this project has
already been caught by once.

The rule is MODELLED and named in the output: two squares are on one layer when their elevations
are equal, or when either is nought — the value a walker may step onto from anywhere. Nothing here
has read the engine's own rule.

## The first answer was wrong, and why is worth keeping

Filled over `map.Collision` the loss came out at **8397 squares across 50 maps**. That number is
about the code. Water in this cartridge is **collision-zero** — a sea square's own bits say
walkable, and it is made solid by a metatile *behaviour* — so the fill was walking on the sea, and
the biggest cross-layer pair was `1 beside 3`, which is every shoreline in the game.

`GridFor(false)` is the grid the run actually steps against. Over that:

```
    79594 square(s) reached flat, 78843 layered — 751 (0.94%) across ONE map
    185 map(s) have walkable squares at more than one elevation
```

**One map.** `3.35` ROUTE 17 — and every one of the 751 lost squares is at **elevation 1**. So the
layer rule is not finding a bridge. It is finding water.

## Elevation 1 is the sea, and two readings of that disagree

22250 squares carry elevation 1 and the behaviour pass already makes 21185 of them solid. The 1065
left over are the disagreement, and asking what behaviours they carry settles it with no threshold
at all:

```
    0x1B   751 squares — elevation 1 on 751 of its    751 in the world (100%), 1 map
    0x52   142 squares —              142 of    142       (100%), 1 map
    0x13    64 squares —               64 of     80        (80%), 3 maps
    0x53    45 squares —               45 of     45       (100%), 1 map
    0x50    42 squares —               42 of     42       (100%), 2 maps
    0x00     9 squares —               14 of 143184         (0%), 3 maps
    0x11     7 squares —                7 of   2831         (0%), 2 maps
    0x17     4 squares —                4 of    775         (1%), 2 maps
    0x21     1 square  —                1 of   1505         (0%), 1 map
```

**Four behaviours sit at sea level on every square they occupy in sixteen megabytes. The rest sit
there on nought to one per cent.** There is no band boundary in that and none is needed — the same
property that makes 244's written-ness rule work.

And `0x1B`'s 751 are **exactly** the 751 ROUTE 17 squares the layered fill loses. One behaviour
value and one number, arrived at from two directions that did not know about each other.

## Not adopted

`MetatileBehaviour.IsWater` is a READ list and each of the four is on **one or two maps**, which is
below the bar 237 set when it declined `[0x89] = 2` on a single site. The finding is reported with
the number it would be worth — **980 squares, and nought maps on the run's own reach** — and the
list is left alone. A second map would settle `0x1B`; there is not one.

## What a layer could bite at all

```
    134235 pair(s) of walkable neighbours share an elevation
      1136 have NOUGHT on one side
       675 join two different non-nought layers — 269 of them 3 beside 4
```

Those 269 are the bridges. **The flat walk steps across every one of them and it costs nothing**,
because both ends are reachable another way. That is the negative this reading exists to be able
to produce, and it is the answer.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the nought wildcard goes | 2 | 2 |
| the fill ignores the rule it was given | 1 | 1 |
| a fill seeded on a solid square | 1 | 1 |
| the elevation comes off the collision bits | 1 | 1 |
| **CONTROL:** the fill walks the directions in another order | **0** | **0** |

**And this control's nought is not a hole.** 257's and 258's green controls each turned out to be
a rule nothing checked, and both earned a fixture. This one cannot: a flood fill's answer does not
depend on the order it visits neighbours in, so there is nothing there to guard. *Not every green
control is a missing fixture* — the question is whether the thing the break changed can affect the
answer at all, and here it provably cannot.

## What is left

* **The four behaviours.** `0x1B`, `0x52`, `0x53`, `0x50` — 100% at sea level, one or two maps
  each. Something that is not the elevation nibble would settle them; nothing else has been tried.
* **`0x13` at 80%.** Sixty-four of eighty. Neither in nor out.
* **Whether the walk should enforce layers.** It costs nought maps and 751 squares, all of them
  water, so the honest answer is that fixing the WATER list would be worth more than fixing the
  walk — and neither is worth it until the four behaviours are settled.
* **One-way steps.** The rule is symmetric and this cartridge has ledges you can jump down and not
  climb. Whether a step's direction matters has not been asked, and the fill would need a
  different shape to ask it.
