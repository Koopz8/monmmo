# Milestone 255: a value the reading never followed

`--arrivals` has carried the same caveat since 229, in its own documentation:

> *Only the write whose value is in the command. A `copyvar` or an `addvar` puts something in a
> variable too, and what it puts there is not readable from the bytes — so a condition satisfied
> only by one of those reads as satisfied by nothing. That is the direction this is allowed to be
> wrong in.*

Twenty-five milestones of quoting a limitation with no number on it. Most of it turns out to be
readable.

---

## `setvar 0x8004, 3 ; copyvar 0x406F, 0x8004`

```
0x1BB742:  16 04 80 03 00   19 6F 40 04 80      setvar 0x8004, 3 ; copyvar 0x406F, 0x8004
0x1BBAC5:  16 04 80 06 00   19 6F 40 04 80      setvar 0x8004, 6 ; copyvar 0x406F, 0x8004
0x1BB5BC:  25 4B 01         19 6F 40 04 80      special 0x014B   ; copyvar 0x406F, 0x8004
```

**That writes three into `0x406F`.** It is in the bytes as plainly as a `setvar` is; the reading
just never followed it.

229's headline — carried in the prompt ever since — was:

> `0x406F`: 20 maps want 1/2/3/5/6/7/8 and the only writer in the scan writes 0, at 3 places.

The writer writes **0, 3 and 6**. Two of the seven wanted values are written after all, by a
two-command idiom on one map, and the reading that said otherwise was the one that declared it
might.

## What the caveat is worth

```
  the middle bucket is 364 condition(s), 84 distinct, and it is answered off setvar alone:
     76 condition(s),   4 distinct — SOMETHING DOES WRITE THAT VALUE, one hop
      3 condition(s),   3 distinct — a COUNTER can reach it
    192 condition(s),  10 distinct — copied from a source this cannot read
     93 condition(s),  67 distinct — the bucket means what it says
```

**Twenty-one per cent of the middle bucket by condition count was wrong**, and the cause is one
idiom. The counter half is real too and much smaller: `addvar`'s step is a literal, so a variable
something sets to nought and something adds one to can hold one, two and three — that accounts
for three conditions.

And 192 conditions across ten variables are behind a copy from a source this project cannot read.
That is the part of the caveat that survives, now with a number on it and separated from the part
that was simply a miss.

## Adjacency, and not a barrier list

The rule is that the literal comes from **the command immediately before**, putting a value in
**the very variable being copied from**. Nothing further back counts.

The third of `0x406F`'s three copies is why. It has `special 0x014B` before it — a routine whose
reply this project cannot read — and a reading that carried a literal from an earlier `setvar`
past that would invent a value the cartridge never writes. The alternative is a list of every
command that might write a variable, and this project has had to fix such a list twice already
(214, 220). **Adjacency needs no list and cannot go stale.**

The cost is that it is conservative in the safe direction, which is where every reading in this
project is allowed to be wrong: `0x406F` still reports as copied-into, because one of its three
copies genuinely is unreadable.

## A break came back green, predicted at one

| break | predicted | went red |
|---|---|---|
| the literal need not be in the source variable | 1 | 1 |
| anything before counts, readable or not | 2 | 2 |
| **the counting walk has no ceiling** | **1** | **nothing** |

`CanReach` states the ceiling twice: a guard that refuses a value past it on the way in, and a
bound on the walk. The fixture asked for a value past the ceiling, so **the guard answered and the
walk was never reached** — fixture-lie 12, two statements of one rule and a fixture that only
touches one of them.

The replacement asks for **three** with a ceiling of eight, where the only route is `0 → 9 → 3`.
Well inside the bound, and unreachable without exceeding it. The same break then kills exactly
one.

3124 → 3135 tests. **The floor table did not move.**

---

## What is still owed

* **The 93 that survive** (67 distinct) — the middle bucket as it actually is, and nothing has
  looked at what those conditions gate.
* **The 192 behind an unreadable copy** (10 distinct). Following the source variable back is a
  second hop and a different instrument; `--through-a-call` (218) is the shape.
* **229's own numbers in the prompt** say "28 of 69 want a value nobody writes" for the arrival
  list. That is the setvar-only reading and it is now known to be an overstatement; the corrected
  split is printed but the primary three buckets still use `setvar` alone, deliberately, so that
  what changed stays visible.
* **Whose square `0x42` leaves** (254); `0x42 arg2`; the whole-image operand sweep (252).
* `0x405F` (250); the base (248); the eight unused indices and the spare bit (248); collecting the
  buried items (249, a decision); `0x8013` and `0x4025` (251).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `9.6`'s puzzle.
