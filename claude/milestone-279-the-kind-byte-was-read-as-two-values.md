# Milestone 279: the kind byte was read as two values and it takes five

The task was the two leftovers from 248 and 249: eight holes in the buried index, and a spare bit
six records set. The holes are unreadable and the bit is not named. **What the chase found instead
is that this project has been reading the sign record's KIND byte as two values since 248, and it
takes five — and three of them say which side of the sign you have to be standing on.**

---

## Counting a thing instead of asserting it

A hole in the index is a slot nothing claims, and the first thing to ask is whether some other kind
of sign claims it. That is a count. Counting it:

```
      kind   records   a pointer           north         south          west          east
      0x00       422     422/422         243/422       368/422       231/422       198/422
      0x07       183       0/183         120/183       147/183       142/183       127/183
      0x01        73       73/73           46/73     73/73 ALL         23/73         28/73
      0x03        14       14/14            4/14          3/14     14/14 ALL          0/14
      0x04        10       10/10            2/10          2/10          0/10     10/10 ALL
```

**519 script signs are four kinds and this project reads them as one.** Every one of the 519 holds a
ROM pointer and none of the 183 buried ones does, so the kind byte separates the two record shapes
perfectly — a much stronger statement of 248's reading than "we filter on kind seven".

## Three of the four name a side

242 established that this project reads a sign from its own square or any of the four around it, and
that its own square is solid. So the walkable NEIGHBOURS of a kind are a test of whether the kind
picks a side, and the floor is the commonest kind's own rates — 0x00 names no side and is the only
population big enough to have one.

> **Kind 0x01 is read from the SOUTH**: 73 of 73, against a floor of 0.0046%.
> **Kind 0x03 from the WEST**: 14 of 14, against 0.0217%.
> **Kind 0x04 from the EAST**: 10 of 10, against 0.0517%.

And the other half, which is what turns "one side is always open" into "this side and not that one":
on 0x03 the **east** side is open **0 of 14**, and on 0x04 the **west** side is open **0 of 10**.
These are not squares that merely have a lot of open neighbours.

`0x02` does not occur in this cartridge at all, which fits a north nothing here uses — that is an
inference and is marked as one.

## What it costs

**97 signs are readable from one side and this project reads them from four**, and 68 of them have
another walkable neighbour, which is the blast radius rather than the count of the fault (9). Making
the walk obey it is a change to the RUN and a decision, deliberately not made: 241 measured signs at
0 maps and 7/3/2 flags across the lever settings, so restricting them can only take flags away, and
what it takes is not measured.

## The spare bit: what the six have, and what the bit is not

Six records set the bit the count does not use. Every candidate this project can read, with the
exact chance that six drawn from the 183 without replacement would all hold it — a product of six
fractions, no independence assumed:

```
    property                                  of the six     of all   chance
    its item is named by NO script                   6/6     68/183   0.228 %   <- all of them
    its item is buried in ONE place                  4/6     20/183   0.000 %
    it does something when HELD                      4/6     36/183   0.004 %
    it cannot be bought (price 0)                    0/6     14/183   0.000 %
    its own square is walkable                       6/6    142/183   21.306 %
    the count is one                                 6/6    171/183   66.179 %
```

The six are SOOTHE BELL, SACRED ASH, LEFTOVERS twice, PP MAX and MACHO BRACE — every one an item no
script in the game hands over, sells, asks for or loads, at one chance in 440.

**Two of the rows are 6 of 6 and mean nothing**, and they are in the table for that reason: the
square is walkable on 6 of 6 at a chance of one in five, and the count is one on 6 of 6 at two in
three. A perfect score is worth what its floor is small.

**And the bit is not that property**: 62 other records name an item no script names and do not set
it. So it is a thing the six have and not the thing the bit means. What the bit DOES is engine
behaviour and is not read.

## The holes stay unread, and now it is clear why

A hole is only readable against the order the slots are handed out in, and it is neither of the two
orders this project has: **12 of the 79 maps hold their slots in more than one run** (`3.42` has
three: 34-36, 145-148, 155), and **40 of the 182 steps up the index go DOWN the file**. So the 183
slots are in an authored order the cartridge does not otherwise expose, seven of the eight holes are
in the first quarter of the range and four of those are consecutive, and there is nothing to read
them against. No other sign kind claims them — every other kind holds a pointer.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the side rule takes the FIRST qualifying side rather than requiring exactly one | 1 | **1** |
| **CONTROL:** the empty-population guard removed | **0** | **0** |
| `Across` sends west to itself | 2 | **2** |
| a pointer is any non-zero word | 1 | **1** |
| `EverySign` skips the buried kind | 1 | **1** |
| **CONTROL:** `Count == 1` written `Count() == 1` | **0** | **0** |

Six predictions, six matches — the third time (246, 277).

The second one was a control by accident and became a finding: **the guard was unbreakable because
it was redundant.** With the exactly-one rule, an empty population makes all four sides qualify
vacuously, so it is not exactly one and the answer is already nought. 219's rule — a guard nothing
can fail is not a guard — so it was deleted rather than kept and decorated, and the test that says
the behaviour still holds stays.

## What is left

* **Whether the walk should obey the side.** 97 signs, 68 with somewhere else to stand. A change to
  the run and a DECISION; what it costs in flags is not measured.
* **`0x02` is absent and would be north.** Four kinds, three sides named, one direction missing —
  inferred, not read, and no cartridge evidence can settle it.
* **The exported map record carries no sign kind.** Making the walk obey the side needs it there
  first, which is 239's shape again (`MapData` carried no sign list at all).
* **The spare bit's meaning** is engine behaviour — the sixth wall of that kind.
* **The eight holes** are unreadable against an order that is not exposed. Withdrawn as a question
  rather than left open.
