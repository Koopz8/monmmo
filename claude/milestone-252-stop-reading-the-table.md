# Milestone 252: stop reading the table and sweep it

251's finding was that `copyvar`'s destination was missing from **both** of this repository's
write tables, and that having two of them caught nothing because they were wrong in the same
place. The obvious next question — *is there a third operand nobody has noticed?* — cannot be
answered by reading the tables again.

So `--operands` does not read them. It takes **every halfword-aligned operand position of every
command the map scan reads** and scores each one by the rule 244 and 251 were both settled by: a
variable something looks at is a variable something writes.

---

## The spread has a chasm in it

```
  how the scores spread, in tenths:
      0-  9%  ############################################################ 83
     10- 19%  # 1
     20- 29%  ## 2
     90- 99%  ########## 10
```

Eighty-three operands under a tenth, three between, ten above nine tenths, and **nothing at all
in the middle sixty per cent of the range.** The half-way threshold is doing no work and the
histogram is what says so — printed first, before the answer, because a threshold with nothing
behind it is a number that decides the result.

Nothing here needed a band boundary or any outside knowledge. An operand naming items, text ids,
coordinates, movement types or plain values comes in near nought, because nothing in this game
ever `setvar`s an item id.

## Three of the ten were in neither table

```
    0x26 arg0:    5 number(s) at   359 place(s),    5 written — 100%
      names: 0x800D x353, 0x8006 x2, 0x8005 x2, 0x8004 x1, 0x8008 x1
    0x42 arg0:    4 number(s) at     8 place(s),    4 written — 100%
      names: 0x8004 x4, 0x8008 x2, 0x4001 x1, 0x400D x1
    0x42 arg2:    4 number(s) at     8 place(s),    4 written — 100%
      names: 0x8005 x4, 0x8009 x2, 0x4002 x1, 0x800D x1
```

**`0x26` is `specialvar`.** Its first word is the variable a routine's answer goes into, and this
repository has read it that way in five files since 214 — `WhoTheCompareBelongsTo`,
`SpecialContracts`, `ScriptRunner`, `WhatItIsWaitingFor`, and the width table that names it. It
puts `0x800D` at three hundred and fifty-three places. **It was in neither write table.**

**`0x42` has no name in this project**, and the comment beside its width says:

> *Those are var 0x8004 and var 0x8005, and the very next command compares 0x8004 against nine.*
> ***A command taking two variables*** *and then being asked about one of them is not a
> coincidence that four bytes could produce twice.*

The knowledge was written down, in the repository, next to the number. It was in neither table
either.

**Both faults were already known somewhere else in this project.** 251's lesson was that two
lists cannot check each other; 252's is that the check was never against a list at all — it was
against the cartridge, and the answer had been sitting in prose in four other files.

## Direction is a different question and gets a different test

Written-ness says an operand names a variable. It says nothing about which way the number goes.
The evidence available in one pass is whether the **very next command compares that very number**
— an operand whose value is tested in the next breath left something there.

```
    over all 96 operand(s): 453 of 30766 place(s) — 1.5 %
    0x26 arg0: 326 of 359 — 91 %   <- NAMED BY NEITHER TABLE
    0x42 arg0:   6 of   8 — 75 %   <- NAMED BY NEITHER TABLE
    0x19 arg0: 116 of 179 — 65 %   <- already named
```

The floor is one and a half per cent of thirty thousand operand places. The third row is the
control: `copyvar`'s destination, a write **251** established for entirely separate reasons,
sitting between the two unknowns. A test whose positive control lands in the middle of its own
findings is one that did not need arranging.

`0x42 arg2` is at **12%** and stays out of both tables. It names a variable — all four of its
numbers are written — and which way that one goes is not read. The instrument reports it as the
remaining candidate rather than guessing, and it will keep reporting it until somebody settles it.

## What moved, and what did not

```
  115 variable(s) the map scan WRITES        (was 106 at 251, 90 before it)
    7 never looked at by anything            (unchanged)
  NOTHING this cartridge writes goes unconsulted: every one of the 115
```

Twenty-five more variables than this project counted two milestones ago, and **250's answer holds
over each larger population in turn**. The deaf list did not move because the numbers the new
operands name were already written by something else — a fix that changes no headline, which is
not evidence it was not a fix.

`--who-writes` also stopped printing a routine number as a value: `specialvar` says
`asking routine`, the copying pair says `from`, `0x42` says `and`, and only the three that carry
a real value say `=`.

## The breaks

| break | predicted | went red |
|---|---|---|
| written seeded from every operand rather than the writers | 2 | **4** |
| the compare need not be on this number | 1 | 1 |
| operands naming one number are scored too | 1 | 1 |
| candidates do not exclude what the tables name | 1 | 1 |
| the sweep starts at the second operand | 4 | **5** |
| specialvar and 0x42 out of the whole-image write table | 3 | 3 |

Two predictions were low and both in the safe direction — more fixtures leaned on the seed and on
the sweep's starting position than I had modelled. Trap 32 said a missed prediction tells you
which fixture does not cover what you thought; it also tells you which ones cover more.

`EveryWayANumberGetsIntoAVariableIsFound` has now been wrong twice — four of five at 251, five of
seven at 252 — and both times the fixture supplied exactly the list the code had. It names all
seven now.

3106 → 3114 tests. **The floor table did not move.**

---

## What is still owed

* **`0x42`.** It writes its first operand and names a variable in its second, and it has no name.
  Eight places, all of them a `compare` away from telling you what it computed. `--read-from` on
  those eight is one command.
* **`0x42 arg2`'s direction** — the one candidate the sweep still reports.
* **The whole-image sweep has never had this treatment.** `--operands` asks the map scan, which is
  0.6% of the file, and the same question asked of sixteen megabytes is `--in-the-image`'s shape
  and has never been pointed here.
* **The reading operands have never been swept for.** This found *writers* by seeding on writers;
  the mirror — seed on the readers and find an operand that reads — has not been run, and 251's
  fault could as easily be on that side.
* **The 82 trigger conditions waiting on a value nobody writes** (250), 56 distinct.
* `0x405F` (250); the base (248); the eight unused indices and the spare bit (248); collecting the
  buried items (249, a decision); `0x8013` and `0x4025` (251).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.
