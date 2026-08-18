# Milestone 254: a column, read off the maps' own sizes

253 closed the operand audit with one thing outstanding. `0x42` writes its first operand (252),
has eight places in the whole game, and every one of them is a `compare` away from saying what it
computed. The compares are against **6, 7, 9, 9, 18, 24 and 50**, and a list of seven numbers is
not a finding.

It becomes one the moment you put each number beside the map it was compared on.

---

## Twenty-four is not a row on a map twenty-four tall

```
    0x42 arg0: 6 of 8 — 75 %
        0x162FC5  1.86    compared against  24 on a 38x24 map  — a column, TOO TALL
        0x163144  1.87    compared against   9 on a 38x24 map  — a column, a row
        0x164562  1.121   compared against  50 on a 60x32 map  — a column, TOO TALL
        0x166F24  3.8     compared against  18 on a 24x20 map  — a column, a row
        0x16795D  3.14    compared against   9 on a 24x40 map  — a column, a row
        0x167973  3.14    compared against   9 on a 24x40 map  — a column, a row
        of 6 compared place(s): 6 could be a column, 4 could be a row, 2 could ONLY be a
        column, 0 neither   <- SO THIS OPERAND IS AN X
```

**`0x42` leaves a square, and its first operand is the column.** SEAFOAM ISLANDS is thirty-eight
wide and twenty-four tall, so a comparison against twenty-four is a column and cannot be a row;
PATTERN BUSH is sixty by thirty-two and the fifty is the same argument again. Four of the six say
nothing on their own and two of them settle it, which is what a discrimination looks like when
only some of the maps are much wider than they are tall.

Every number in this came off the cartridge already: the widths and heights are read for all four
hundred and twenty-five maps to draw them.

## The negative controls are what make it worth anything

The same test, asked of two operands this project already knows are not coordinates:

```
    0x26 arg0 (specialvar's answer): of 326 compared place(s): 324 could be a column, 324 could
    be a row, 0 could ONLY be a column, 2 neither   <- which does not name it

    0x19 arg0 (copyvar's destination): of 116 compared place(s): 115 / 115 / 0 / 1
      <- which does not name it
```

A routine's reply, compared three hundred and twenty-six times, and a copied-into variable
compared a hundred and sixteen — both fit inside both bounds everywhere and **neither gets named**.
A test that named those two would name anything with a small number after it.

And the operand this cannot settle is reported as unsettled: `0x42 arg2` has **one** compared
place, on a seventy-by-thirty-two map against six, and six is a column and a row. It stays out of
both tables and out of the answer.

## What is claimed and what is not

`0x42` leaves a column in its first operand and a row in its second — the second by position
rather than by measurement, because one site cannot discriminate. **Whose square it is, is not
read.** The two places on `3.14` are a person standing at `(7, 24)` branching on whether the
number is at least nine, which would be a strange thing to ask about oneself; that is an argument
and not a reading, and it is not in the output.

`--operands` now prints every candidate's places with the script that opened each, because a
sweep that reports an operand and not its addresses hands the reader a number and a hunt.

## The breaks

| break | predicted | went red |
|---|---|---|
| the height bound is not checked | 2 | 2 |
| any compare counts, not one on this number | 1 | 1 |
| a place on no map is given one anyway | 1 | 1 |
| the verdict stops counting the only-a-column case | 1 | 1 |

Four for four, after three milestones of predictions coming in low. 3117 → 3124 tests.
**The floor table did not move.**

---

## What is still owed

* **Whose square.** `0x42` leaves one and this cannot say whose. 226 settled the same class of
  question for `0x63` by measuring how often the square is the person's own against a chance floor
  of 0.45 — eight places is thin for that, and it is the shape to use if anyone tries.
* **`0x42 arg2`** — one compared place, which names nothing. A second would settle it.
* **`0x42` still has no name in `ScriptCommands`.** It leaves a square; what it is called is a
  different question and this project names commands for what the bytes show them doing.
* **The whole image has never had the operand sweep** — `--operands` asks the map scan, 0.6% of
  the file.
* **The 82 trigger conditions waiting on a value nobody writes** (250), 56 distinct.
* `0x405F` (250); the base (248); the eight unused indices and the spare bit (248); collecting the
  buried items (249, a decision); `0x8013` and `0x4025` (251).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.
