# Milestone 219: walking back where the call touched nothing

218 left forty places where a called block leaves the answer variable **alone** — the compare
after the call is reading something older, and nothing said what. This walks back past the call
to find it, and in doing so catches two instruments in this repository giving opposite answers
about the same nineteen sites.

---

## What the older answer is

```
  40 place(s) call a block that leaves the answer variable alone, so the compare is reading
  something older. Walking back in the caller:
        19 — routine 0x01D's answer   e.g. 5.5 person 3 at 0x081BB6DF
        19 — routine 0x01C's answer   e.g. 5.5 person 3 at 0x081BB56B
         2 — whatever is left by a jump not followed here   e.g. 14.2 person 6 at 0x0816EC29
```

Thirty-eight of the forty. The shape is the same at both:

```
  0x1BB567   25 1C 00              special 0x001C
  0x1BB56A   27                    waitstate
  0x1BB56B   04 75 66 1A 08        call 0x081A6675
  0x1BB570   21 0D 80 01 00        compare 0x800D, 1
  0x1BB575   06 01 B3 B5 1B 08     if EQUAL goto 0x081BB5B3
```

And the block it calls, in full:

```
  0x1A6675   19 12 80 13 80        copyvar 0x8012, 0x8013
  0x1A667A   03                    return
```

**The call moves one argument slot into another and returns.** It cannot have answered, so the
compare is reading `0x001C`'s answer across it. That is a reading, not a guess, and the licence
for it is exactly the condition `WhatACallLeaves` reports: walk back **only** where the call
provably left the variable alone.

Of the 336 places that call a block and compare straight after, **eleven now have no owner** —
two here and the nine 218 found jumping away.

## And the two instruments disagree

Ask this repository what `0x001C` is, twice:

```
  --special 0x1C   called, but never branched on — it does something rather than answering
  --routines       0x01C   19 site(s),  19 branch, 0 argument(s)
                             compared against 1x19
```

**Both about the same nineteen sites.** `SpecialCalls` stops at the `call` — the barrier 214
added after it caught the scan crediting `0x0028` with `0x005D`'s reply — and reports no branch
at all. `SpecialContracts`, which is what `--routines` prints, walks four commands past the
`special` and stops at nothing except a `setvar` to the answer variable: not a `call`, not a
`specialvar`, not `callstd`, not `0xA0`. It has never had the barrier.

So one instrument is silent out of caution and the other is confident without having looked, and
the walk-back is what settles it: here the confident one is **right**, because the call in
between is a `copyvar`. It is right by luck. Nothing in `SpecialContracts` checked, and nothing
anywhere had noticed the two disagreed.

That is the same fault as 207 and 173 — a rule fixed in one arm and left standing in the other,
found only when a third reading crossed both. `SpecialCalls` learned the barrier at 214;
`--routines` was never re-run against it.

## The break that came back green

Four breaks. Three caught. The third — *a second call is followed rather than stopping the walk*
— came back **green**, and the fixture was not the problem.

The walk had a `case Call` arm of its own, sitting immediately above a barrier check that already
contained `call`. Two statements of one rule, and nothing could reach the second one. Breaking it
changed no behaviour because it changed no reachable code.

A guard no test can fail is not a guard. The arm is deleted, the barrier list decides alone, and
the break re-run against the list fails `AnotherCallStopsTheWalkRatherThanBeingFollowed` and
nothing else in that file. Why it was green is written where the break happens rather than only
here.

2877 → 2882 tests, all green. Nothing the run does changed.

---

## What is still owed

* **`SpecialContracts` has no barrier, and nobody has measured what that costs.** Every
  branch count and compared-against value `--routines` prints may have crossed a `call`, a
  `specialvar` or a `callstd` on its way to the compare. Thirty-eight of them are now known to
  be safe. The rest are unchecked, and `--routines` is what the routine work has been read off
  since it was written.
* The **two** places whose older answer is behind a jump, and the **nine** from 218. Eleven of
  336 with no owner.
* `0x081A77B0` is where 218's jumping arm goes, from nineteen places.
* `0x0153`'s own sites — half of every one of the fifty-seven decisions, and still unread.
* Everything owed at 215 and 216 stands.
