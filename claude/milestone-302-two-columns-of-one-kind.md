# Milestone 302: two columns of one kind

301 left `0xA2` on the list: *two species-shaped operands over 533 places, both entirely inside the
named set, unasked.* It is the biggest species-carrying command in the game — `0xB6` has ten places
and this has five hundred and thirty-three.

---

## What it is

**`0xA2` is four halfwords: a species, a species, an index and a nought-or-one.** Bytes 1, 3 and 7
are nought in all 533 records, which is what the high half of a halfword field looks like, and byte
5 takes 1, 2 and 3.

```
    a species x35, a species x33, an index x98 (299..965), and a nought-or-one x2.
    239 distinct pair(s), 24 of them the same species twice, 37 species named in all
    and 37 of those named by the table.
```

533 byte positions on 30 maps — `1.59` has 156, `1.48` SILPH CO. 119, `12.0` CINNABAR ISLAND 38,
`9.6` VERMILION CITY 30. The blocks are two `0xA2`s and a `return`, reached by
`checkflag F ; call_if 1, <block>`.

## That the two are the same kind is read off the PAIR

301's whole lesson was that the range test is worthless on its own — fifteen operand positions in
the map scan have every distinct value inside the species table's named set. So this one is read a
different way: **of the 134 pairs of operands of ONE command where both take eight or more distinct
values, which two draw most from the same set?**

```
      a            b            overlap   |a|   |b|   of the union
      0xA2 arg0    0xA2 arg2         31    35    33         83.8%   <- rank 1 of 134
      0x5C arg6    0x5C arg10       242   335   260         68.6%
      0x63 arg1    0x63 arg3         12    22    16         46.2%
      0x19 arg0    0x19 arg2          9    16    14         42.9%
```

**Rank 1 of 134.** The share is of the UNION and not of the smaller set, because against the
smaller one a pair where one operand takes two values and the other two hundred wins outright by
containing it — a fact about the sizes rather than about the fields.

And 9 of the 37 are also named by `0xA1 arg0`, which 301 read as a species, against a chance
overlap of about 3.

## The index runs with the first species

```
      9.6  second CHARIZARD    VENUSAUR=699, CHARMANDER=700, CHARMELEON=701, CHARIZARD=702, SQUIRTLE=703
      9.6  second SQUIRTLE     VENUSAUR=707, CHARMANDER=708, CHARMELEON=709, CHARIZARD=710, SQUIRTLE=711
```

One scene, one second species, and the third halfword steps by **one** down the first column, then
jumps to a new base when the second changes. That is an index into a table, not a value.

## And the table is not found

**It is not a trainer id.** 1 of its 98 values is one, against 474 trainer ids spanning 89..742 —
the same range, so the negative has a denominator rather than being a shrug.

The hunt for what it *does* index is 222's shape:

* **462** four-aligned bases in the image put ALL 98 values on a ROM address.
* **Nought** in the reversed image — so the shape is not what these bytes do by accident.
* And of the 98 targets at the best of those bases, **one** reads as dialogue.

So there is table-shaped structure and no way to choose among 462 candidates, and the targets are
not text. **A hunt that finds candidates and cannot choose between them has not found the table,
and saying so is the answer.**

What is READ: the layout, the population, that the two columns are one kind, and that the third
halfword is an index. What is NOT: what it indexes, and what the nought-or-one selects.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the share is of the smaller set | 1 | **0**, then **1** |
| a base counts when ANY value lands on a pointer | 1 | **0**, then **1** |
| no values finds every base | 1 | **1** |
| the distinct count is the raw count | 1 | **1** |
| **CONTROL:** the empty-set guard written the other way | **0** | **0** |

**Two green breaks, and they are two different faults.**

The share fixture built the record **by hand with the share already worked out**, so it guarded the
arithmetic in the TEST and not the arithmetic in the reading — fixture-lie 4, a stand-in guarding
the plumbing. `Share` is split out of the whole-cartridge sweep and the fixture reaches it.

The base hunt **stated one rule twice**: a clear-and-break inside the loop and a count check after
it. The break landed on the first and the second caught it, so nothing went red. 219's rule for the
fourth time in six milestones — *when a break is green, ask whether the line you edited is the line
that decides.* The early exit is marked as one and the count is the rule.

## What is left

* **What the index indexes.** 462 candidates, and nothing here separates them. Constraints not
  tried: that the table be contiguous, that its span match 299..965, that the entries be
  four-aligned pointers to something with a common shape.
* **The nought-or-one.** 272 and 261, near enough half and half, and nothing says what it picks.
* **`1.59` is SECTION 47** — the region-name table has no name for it, and it holds 156 of the 533.
* **`0x0136` takes four arguments at 24 places** (301) and is still unasked.
* **`0x5C arg6` / `arg10` at rank 2**, 242 shared of 335 and 260. That is `trainerbattle`, and two
  of its operands drawing from one set of hundreds is a reading nobody has made.
