# Milestone 260: the elevation in every record, and the byte that says which

259 found the object table's kind byte **by hand** — `0xFF` on nine records where 1639 had nought,
in a byte no reader consumed — from a hexdump and a hunch. Signs have a kind byte too, read at 248
from a hexdump and a hunch. Nobody had asked whether warps and triggers do.

Asking that record by record is the same hunch a third time. Asking it of all four lists at once is
an instrument.

---

## The reader says which bytes it reads

The question needs a list of which offsets this project consumes. Writing that list down beside
the readers is the obvious move and it is the fault this project has fixed at 220, 224, 251 and
258: a second statement of a rule goes stale the first time the first one changes.

`Rom.WatchReads` records every byte position read through the image until it is disposed. The
sweep runs the readers under it and subtracts what they touched. **It cannot disagree with the
readers, because it is the readers.**

```
    object:  7 bytes nothing reads, 2 of which take more than nought
    sign:    7 bytes nothing reads, 4 of which take more than nought
    trigger: 4 bytes nothing reads, 1 of which takes more than nought
    warp:    1 byte  nothing reads, and it takes more than nought
```

A byte nothing reads is not a finding — `object +3`, `+11`, `+22`, `+23` and `trigger +5`, `+10`,
`+11` are nought in every record in the game, and that is what spare looks like. **A byte nothing
reads that takes more than one value is**, and the difference is the whole instrument.

## The same alphabet, four times

```
    object  +8   0x03 x1472, 0x00 x96, 0x01 x45, 0x04 x18, 0x05 x10, 0x0A x2, ...
    warp    +4   0x03 x898,  0x00 x341, 0x04 x49, 0x01 x5,  0x05 x1
    trigger +4   0x03 x179,  0x00 x31,  0x01 x18
    sign    +4   0x00 x528,  0x03 x173, 0x05 x1
```

Four lists, four different offsets, one small alphabet dominated by three. Two of those four record
layouts were *derived* by this project rather than known, so their offsets agreeing on an alphabet
is four independent things saying the same word.

**Every map block carries an elevation nibble.** `MapBlock.Elevation`, `(Raw >> 12) & 0xF`, read
for drawing rather than for this — a table that cannot have been tuned to agree. So the test is
whether the record's byte is the elevation of the square the record stands on:

```
    object   +8   1599 of 1639  (97.6%)  against a floor of 44.0%
    sign     +4    613 of  702  (87.3%)  against            45.6%
    trigger  +4    196 of  228  (86.0%)  against            42.8%
    warp     +4   1206 of 1294  (93.2%)  against            45.3%
```

The floor is the share of each map's own squares at the value the record carries, summed — so a
map that is all one elevation contributes nothing to the difference, which is the trap a
whole-cartridge base rate would have walked into.

**And nought is the wildcard**, the elevation the game uses where a walker may change layer.
Counting it as a miss is a bucket that is not an operation (236). Split out:

```
    object   38 wildcard,  2 disagree        sign     89 wildcard,  0 disagree
    warp     87 wildcard,  1 disagree        trigger  32 wildcard,  0 disagree
```

**Three records in 3863 genuinely disagree.** The byte is an elevation, READ.

## What it costs

**423 of 425 maps carry more than one elevation among their own squares.** That is the denominator
on whether any of this can matter, and it says the world is layered nearly everywhere while this
project's collision reading is two-dimensional. A person at elevation 4 and a person at elevation 3
standing beside each other are not beside each other.

Nothing in the walk uses it yet. What that is worth is a measurement nobody has taken.

## The positive control is in the table

`sign +8`, `+10` and `+11` are the **item, the index and the count** a buried sign keeps where every
other sign keeps a script pointer — 248's reading, arrived at from a hexdump. This sweep surfaces
all three from cold, without being told they are there, which is exactly what it would have done
for the object table's kind byte before 259 read it by hand.

A sweep that has only ever come back empty has not been shown able to come back full (253). This
one comes back full on a field the project already knows, and the knowing was not what found it.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the watch records only the first byte of a wide read | 1 | 1 |
| the watch never stops | 1 | 1 |
| every byte is reported, read or not | 1 | 1 |
| a non-nought constant counts as spare | 1 | 1 |
| the unusual count is measured off the rarest value | 1 | 1 |
| **CONTROL:** the watch is cleared between maps | **0** | **1** |

The control was predicted to kill nothing on the reasoning that byte positions are absolute and no
two maps' records overlap — which is true and is not what the code does. The record values are
checked **after every map has been read**, so clearing the set between maps empties it for all but
the last one. **The prediction was wrong about the code rather than about the fixture**, which is
the other way a prediction can miss and the first time this project has recorded it. The guard was
right; the model of the guard was not.

## What is left

* **What elevation costs the walk.** 423 of 425 maps are layered and the reach numbers have never
  been measured against it. It is a `--play` question and a real one.
* **`object +14` on 1199 non-trainers**, where the reader takes it only when the trainer type is
  non-zero: 1197 nought, one `0x01`, one `0x03`. Two records, unexplained.
* **`sign +4` is nought on 528 of 702** where the other three lists are mostly 3 — signs sit at
  elevation nought far more often than anything else does, and nobody has asked why.
* **`trigger +5`, `+10`, `+11` and `object +3`, `+11`, `+22`, `+23`** are nought in every record in
  the game. That is a fact about the cartridge and worth not re-deriving.
