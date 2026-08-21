# Milestone 274: the bound was tighter than its evidence

273 found a mixture bound whose whole answer was its sample size, and ended by noting that 268's
own bound — "at most 3.1% and 1.8%", about 121 blocks of 4825 — has no error bar on it either.
This puts one there. It costs 268 an order of magnitude, and it kills a reading of my own on the
way past.

---

## The bound, with the band given back

```
      outside, named ALONE       at most   3.1 % — about 82 of 2671 block(s)
        0.690 from the maps' own, which is 97 % of the 0.711 to the reversal
        a sample of 972 of the maps' OWN blocks scores 0.086..0.167 (4 group(s)) — give that back
        and it is at most 26.6 %.
      outside, named IN A TABLE  at most   1.8 % — about 39 of 2154 block(s)
        0.698 from the maps' own, which is 98 % of the 0.711 to the reversal
        ... give that back and it is at most 25.3 %.
```

The bound divides `d(mixed, real)` by `d(junk, real)`, and `d(mixed, real)` carries the sampling
noise of both populations it is measured on. A sample of 972 blocks drawn from the maps' own
scripts sits **0.086 to 0.167** from its own whole. Hand that much distance back and 3.1% becomes
26.6%.

**972 is smaller than the 2671 being bounded, and a smaller sample is noisier**, so handing back
the 972-block band over-corrects: the true bound is between 3.1% and 26.6%. That range is printed
rather than resolved, because resolving it means a band at 2671 and the maps' own can only supply
one group that size — and a band of one group is not a band.

**So 268's "about 121 of 4825" is not supported.** The honest sentence is *at most somewhere
between 121 and about 1266*, and the direction of 268's conclusion survives while its sharpness
does not. The maps still lead to most of the script this cartridge has; "very nearly all" was a
number with no error bar on it.

## A homogeneity reading that looked clean and was the group size

If a body of real script is made of the same commands throughout and a body of accidents is made
of whatever the bytes happened to be, then each population's own quarters against its own whole
should tell them apart with no other population involved. It looks superb:

```
      population                     own quarters      groups of 114   blocks
      the maps' own scripts          0.086..0.167       0.163..0.425     3888 (quarters of 972)
      outside, named ALONE           0.215..0.483       0.215..0.704     2671 (quarters of 667)
      outside, named IN A TABLE      0.127..0.301       0.255..0.475     2154 (quarters of 538)
      the reversed image             0.226..0.506       0.226..0.506      456 (quarters of 114)
```

The first column separates the maps' own from everything else cleanly. **The second column is the
same four populations at one group size and they overlap** — the maps' own 0.163..0.425 against
the reversal's 0.226..0.506.

The separation was the group size. A quarter of 3888 is 972 blocks and a quarter of 456 is 114,
and a 972-block group is tighter against its whole than a 114-block group for no reason but the
count. **A comparison across populations at each one's own natural split is a comparison
confounded by that split**, and the natural split is the seductive one because every row is doing
the same thing to itself.

The command prints both columns and works out the verdict rather than leaving it to be read:
*HOMOGENEITY DOES NOT DISCRIMINATE HERE*. Kept in the output as a negative — an instrument that
can only produce a separation produces one whether or not there is anything there (30).

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the widest band takes what was asked for, however few groups | 1 | **2** |
| it never backs off past what was asked | 1 | 1 |
| a population too small still returns a band | 1 | 1 |
| **CONTROL:** `leastGroups = 4` spelled `2 + 2` | **0** | **0** |

## What is left

* **A band at the size actually being bounded.** The maps' own 3888 blocks cannot supply four
  groups of 2671. Overlapping groups would, at the cost of correlated samples; a subsample of
  the outside population down to a size both can support would too, and neither was tried.
* **The 0.711 denominator has noise as well**, and only the numerator was corrected here. That
  moves the bound the other way, so the printed range is conservative on one side and not the
  other — which is stated in the output and not measured.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — still owed.
