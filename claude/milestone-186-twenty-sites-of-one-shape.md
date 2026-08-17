# Milestone 186 — Twenty sites of one shape, and a scorer that could not choose

Delivered as `claude-222.bundle` on the tip of `221`. 2715 tests green from a clean clone at
the base. Measured against the cartridge.

## `0x3F` is seven

Fifteen stopped blocks, and the top of the list ranked by what is behind them — a `clearflag`
and a `call`. Twenty sites, all one shape:

```
16 06 80 03 00 | 3F 01 2A FF 18 00 19 00 | 21 3A 40 03 00
16 06 80 02 00 | 3F 01 2B FF 1C 00 10 00 | 21 ...
                 3F 01 2F FF 16 00 03 00 | 21 ...
                 3F 01 34 FF 14 00 03 00 | 21 ...
```

A byte, a byte that counts up across the sites, `0xFF` — which is how this cartridge writes
*the player* in every `applymovement` — and then two little-endian words whose high bytes are
zero at all twenty sites, which is what a pair of coordinates on a map this size looks like.

**Six also parses**, and the twenty sites say which:

```
width 7 -> next opcode: 0x21 x20        (compare)
width 6 -> next opcode: 0x00 x20        (a nop)
```

A width that lands on padding at every site has landed in the tail of an argument. That is this
project's own rule, written into the scorer years ago, and it decides this one outright.

**80 → 65 stopped blocks. No world number moved** — 258 flags touched, 233 on the boundary, 390
of 425 maps. Worth saying: fifteen blocks became readable and nothing downstream changed.

## The scorer cannot say so, and I nearly made it lie

`--derive` throws out **both** plausible widths for *resuming on the same byte at nearly every
site*. That rule is sound — a width landing inside an argument keeps hitting whatever recurs
there — and it is exactly backwards here, because these twenty sites are one idiom repeated, so
the **correct** width resumes on a column too.

So I wrote a suppression: switch the column rule off when the sites are duplicates. Then I took
it out.

It did not work — the sites vary in one byte of their run-up, so the measure came in under any
threshold I would have had to pick, and picking one would have meant choosing the number that
produced the answer I already had. **Tuning a scorer until it agrees with a reading is
decoration, not evidence.** The verdict already says *undecided — read the bytes*, which is
correct and is what I did.

What was kept is the honest half:

* the report now says **which** rule threw a width out. It printed "it eats a page, an
  instruction, or resumes on a column" for three different rules, so a width could not be
  argued with — and one of those rules was throwing out the right answer.
* it prints **how much of their run-up the sites share**, beside the column figure, so a reader
  can see what that test was worth here rather than having to work it out.

`AreOneIdiom` is measured and printed and deliberately not wired into any verdict.

## And the weakness of that measure, in its own doc

**Padding reads as an idiom.** Four sites in a stretch of zeroes all share the run-up
`00 00 00 00 00` and score a perfect one — correctly, and uselessly. I found that by writing a
test whose "sites that share nothing" case was four offsets in the zero-filled part of the
fixture; it failed, and it was right to.

That is a second reason the measure is printed rather than acted on: a rule that suppressed
another rule on this evidence would be wrong wherever a scan stopped in dead space.

## A test renamed for what it asserts

`WhatWasBehindTheWidestOneIsReachedNow` did not tell six from seven — at six the read calls the
coordinate's high byte a nop and carries on to the same place. The two widths only separate
across the twenty sites on the cartridge. Renamed to
`TheBlockBehindItReadsThroughRatherThanStoppingDead`, with the limitation in its own summary.

A test named for a discrimination it does not make is worse than no test, and this session has
said so about other people's code five times.

## The guards

**Three breaks, three caught**: `0x3F` back to six; the idiom measure ignoring the run-up
entirely; and one site counting as a population, which would rule the column test out exactly
where it matters least.

## What is next

* **An ordered playthrough** — still the largest piece. `--play` is a fixpoint and a story
  counter is what a fixpoint cannot hold.
* **Which move crosses water, READ rather than assumed.** `--surf` stands in for it and turns
  390 of 425 from a ceiling into a floor.
* **`0xE6` (6), `0xC0` (5), `0xA7` (4)** — the rest of the list ranked by what is behind them.
  Each has fifteen to twenty-one commands behind it, including `setvar` and branches.
* **The four that no width reads on from** — `0x92`, `0x9B`, `0xD3`, `0x62`. Misreads, so those
  blocks are wrong earlier and finding where is a different job.
* **The five wall flags**, and the ~28 hand-rolled map walks left in `Program.cs`.

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound; and
`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`, which is timing-dependent.
