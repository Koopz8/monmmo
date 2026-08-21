# Milestone 273: thirty-eight blocks is a small sample

271 left thirty-eight gating flags whose only movers read as script and that nothing in sixteen
megabytes names. 268 built the axis that can say what a population of blocks is made of. This
points one at the other, and the answer needed a control neither milestone had.

---

## The three populations, all the same shape

A block read from a `setflag` or `clearflag` site to its end. The map scan's own such sites are
the real thing; the reversed image's are junk; the 38 are the question. Same reader, same shape,
so the only difference is where the sites came from.

```
        population                    blocks  per block    from the maps'   from the reversal's
        the maps' own sites              435        6.6             0.000                 0.504
        the reversal's sites            4167        6.2             0.504                 0.000
        the 38                            38        5.9             0.601                 0.373
```

Read as it stands that says the 38 are **farther from real script than junk is**, and the mixture
bound clamps to nought because no share of a mixture can sit outside its own endpoints. That
reading is wrong, and the reason is the last column of the first two rows: 0.504 is measured on
435 blocks against 4167, and 0.601 is measured on **38**.

## The control: what a small sample scores against its own kind

`SamplingBand` splits a population into consecutive groups of N and scores each against the whole
population it came from. Consecutive rather than drawn at random, for 269's reason — a control
that cannot be reproduced from the file alone is a control nobody can check — and consecutive is
the conservative choice, because blocks near each other in the file are more alike, so a group of
neighbours sits FARTHER from the whole than a scattered sample would.

```
        a sample of 38 drawn from the maps' own scores 0.220..0.360 from the whole (11 group(s));
        one drawn from the reversal's scores 0.333..0.822 from ITS whole (109)

        SO: 0.601 from the maps' own is OUTSIDE that band
            and 0.373 from the reversal's is INSIDE its band
            — these are the reversal's kind and not the maps'
```

**A sample of 38 drawn from the maps' own scripts scores 0.22 to 0.36 against its own whole.**
That is the cost of thirty-eight blocks, and it is most of the distance the raw table was being
read against. The 38's 0.601 is well outside it; its 0.373 from the reversal is inside the
reversal's own band and near the bottom of it.

So the answer is the one 269 predicted and could not demonstrate: **the 38 are the reversal's
kind.** They are what a `setflag` pattern followed by bytes that happen to decode looks like, and
the sixty-flag bucket `--flags` has offered as entry points since 175 is now fully accounted for:
21 the opening, 1 THUMB code (271), 38 accidents, and nothing left over.

**And the clamp was the sample size, not a finding.** The command says so in its own output
rather than leaving the nought to be read as "less real than junk".

## The per-site column, printed because it is weak

Each of the 38 with its block's length and how many of its commands are among the maps' sixteen
commonest. Twenty-two of the 38 are two or three commands long — `setflag ; return`,
`clearflag ; 0x20 ; return` — which score two-of-two and two-of-three and mean nothing at all.
That is exactly why the population bound is the reading and this column is beside it: a short
block scores anything, and printing both lets the reader see which is doing the work (246).

The three longest are worth a name: `0x0060` at `0x1FC853` is 63 commands of mostly `nop`,
`0x0028` at `0x1C7FAE` and `0x002A` at `0x1F0CC6` are twelve each and also mostly `nop`. A run of
zeros is a run of no-ops that reaches whatever end follows it — 269's sixth break, in the
cartridge this time rather than in a fixture.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| groups overlap instead of being disjoint | 1 | **3** |
| the last part-group is scored too | 1 | 1 |
| each group against the first group, not the whole | 2 | **0** — no fixture could tell |
| ... after adding the fixture | 1 | 1 |
| a sample larger than the population | 1 | **0** — the line decides nothing |
| the whole is one block rather than the population | 1 | 2 |
| **CONTROL:** the band returned unsorted | **0** | **1** — not a control; `Assert.Equal([..Order()], band)` is a rule |

**The third is the one worth keeping.** Aiming every group's reference at the first group instead
of at the whole passed all five tests, because in every fixture the first group either was the
whole or was identical to it — the band would then have measured group-to-group variation and
nobody would have known. Ten blocks of one kind followed by thirty of another tell them apart.

**The fifth is 219's fault again**: `population.Count < howMany` sits above a loop whose own
condition already refuses every group, so there was nothing there to break. Deleted rather than
fixtured.

And **the failing test in the first run was my fixture, not the code** (61): half one kind and
half the other, in file order, makes every group pure at BOTH sizes, so the two sizes tied at
0.250 and "a smaller sample is farther" could not be shown. What drives the effect is a
proportion a small group cannot represent — one block in forty is 2.5% of the whole, 5% of a
group of twenty, 10% of a group of ten.

## What is left

* **`AsksWhoKnows`'s nudge** (272) — it takes a bound rather than an id.
* **The seam** (269).
* **The same band belongs over 268's own numbers.** "outside-alone 0.690, outside-in-a-table
  0.698, the reversed image 0.711" are populations of thousands, so the band is small there —
  but it has not been printed, and 268's "at most 3.1% and 1.8%" is a bound with no error bar
  on it.
