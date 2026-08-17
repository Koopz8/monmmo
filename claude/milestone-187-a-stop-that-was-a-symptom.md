# Milestone 187 — A stop that was a symptom, and the first width that was wrong

Delivered as `claude-223.bundle` on the tip of `222`. 2719 tests green from a clean clone at
the base. Measured against the cartridge.

## `--stops`, and the half of it that mattered

`--scripts` prints **one** example per command that stops a read. That is enough to know a
command is in the way, and not enough to say which width is right — because that is a question
about what all its sites have in common, and every width adopted in this project so far was
read off a column pasted together by hand.

`--stops 0xNN` prints every stopped read of a command: the run-up, the command, what follows,
and what each candidate width would resume on across all the sites at once.

And then the half that turned out to matter:

```
0x16C1FF  80 00 00 06 01 | E6 | C2 16 08 21 00 80 01 00 06 01   read from 0x0816C1E7 (+24)
```

**Where the read started, beside where it stopped.** A stop is only a command if the reader was
in step to begin with — and `06 01 E6 C2 16 08` is a `gotoif` whose pointer is `0x0816C2E6`. The
`0xE6` was inside a pointer. The block it belongs to decodes perfectly from its own start.

## Which made the real fault visible

Twenty-four bytes upstream, five consecutive blocks:

```
70 00 00 | 1F 00 00 | 05 F3 C1 16 08 | 02
70 00 00 | 1F 01 00 | 05 F3 C1 16 08 | 02
70 00 00 | 1F 02 00 | 05 F3 C1 16 08 | 02
70 00 00 | 1F 03 00 | 05 F3 C1 16 08 | 02
70 00 00 | 1F 04 00 | 05 F3 C1 16 08 | 02
```

A counter and a `goto` to the same shared block, five times over. `[0x1F]` was **5** — the width
of Ruby's `comparefarbytetobyte`. It is **2**: at two the next command is that `goto` at five of
five; at five the goto's opcode *and its pointer* are swallowed whole and the read carries on
into the middle of the block it points at.

**This is the first width in this project that was wrong rather than missing, and it is a
different animal.** A missing width stops a read and says so. A wrong one stops nothing — it
eats the commands after it and reads whatever it lands on, so the block comes back full of
instructions that are not there. It never failed. It produced a phantom stop two dozen bytes
downstream, on a byte that was not a command at all.

The note at the top of the width table has warned since milestone 14 that these lengths were
written from memory of the Ruby and Emerald set and that a real FireRed image says they are not
good enough. Until now every consequence of that had been a missing entry.

## Two more adopted, both by tests already in the project

* **`0xA7` = 2.** Four sites, and at two the third byte is a `return`. A constant `0x03` could be
  an argument — it is not: at all four sites the byte after it begins a block that **something
  else in the image points at**, and you do not fall into a block that has its own pointer. The
  same test that settled `0xD0`, unanimous.
* **`0xC0` = 2.** Three of its five sites are one shape — `C0 00 00 | 0F 00 57 70 19 08 | 09 04`,
  a `loadpointer` and a `callstd`, which is how every text box in this game opens. The other two
  sit inside a `gotoif`'s pointer: a read that had already drifted, which is now a recognisable
  category rather than a puzzle.

**65 → 58 stopped blocks. 3771 → 3806 blocks reached. 258 → 259 flags touched.** The boundary,
the wall list and 390 of 425 maps are unchanged.

## And a fixture defect worth naming

`AWrongWidthSwallowsTheGotoAndEverythingBehindIt` passed with the width broken back to five. The
fixture is zero-filled, and **a zero-filled fixture is a nop slide**: a read that drifted past
the `goto` walked through sixty bytes of `0x00` — every one of them a valid no-op — and arrived
at the `setflag` anyway. The test passed at the wrong width, for the wrong reason.

Fixed with one byte the reader cannot step over, right after the block. Empty space in a fixture
is not empty; it is the most permissive instruction in the set, repeated.

That is the third fixture defect this session and they rhyme: **the fixture was more forgiving
than the cartridge**, so the guard could not fail. Worth checking for directly rather than
waiting to be caught by a break.

## The guards

**Four breaks, four caught**: each of the three widths put back to a plausible wrong value, and
the behavioural one for `0x1F` once the slide was blocked.

## What is next

* **The two `0xC0` sites that had already drifted.** Same disease as `0xE6`, so there is at
  least one more wrong width upstream of them. `--stops` now shows where each read began, which
  is the handle.
* **An ordered playthrough** — still the largest piece; `--play` is a fixpoint and a story
  counter is what a fixpoint cannot hold.
* **Which move crosses water, READ rather than assumed** — turns 390 of 425 from a ceiling into
  a floor.
* **58 blocks still stop**, led by `0x92` (4) and `0x73` (4), of which `0x73` is worth nothing
  and four read on from no width at all.
* **The five wall flags**, and the ~28 hand-rolled map walks left in `Program.cs`.

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound; and
`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`, which is timing-dependent.
