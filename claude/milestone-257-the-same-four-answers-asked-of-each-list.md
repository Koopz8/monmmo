# Milestone 257: the same four answers, asked of each list

250 exists because `--arrivals` asked one of its two condition lists whether anything writes the
variable a condition names, got **nought**, and quoted the nought. Asked of the other list the
same bucket holds **forty-three**.

255 then split the middle bucket four ways and printed the split of **the two lists added
together**. That is the same shape one level in: a classification of a total that mixes two
populations cannot come back different for them.

It does.

---

## The two lists disagree completely

```
  the middle bucket, BOTH LISTS TOGETHER: 364 condition(s), 84 distinct
      76 condition(s),   4 distinct — SOMETHING DOES WRITE THAT VALUE, one hop
       3 condition(s),   3 distinct — a COUNTER can reach it
     192 condition(s),  10 distinct — copied from a source this cannot read
      93 condition(s),  67 distinct — the bucket means what it says
    so the correction moves 79 of 364 condition(s) (21.7%) and 7 of 84 distinct

  the middle bucket, ON ARRIVAL only: 282 condition(s), 28 distinct
      76 / 0 / 192 / 14        the correction moves 76 of 282 (27.0%)

  the middle bucket, ON A SQUARE only: 82 condition(s), 56 distinct
      0 / 3 / 0 / 79           the correction moves 3 of 82 (3.7%)
```

**Neither mechanism touches both lists.** The one-hop copy idiom 255 is named for — `setvar
0x8004, N ; copyvar X, 0x8004` — is worth 76 conditions on the arrival list and **nought** on the
square list. The counter is worth nought on the arrival list and **all three** of what the square
list gained. The "copied from something unread" admission is 192 on the arrival list and **nought**
on the square list, so on that list this reading has no unknowns of that kind at all.

255's sentence — *a fifth of it by condition count was wrong, and the cause is one two-command
idiom* — is true of the total and is a fact about **one** of the two lists. The other list's
answer is 3.7%, by a different mechanism, and 21.7% is the average of two numbers that have
nothing to do with each other.

A mechanism that is a rounding error in the total can be the whole of one list's answer.

## What the eighty-two actually are

The square list's middle bucket was the thing this milestone set out to open. It is not what its
name suggests.

```
  THE VERDICT ON EVERY CONDITION, per list — and the last column is the error bar on the one before it:
                    something     armed at      NOTHING CAN     does not know
                    writes it     the start     produce it      (a copy it cannot read)
                    READ          MODELLED      READ            READ
    on arrival        144             8             6             192   of 350
    on a square       106            72             8              42   of 228
```

**71 of the square list's 82 want NOUGHT.** Every variable holds nought before anything writes it,
so those conditions are armed at the start of the game and something has to write the variable to
turn them **off**. For most of that list the middle bucket's name — *a variable something writes,
but nobody writes THAT VALUE* — reads as unsatisfiable and means the opposite.

That leaves **8 of 228** square conditions that nothing this reading can see could produce.

**And the column that carries it is MODELLED, and now says so.** Nothing in this repository has
read what the save's variable block holds before a script writes it. 250 asserted it in prose —
*a variable nothing writes holds nought* — and did not mark it. It is load-bearing: it is the
difference between 72 armed and 72 dead.

## Neither list can support a count of dead conditions

```
    on arrival: 6 can never fire against 192 this cannot read — the error bar is the LARGER
    on a square: 8 can never fire against 42 this cannot read — the error bar is the LARGER
```

"N conditions can never fire" is the sentence this whole reading exists to produce, and the
honest answer on this cartridge is that it cannot be said about either list. The command prints
the comparison rather than leaving it to be made.

## 250's headline is wrong, and 251 is why

250 reported, and the prompt has carried since:

> **`0x405F` is written by NOTHING** — no setvar in the scan, no place in sixteen megabytes, and
> no literal the code loads. A variable nothing writes holds nought, so **1 of the 43 is armed
> from the start and 42 can never fire**.

`--who-writes 0x405F` today:

```
  0x405F — 6 site(s) in the file, 4 of which read as script, 4 of which the map scan opened
    = copied: 4 site(s), 4 opened — 0x1A7958 (3.42 person 1), 0x1A7967 (3.42 person 1),
      0x1A7AA1 (3.42 trigger (13,149))
```

```
081A7958  19 5F 40 01 40        copyvar 0x405F, 0x4001
081A7967  19 5F 40 01 40        copyvar 0x405F, 0x4001
081A7AA1  19 5F 40 01 40        copyvar 0x405F, 0x4001
```

**`0x405F` is filled from `0x4001`, four times, on the map most of the forty-three squares are
on.** And `0x4001` is set to 0/1/2/3/4/5/6/7/8/… — **all eight** of the values those squares want.

That is not proof any of them fires: what the source held at the moment of the copy is a fact
about a run, not about the file, and the command before each copy is not a `setvar` so 255's
one-hop rule correctly declines to read a value. But it is the reason **CANNOT was the wrong
word**. The verdict is DOES NOT KNOW, and the forty-two move out of the dead column into the
error bar.

**Nothing about the cartridge changed.** 251 put `copyvar`'s destination into both of this
repository's write tables — that is where these four sites came from — and 250's sentence was
written one milestone earlier and copied forward through 251, 252, 253, 254, 255 and 256 without
anybody re-running the instrument under it. The prompt's own rule (trap 14, written at 230) says
to run a number's instrument before quoting it. Six milestones did not.

`--arrivals` now says so in the place the sentence was, and names the source of every unresolved
copy — `copyvar`'s second operand is a variable id and is in the bytes even when the value is not.
Throwing that name away is the whole of how this happened.

## The rules moved out of the printer

255 decided its four answers with four lambdas inside `WriteArrivals`, a function that needs a
whole cartridge. That is the fault this project fixed at 219, 221, 222 and 223: a break aimed at a
rule no fixture can reach comes back green whatever it does.

* `WhatAVariableCanHold.HowReached` — the four answers, decided **in order**, one per condition.
* `WhatAVariableCanHold.CanItFire` — the verdict, three readings and an admission.
* `WhenAMapRunsSomething.ByList` — the grouping this milestone is about.
* `WhatAVariableCanHold.From` — the reading over command sequences, so `In` only chooses scripts.

The printer reads `ByList`'s answers and prints them beside its own, so if the two groupings ever
disagree the reader sees it rather than being told they agree.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| a copy outranks a value something sets | 1 | 1 |
| a copy outranks wanting nought | 1 | 1 |
| `ByList` stops filtering by which list asked | 5 | 5 |
| a resolved copy names its source too | 1 | 1 |
| the four answers count the whole list, not the middle bucket | 2 | 2 |
| **CONTROL:** the rows come out in the cartridge's order | **0** | **0** |

The control is the finding. A break that swaps the named pair of lists for the distinct values in
the order they turn up passed everything — the row order was a rule the code stated in a comment
and nothing checked, and it is what makes two runs of `--arrivals` diffable. A fixture whose
square comes first went in and the same break now kills exactly one.

**And one break was wrong rather than the guard.** The fourth was written to remove an `else` and
its payload deleted the line under it as well, so it killed two. Corrected, it kills the one that
was predicted. A break that kills MORE than predicted is the same signal as one that kills fewer:
look at the break first.

## What is left

* **`0x4001` is the source, and it is scratch.** 285 scripts write it, and the prompt already
  rules it out as a story counter. Whether the copy on `3.42` is reading a value some earlier
  command on the same run put there is a question about the run, and `--trace 0x4001` is the
  instrument for it.
* **The eight that nothing can produce** on the square list, and the **six** on the arrival list —
  small enough to read individually for the first time.
* **The 42 in the error bar** are now a question with an address on it rather than a boundary.
* **The MODELLED nought.** Reading what the save's variable block holds at the start means reading
  compiled code, which is the same wall `--buried`'s base hunt hit at 248.
