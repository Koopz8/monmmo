# Milestone 281: the sign board, and 242's rule was wrong for eighty-five signs

280 left two questions about the sign record: whether the client knows about the side, and whether a
buried sign has one. Asking the second meant asking what each kind of sign STANDS on, and that
turned out to be worth more than either.

---

## The 97 that name a side are on walls, all of them

```
      kind   records   its OWN square is walkable
      0x00       422       85/422    20.1 %
      0x07       183      142/183    77.6 %
      0x01        73        0/73      0.0 %
      0x03        14        0/14      0.0 %
      0x04        10        0/10      0.0 %
```

A sign you read from one side is a thing on a wall — **0 of 73, 0 of 14, 0 of 10**, which is 279's
reading arriving from a direction it did not use.

**And 242's rule is wrong for eighty-five signs.** *A sign's own square is SOLID — that is what a
sign is* is true of every one of the 97 and false of 85 of the 422 that name no side. 83 of those 85
stand on ordinary ground.

**And the buried kind is in the ground rather than on a wall**: 142 of 183 walkable, against 85 of
the 519 script kinds. Which is what a thing you dig up should be.

## 0x84 is the sign board

"179 signs stand on `0x84`" names nothing — it reads the same whether that byte is a sign board or
every wall in the game. The direction that names it is the other one:

```
     behaviour   squares   hold a sign      share   against the world
          0x00    143184           421      0.3 %          1x
          0x84       189           179     94.7 %        315x
          0x9A         7             5     71.4 %        238x
          0x20        15             4     26.7 %         89x
          0x81       161            12      7.5 %         25x
          0x21      1505            25      1.7 %          6x
     every square in the game: 233741, of which 702 hold a sign — 0.300 %
```

**189 squares of `0x84` exist in this cartridge and 179 of them hold a sign.** It is named on
`MetatileBehaviour.SignBoard` with the evidence, and it belongs to ONE kind: all 179 are kind
`0x00`. Not one of the 97 that name a side stands on it, and neither does any of the 183 buried
ones.

The ten with nothing on them are **nine on `3.11` and one on `10.19`** — nine of ten on one map is
either a decoration or nine records that were removed, and there is nothing here to tell those
apart.

`0x9A` at 238-fold and `0x20` at 89-fold are seven squares and fifteen. **Declined on 237's bar**,
which is the bar that has cost this project nothing every time it has been applied and would have
cost it a wrong water list at 261.

## And the one 242 could not place

`10.6 (4,1)` has been in this prompt since 242 as the single sign in the cartridge that nothing can
stand beside — a mistake, furniture, or a square the collision reading gets wrong. Now that the
bytes are printed:

> kind `0x00`, its own square `0x00` and solid, its four neighbours `0x00` and all four shut.

**It is not a sign board and not a collision reading of that kind.** It is an ordinary byte in a
walled block of ordinary bytes, which rules out the third possibility and leaves the first two.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `HowOften` counts a square only when it is marked | 4 | **4** |
| the floor divides by the MARKED total | 3 | **2** |
| the floor divides by how many BEHAVIOURS there are | 2 | **2** |
| **CONTROL:** the tally read with `TryGetValue` | **0** | **0** |

One over-prediction: the empty-tally test survives both floor breaks, because nought over nought is
nought either way, and I had counted it.

## What is left

* **The nine `0x84` squares on `3.11`.** Decoration or nine removed records; nothing here separates
  them, and it is the same shape as 279's eight holes in the buried index.
* **`0x9A` and `0x20`** are above any fold-change you like and below the bar on count. If a later
  milestone finds a second line of evidence for either, they are cheap to adopt.
* **Nothing reads `SignBoard` yet.** It is named because the evidence is in hand, not because a
  reading depends on it.
* **The client still does not know about the side** (280), and **whether the walk should stand ON a
  buried square** rather than beside it is now a sharper question than it was: 142 of 183 of them
  are squares somebody can stand on.
