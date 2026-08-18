# Milestone 235: waiting is about the routine, not the site

232 measured `0x27` and stopped in the right place: 68 of its 98 byte positions sit immediately
after a `special`, against a chance floor of 2.35%. That says the command belongs after a routine
call and it says nothing else.

The question worth asking next is not how often it happens. **A routine asked in seven places with
a wait after all seven is saying something about that routine; the same seven waits scattered over
seven different routines are saying something about the scripts — and the two read identically as a
count.**

---

## The answer, with the denominator that decides it

```
    68 of 936 call place(s) are followed straight away by a wait, at 36 routine(s)
      22 of those 36 are asked in ONE place, where all-or-nothing says nothing at all;
         the claim below is about the other 14
      13 of the 14 are waited for at EVERY place that asks them; 1 at some but not all
      if each place were decided on its own at the overall rate of 7.3%, the number of
         multi-place routines waited at every one would be 0.21

        0x194   1 of 34 place(s)   NOT ALL
        0x09F   7 of  7 place(s)   every one
        0x020   6 of  6 place(s)   every one
        0x138   6 of  6 place(s)   every one
        0x029   5 of  5 place(s)   every one
        0x111   4 of  4 place(s)   every one
        0x158   3 of  3 place(s)   every one
        0x021 0x09E 0x0BC 0x0FE 0x166 0x18E 0x1A7   2 of 2 each

    and the other side: 68 of the 82 routines asked in more than one place are waited
    for at NONE of them
```

**Thirteen against an expectation of nought point two one.** A routine either has a wait after
every place that asks it or after none of them; only `0x194` does neither, at 1 of its 34.

**Twenty-two of the thirty-six are the whole reason this needed a denominator.** A routine asked in
one place is waited for "at every place" the moment it is waited for at all, and quoting 35 of 36
— which is what the raw figure looks like — would have been a finding built out of nothing. The
instrument prints the exclusion and its size rather than doing it quietly.

The other comparison, and it is weak: 31 of the 36 are never branched on, against a population rate
of 130 of 178. That is 86% against 73% — 1.18 times, on a sample of thirty-six. Reported as what it
is rather than dressed up. **The all-or-nothing shape is the finding; "routines you wait for are
routines you do not ask" is not established here.**

## And a number of my own, wrong two milestones ago

232's document says the 68 sites are *"across 41 distinct routines"*. They are **36**, and the data
said 36 at the time — the histogram it was read off has thirteen routines with more than one site
and twenty-three with one, which is thirty-six. Nothing computed 41; it was written down.

Also read here and worth having: **nought of the 98 `0x27`s follow a `specialvar`.** Every one of
the 68 follows a plain `special` — the form that leaves its answer in `0x800D` by default rather
than being told where to put it. That is a fact about the shape and it is free.

That is four wrong numbers in six milestones, all of them this project's own rather than the
cartridge's, and every one found by printing the thing again rather than by reasoning about it.

---

## What changed

* `WhatIsWaitedFor` — call places and waited places per routine, the three answers (every, some,
  none), and the expectation under per-site sprinkling. Reachable from a test with no cartridge.
* `--routines` prints it.

Four breaks, four catches:

| break | what went red |
|---|---|
| places counted as calls | three tests |
| some-only folded into every | `EverySomeAndNoneAreThreeAnswers` |
| the expectation keeps the routines asked once | `TheExpectationLeavesOutTheRoutinesAskedOnce` |
| the rate taken over the waiting routines only | `TheRateIsOverEveryPlaceAndNotOnlyTheWaitingOnes` |

The last two are the ones that matter: both move the answer **in the direction that flatters the
finding**, and that is the direction to guard hardest. Leaving single-place routines in the
expectation puts the population's own rate straight back into the null; taking the rate over only
the routines that wait builds the null out of the sites that agree with it.

2988 → 2993 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **`0x194`**, the one exception: 1066 calls at 34 places (231), waited for at exactly one of them.
  Which one, and what is different about it.
* **What `0x27` DOES.** That it is a property of the routine is read; that it is *waiting* is still
  the guess 232 made and did not claim.
* **`0x011E`**, the routine `10.14`'s nineteen shared signs ask.
* **The three numbers nothing computes** (231): `62 gates hold 240 people`, `146 trees and rocks`,
  `158 objects`, and `the ceiling is 45 of 437 byte positions`.
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.
