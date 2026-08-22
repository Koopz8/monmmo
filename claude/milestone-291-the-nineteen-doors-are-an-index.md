# Milestone 291: the nineteen doors are an index

This prompt has said "`0x194`'s nineteen doors on TRAINER TOWER" since 236. They are not doors.

---

## Eighteen values, and three of them are holes

```
    0x0194: 19 distinct argument(s) over 1066 call(s) at 34 place(s)
    the values run 0..20 and 3 of them are never used: 13, 14, 15
```

A contiguous run with three holes in it is a **table index**, and 236 had every number needed to
say so — it counted the values and called them places.

**1066 calls at 34 places** is in the line on purpose. The routine inflation on this cartridge runs
from 1x to 97x, and a report saying "236 places" when it means 236 calls has made 224's mistake in
a new list. Every row of this instrument carries both.

## What the argument selects, said in the script's own words

What a routine DOES is ARM code and unreadable (67). What the scripts do about its ANSWER is not:

```
      0x8004 = 18   21 call(s) at 2 place(s), compared against 0/1
      0x8004 = 5    18 call(s) at 2 place(s), compared against 0/1
      0x8004 = 16   16 call(s) at 2 place(s), compared against 0
      0x8004 = 17    1 call(s) at 1 place(s), compared against 0
      0x8004 = 20    1 call(s) at 1 place(s), compared against 1
      ...and fourteen more arguments whose answer nothing compares at all
```

At `= 16` a nought means **"This is a two-on-two battle."** At `= 18` a one runs a **`warp`**. The
same routine, two arguments, two unrelated questions. That is what a selector is: the value picks
which field is being read, and the script's own branches say the fields are different.

## The floor, and the half of it that is not a hit

```
    22 routine(s) are called with more than one value in 0x8004
    2 of those have the answer compared against DIFFERENT things depending on which: 0x0194, 0x017C
    asking it of the calls that CARRY a value leaves 1: 0x0194
```

**`0x17C` is not really one.** It is three calls: one with no argument compared against 1, and two
with arguments 129 and 214 both compared against 0. The difference is between being handed an
argument and not, which says nothing about what a value selects — a different claim wearing the
same test's name. Both numbers are printed because dropping either would be a choice made here.

So: **one routine of twenty-two**, and it is the one 236 was looking at.

## What is still not read

`0x194`'s answer is compared at five of its nineteen arguments and ignored at the other fourteen —
those calls are made for their effect, not their answer, and nothing here says what the effect is.
The values 13, 14 and 15 exist in the run and are used nowhere, which is a fact about the
cartridge's table and not about this cartridge's scripts.

And "TRAINER TOWER" is where all of them are, which is 236's reading and stands.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| an argument whose answer nothing compares counts as a question | 1 | **1** |
| `Places` counts calls | 1 | **1** |
| the argument is the FIRST value put in the slot | 1 | **1** |
| any variable's value counts as the argument | 1 | **1** |
| a routine with one argument value is reported | 1 | **1** |
| the value test does not exclude the no-argument calls | 1 | **1** |
| **CONTROL:** `Distinct().Count()` written `ToHashSet().Count` | **0** | **0** |

Six predictions, six matches.

## What is left

* **The fourteen arguments nothing compares.** 1066 calls and the answer is looked at at five of
  the nineteen; the rest are called for what they do. What that is stays behind the boundary.
* **13, 14 and 15.** Three slots in a twenty-one-slot range that this cartridge's scripts never
  ask for. Either the table has them and nothing uses them, or the range is not what it looks
  like.
* **`0x17C`'s 129 and 214** are two numbers eighty-four apart with everything between them unused,
  which is not an index and is not read here.
* **The floor is 22 routines wide.** Only 25 of the 178 take an argument at all (236) and only 22
  take more than one value, so this is the whole population the cartridge affords.
