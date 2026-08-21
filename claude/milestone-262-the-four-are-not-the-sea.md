# Milestone 262: the four are not the sea, and neither is elevation one

261 found four metatile behaviours at elevation 1 on **100% of their squares in sixteen
megabytes**, called elevation 1 the sea, and declined to adopt them because each is on one or two
maps.

Two of those three things were wrong. The third is what saved the project from them.

---

## Asked two ways that cannot see an elevation

`--sea` asks what a square of each behaviour **borders**, and whether anything ever **stands** on
one — both measured against the two behaviours this project already reads as water, and neither
able to see an elevation nibble.

```
    behaviour   squares  maps   beside known water    stood on   at elevation 1
    -- the ceiling --
    0x15 WATER    34484    68   127634/133299 95.8%      0.15%        59.6%
    0x10 POND       635    11      2058/2540  81.0%      0.31%        99.2%
    -- 261's four, and the one it left open --
    0x1B            751     1         0/3004   0.0%      0.00%       100.0%
    0x50             42     2         3/168    1.8%      0.00%       100.0%
    0x52            142     1         4/568    0.7%      3.52%       100.0%
    0x53             45     1         2/180    1.1%      4.44%       100.0%
    0x13             80     4        18/320    5.6%      0.00%        80.0%
    -- the floor --
    0x00 NORMAL  143184   422    4457/547234   0.8%      1.39%         0.0%
    0x02 GRASS     5303    43      148/21199   0.7%      0.57%         0.0%
    0x80 COUNTER    730    89        0/2861    0.0%      0.00%         0.0%
```

**None of the four is water.** `0x1B` does not touch a known water square once in three thousand
chances — less than ordinary ground manages. `0x52` and `0x53` carry people, doors and signs more
often than ordinary ground does, and nobody is placed in the sea. On both columns they look like
the floor and nothing like the ceiling.

The instrument tests itself in its own output: if the tallies were wrong, water would not come
back at 95.8% and ordinary ground at 0.8%. **The known rows are the aggregation's own control,
run on every invocation.**

## And the premise was never tested either

261 reasoned from *22250 squares carry elevation 1 and the water pass makes most of them solid* to
*elevation 1 is the sea*. Asked the other way round:

```
    0x15 is at elevation 1 on 20550 of its 34484 squares — 59.6%
    0x10 is at elevation 1 on   630 of its   635 squares — 99.2%
```

**Elevation 1 is not the sea.** Forty per cent of this cartridge's water is at some other
elevation. 261's 100% was a true fact about elevation 1 and said nothing about water, and the step
from one to the other was the whole of the error.

## What saved it was the bar

237 declined `[0x89] = 2` on a single site. 261 declined these four on one or two maps each.
`MetatileBehaviour.IsWater` still holds exactly two values and **this milestone had nothing to
undo** — the reading that turned out to be wrong never entered a READ list.

That is the bar doing the only job a bar has. It cost 261 a headline it wanted and it bought 262 a
clean correction.

## And the rule itself is refuted

261's remaining 751 lost squares are all on `3.35` ROUTE 17. What the flat fill crosses to reach
them:

```
    crossings: 1->3 (0x1B beside 0xD0) x171, 3->1 (0xD0 beside 0x1B) x165
```

**336 direct neighbour pairs, running the length of the map.** A road whose two sides touch three
hundred times is not two layers a player cannot cross. So *equal elevations, or nought on either
side* is **not this cartridge's rule**, and 261's 751 is a number about the rule rather than a
cost to the walk.

**What elevation costs the walk, as far as anything here can show, is nought.**

A difference produced by a MODELLED rule is a number about the rule until the rule has been tested
against the cartridge. 261 printed the rule honestly, said it was modelled, and still reported its
output as a cost. Saying "this is modelled" is not the same as testing it.

## The fill got one real fix on the way

A ledge is solid to stand on and passable to cross — `MapData.HopOnto`, a two-square move landing
past it, which this project has modelled for milestones. **261's fill could not do it**, which made
it weaker than the walk it claimed to be measuring, on ROUTE 17 of all maps.

It takes the hop now, and **through the ledge rather than over it**: a ledge carries elevation
nought, the wildcard, and hopping one is how a walker changes layer. Asking whether the start
connects to the landing refuses exactly the move the ledge exists to allow. Flat reach 79594 →
79886.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the hop is checked start-to-landing again | 2 | 2 |
| the fill cannot hop at all | 2 | 2 |
| **CONTROL:** the hop is tried before the ordinary step | **0** | **0** |

The control's nought is 261's kind rather than 257's: a square cannot be both walkable-and-
connected and a ledge, because a ledge is solid, so the order the two are tried in cannot change
an answer.

## What is left

* **What `0x1B` and `0xD0` are.** 751 and its border, one map, elevation 1 and 3, no people, never
  beside water. Something names them and it is not anything this project reads.
* **This cartridge's actual layer rule.** Refuted, not replaced. `--layers` still prints the
  modelled one and now says in its own output that it is wrong.
* **`0x52` and `0x53` carrying people at 3.5% and 4.4%** — above ordinary ground. Whatever they
  are, they are somewhere people stand more than usual.
* **`--sea`'s tallies live in the printer.** The known-water and known-land rows control them on
  every run, which is better than nothing and is not a fixture.
