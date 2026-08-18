# Milestone 214: the third ceiling is mostly not one, and two things were wrong on the way

`special 0x0187` heads all three obstacle scripts, so 213 put it next. Reading it took one
hexdump. Following it took two corrections to instruments — one old, one written this session —
and both were caught by an instrument printing two numbers that could not both be true.

---

## What `0x0187` is asked

```
  25 87 01              special 0x0187
  21 0D 80 02 00        compare 0x800D, 2
  06 01 E0 7A 1A 08     if equal goto 0x081A7AE0
```

`0x081A7AE0` is two bytes: `6C 02` — **release, end**. Answer 2 and the obstacle does nothing.

`--routines` has always had the shape: `0x187 — 376 site(s), 376 branch, compared against
2x376`. Three hundred and seventy-six calls, one tested value, every time. The run's silent
nought is not a wrong answer with consequences; the file cannot tell it from 3, 4 or 9.

## Which made the ceiling line worth reading

`--play` has printed **"N place(s) call M routines it could not answer — every one took the zero
arm"** since milestone 200, and neither half of what that means has ever been checked. The join
was never made: `--routines` reads the ROM and has never seen an `Attempt`; the run knows what
it asked and nothing about what the file does with the answer.

```
--play                                                    --play --say-yes --boat --in-order
  396 places, 33 routines                                   766 places, 63 routines
    201 / 24  nothing branches on the answer                  187 / 45  nothing branches
    158 /  6  nought takes no branch (0 of 636)               430 / 11  nought takes none (0 of 647)
     35 /  1  nought takes EVERY branch (2 of 2)               88 /  1  every branch (2 of 2)
      2 /  2  nought takes some and not others (2 of 4)        61 /  6  some (44 of 68)
```

**Two hundred and one of the floor's three hundred and ninety-six places are calls whose answer
nobody ever branches on.** There is no arm to take. A hundred and fifty-eight more are places
where nought takes no branch at all.

`SpecialCalls.ZeroIsMisleading` has said this in a doc comment for a long time — *"a zero is an
answer, not an absence"* — and only `--specials` ever asked it, off the ROM, with no run in
sight. Both halves have been in the repository the whole time and never in one sentence.

---

## The first thing that was wrong: an ordinary `call`

The forward scan that decides which compare reads a routine's answer stops at anything that
could have answered in the meantime. Its own comment says why: *"getting that wrong is not a
small error — it credits one routine with another's reply"*, and names `0xA0` in BILL's house as
the case it was written for.

It did not stop at a plain `call`. SEVEN ISLAND:

```
  0x1709C3   25 28 00              special 0x0028
  0x1709C6   04 AF 4E 1A 08        call 0x081A4EAF
  0x1709CB   21 0D 80 00 00        compare 0x800D, 0
```

and `0x081A4EAF` is three commands long: `special 0x005D ; 0x27 ; return`. **The answer being
read belongs to a routine two levels away.** `0x0028` had been credited with it, and `0x0028`
was one of the two places 214 first reported as the last of the ceiling.

Adding `call` to the barrier list costs, across the cartridge:

```
  49 -> 46   routines "asked a question"
  18 -> 17   routines branching away on the nought they get by default
 213 -> 212  sites where that happens, of 1097 -> 1055 branching sites in the file
```

**Forty-two of 1097 attributions were reading somebody else's answer.** Losing attributions is
the only direction this can safely be wrong in: a missed reading is a reading nobody makes, a
false one goes in a doc as a fact.

## The second thing: the condition is half the question

The first version of the new classifier sorted routines by the values their answers are compared
against — nought is an assertion where the file tests nought, a refusal otherwise. It printed,
in the same block:

```
  158 of the places: zero is never the value tested, so it falls through
                     — and 39 of their 690 branching sites are taken by nought
```

Thirty-nine branches taken by nought, in the bucket named for nought never being tested. Both
numbers cannot be true, and the wrong one was mine: `compare 0x800D, 1 ; if LESS` is taken by
nought and does not test nought. **The condition is half the question and the values are the
other half**, and `Profile.BranchesTakenByZero` had been doing it properly all along by
evaluating the condition.

On the cartridge the case is `0x084`: tested against 1 and 2, and nought takes **nineteen of its
twenty-one** branches. The value-reading rule called that a refusal.

Classifying on branches-taken instead makes the contradiction go away — the "nought takes no
branch" bucket now reads 0 of 647 — which is the check, not the fix.

---

## What changed

* `SpecialCalls.Answering` gains `0x04`, and `SpecialCalls.WhatIsComparedAfter` exposes the scan
  so the barrier can be tested against a handful of bytes instead of a whole world.
* `SpecialCalls.ZeroAt` joins the run's asked-counts to the file's branch-counts and answers
  **never tested / a refusal / an assertion / both** — on what nought *does*, not on what it is
  compared against. It carries the branching denominator, because a routine asked eighty-eight
  times whose answer is branched on twice is a routine whose silence can matter twice.
* `--play` prints it per routine and as a split, and **names every routine in the assertion and
  mixed buckets whatever the ranking says** — the eight it lists are the most-asked, and the
  routines that matter are asked once or twice. A filter that keeps output readable must never
  decide which question gets asked.

Eight breaks, eight catches, including both directions of the barrier (a call is one; not
everything is one) and both directions of the classifier (values are not the rule; taking some
branches is not taking all).

2853 → 2858 tests, all green. Nothing the run does changed.

---

## What is still owed

* **`0x188` is the whole of what is left.** One routine, asked 35 times at the floor and 88 at
  the widest setting, branched on at **two** sites in the entire cartridge, and nought takes
  both. Those two sites have not been read.
* `0x194` has 747 sites, the most of anything, is asked 54 times by the widest run, and nought
  takes 1 of its 18 branches. Which one has not been asked.
* `0x0028`'s real answerer is `special 0x005D`, reached through a call. Nothing in this project
  follows a call to attribute an answer, and now that the barrier stops there, `0x005D` is
  credited with nothing at that site either. Following one level would be a real instrument.
* The 201 places whose answer is never branched on could come out of the ceiling line entirely
  rather than being reported and then explained away.
