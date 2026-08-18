# Milestone 236: nineteen askings in one bucket

235 found that a routine either has a wait after every place that asks it or after none, thirteen
of fourteen, and named `0x194` as the exception: waited for at **1 of its 34 places**.

It was not an exception. It was the wrong bucket.

---

## What `0x194` actually is

Thirty-one of its thirty-four call places set `0x8004` in the run of setvars immediately before the
call, to **eighteen different values** — and every one of the thirty-four is on `2.1`, `2.2` or
`2.10`, which the export calls **TRAINER TOWER**. It is not one operation asked thirty-four times;
it is one entry point asked nineteen different ways.

```
    0x194 with 0x8004 =   19    6 place(s), 0 waited
    0x194 with 0x8004 =    3    4 place(s), 0 waited
    0x194 with 0x8004 = none    3 place(s), 0 waited
    0x194 with 0x8004 =    1    2 place(s), 0 waited
    0x194 with 0x8004 =    5    2 place(s), 0 waited
    0x194 with 0x8004 =   10    2 place(s), 0 waited
    0x194 with 0x8004 =   16    2 place(s), 0 waited
    0x194 with 0x8004 =   18    2 place(s), 0 waited
    0x194 with 0x8004 =    2    1 place(s), 1 waited   <- the whole of 235's exception
    ... and ten more with one place each, none waited
```

The single wait is on the one place that passes **2**, and 2 is passed at exactly that place. So
`0x194` is all-or-nothing too, at the level of what is being asked.

## The rule, restated, with the right denominator

```
    269 (routine, argument) pair(s), 95 of them in more than one place
    0 of those 95 are waited for at SOME places and not others
    chance at 7.3% a place would give 26.6
```

**Nought of ninety-five, against twenty-six point six.**

The null changed with the question and that matters more than the number. 235 asked how many
groups would be waited at EVERY place by chance and got 0.21 — a null dominated by the groups that
wait for nothing, where "all" and "none" are the same event. The thing actually observed is that
**no group is mixed**, and its null is `1 − pⁿ − (1−p)ⁿ` summed over the groups that could show
it. That is 26.6, and 0 against 26.6 is the statement worth making.

Note what the test for this deliberately **cannot** do: an asking with one place contributes
`1 − p − (1 − p) = 0` whatever `p` is, so excluding single-place askings changes no answer and no
fixture can catch a reading that keeps them. The filter is a statement of intent and it is written
into the test as one.

## What this says about 235, and about buckets

235's finding survives and gets stronger; 235's *exception* was an artefact of grouping by routine
number when the cartridge groups by routine number **and argument**. The routine number is the
entry point; `0x8004` is which door.

That is worth carrying, because this project has walked into the same shape before from the other
side: **a count is not a ranking** (trap 3), and now **a bucket is not an operation.** Before
reporting one item as an exception to a rule, check that the bucket is the thing the rule is about.

The check is cheap and it is the reason this milestone exists at all: `0x194` was the one row that
looked wrong, and looking at it dissolved it.

---

## What changed

* `WhatIsWaitedFor.SelectorBefore` — what the unbroken run of setvars touching a call put in
  `0x8004`, or nothing, which is a bucket of its own rather than nought.
* `WhatIsWaitedFor.ByAsking` — the same all-or-nothing question keyed on (routine, argument).
* `WhatIsWaitedFor.ExpectedMixed` — the null for the outcome actually observed.
* `--routines` prints both levels and `0x194` broken out.

Four breaks, four catches:

| break | what went red |
|---|---|
| the argument is any setvar nearby, not the run touching the call | `OnlyTheRunOfSetvarsTouchingTheCallIsTheArgument` |
| nothing handed over folded in with nought | `TheNearestOneWinsAndNothingIsNotNought` |
| the askings bucket by routine alone | `OneRoutineAskedTwoWaysIsTwoAskings` |
| the null is the chance of not-all instead of mixed | `TheNullIsTheChanceOfTheMixedOutcome` |

The third is the milestone's own fault written as a break: bucketing by routine alone is exactly
what 235 did, and the test asserts that the same three calls read as an exception one way and as
two clean askings the other.

2993 → 2998 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **What `0x27` DOES.** That it is a property of the asking is read; that it is *waiting* is still
  a guess.
* **`0x194`'s nineteen doors.** Eighteen values and a no-argument form, all on TRAINER TOWER, none
  of them read.
* **`0x011E`**, the routine `10.14`'s nineteen shared signs ask.
* **The three numbers nothing computes** (231): `62 gates hold 240 people`, `146 trees and rocks`,
  `158 objects`, and `the ceiling is 45 of 437 byte positions`.
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
