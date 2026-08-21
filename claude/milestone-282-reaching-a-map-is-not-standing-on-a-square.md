# Milestone 282: reaching a map is not standing on a square

281 found that a buried item is in the ground — its own square is walkable on 142 of 183, where a
sign that names a side is on a wall on all 97. That made 249's number worth re-reading, and it does
not say what it has been quoted as saying.

---

## The number was a map count

> *the widest walk stands on 182 of 183, on 78 of 79 maps*

`Attempt` carried `Reached`, which is a list of MAPS, and 249 asked its question with it. So "stands
on 182 of 183" is **the count of buried items whose map the run got to**. A map-level answer cannot
see a square, and 41 of the 183 sit on squares nothing can stand on at all.

The walk has always had the squares — `Reach.Stood` is what every reach number in this project is
counted off — and they stopped at the edge of the record. `Attempt.StoodOn` is the pass the loop
stopped on, which is what every other number on it is.

```
    --play                                     map reached 101   BESIDE  89   UNDERFOOT  58   of 183
    --play --say-yes                           map reached 122   BESIDE 119   UNDERFOOT  83   of 183
    --play --say-yes --in-order                map reached 122   BESIDE 119   UNDERFOOT  83   of 183
    --play --say-yes --boat                    map reached 182   BESIDE 177   UNDERFOOT 137   of 183
    --play --say-yes --boat --in-order         map reached 182   BESIDE 177   UNDERFOOT 137   of 183
    --play --say-yes --boat --surf --in-order  map reached 182   BESIDE 177   UNDERFOOT 137   of 183
```

**182 was three columns away from the answer.** BESIDE is the sign rule (242) asked of a kind it is
not about; UNDERFOOT is the column that matches what the record is.

## And the denominator is not 183 either

**142 of the 183 sit on a square somebody could stand on**, so the widest run is underfoot on 137 of
the 142 it *could* be — five short, not forty-six. The other **41 can never be stood on at any lever
setting**, on 29 maps, and what they stand on is `0x00` ×35, `0x9A` ×4 and `0x08` ×2.

`0x9A` turning up again is worth noting: 281 measured it at seven squares in the world of which five
hold a sign, and four of those five are buried items on solid ground. Seven squares is far below
237's bar and it stays unnamed, but it is now the second reading to trip over it.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `StoodOn` comes back empty | 3 | **3** |
| `StoodOn` is one square per REACHED MAP, which is the fault itself | 3 | **2** |
| **CONTROL:** the set built inline instead of from the local | **0** | **0** |

The over-prediction is `TheRunSaysWhichSquaresItStoodOn`, which only asks that the set is non-empty
and on the right map — a map-per-square set satisfies both, which is exactly why the other two
fixtures exist.

## What is left

* **Whether the 41 are a fact about the cartridge or about the collision reading.** 35 of them are
  on ordinary ground marked solid, which is the same shape as `10.6 (4,1)` (281) and has the same
  two candidate explanations and no third.
* **Whether the walk should pick them up.** 249 established it costs no reach; 282 says the run is
  underfoot on 137 of them at the widest, so what it would change is what the party ends with. Still
  a DECISION.
* **`0x9A`** — seven squares, five signs, four of them buried on solid ground. Below the bar twice
  over and interesting twice over.
